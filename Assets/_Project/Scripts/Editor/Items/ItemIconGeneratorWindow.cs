using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VRGame.Items.Editor
{
    public sealed class ItemIconGeneratorWindow : EditorWindow
    {
        private IconGenerationProfile profile;
        private ItemDefinition selectedItemDefinition;
        private ItemDefinitionDatabase selectedDatabase;
        private string outputFolder = ItemIconGenerationUtility.DefaultOutputFolder;
        private bool overwriteExisting = true;
        private Vector2 scrollPosition;
        private readonly List<ItemIconGenerationResult> lastResults = new List<ItemIconGenerationResult>();

        [MenuItem("Tools/VRGame/Items/Icon Generator")]
        public static ItemIconGeneratorWindow Open()
        {
            ItemIconGeneratorWindow window = GetWindow<ItemIconGeneratorWindow>("Item Icon Generator");
            window.minSize = new Vector2(420f, 420f);
            window.PullSelection();
            return window;
        }

        public static void OpenForItem(ItemDefinition itemDefinition)
        {
            ItemIconGeneratorWindow window = Open();
            window.selectedItemDefinition = itemDefinition;
            window.Show();
        }

        public static void OpenForDatabase(ItemDefinitionDatabase database)
        {
            ItemIconGeneratorWindow window = Open();
            window.selectedDatabase = database;
            window.Show();
        }

        [MenuItem("Tools/VRGame/Items/Generate Icon For Selected Item Definition")]
        public static void GenerateSelectedItemMenu()
        {
            ItemDefinition itemDefinition = Selection.activeObject as ItemDefinition;
            if (itemDefinition == null)
            {
                Debug.LogWarning("Select an ItemDefinition asset before generating an item icon.");
                return;
            }

            ItemIconGeneratorWindow window = Open();
            window.selectedItemDefinition = itemDefinition;
            window.GenerateSelectedItem();
        }

        [MenuItem("Tools/VRGame/Items/Generate Icons For Selected Item Database")]
        public static void GenerateSelectedDatabaseMenu()
        {
            ItemDefinitionDatabase database = Selection.activeObject as ItemDefinitionDatabase;
            if (database == null)
            {
                Debug.LogWarning("Select an ItemDefinitionDatabase asset before generating item icons.");
                return;
            }

            ItemIconGeneratorWindow window = Open();
            window.selectedDatabase = database;
            window.GenerateDatabase();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Inventory Icon Generation", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                profile = (IconGenerationProfile)EditorGUILayout.ObjectField("Icon Profile", profile, typeof(IconGenerationProfile), false);
                if (profile == null)
                {
                    EditorGUILayout.HelpBox("No icon profile assigned. Generation uses a transient default profile and logs a warning-style result.", MessageType.Warning);
                }
                else
                {
                    DrawProfilePreview(profile);
                }
            }

            EditorGUILayout.Space();

            selectedItemDefinition = (ItemDefinition)EditorGUILayout.ObjectField("Item Definition", selectedItemDefinition, typeof(ItemDefinition), false);
            selectedDatabase = (ItemDefinitionDatabase)EditorGUILayout.ObjectField("Item Database", selectedDatabase, typeof(ItemDefinitionDatabase), false);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing", overwriteExisting);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selection"))
                {
                    PullSelection();
                }

                using (new EditorGUI.DisabledScope(selectedItemDefinition == null))
                {
                    if (GUILayout.Button("Generate Selected"))
                    {
                        GenerateSelectedItem();
                    }
                }

                using (new EditorGUI.DisabledScope(selectedDatabase == null))
                {
                    if (GUILayout.Button("Generate Database"))
                    {
                        GenerateDatabase();
                    }
                }
            }

            DrawSelectionValidation();
            DrawResults();
        }

        private void PullSelection()
        {
            if (Selection.activeObject is ItemDefinition itemDefinition)
            {
                selectedItemDefinition = itemDefinition;
            }
            else if (Selection.activeObject is ItemDefinitionDatabase database)
            {
                selectedDatabase = database;
            }
            else if (Selection.objects != null)
            {
                for (int i = 0; i < Selection.objects.Length; i++)
                {
                    if (selectedItemDefinition == null && Selection.objects[i] is ItemDefinition selectedItem)
                    {
                        selectedItemDefinition = selectedItem;
                    }

                    if (selectedDatabase == null && Selection.objects[i] is ItemDefinitionDatabase selectedDb)
                    {
                        selectedDatabase = selectedDb;
                    }
                }
            }
        }

        private void GenerateSelectedItem()
        {
            lastResults.Clear();
            ItemIconGenerationResult result = ItemIconGenerationUtility.GenerateIcon(selectedItemDefinition, profile, outputFolder, overwriteExisting);
            lastResults.Add(result);
            LogResult(result, selectedItemDefinition);
            AssetDatabase.SaveAssets();
        }

        private void GenerateDatabase()
        {
            lastResults.Clear();
            if (selectedDatabase == null)
            {
                ItemIconGenerationResult result = new ItemIconGenerationResult(ItemIconGenerationStatus.MissingItemDefinition, "Item definition database is null.");
                lastResults.Add(result);
                LogResult(result, null);
                return;
            }

            IReadOnlyList<ItemDefinition> definitions = selectedDatabase.ItemDefinitions;
            try
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    ItemDefinition itemDefinition = definitions[i];
                    if (itemDefinition == null)
                    {
                        continue;
                    }

                    float progress = definitions.Count == 0 ? 1f : (float)i / definitions.Count;
                    EditorUtility.DisplayProgressBar("Generating Item Icons", itemDefinition.name, progress);
                    ItemIconGenerationResult result = ItemIconGenerationUtility.GenerateIcon(itemDefinition, profile, outputFolder, overwriteExisting);
                    lastResults.Add(result);
                    LogResult(result, itemDefinition);
                }

                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void DrawProfilePreview(IconGenerationProfile iconProfile)
        {
            EditorGUILayout.LabelField("Rotation", iconProfile.Rotation.ToString("F2"));
            EditorGUILayout.LabelField("Scale", iconProfile.Scale.ToString("0.###"));
            EditorGUILayout.LabelField("Camera Offset", iconProfile.CameraOffset.ToString("F2"));
            EditorGUILayout.LabelField("Orthographic Size", iconProfile.UsesAutomaticOrthographicSize ? "Auto" : iconProfile.OrthographicSize.ToString("0.###"));
            EditorGUILayout.LabelField("Model Offset", iconProfile.ModelOffset.ToString("F2"));
            EditorGUILayout.LabelField("Lighting", iconProfile.LightingPreset.ToString());
            EditorGUILayout.LabelField("Output", $"{iconProfile.OutputSize} x {iconProfile.OutputSize}");
            EditorGUILayout.LabelField("Transparent", iconProfile.TransparentBackground ? "Yes" : "No");
        }

        private void DrawSelectionValidation()
        {
            if (selectedItemDefinition != null && selectedItemDefinition.WorldPrefab == null)
            {
                EditorGUILayout.HelpBox($"'{selectedItemDefinition.name}' has no world prefab.", MessageType.Warning);
            }

            if (selectedDatabase != null)
            {
                int missingPrefabCount = 0;
                IReadOnlyList<ItemDefinition> definitions = selectedDatabase.ItemDefinitions;
                for (int i = 0; i < definitions.Count; i++)
                {
                    if (definitions[i] != null && definitions[i].WorldPrefab == null)
                    {
                        missingPrefabCount++;
                    }
                }

                if (missingPrefabCount > 0)
                {
                    EditorGUILayout.HelpBox($"{missingPrefabCount} item definition(s) in the database are missing world prefabs.", MessageType.Warning);
                }
            }
        }

        private void DrawResults()
        {
            if (lastResults.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last Results", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int i = 0; i < lastResults.Count; i++)
            {
                ItemIconGenerationResult result = lastResults[i];
                MessageType messageType = result.Success ? MessageType.Info : MessageType.Warning;
                if (result.Status == ItemIconGenerationStatus.RenderFailed ||
                    result.Status == ItemIconGenerationStatus.SaveFailed ||
                    result.Status == ItemIconGenerationStatus.MissingWorldPrefab ||
                    result.Status == ItemIconGenerationStatus.MissingRenderablePrefab)
                {
                    messageType = MessageType.Error;
                }

                EditorGUILayout.HelpBox(result.Message, messageType);
                if (!string.IsNullOrWhiteSpace(result.AssetPath))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Ping Icon", GUILayout.Width(88f)))
                        {
                            Object iconAsset = AssetDatabase.LoadMainAssetAtPath(result.AssetPath);
                            if (iconAsset != null)
                            {
                                EditorGUIUtility.PingObject(iconAsset);
                            }
                        }
                    }
                }

                if (result.Sprite != null)
                {
                    Rect rect = GUILayoutUtility.GetRect(64f, 64f, GUILayout.Width(72f), GUILayout.Height(72f));
                    GUI.DrawTexture(rect, result.Sprite.texture, ScaleMode.ScaleToFit, true);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static void LogResult(ItemIconGenerationResult result, ItemDefinition itemDefinition)
        {
            string itemName = itemDefinition != null ? itemDefinition.name : "(none)";
            if (result == null)
            {
                Debug.LogError($"Icon generation for {itemName} returned a null result.");
                return;
            }

            if (result.Success)
            {
                if (result.Status == ItemIconGenerationStatus.MissingIconProfile)
                {
                    Debug.LogWarning($"Icon generation warning for {itemName}: {result.Message}");
                }
                else
                {
                    Debug.Log($"Icon generation complete for {itemName}: {result.Message}");
                }
            }
            else
            {
                Debug.LogError($"Icon generation failed for {itemName}: {result.Message}");
            }
        }
    }
}
