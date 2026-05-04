using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRGame.Items.Editor
{
    public enum ItemIconGenerationStatus
    {
        Success,
        MissingItemDefinition,
        MissingWorldPrefab,
        MissingIconProfile,
        MissingRenderablePrefab,
        RenderFailed,
        SaveFailed
    }

    public sealed class ItemIconGenerationResult
    {
        public ItemIconGenerationResult(
            ItemIconGenerationStatus status,
            string message,
            string assetPath = "",
            Sprite sprite = null)
        {
            Status = status;
            Message = message ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            Sprite = sprite;
        }

        public ItemIconGenerationStatus Status { get; }

        public string Message { get; }

        public string AssetPath { get; }

        public Sprite Sprite { get; }

        public bool Success
        {
            get { return Status == ItemIconGenerationStatus.Success || (Status == ItemIconGenerationStatus.MissingIconProfile && Sprite != null); }
        }
    }

    public static class ItemIconGenerationUtility
    {
        public const string DefaultOutputFolder = "Assets/_Project/Generated/Icons";

        public static ItemIconGenerationResult GenerateIcon(
            ItemDefinition itemDefinition,
            IconGenerationProfile profile,
            string outputFolder = DefaultOutputFolder,
            bool overwriteExisting = true)
        {
            if (itemDefinition == null)
            {
                return new ItemIconGenerationResult(ItemIconGenerationStatus.MissingItemDefinition, "Item definition is null.");
            }

            if (itemDefinition.WorldPrefab == null)
            {
                return new ItemIconGenerationResult(
                    ItemIconGenerationStatus.MissingWorldPrefab,
                    $"Item definition '{itemDefinition.name}' has no world prefab.");
            }

            bool usedFallbackProfile = false;
            IconGenerationProfile resolvedProfile = profile;
            if (resolvedProfile == null)
            {
                usedFallbackProfile = true;
                resolvedProfile = CreateTransientDefaultProfile();
            }

            try
            {
                string folder = string.IsNullOrWhiteSpace(outputFolder) ? DefaultOutputFolder : outputFolder.Trim();
                EnsureAssetFolder(folder);

                string iconAssetPath = GetIconAssetPath(itemDefinition, folder, overwriteExisting);
                Texture2D texture = RenderPrefabToTexture(itemDefinition.WorldPrefab, resolvedProfile, out string renderMessage, out ItemIconGenerationStatus renderFailureStatus);
                if (texture == null)
                {
                    return new ItemIconGenerationResult(renderFailureStatus, renderMessage);
                }

                try
                {
                    byte[] pngBytes = texture.EncodeToPNG();
                    if (pngBytes == null || pngBytes.Length == 0)
                    {
                        return new ItemIconGenerationResult(ItemIconGenerationStatus.SaveFailed, "Rendered texture could not be encoded as PNG.");
                    }

                    File.WriteAllBytes(ToAbsolutePath(iconAssetPath), pngBytes);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                AssetDatabase.ImportAsset(iconAssetPath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureSpriteImporter(iconAssetPath, resolvedProfile);

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconAssetPath);
                if (sprite == null)
                {
                    return new ItemIconGenerationResult(ItemIconGenerationStatus.SaveFailed, $"Icon PNG was saved but no Sprite imported at '{iconAssetPath}'.");
                }

                AssignGeneratedIcon(itemDefinition, sprite);
                string message = usedFallbackProfile
                    ? $"Generated icon with fallback default profile because no icon profile was assigned: {iconAssetPath}"
                    : $"Generated icon: {iconAssetPath}";
                return new ItemIconGenerationResult(usedFallbackProfile ? ItemIconGenerationStatus.MissingIconProfile : ItemIconGenerationStatus.Success, message, iconAssetPath, sprite);
            }
            catch (Exception exception)
            {
                return new ItemIconGenerationResult(ItemIconGenerationStatus.RenderFailed, exception.Message);
            }
            finally
            {
                if (usedFallbackProfile && resolvedProfile != null)
                {
                    UnityEngine.Object.DestroyImmediate(resolvedProfile);
                }
            }
        }

        public static List<ItemIconGenerationResult> GenerateIconsForDatabase(
            ItemDefinitionDatabase database,
            IconGenerationProfile profile,
            string outputFolder = DefaultOutputFolder,
            bool overwriteExisting = true)
        {
            List<ItemIconGenerationResult> results = new List<ItemIconGenerationResult>();
            if (database == null)
            {
                results.Add(new ItemIconGenerationResult(ItemIconGenerationStatus.MissingItemDefinition, "Item definition database is null."));
                return results;
            }

            IReadOnlyList<ItemDefinition> itemDefinitions = database.ItemDefinitions;
            for (int i = 0; i < itemDefinitions.Count; i++)
            {
                ItemDefinition itemDefinition = itemDefinitions[i];
                if (itemDefinition == null)
                {
                    continue;
                }

                results.Add(GenerateIcon(itemDefinition, profile, outputFolder, overwriteExisting));
            }

            AssetDatabase.SaveAssets();
            return results;
        }

        public static string GetSafeIconFileName(ItemDefinition itemDefinition)
        {
            string sourceName = itemDefinition != null && !itemDefinition.ItemDefId.IsEmpty
                ? itemDefinition.ItemDefId.Value
                : itemDefinition != null ? itemDefinition.name : "item_icon";

            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                sourceName = sourceName.Replace(invalidCharacter, '_');
            }

            return sourceName.Replace('.', '_').Replace(' ', '_');
        }

        private static Texture2D RenderPrefabToTexture(GameObject prefab, IconGenerationProfile profile, out string message, out ItemIconGenerationStatus failureStatus)
        {
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            Scene previousActiveScene = SceneManager.GetActiveScene();
            GameObject instance = null;
            GameObject cameraObject = null;
            List<GameObject> lightObjects = new List<GameObject>();
            RenderTexture renderTexture = null;
            RenderTexture previousActive = RenderTexture.active;
            Color previousAmbientLight = RenderSettings.ambientLight;
            bool changedActiveScene = false;
            failureStatus = ItemIconGenerationStatus.RenderFailed;

            try
            {
                changedActiveScene = SceneManager.SetActiveScene(previewScene);
                instance = UnityEngine.Object.Instantiate(prefab);
                instance.name = prefab.name + "_IconPreview";
                instance.hideFlags = HideFlags.HideAndDontSave;
                SceneManager.MoveGameObjectToScene(instance, previewScene);
                PreparePreviewInstance(instance, profile);

                if (!TryCalculateBounds(instance, out Bounds bounds))
                {
                    message = $"Prefab '{prefab.name}' has no enabled renderers to capture.";
                    failureStatus = ItemIconGenerationStatus.MissingRenderablePrefab;
                    return null;
                }

                cameraObject = new GameObject("Icon Preview Camera");
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
                Camera camera = cameraObject.AddComponent<Camera>();
                ConfigureCamera(camera, profile, bounds);

                CreateLights(previewScene, profile, bounds, lightObjects);

                int outputSize = profile.OutputSize;
                renderTexture = new RenderTexture(outputSize, outputSize, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 8,
                    name = "ItemIconGenerationRT"
                };

                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                GL.Clear(true, true, profile.BackgroundColor);
                camera.Render();

                Texture2D texture = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0f, 0f, outputSize, outputSize), 0, 0);
                texture.Apply(false, false);
                message = "Rendered prefab icon.";
                failureStatus = ItemIconGenerationStatus.Success;
                return texture;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (changedActiveScene && previousActiveScene.IsValid())
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }

                RenderSettings.ambientLight = previousAmbientLight;
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }

                if (cameraObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                }

                for (int i = 0; i < lightObjects.Count; i++)
                {
                    if (lightObjects[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(lightObjects[i]);
                    }
                }

                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }

                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void PreparePreviewInstance(GameObject instance, IconGenerationProfile profile)
        {
            Transform transform = instance.transform;
            transform.position = profile.ModelOffset;
            transform.rotation = Quaternion.Euler(profile.Rotation);
            transform.localScale = Vector3.one * profile.Scale;

            Rigidbody[] rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                rigidbodies[i].isKinematic = true;
                rigidbodies[i].detectCollisions = false;
            }
        }

        private static bool TryCalculateBounds(GameObject instance, out Bounds bounds)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return initialized;
        }

        private static void ConfigureCamera(Camera camera, IconGenerationProfile profile, Bounds bounds)
        {
            Vector3 cameraOffset = profile.CameraOffset;
            Vector3 center = bounds.center;
            camera.transform.position = center + cameraOffset;
            camera.transform.LookAt(center);
            camera.orthographic = true;
            camera.orthographicSize = profile.UsesAutomaticOrthographicSize
                ? CalculateAutomaticOrthographicSize(bounds, camera)
                : profile.OrthographicSize;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = Mathf.Max(100f, cameraOffset.magnitude + bounds.extents.magnitude + 20f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = profile.BackgroundColor;
            camera.allowHDR = false;
            camera.allowMSAA = true;
        }

        private static float CalculateAutomaticOrthographicSize(Bounds bounds, Camera camera)
        {
            Vector3[] corners = GetBoundsCorners(bounds);
            float maxY = 0f;
            float maxX = 0f;
            Matrix4x4 worldToCamera = camera.worldToCameraMatrix;
            Vector3 cameraCenter = worldToCamera.MultiplyPoint(bounds.center);

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 cameraPoint = worldToCamera.MultiplyPoint(corners[i]) - cameraCenter;
                maxX = Mathf.Max(maxX, Mathf.Abs(cameraPoint.x));
                maxY = Mathf.Max(maxY, Mathf.Abs(cameraPoint.y));
            }

            float size = Mathf.Max(maxY, maxX);
            return Mathf.Max(0.1f, size * 1.18f);
        }

        private static Vector3[] GetBoundsCorners(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            return new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };
        }

        private static void CreateLights(Scene previewScene, IconGenerationProfile profile, Bounds bounds, List<GameObject> lightObjects)
        {
            RenderSettings.ambientLight = profile.AmbientColor;
            Vector3 center = bounds.center;

            switch (profile.LightingPreset)
            {
                case IconLightingPreset.FrontKey:
                    AddDirectionalLight(previewScene, lightObjects, "Front Key", center, new Vector3(-25f, 25f, -30f), 1.6f);
                    break;
                case IconLightingPreset.ThreePoint:
                    AddDirectionalLight(previewScene, lightObjects, "Key", center, new Vector3(-35f, 30f, -40f), 1.35f);
                    AddDirectionalLight(previewScene, lightObjects, "Fill", center, new Vector3(25f, 15f, -20f), 0.65f);
                    AddDirectionalLight(previewScene, lightObjects, "Rim", center, new Vector3(35f, 45f, 60f), 0.85f);
                    break;
                case IconLightingPreset.SoftTop:
                    AddDirectionalLight(previewScene, lightObjects, "Soft Top", center, new Vector3(55f, 0f, 0f), 1.25f);
                    break;
                case IconLightingPreset.Studio:
                default:
                    AddDirectionalLight(previewScene, lightObjects, "Studio Key", center, new Vector3(-35f, 25f, -35f), 1.25f);
                    AddDirectionalLight(previewScene, lightObjects, "Studio Fill", center, new Vector3(30f, -20f, -20f), 0.55f);
                    break;
            }
        }

        private static void AddDirectionalLight(Scene previewScene, List<GameObject> lightObjects, string name, Vector3 target, Vector3 eulerAngles, float intensity)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            lightObject.transform.position = target;
            lightObject.transform.rotation = Quaternion.Euler(eulerAngles);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            SceneManager.MoveGameObjectToScene(lightObject, previewScene);
            lightObjects.Add(lightObject);
        }

        private static string GetIconAssetPath(ItemDefinition itemDefinition, string outputFolder, bool overwriteExisting)
        {
            string baseName = GetSafeIconFileName(itemDefinition);
            string assetPath = $"{outputFolder}/{baseName}.png";
            if (overwriteExisting || !File.Exists(ToAbsolutePath(assetPath)))
            {
                return assetPath;
            }

            return AssetDatabase.GenerateUniqueAssetPath(assetPath);
        }

        private static void ConfigureSpriteImporter(string assetPath, IconGenerationProfile profile)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.spritePixelsPerUnit = profile.SpritePixelsPerUnit;
            importer.SaveAndReimport();
        }

        private static void AssignGeneratedIcon(ItemDefinition itemDefinition, Sprite sprite)
        {
            SerializedObject serializedObject = new SerializedObject(itemDefinition);
            serializedObject.FindProperty("generatedIcon").objectReferenceValue = sprite;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(itemDefinition);
            AssetDatabase.SaveAssetIfDirty(itemDefinition);
        }

        private static IconGenerationProfile CreateTransientDefaultProfile()
        {
            IconGenerationProfile profile = ScriptableObject.CreateInstance<IconGenerationProfile>();
            profile.name = "TransientDefaultIconGenerationProfile";
            profile.hideFlags = HideFlags.HideAndDontSave;
            return profile;
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            string normalized = assetFolder.Replace('\\', '/').Trim('/');
            if (!normalized.StartsWith("Assets", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Icon output folder must be inside Assets.");
            }

            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
