using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VRGame.Items;
using VRGame.Runtime;

namespace VRGame.Manifestation.Editor
{
    public static class Quest3DemoSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Quest3DemoScene.unity";
        private const string DataRoot = "Assets/_Project/Data/Quest3Demo";
        private const string PrefabRoot = "Assets/_Project/Prefabs/Quest3Demo";
        private const string IconRoot = "Assets/_Project/GeneratedIcons/Quest3Demo";
        private const string MaterialRoot = "Assets/_Project/Materials/Quest3Demo";
        private const string HexaRigPrefabPath = "Assets/HurricaneVR/Framework/Integrations/HexaBodyVR/Player/Hexa4Rig OpenXR.prefab";
        private const string HvrGlobalPrefabPath = "Assets/HurricaneVR/Framework/Prefabs/HVRGlobal.prefab";

        private static readonly ItemDefId WoodId = ItemDefId.FromString("demo.resource.wood");
        private static readonly ItemDefId StoneId = ItemDefId.FromString("demo.resource.stone");
        private static readonly ItemDefId CopperOreId = ItemDefId.FromString("demo.resource.copper_ore");
        private static readonly ItemDefId FishId = ItemDefId.FromString("demo.resource.fish");
        private static readonly ItemDefId CopperPickaxeId = ItemDefId.FromString("demo.tool.copper_pickaxe");
        private static readonly ItemDefId CopperAxeId = ItemDefId.FromString("demo.tool.copper_axe");
        private static readonly ItemDefId FishingTrapId = ItemDefId.FromString("demo.tool.fishing_trap");
        private static readonly ItemDefId CopperSwordId = ItemDefId.FromString("demo.weapon.copper_sword");
        private static readonly ItemDefId CopperHelmetId = ItemDefId.FromString("demo.armor.copper_helmet");
        private static readonly ItemDefId RubyRingId = ItemDefId.FromString("demo.accessory.ruby_ring");
        private static readonly ItemDefId WoodFoundationId = ItemDefId.FromString("demo.placeable.wood_foundation");
        private static readonly ItemDefId WoodWallId = ItemDefId.FromString("demo.placeable.wood_wall");
        private static readonly ItemDefId WoodChairId = ItemDefId.FromString("demo.placeable.wood_chair");
        private static readonly ItemDefId GeneratorId = ItemDefId.FromString("demo.electrical.generator");
        private static readonly ItemDefId SwitchId = ItemDefId.FromString("demo.electrical.switch");
        private static readonly ItemDefId DiodeId = ItemDefId.FromString("demo.electrical.diode");
        private static readonly ItemDefId WireSpoolId = ItemDefId.FromString("demo.electrical.wire_spool");

        private static readonly ModifierId SturdyModifierId = ModifierId.FromString("demo.modifier.sturdy");
        private static readonly ModifierId HardenedModifierId = ModifierId.FromString("demo.modifier.hardened_i");
        private static readonly EnchantmentId EmberEnchantmentId = EnchantmentId.FromString("demo.enchantment.ember");

        [MenuItem("Tools/VRGame/Scenes/Build Quest 3 Demo Scene")]
        public static void BuildQuest3DemoScene()
        {
            EnsureFolders();
            DeleteInvalidGeneratedAssets();
            QuestSettingsReport questSettings = ConfigureQuestSettings();
            DemoAssetCatalog catalog = CreateOrUpdateDemoAssets();
            ReloadCoreCatalogAssets(catalog);
            Scene scene = CreateScene();
            BuildScene(scene, catalog, questSettings);
            AddSceneToBuildSettings(ScenePath, 1);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            RunEditorSmokeCheck(catalog);
            Debug.Log($"Quest 3 demo scene built at {ScenePath}.");
        }

        private static QuestSettingsReport ConfigureQuestSettings()
        {
            QuestSettingsReport report = new QuestSettingsReport();

            QualitySettings.antiAliasing = 4;
            report.QualityMsaa = "QualitySettings.antiAliasing set to 4.";

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            report.AndroidGraphicsApi = "Android graphics APIs set to Vulkan first/only for Quest OpenXR optimizations.";

            ConfigureOpenXrSettings(report);
            ConfigureHdrpMsaa(report);

            return report;
        }

        private static void ConfigureOpenXrSettings(QuestSettingsReport report)
        {
            Type openXrSettingsType = FindType("UnityEngine.XR.OpenXR.OpenXRSettings");
            if (openXrSettingsType == null)
            {
                report.OpenXrStatus = "Unity OpenXR settings type was unavailable; settings were documented but not changed.";
                return;
            }

            MethodInfo getSettings = openXrSettingsType.GetMethod("GetSettingsForBuildTargetGroup", BindingFlags.Public | BindingFlags.Static);
            UnityEngine.Object settings = getSettings?.Invoke(null, new object[] { BuildTargetGroup.Android }) as UnityEngine.Object;
            if (settings == null)
            {
                report.OpenXrStatus = "Android OpenXR settings asset was unavailable; enable OpenXR for Android in XR Plug-in Management if needed.";
                return;
            }

            SerializedObject settingsObject = new SerializedObject(settings);
            SetSerializedEnum(settingsObject, "m_renderMode", 1, "Single Pass Instanced / Multi-view", report);
            SetSerializedBool(settingsObject, "m_symmetricProjection", true, "Symmetric Projection", report);
            SetSerializedBool(settingsObject, "m_optimizeBufferDiscards", true, "Optimize Buffer Discards", report);
            SetSerializedEnum(settingsObject, "m_multiviewRenderRegionsOptimizationMode", 1, "Multiview Render Regions Optimization: Final Pass", report);
            SetSerializedEnum(settingsObject, "m_latencyOptimization", 1, "Latency Optimization: Prioritize Input Polling", report);
            settingsObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);

            Array features = InvokeArray(settings, "GetFeatures");
            bool metaQuestFeatureFound = false;
            bool controllerProfileFound = false;
            if (features != null)
            {
                for (int i = 0; i < features.Length; i++)
                {
                    UnityEngine.Object feature = features.GetValue(i) as UnityEngine.Object;
                    if (feature == null)
                    {
                        continue;
                    }

                    string featureTypeName = feature.GetType().FullName;
                    if (featureTypeName == "UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature")
                    {
                        metaQuestFeatureFound = true;
                        EnableOpenXrFeature(feature);
                        ConfigureMetaQuestFeature(feature, report);
                    }
                    else if (featureTypeName != null &&
                             (featureTypeName.Contains("OculusTouchControllerProfile") ||
                              featureTypeName.Contains("MetaQuestTouchPlusControllerProfile") ||
                              featureTypeName.Contains("MetaQuestTouchProControllerProfile")))
                    {
                        controllerProfileFound = true;
                        EnableOpenXrFeature(feature);
                    }
                }
            }

            report.OpenXrStatus = metaQuestFeatureFound
                ? "Android OpenXR settings updated: Meta Quest Support enabled with symmetric projection, buffer discards, and multiview render regions Final Pass when available."
                : "OpenXR settings found, but Meta Quest Support feature asset was unavailable.";
            report.ControllerProfilesStatus = controllerProfileFound
                ? "Quest/Oculus controller interaction profiles enabled when found."
                : "No Quest/Oculus controller interaction profile feature was found to enable automatically.";
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        private static void ConfigureMetaQuestFeature(UnityEngine.Object feature, QuestSettingsReport report)
        {
            SerializedObject featureObject = new SerializedObject(feature);
            SetSerializedBool(featureObject, "m_symmetricProjection", true, "Meta Quest Support Symmetric Projection", report);
            SetSerializedBool(featureObject, "m_optimizeBufferDiscards", true, "Meta Quest Support Optimize Buffer Discards", report);
            SetSerializedEnum(featureObject, "m_multiviewRenderRegionsOptimizationMode", 1, "Meta Quest Support Multiview Render Regions Final Pass", report);

            SerializedProperty targetDevices = featureObject.FindProperty("targetDevices");
            if (targetDevices != null && targetDevices.isArray)
            {
                for (int i = 0; i < targetDevices.arraySize; i++)
                {
                    SerializedProperty device = targetDevices.GetArrayElementAtIndex(i);
                    string manifestName = device.FindPropertyRelative("manifestName")?.stringValue ?? string.Empty;
                    SerializedProperty enabled = device.FindPropertyRelative("enabled");
                    if (enabled == null)
                    {
                        continue;
                    }

                    enabled.boolValue = manifestName != "quest";
                }

                report.TargetDeviceStatus = "Quest 2, Quest Pro, Quest 3, and Quest 3S kept enabled; original Quest disabled because Symmetric Projection requires Quest 2 or newer.";
            }

            featureObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
            AssetDatabase.SaveAssetIfDirty(feature);
        }

        private static void ConfigureHdrpMsaa(QuestSettingsReport report)
        {
            int updatedAssets = 0;
            string[] guids = AssetDatabase.FindAssets("t:RenderPipelineAsset", new[] { "Assets/Settings" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset == null || !asset.GetType().Name.Contains("HDRenderPipelineAsset"))
                {
                    continue;
                }

                SerializedObject serializedObject = new SerializedObject(asset);
                bool changed = false;
                SerializedProperty iterator = serializedObject.GetIterator();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (iterator.name == "enableMSAA" && iterator.propertyType == SerializedPropertyType.Boolean)
                    {
                        iterator.boolValue = true;
                        changed = true;
                    }
                    else if (iterator.name == "msaaSampleCount" && iterator.propertyType == SerializedPropertyType.Integer)
                    {
                        iterator.intValue = 4;
                        changed = true;
                    }
                    else if (iterator.name == "msaaMode" && iterator.propertyType == SerializedPropertyType.Enum)
                    {
                        iterator.enumValueIndex = Mathf.Min(2, Math.Max(0, iterator.enumDisplayNames.Length - 1));
                        changed = true;
                    }
                }

                if (changed)
                {
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                    updatedAssets++;
                }
            }

            report.HdrpMsaaStatus = updatedAssets > 0
                ? $"Updated MSAA-related serialized HDRP fields on {updatedAssets} HDRP asset(s); verify the active HDRP quality tier in Project Settings."
                : "No HDRP assets exposed MSAA fields to the editor builder.";
        }

        private static DemoAssetCatalog CreateOrUpdateDemoAssets()
        {
            DemoAssetCatalog catalog = new DemoAssetCatalog
            {
                WoodMaterial = CreateMaterial("M_Wood", new Color(0.55f, 0.32f, 0.15f, 1f)),
                StoneMaterial = CreateMaterial("M_Stone", new Color(0.46f, 0.48f, 0.5f, 1f)),
                CopperMaterial = CreateMaterial("M_Copper", new Color(0.8f, 0.39f, 0.16f, 1f)),
                GrassMaterial = CreateMaterial("M_Grass", new Color(0.16f, 0.43f, 0.2f, 1f)),
                MetalMaterial = CreateMaterial("M_Metal", new Color(0.38f, 0.43f, 0.48f, 1f)),
                RubyMaterial = CreateMaterial("M_Ruby", new Color(0.75f, 0.05f, 0.18f, 1f)),
                PortalMaterial = CreateMaterial("M_Portal", new Color(0.08f, 0.65f, 0.95f, 0.75f))
            };

            catalog.WoodPhysicalPrefab = CreateWorldItemPrefab("P_WoodResource", PrimitiveType.Cube, new Vector3(0.25f, 0.12f, 0.12f), catalog.WoodMaterial);
            catalog.StonePhysicalPrefab = CreateWorldItemPrefab("P_StoneResource", PrimitiveType.Sphere, new Vector3(0.22f, 0.22f, 0.22f), catalog.StoneMaterial);
            catalog.CopperOrePhysicalPrefab = CreateWorldItemPrefab("P_CopperOreResource", PrimitiveType.Sphere, new Vector3(0.25f, 0.22f, 0.25f), catalog.CopperMaterial);
            catalog.FishPhysicalPrefab = CreateWorldItemPrefab("P_FishResource", PrimitiveType.Capsule, new Vector3(0.15f, 0.08f, 0.28f), catalog.PortalMaterial);
            catalog.PickaxePrefab = CreateToolPrefab("P_CopperPickaxe", catalog.CopperMaterial, HarvestingDomain.Mining, HarvestingSubtype.Pickaxe);
            catalog.AxePrefab = CreateToolPrefab("P_CopperAxe", catalog.CopperMaterial, HarvestingDomain.Lumber, HarvestingSubtype.Axe);
            catalog.FishingTrapPrefab = CreateToolPrefab("P_FishingTrap", catalog.WoodMaterial, HarvestingDomain.Fishing, HarvestingSubtype.Trap);
            catalog.SwordPrefab = CreateSwordPrefab("P_CopperSword", catalog.CopperMaterial);
            catalog.HelmetPrefab = CreateWorldItemPrefab("P_CopperHelmet", PrimitiveType.Sphere, new Vector3(0.28f, 0.18f, 0.28f), catalog.CopperMaterial);
            catalog.RingPrefab = CreateWorldItemPrefab("P_RubyRing", PrimitiveType.Cylinder, new Vector3(0.18f, 0.04f, 0.18f), catalog.RubyMaterial);
            catalog.FoundationPlacedPrefab = CreateFrameworkPlacedPrefab("P_WoodFoundationPlaced", FrameworkPieceKind.Foundation, new Vector3(2f, 0.18f, 2f), catalog.WoodMaterial);
            catalog.WallPlacedPrefab = CreateFrameworkPlacedPrefab("P_WoodWallPlaced", FrameworkPieceKind.Wall, new Vector3(2f, 1.8f, 0.16f), catalog.WoodMaterial);
            catalog.ChairPlacedPrefab = CreatePlacedPrefab("P_WoodChairPlaced", PrimitiveType.Cube, new Vector3(0.6f, 0.7f, 0.6f), catalog.WoodMaterial);
            catalog.GeneratorPlacedPrefab = CreateElectricalPlacedPrefab("P_GeneratorPlaced", typeof(ElectricalGenerator), catalog.MetalMaterial);
            catalog.SwitchPlacedPrefab = CreateElectricalPlacedPrefab("P_SwitchPlaced", typeof(ElectricalSwitch), catalog.MetalMaterial);
            catalog.DiodePlacedPrefab = CreateElectricalPlacedPrefab("P_DiodePlaced", typeof(ElectricalDiode), catalog.MetalMaterial);
            catalog.WireSpoolPhysicalPrefab = CreateWorldItemPrefab("P_WireSpool", PrimitiveType.Cylinder, new Vector3(0.2f, 0.16f, 0.2f), catalog.MetalMaterial);

            Dictionary<ItemDefId, Sprite> icons = CreateGeneratedIcons();

            catalog.ItemDatabase = CreateOrUpdateAsset<ItemDefinitionDatabase>("Quest3Demo_ItemDefinitionDatabase.asset");
            catalog.AffixDatabase = CreateOrUpdateAsset<ItemAffixDefinitionDatabase>("Quest3Demo_ItemAffixDefinitionDatabase.asset");
            catalog.LoadoutConfig = CreateOrUpdateAsset<EquipmentLoadoutConfig>("Quest3Demo_EquipmentLoadout.asset");
            catalog.TreeHarvestProfile = CreateOrUpdateAsset<HarvestableProfileDefinition>("Quest3Demo_TreeHarvest.asset");
            catalog.RockHarvestProfile = CreateOrUpdateAsset<HarvestableProfileDefinition>("Quest3Demo_RockHarvest.asset");
            catalog.FishingHarvestProfile = CreateOrUpdateAsset<HarvestableProfileDefinition>("Quest3Demo_FishingCatch.asset");
            catalog.IconProfile = CreateOrUpdateAsset<IconGenerationProfile>("Quest3Demo_IconGenerationProfile.asset");

            ItemDefinition wood = ConfigureBasicItem(WoodId, "Wood", "Raw lumber used for early framework building.", "Resource > Natural", ItemFlags.Resource | ItemFlags.Material | ItemFlags.CanBeHeld | ItemFlags.CanBeManifested | ItemFlags.CanBeCrafted, catalog.WoodPhysicalPrefab, icons[WoodId]);
            ItemDefinition stone = ConfigureBasicItem(StoneId, "Stone", "Mineable natural stone.", "Resource > Natural", ItemFlags.Resource | ItemFlags.Material | ItemFlags.CanBeHeld | ItemFlags.CanBeManifested | ItemFlags.CanBeCrafted, catalog.StonePhysicalPrefab, icons[StoneId]);
            ItemDefinition copperOre = ConfigureBasicItem(CopperOreId, "Copper Ore", "Early metal ore for tools and wiring.", "Resource > Ore", ItemFlags.Resource | ItemFlags.Material | ItemFlags.CanBeHeld | ItemFlags.CanBeManifested | ItemFlags.CanBeCrafted, catalog.CopperOrePhysicalPrefab, icons[CopperOreId]);
            ItemDefinition fish = ConfigureBasicItem(FishId, "Cave Fish", "Demo fishing catch stack item.", "Resource > Fishing", ItemFlags.Resource | ItemFlags.Material | ItemFlags.CanBeHeld | ItemFlags.CanBeManifested, catalog.FishPhysicalPrefab, icons[FishId]);

            ItemDefinition pickaxe = ConfigureToolItem(CopperPickaxeId, "Copper Pickaxe", "Held VR mining tool. It has no durability.", "Equipment > Tool > Mining > Pickaxe", catalog.PickaxePrefab, icons[CopperPickaxeId], HarvestingDomain.Mining, HarvestingSubtype.Pickaxe, 3f, 1, 1.25f);
            ItemDefinition axe = ConfigureToolItem(CopperAxeId, "Copper Axe", "Held VR lumber tool. It has no durability.", "Equipment > Tool > Lumber > Axe", catalog.AxePrefab, icons[CopperAxeId], HarvestingDomain.Lumber, HarvestingSubtype.Axe, 2f, 1, 1.15f);
            ItemDefinition fishingTrap = ConfigureToolItem(FishingTrapId, "Fishing Trap", "Prototype held fishing/trap tool.", "Equipment > Tool > Fishing > Trap", catalog.FishingTrapPrefab, icons[FishingTrapId], HarvestingDomain.Fishing, HarvestingSubtype.Trap, 1f, 1, 1f);
            ItemDefinition sword = ConfigureSwordItem(CopperSwordId, "Copper Sword", catalog.SwordPrefab, icons[CopperSwordId]);
            ItemDefinition helmet = ConfigureArmorItem(CopperHelmetId, "Copper Helmet", "Loadout armor with demo modifier and enchantment.", "Equipment > Armor > Head", catalog.HelmetPrefab, icons[CopperHelmetId], EquipmentSlotKind.Head, 2f);
            ItemDefinition ring = ConfigureRingItem(RubyRingId, "Ruby Ring", catalog.RingPrefab, icons[RubyRingId]);

            ItemDefinition foundation = ConfigurePlaceableItem(WoodFoundationId, "Wood Foundation", "ARK-style framework foundation.", "Placeable > Framework > Foundation", catalog.WoodPhysicalPrefab, icons[WoodFoundationId], catalog.FoundationPlacedPrefab, PlacementMode.FrameworkSnap, PlaceableKind.Block, FrameworkPieceKind.Foundation);
            ItemDefinition wall = ConfigurePlaceableItem(WoodWallId, "Wood Wall", "Framework wall that requires a valid snap point.", "Placeable > Framework > Wall", catalog.WoodPhysicalPrefab, icons[WoodWallId], catalog.WallPlacedPrefab, PlacementMode.FrameworkSnap, PlaceableKind.Wall, FrameworkPieceKind.Wall);
            ItemDefinition chair = ConfigurePlaceableItem(WoodChairId, "Wood Chair", "Free furniture placement sample.", "Placeable > Furniture", catalog.WoodPhysicalPrefab, icons[WoodChairId], catalog.ChairPlacedPrefab, PlacementMode.FreeFurniture, PlaceableKind.Furniture, FrameworkPieceKind.None);
            ItemDefinition generator = ConfigurePlaceableItem(GeneratorId, "Generator", "Electrical generator device sample.", "Placeable > Electrical > Generator", catalog.WireSpoolPhysicalPrefab, icons[GeneratorId], catalog.GeneratorPlacedPrefab, PlacementMode.ElectricalDevice, PlaceableKind.ElectricalDevice, FrameworkPieceKind.None, ItemFlags.Electrical);
            ItemDefinition switchItem = ConfigurePlaceableItem(SwitchId, "Switch", "Electrical switch device sample.", "Placeable > Electrical > Switch", catalog.WireSpoolPhysicalPrefab, icons[SwitchId], catalog.SwitchPlacedPrefab, PlacementMode.ElectricalDevice, PlaceableKind.ElectricalDevice, FrameworkPieceKind.None, ItemFlags.Electrical);
            ItemDefinition diode = ConfigurePlaceableItem(DiodeId, "Diode", "Directional electrical device sample.", "Placeable > Electrical > Diode", catalog.WireSpoolPhysicalPrefab, icons[DiodeId], catalog.DiodePlacedPrefab, PlacementMode.ElectricalDevice, PlaceableKind.ElectricalDevice, FrameworkPieceKind.None, ItemFlags.Electrical);
            ItemDefinition wireSpool = ConfigurePlaceableItem(WireSpoolId, "Wire Spool", "Wire stack consumed by the wire tool.", "Placeable > Electrical > Wire", catalog.WireSpoolPhysicalPrefab, icons[WireSpoolId], null, PlacementMode.Wire, PlaceableKind.Wire, FrameworkPieceKind.None, ItemFlags.Electrical);

            ModifierDefinition sturdy = ConfigureModifier(SturdyModifierId, "Sturdy", "Demo equipment modifier: adds defense.", new[] { new StatModifier(StatIds.Defense, StatModifierOperation.Flat, 2f, SturdyModifierId.Value) });
            ModifierDefinition hardened = ConfigureModifier(HardenedModifierId, "Hardened I", "Demo tool treatment: raises effective material hardness.", new[] { new StatModifier(StatIds.ToolHardness, StatModifierOperation.Flat, 2f, HardenedModifierId.Value) });
            EnchantmentDefinition ember = ConfigureEnchantment(EmberEnchantmentId, "Ember", "Demo enchantment: adds melee damage scaling.", 3, new[] { new EnchantmentStatEffectData(StatIds.MeleeDamage, StatModifierOperation.AdditivePercent, 0.1f, 0.05f) });

            SetDatabaseDefinitions(catalog.ItemDatabase, wood, stone, copperOre, fish, pickaxe, axe, fishingTrap, sword, helmet, ring, foundation, wall, chair, generator, switchItem, diode, wireSpool);
            SetAffixDatabaseDefinitions(catalog.AffixDatabase, new[] { sturdy, hardened }, new[] { ember });
            ConfigureLoadout(catalog.LoadoutConfig);
            ConfigureHarvestProfile(catalog.TreeHarvestProfile, HarvestingDomain.Lumber, HarvestingSubtype.Axe, 1f, 1, 2f, new HarvestDropEntry(WoodId, StackQuantity.FromLong(5)), "wood", "tree");
            ConfigureHarvestProfile(catalog.RockHarvestProfile, HarvestingDomain.Mining, HarvestingSubtype.Pickaxe, 2f, 1, 2f, new HarvestDropEntry(StoneId, StackQuantity.FromLong(4)), "stone", "ore");
            ConfigureHarvestProfile(catalog.FishingHarvestProfile, HarvestingDomain.Fishing, HarvestingSubtype.Trap, 0f, 1, 1.5f, new HarvestDropEntry(FishId, StackQuantity.FromLong(2)), "water", "fishing");
            ConfigureIconProfile(catalog.IconProfile);

            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static void ReloadCoreCatalogAssets(DemoAssetCatalog catalog)
        {
            catalog.ItemDatabase = LoadGeneratedAsset<ItemDefinitionDatabase>("Quest3Demo_ItemDefinitionDatabase.asset");
            catalog.AffixDatabase = LoadGeneratedAsset<ItemAffixDefinitionDatabase>("Quest3Demo_ItemAffixDefinitionDatabase.asset");
            catalog.LoadoutConfig = LoadGeneratedAsset<EquipmentLoadoutConfig>("Quest3Demo_EquipmentLoadout.asset");
            catalog.TreeHarvestProfile = LoadGeneratedAsset<HarvestableProfileDefinition>("Quest3Demo_TreeHarvest.asset");
            catalog.RockHarvestProfile = LoadGeneratedAsset<HarvestableProfileDefinition>("Quest3Demo_RockHarvest.asset");
            catalog.FishingHarvestProfile = LoadGeneratedAsset<HarvestableProfileDefinition>("Quest3Demo_FishingCatch.asset");
            catalog.IconProfile = LoadGeneratedAsset<IconGenerationProfile>("Quest3Demo_IconGenerationProfile.asset");
        }

        private static T LoadGeneratedAsset<T>(string fileName) where T : ScriptableObject
        {
            string path = $"{DataRoot}/{fileName}";
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Debug.LogWarning($"Quest3Demo builder could not load generated asset {path} as {typeof(T).Name}.");
            }

            return asset;
        }

        private static Scene CreateScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            return scene;
        }

        private static void BuildScene(Scene scene, DemoAssetCatalog catalog, QuestSettingsReport questSettings)
        {
            GameObject environment = new GameObject("Quest3Demo_Environment");
            GameObject systems = new GameObject("Quest3Demo_RuntimeSystems");
            GameObject samples = new GameObject("Quest3Demo_SampleObjects");

            BuildEnvironment(environment.transform, catalog);
            GameObject player = BuildPlayerRig();
            BuildRuntimeSystems(systems.transform, player, catalog);
            BuildSampleObjects(samples.transform, catalog);
            BuildSceneNotes(systems.transform, questSettings);

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void BuildEnvironment(Transform root, DemoAssetCatalog catalog)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Quest3Demo_PerformanceFloor_Static";
            floor.transform.SetParent(root, false);
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(18f, 0.1f, 14f);
            floor.GetComponent<Renderer>().sharedMaterial = catalog.GrassMaterial;
            GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);

            GameObject lightObject = new GameObject("Quest3Demo_BakedKeyLight");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2.5f;
            light.lightmapBakeType = LightmapBakeType.Baked;

            Material skybox = CreateSkyboxMaterial();
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.55f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.28f, 0.32f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.14f, 0.12f);
        }

        private static GameObject BuildPlayerRig()
        {
            GameObject player = null;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HexaRigPrefabPath);
            if (prefab != null)
            {
                player = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                player.name = "Quest3Demo_Player_Hexa4Rig_OpenXR";
            }
            else
            {
                player = new GameObject("Quest3Demo_Player_FallbackOpenXRRig");
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.transform.SetParent(player.transform, false);
                cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.stereoTargetEye = StereoTargetEyeMask.Both;
                cameraObject.AddComponent<AudioListener>();
            }

            player.transform.position = new Vector3(0f, 0f, -4f);
            GameObject hvrGlobalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HvrGlobalPrefabPath);
            if (hvrGlobalPrefab != null)
            {
                GameObject hvrGlobal = PrefabUtility.InstantiatePrefab(hvrGlobalPrefab) as GameObject;
                hvrGlobal.name = "Quest3Demo_HVRGlobal";
            }

            Transform left = new GameObject("Quest3Demo_LeftHandSpawn").transform;
            left.SetParent(player.transform, false);
            left.localPosition = new Vector3(-0.32f, 1.25f, 0.55f);
            Transform right = new GameObject("Quest3Demo_RightHandSpawn").transform;
            right.SetParent(player.transform, false);
            right.localPosition = new Vector3(0.32f, 1.25f, 0.55f);
            Transform fallback = new GameObject("Quest3Demo_FallbackSpawn").transform;
            fallback.SetParent(player.transform, false);
            fallback.localPosition = new Vector3(0f, 1.25f, 0.75f);

            return player;
        }

        private static void BuildRuntimeSystems(Transform root, GameObject player, DemoAssetCatalog catalog)
        {
            ReloadCoreCatalogAssets(catalog);
            ItemDefinitionDatabase itemDatabase = catalog.ItemDatabase;
            ItemAffixDefinitionDatabase affixDatabase = catalog.AffixDatabase;
            EquipmentLoadoutConfig loadoutConfig = catalog.LoadoutConfig;

            GameObject runtimeObject = new GameObject("Quest3Demo_ItemInventoryRuntime");
            runtimeObject.transform.SetParent(root, false);

            DebugInventoryStateProvider provider = runtimeObject.AddComponent<DebugInventoryStateProvider>();
            ItemManifestationService manifestationService = runtimeObject.AddComponent<ItemManifestationService>();
            DefaultHandItemSpawner spawner = runtimeObject.AddComponent<DefaultHandItemSpawner>();
            ManifestationPortal portal = runtimeObject.AddComponent<ManifestationPortal>();
            ItemPlacementService placementService = runtimeObject.AddComponent<ItemPlacementService>();
            Quest3DemoRuntimeBootstrap bootstrap = runtimeObject.AddComponent<Quest3DemoRuntimeBootstrap>();

            Transform leftSpawn = player != null ? player.transform.Find("Quest3Demo_LeftHandSpawn") : null;
            Transform rightSpawn = player != null ? player.transform.Find("Quest3Demo_RightHandSpawn") : null;
            Transform fallbackSpawn = player != null ? player.transform.Find("Quest3Demo_FallbackSpawn") : null;
            Transform spawnedParent = new GameObject("Quest3Demo_SpawnedWorldItems").transform;
            spawnedParent.SetParent(root, false);

            AssignSerialized(provider, ("ownerId", "quest3_demo_player"), ("itemDefinitionDatabase", itemDatabase));
            AssignSerialized(spawner, ("leftHandSpawnTransform", leftSpawn), ("rightHandSpawnTransform", rightSpawn), ("fallbackSpawnTransform", fallbackSpawn));
            AssignSerialized(manifestationService,
                ("handItemSpawnerBehaviour", spawner),
                ("defaultSpawnOrigin", fallbackSpawn),
                ("spawnedItemParent", spawnedParent),
                ("verboseLogging", true));
            AssignSerialized(portal,
                ("manifestationService", manifestationService),
                ("itemDefinitionDatabase", itemDatabase),
                ("inventoryStateProviderBehaviour", provider));
            AssignSerialized(placementService,
                ("itemDefinitionDatabase", itemDatabase),
                ("inventoryStateProviderBehaviour", provider),
                ("placedObjectParent", root),
                ("previewObjectParent", root));

            GameObject canvasObject = new GameObject("Quest3Demo_ThreePanelInventoryUI");
            canvasObject.transform.SetParent(root, false);
            canvasObject.transform.position = new Vector3(0f, 1.45f, -1.35f);
            canvasObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            canvasObject.transform.localScale = Vector3.one * 0.0014f;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            TryAddComponentByTypeName(canvasObject, "UnityEngine.UI.GraphicRaycaster");
            VRInventoryUIController uiController = canvasObject.AddComponent<VRInventoryUIController>();

            GameObject portalVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            portalVisual.name = "Quest3Demo_ManifestationPortal_GripTarget";
            portalVisual.transform.SetParent(root, false);
            portalVisual.transform.position = new Vector3(0f, 1.1f, -1.1f);
            portalVisual.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            portalVisual.transform.localScale = new Vector3(0.55f, 0.03f, 0.55f);
            portalVisual.GetComponent<Renderer>().sharedMaterial = catalog.PortalMaterial;
            portalVisual.AddComponent<Rigidbody>().isKinematic = true;

            AssignSerialized(uiController,
                ("itemDefinitionDatabase", itemDatabase),
                ("equipmentLoadoutConfig", loadoutConfig),
                ("inventoryStateProviderBehaviour", provider),
                ("manifestationPortal", portal),
                ("manifestationService", manifestationService),
                ("verboseLogs", true));
            AssignSerialized(bootstrap,
                ("itemDefinitionDatabase", itemDatabase),
                ("affixDefinitionDatabase", affixDatabase),
                ("equipmentLoadoutConfig", loadoutConfig),
                ("inventoryStateProvider", provider),
                ("manifestationService", manifestationService),
                ("manifestationPortal", portal),
                ("inventoryUiController", uiController),
                ("placementService", placementService),
                ("logResults", true));
            SetBootstrapSeeds(bootstrap);
        }

        private static void BuildSampleObjects(Transform root, DemoAssetCatalog catalog)
        {
            ReloadCoreCatalogAssets(catalog);
            Harvestable tree = CreateTree(root, new Vector3(-5f, 0f, 1.5f), catalog);
            Harvestable rock = CreateRock(root, new Vector3(-2.5f, 0f, 2.2f), catalog);
            Harvestable fish = CreateFishingCatch(root, new Vector3(0.2f, 0f, 2.7f), catalog);

            Quest3DemoRuntimeBootstrap bootstrap = UnityEngine.Object.FindAnyObjectByType<Quest3DemoRuntimeBootstrap>();
            if (bootstrap != null)
            {
                SerializedObject serializedObject = new SerializedObject(bootstrap);
                SerializedProperty harvestables = serializedObject.FindProperty("harvestables");
                harvestables.arraySize = 3;
                harvestables.GetArrayElementAtIndex(0).objectReferenceValue = tree;
                harvestables.GetArrayElementAtIndex(1).objectReferenceValue = rock;
                harvestables.GetArrayElementAtIndex(2).objectReferenceValue = fish;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.name = "Quest3Demo_MeleeDamageDummy";
            dummy.transform.SetParent(root, false);
            dummy.transform.position = new Vector3(3.2f, 1f, 0.9f);
            dummy.transform.localScale = new Vector3(0.9f, 1.2f, 0.9f);
            dummy.AddComponent<Rigidbody>().isKinematic = true;
            dummy.AddComponent<MeleeDamageDummy>();

            GameObject foundation = PrefabUtility.InstantiatePrefab(catalog.FoundationPlacedPrefab) as GameObject;
            foundation.name = "Quest3Demo_PrePlaced_WoodFoundation";
            foundation.transform.SetParent(root, false);
            foundation.transform.position = new Vector3(4.8f, 0.09f, 2.4f);
            FrameworkStructurePiece foundationPiece = foundation.GetComponent<FrameworkStructurePiece>();
            CreateSnapPoint("WallSnap_North", foundation.transform, new Vector3(0f, 1f, 1.08f), foundationPiece);

            GameObject wall = PrefabUtility.InstantiatePrefab(catalog.WallPlacedPrefab) as GameObject;
            wall.name = "Quest3Demo_PrePlaced_WoodWall";
            wall.transform.SetParent(root, false);
            wall.transform.position = new Vector3(4.8f, 1f, 3.48f);

            GameObject generator = PrefabUtility.InstantiatePrefab(catalog.GeneratorPlacedPrefab) as GameObject;
            generator.name = "Quest3Demo_Electrical_Generator";
            generator.transform.SetParent(root, false);
            generator.transform.position = new Vector3(-5.5f, 0.35f, -1.4f);

            GameObject switchObject = PrefabUtility.InstantiatePrefab(catalog.SwitchPlacedPrefab) as GameObject;
            switchObject.name = "Quest3Demo_Electrical_Switch";
            switchObject.transform.SetParent(root, false);
            switchObject.transform.position = new Vector3(-3.6f, 0.35f, -1.4f);

            GameObject diode = PrefabUtility.InstantiatePrefab(catalog.DiodePlacedPrefab) as GameObject;
            diode.name = "Quest3Demo_Electrical_Diode";
            diode.transform.SetParent(root, false);
            diode.transform.position = new Vector3(-1.7f, 0.35f, -1.4f);

            GameObject wireSystem = new GameObject("Quest3Demo_WireToolSystem");
            wireSystem.transform.SetParent(root, false);
            ElectricalConnectionRegistry registry = wireSystem.AddComponent<ElectricalConnectionRegistry>();
            WireToolAction wireTool = wireSystem.AddComponent<WireToolAction>();
            DebugInventoryStateProvider provider = UnityEngine.Object.FindAnyObjectByType<DebugInventoryStateProvider>();
            wireTool.BindRuntime(registry, catalog.ItemDatabase, provider, WireSpoolId);

            CreateLabel(root, "Inventory / Portal / Equipment", new Vector3(0f, 2.45f, -1.4f));
            CreateLabel(root, "Mining and Lumber", new Vector3(-3.8f, 1.4f, 2.6f));
            CreateLabel(root, "Melee Dummy", new Vector3(3.2f, 2.4f, 0.9f));
            CreateLabel(root, "Framework + Electrical", new Vector3(2.7f, 1.8f, 2.7f));
        }

        private static Harvestable CreateTree(Transform root, Vector3 position, DemoAssetCatalog catalog)
        {
            GameObject tree = new GameObject("Quest3Demo_Harvestable_OakTree");
            tree.transform.SetParent(root, false);
            tree.transform.position = position;
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            trunk.transform.localScale = new Vector3(0.35f, 0.8f, 0.35f);
            trunk.GetComponent<Renderer>().sharedMaterial = catalog.WoodMaterial;
            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Crown";
            crown.transform.SetParent(tree.transform, false);
            crown.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            crown.transform.localScale = new Vector3(1.15f, 1f, 1.15f);
            crown.GetComponent<Renderer>().sharedMaterial = catalog.GrassMaterial;
            Harvestable harvestable = tree.AddComponent<Harvestable>();
            AssignSerialized(harvestable,
                ("profileDefinition", catalog.TreeHarvestProfile),
                ("itemDefinitionDatabase", catalog.ItemDatabase),
                ("affixDefinitionDatabase", catalog.AffixDatabase),
                ("inventoryStateProviderBehaviour", UnityEngine.Object.FindAnyObjectByType<DebugInventoryStateProvider>()));
            return harvestable;
        }

        private static Harvestable CreateRock(Transform root, Vector3 position, DemoAssetCatalog catalog)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Quest3Demo_Harvestable_CopperRock";
            rock.transform.SetParent(root, false);
            rock.transform.position = position + new Vector3(0f, 0.45f, 0f);
            rock.transform.localScale = new Vector3(1.2f, 0.75f, 1f);
            rock.GetComponent<Renderer>().sharedMaterial = catalog.StoneMaterial;
            Harvestable harvestable = rock.AddComponent<Harvestable>();
            AssignSerialized(harvestable,
                ("profileDefinition", catalog.RockHarvestProfile),
                ("itemDefinitionDatabase", catalog.ItemDatabase),
                ("affixDefinitionDatabase", catalog.AffixDatabase),
                ("inventoryStateProviderBehaviour", UnityEngine.Object.FindAnyObjectByType<DebugInventoryStateProvider>()));
            return harvestable;
        }

        private static Harvestable CreateFishingCatch(Transform root, Vector3 position, DemoAssetCatalog catalog)
        {
            GameObject catchObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            catchObject.name = "Quest3Demo_Harvestable_FishingCatch";
            catchObject.transform.SetParent(root, false);
            catchObject.transform.position = position + new Vector3(0f, 0.25f, 0f);
            catchObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            catchObject.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            catchObject.GetComponent<Renderer>().sharedMaterial = catalog.PortalMaterial;
            Harvestable harvestable = catchObject.AddComponent<Harvestable>();
            AssignSerialized(harvestable,
                ("profileDefinition", catalog.FishingHarvestProfile),
                ("itemDefinitionDatabase", catalog.ItemDatabase),
                ("affixDefinitionDatabase", catalog.AffixDatabase),
                ("inventoryStateProviderBehaviour", UnityEngine.Object.FindAnyObjectByType<DebugInventoryStateProvider>()));
            return harvestable;
        }

        private static void BuildSceneNotes(Transform root, QuestSettingsReport report)
        {
            GameObject notes = new GameObject("Quest3Demo_QuestSettingsSummary");
            notes.transform.SetParent(root, false);
            TextMesh textMesh = notes.AddComponent<TextMesh>();
            textMesh.text =
                "Quest 3 settings applied where available:\n" +
                "- Vulkan Android graphics API\n" +
                "- OpenXR Single Pass Instanced / Multi-view\n" +
                "- Symmetric Projection enabled\n" +
                "- Optimize Buffer Discards enabled\n" +
                "- Multiview Render Regions: Final Pass\n" +
                "- Quality MSAA: 4x\n" +
                "See Docs/Scenes/Quest3DemoSceneSetup.md";
            textMesh.fontSize = 36;
            textMesh.anchor = TextAnchor.MiddleLeft;
            notes.transform.position = new Vector3(-5.8f, 2.4f, -3.2f);
            notes.transform.localScale = Vector3.one * 0.035f;
        }

        private static void SetBootstrapSeeds(Quest3DemoRuntimeBootstrap bootstrap)
        {
            SerializedObject serializedObject = new SerializedObject(bootstrap);
            SetStackSeeds(serializedObject.FindProperty("stackSeeds"), new[]
            {
                new Quest3DemoStackSeed(WoodId, StackQuantity.FromLong(250)),
                new Quest3DemoStackSeed(StoneId, StackQuantity.FromLong(150)),
                new Quest3DemoStackSeed(CopperOreId, StackQuantity.FromLong(75)),
                new Quest3DemoStackSeed(WoodFoundationId, StackQuantity.FromLong(12)),
                new Quest3DemoStackSeed(WoodWallId, StackQuantity.FromLong(18)),
                new Quest3DemoStackSeed(WoodChairId, StackQuantity.FromLong(3)),
                new Quest3DemoStackSeed(GeneratorId, StackQuantity.FromLong(2)),
                new Quest3DemoStackSeed(SwitchId, StackQuantity.FromLong(3)),
                new Quest3DemoStackSeed(DiodeId, StackQuantity.FromLong(3)),
                new Quest3DemoStackSeed(WireSpoolId, StackQuantity.FromLong(40))
            });
            SetInstanceSeeds(serializedObject.FindProperty("instanceSeeds"), new[]
            {
                new Quest3DemoInstanceSeed(CopperPickaxeId, ItemInstanceId.FromString("demo.instance.copper_pickaxe"), HardenedModifierId, default, 1, 1101, false, string.Empty),
                new Quest3DemoInstanceSeed(CopperAxeId, ItemInstanceId.FromString("demo.instance.copper_axe"), default, default, 1, 1102, false, string.Empty),
                new Quest3DemoInstanceSeed(FishingTrapId, ItemInstanceId.FromString("demo.instance.fishing_trap"), default, default, 1, 1103, false, string.Empty),
                new Quest3DemoInstanceSeed(CopperSwordId, ItemInstanceId.FromString("demo.instance.copper_sword"), default, EmberEnchantmentId, 1, 1201, false, string.Empty),
                new Quest3DemoInstanceSeed(CopperHelmetId, ItemInstanceId.FromString("demo.instance.copper_helmet"), SturdyModifierId, EmberEnchantmentId, 1, 1301, true, EquipmentSlotIdUtility.GetDefaultSlotId(EquipmentSlotKind.Head)),
                new Quest3DemoInstanceSeed(RubyRingId, ItemInstanceId.FromString("demo.instance.ruby_ring"), SturdyModifierId, EmberEnchantmentId, 2, 1302, true, EquipmentSlotIdUtility.GetGeneratedRingSlotId(0))
            });
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RunEditorSmokeCheck(DemoAssetCatalog catalog)
        {
            ReloadCoreCatalogAssets(catalog);
            Quest3DemoRuntimeBootstrap bootstrap = UnityEngine.Object.FindAnyObjectByType<Quest3DemoRuntimeBootstrap>();
            DebugInventoryStateProvider provider = UnityEngine.Object.FindAnyObjectByType<DebugInventoryStateProvider>();
            ItemManifestationService manifestationService = UnityEngine.Object.FindAnyObjectByType<ItemManifestationService>();
            ItemPlacementService placementService = UnityEngine.Object.FindAnyObjectByType<ItemPlacementService>();
            int failures = 0;

            if (bootstrap == null || provider == null || manifestationService == null)
            {
                Debug.LogWarning("Quest3Demo smoke check skipped because runtime services were not found.");
                return;
            }

            bootstrap.BindRuntimeServices();
            bootstrap.SeedOnce();
            PlayerInventoryState inventoryState = provider.InventoryState;

            failures += ExpectTrue("Wood stack seeded", PlayerInventoryOperations.HasStack(inventoryState, WoodId, StackQuantity.One));
            failures += ExpectTrue("Copper sword instance seeded", inventoryState.TryGetInstance(ItemInstanceId.FromString("demo.instance.copper_sword"), out _));
            failures += ExpectTrue("Helmet equipped on Head", inventoryState.EquipmentLoadout.TryGetEquippedItem(EquipmentSlotIdUtility.GetDefaultSlotId(EquipmentSlotKind.Head), out _));

            ItemManifestationResult woodManifest = manifestationService.ManifestStack(inventoryState, catalog.ItemDatabase, WoodId, "right");
            failures += ExpectOperation("Manifest Wood", woodManifest.InventoryResult);
            if (woodManifest.Success)
            {
                failures += ExpectOperation("Return Wood", manifestationService.ReturnToInventory(inventoryState, catalog.ItemDatabase, woodManifest.Reservation.RequestId));
            }

            ItemManifestationResult swordManifest = manifestationService.ManifestItemInstance(inventoryState, catalog.ItemDatabase, ItemInstanceId.FromString("demo.instance.copper_sword"), "right");
            failures += ExpectOperation("Manifest Copper Sword", swordManifest.InventoryResult);
            if (swordManifest.Success)
            {
                failures += ExpectOperation("Return Copper Sword", manifestationService.ReturnToInventory(inventoryState, catalog.ItemDatabase, swordManifest.Reservation.RequestId));
            }

            if (placementService != null)
            {
                PlacementResult placementValidation = placementService.ValidatePlacement(WoodFoundationId, new PlacementPose
                {
                    Position = new Vector3(7f, 0.1f, -2f),
                    Rotation = Quaternion.identity,
                    SurfaceNormal = Vector3.up,
                    SurfaceCollider = UnityEngine.Object.FindAnyObjectByType<Collider>()
                });
                failures += ExpectTrue("Foundation placement validates", placementValidation != null && placementValidation.Success);
            }

            Debug.Log(failures == 0
                ? "Quest3Demo editor smoke check passed."
                : $"Quest3Demo editor smoke check completed with {failures} issue(s).");
        }

        private static int ExpectOperation(string label, InventoryOperationResult result)
        {
            if (result != null && result.Success)
            {
                Debug.Log($"PASS: {label}");
                return 0;
            }

            Debug.LogWarning($"WARN: {label} - {(result == null ? "null result" : result.FailureReason + ": " + result.Message)}");
            return 1;
        }

        private static int ExpectTrue(string label, bool condition)
        {
            if (condition)
            {
                Debug.Log($"PASS: {label}");
                return 0;
            }

            Debug.LogWarning($"WARN: {label}");
            return 1;
        }

        private static ItemDefinition ConfigureBasicItem(ItemDefId id, string displayName, string description, string categoryPath, ItemFlags flags, GameObject worldPrefab, Sprite icon)
        {
            ItemDefinition definition = CreateOrUpdateAsset<ItemDefinition>(SafeFileName(id.Value) + ".asset");
            SerializedObject so = new SerializedObject(definition);
            SetIdentity(so, id, displayName, description, categoryPath, flags, worldPrefab, icon);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static ItemDefinition ConfigureToolItem(ItemDefId id, string displayName, string description, string categoryPath, GameObject worldPrefab, Sprite icon, HarvestingDomain domain, HarvestingSubtype subtype, float hardness, int tier, float speed)
        {
            ItemDefinition definition = ConfigureBasicItem(id, displayName, description, categoryPath, ItemFlags.Equipment | ItemFlags.Tool | ItemFlags.CanBeHeld | ItemFlags.CanBeManifested, worldPrefab, icon);
            SerializedObject so = new SerializedObject(definition);
            so.FindProperty("hasEquipmentProfile").boolValue = true;
            SerializedProperty equipment = so.FindProperty("equipmentProfile");
            equipment.FindPropertyRelative("family").enumValueIndex = (int)EquipmentFamily.Tool;
            equipment.FindPropertyRelative("canEquipToLoadout").boolValue = false;
            equipment.FindPropertyRelative("canBeHeldAsItem").boolValue = true;
            so.FindProperty("hasToolProfile").boolValue = true;
            SerializedProperty tool = so.FindProperty("toolProfile");
            tool.FindPropertyRelative("harvestingType").enumValueIndex = (int)domain;
            tool.FindPropertyRelative("toolSubtype").enumValueIndex = (int)subtype;
            tool.FindPropertyRelative("baseMaterialHardnessScore").floatValue = hardness;
            tool.FindPropertyRelative("toolTier").intValue = tier;
            tool.FindPropertyRelative("harvestSpeed").floatValue = speed;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static ItemDefinition ConfigureSwordItem(ItemDefId id, string displayName, GameObject worldPrefab, Sprite icon)
        {
            ItemDefinition definition = ConfigureBasicItem(id, displayName, "Held VR melee weapon. Held item state is separate from equipment loadout.", "Equipment > Weapon > Melee > True Melee", ItemFlags.Equipment | ItemFlags.Weapon | ItemFlags.CanBeHeld | ItemFlags.CanBeManifested, worldPrefab, icon);
            SerializedObject so = new SerializedObject(definition);
            so.FindProperty("hasEquipmentProfile").boolValue = true;
            SerializedProperty equipment = so.FindProperty("equipmentProfile");
            equipment.FindPropertyRelative("family").enumValueIndex = (int)EquipmentFamily.Weapon;
            equipment.FindPropertyRelative("canEquipToLoadout").boolValue = false;
            equipment.FindPropertyRelative("canBeHeldAsItem").boolValue = true;
            so.FindProperty("hasWeaponProfile").boolValue = true;
            so.FindProperty("weaponProfile").FindPropertyRelative("family").enumValueIndex = (int)WeaponFamily.Melee;
            so.FindProperty("hasMeleeWeaponProfile").boolValue = true;
            SerializedProperty melee = so.FindProperty("meleeWeaponProfile");
            melee.FindPropertyRelative("baseDamage").floatValue = 12f;
            melee.FindPropertyRelative("critChance").floatValue = 0.08f;
            melee.FindPropertyRelative("knockback").floatValue = 2.5f;
            melee.FindPropertyRelative("swingSpeed").floatValue = 1f;
            melee.FindPropertyRelative("trueMelee").boolValue = true;
            melee.FindPropertyRelative("minimumHitVelocity").floatValue = 1.2f;
            melee.FindPropertyRelative("hitCooldownSeconds").floatValue = 0.28f;
            SerializedProperty zones = melee.FindPropertyRelative("damageZones");
            zones.arraySize = 1;
            zones.GetArrayElementAtIndex(0).FindPropertyRelative("zoneId").stringValue = "blade";
            zones.GetArrayElementAtIndex(0).FindPropertyRelative("damageMultiplier").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static ItemDefinition ConfigureArmorItem(ItemDefId id, string displayName, string description, string categoryPath, GameObject worldPrefab, Sprite icon, EquipmentSlotKind slotKind, float defense)
        {
            ItemDefinition definition = ConfigureBasicItem(id, displayName, description, categoryPath, ItemFlags.Equipment | ItemFlags.Armor | ItemFlags.CanBeHeld | ItemFlags.CanBeManifested | ItemFlags.CanBeEquipped | ItemFlags.CanBeSocketed, worldPrefab, icon);
            SerializedObject so = new SerializedObject(definition);
            so.FindProperty("hasEquipmentProfile").boolValue = true;
            SerializedProperty equipment = so.FindProperty("equipmentProfile");
            equipment.FindPropertyRelative("family").enumValueIndex = (int)EquipmentFamily.Armor;
            equipment.FindPropertyRelative("canEquipToLoadout").boolValue = true;
            equipment.FindPropertyRelative("canBeHeldAsItem").boolValue = true;
            equipment.FindPropertyRelative("socketLimit").intValue = 1;
            SetCompatibleSlots(equipment.FindPropertyRelative("compatibleSlots"), slotKind);
            SetStatModifiers(so.FindProperty("baseStatModifiers"), new[] { new StatModifier(StatIds.Defense, StatModifierOperation.Flat, defense, id.Value) });
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static ItemDefinition ConfigureRingItem(ItemDefId id, string displayName, GameObject worldPrefab, Sprite icon)
        {
            ItemDefinition definition = ConfigureBasicItem(id, displayName, "Ring accessory. Ring slots are generated by loadout config.", "Equipment > Accessory > Ring", ItemFlags.Equipment | ItemFlags.Accessory | ItemFlags.CanBeHeld | ItemFlags.CanBeManifested | ItemFlags.CanBeEquipped | ItemFlags.CanBeSocketed, worldPrefab, icon);
            SerializedObject so = new SerializedObject(definition);
            so.FindProperty("hasEquipmentProfile").boolValue = true;
            SerializedProperty equipment = so.FindProperty("equipmentProfile");
            equipment.FindPropertyRelative("family").enumValueIndex = (int)EquipmentFamily.Accessory;
            equipment.FindPropertyRelative("canEquipToLoadout").boolValue = true;
            equipment.FindPropertyRelative("canBeHeldAsItem").boolValue = true;
            equipment.FindPropertyRelative("socketLimit").intValue = 1;
            SetCompatibleSlots(equipment.FindPropertyRelative("compatibleSlots"), EquipmentSlotKind.Ring);
            SetStatModifiers(so.FindProperty("baseStatModifiers"), new[] { new StatModifier(StatIds.MeleeDamage, StatModifierOperation.AdditivePercent, 0.05f, id.Value) });
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static ItemDefinition ConfigurePlaceableItem(ItemDefId id, string displayName, string description, string categoryPath, GameObject worldPrefab, Sprite icon, GameObject placedPrefab, PlacementMode mode, PlaceableKind kind, FrameworkPieceKind frameworkKind, ItemFlags extraFlags = ItemFlags.None)
        {
            ItemDefinition definition = ConfigureBasicItem(id, displayName, description, categoryPath, ItemFlags.Placeable | ItemFlags.Material | ItemFlags.CanBeHeld | ItemFlags.CanBeManifested | ItemFlags.CanBeCrafted | extraFlags, worldPrefab, icon);
            SerializedObject so = new SerializedObject(definition);
            so.FindProperty("hasPlaceableProfile").boolValue = true;
            SerializedProperty profile = so.FindProperty("placeableProfile");
            profile.FindPropertyRelative("placementMode").enumValueIndex = (int)mode;
            profile.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            profile.FindPropertyRelative("placedPrefab").objectReferenceValue = mode == PlacementMode.Wire ? null : placedPrefab;
            profile.FindPropertyRelative("previewPrefab").objectReferenceValue = mode == PlacementMode.Wire ? null : placedPrefab;
            profile.FindPropertyRelative("consumedItemQuantity").FindPropertyRelative("value").longValue = 1;
            profile.FindPropertyRelative("surfaceSnapMode").enumValueIndex = mode == PlacementMode.FrameworkSnap && frameworkKind == FrameworkPieceKind.Foundation
                ? (int)PlacementSurfaceSnapMode.Required
                : (int)PlacementSurfaceSnapMode.Optional;
            profile.FindPropertyRelative("frameworkPieceKind").enumValueIndex = (int)frameworkKind;
            profile.FindPropertyRelative("validGroundLayers").intValue = ~0;
            profile.FindPropertyRelative("collisionRules").FindPropertyRelative("requireNoBlockingOverlap").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static ModifierDefinition ConfigureModifier(ModifierId id, string displayName, string description, IReadOnlyList<StatModifier> statModifiers)
        {
            ModifierDefinition definition = CreateOrUpdateAsset<ModifierDefinition>(SafeFileName(id.Value) + ".asset");
            SerializedObject so = new SerializedObject(definition);
            so.FindProperty("modifierId").FindPropertyRelative("value").stringValue = id.Value;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("rarity").intValue = 1;
            so.FindProperty("weight").floatValue = 1f;
            so.FindProperty("exclusiveGroup").stringValue = id == HardenedModifierId ? "modifier.treatment" : "modifier.primary";
            SetStatModifiers(so.FindProperty("statModifiers"), statModifiers);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static EnchantmentDefinition ConfigureEnchantment(EnchantmentId id, string displayName, string description, int maxLevel, IReadOnlyList<EnchantmentStatEffectData> effects)
        {
            EnchantmentDefinition definition = CreateOrUpdateAsset<EnchantmentDefinition>(SafeFileName(id.Value) + ".asset");
            SerializedObject so = new SerializedObject(definition);
            so.FindProperty("enchantmentId").FindPropertyRelative("value").stringValue = id.Value;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("maxLevel").intValue = maxLevel;
            SerializedProperty groups = so.FindProperty("conflictGroups");
            groups.arraySize = 1;
            groups.GetArrayElementAtIndex(0).stringValue = "elemental";
            SerializedProperty effectsProperty = so.FindProperty("statEffectsPerLevel");
            effectsProperty.arraySize = effects.Count;
            for (int i = 0; i < effects.Count; i++)
            {
                SerializedProperty effect = effectsProperty.GetArrayElementAtIndex(i);
                effect.FindPropertyRelative("statId").FindPropertyRelative("value").stringValue = effects[i].StatId.Value;
                effect.FindPropertyRelative("operation").enumValueIndex = (int)effects[i].Operation;
                effect.FindPropertyRelative("baseValue").floatValue = effects[i].BaseValue;
                effect.FindPropertyRelative("valuePerLevel").floatValue = effects[i].ValuePerLevel;
                effect.FindPropertyRelative("sourceId").stringValue = id.Value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void SetIdentity(SerializedObject so, ItemDefId id, string displayName, string description, string categoryPath, ItemFlags flags, GameObject worldPrefab, Sprite icon)
        {
            so.FindProperty("itemDefId").FindPropertyRelative("value").stringValue = id.Value;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("flags").intValue = (int)flags;
            so.FindProperty("worldPrefab").objectReferenceValue = worldPrefab;
            so.FindProperty("generatedIcon").objectReferenceValue = icon;
            SetCategoryPath(so.FindProperty("categoryPath").FindPropertyRelative("segments"), categoryPath);
        }

        private static void SetCategoryPath(SerializedProperty segmentsProperty, string categoryPath)
        {
            string[] segments = categoryPath.Split('>');
            segmentsProperty.arraySize = segments.Length;
            for (int i = 0; i < segments.Length; i++)
            {
                segmentsProperty.GetArrayElementAtIndex(i).stringValue = segments[i].Trim();
            }
        }

        private static void SetCompatibleSlots(SerializedProperty slots, EquipmentSlotKind slotKind)
        {
            slots.arraySize = 1;
            SerializedProperty slot = slots.GetArrayElementAtIndex(0);
            slot.FindPropertyRelative("slotKind").enumValueIndex = (int)slotKind;
            slot.FindPropertyRelative("indexedSlot").boolValue = slotKind == EquipmentSlotKind.Ring;
            slot.FindPropertyRelative("minIndex").intValue = 0;
            slot.FindPropertyRelative("maxIndex").intValue = slotKind == EquipmentSlotKind.Ring ? 9 : 0;
        }

        private static void SetStatModifiers(SerializedProperty modifiersProperty, IReadOnlyList<StatModifier> modifiers)
        {
            modifiersProperty.arraySize = modifiers?.Count ?? 0;
            if (modifiers == null)
            {
                return;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                SerializedProperty modifierProperty = modifiersProperty.GetArrayElementAtIndex(i);
                modifierProperty.FindPropertyRelative("statId").FindPropertyRelative("value").stringValue = modifiers[i].StatId.Value;
                modifierProperty.FindPropertyRelative("operation").enumValueIndex = (int)modifiers[i].Operation;
                modifierProperty.FindPropertyRelative("value").floatValue = modifiers[i].Value;
                modifierProperty.FindPropertyRelative("sourceId").stringValue = modifiers[i].SourceId;
                modifierProperty.FindPropertyRelative("order").intValue = modifiers[i].Order;
            }
        }

        private static void ConfigureLoadout(EquipmentLoadoutConfig config)
        {
            SerializedObject so = new SerializedObject(config);
            so.FindProperty("includeDefaultBodySlots").boolValue = true;
            so.FindProperty("ringSlotCount").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        private static void ConfigureHarvestProfile(HarvestableProfileDefinition definition, HarvestingDomain domain, HarvestingSubtype subtype, float hardness, int tier, float time, HarvestDropEntry drop, params string[] tags)
        {
            SerializedObject so = new SerializedObject(definition);
            SerializedProperty profile = so.FindProperty("profile");
            profile.FindPropertyRelative("requiredHarvestingType").enumValueIndex = (int)domain;
            profile.FindPropertyRelative("requiredToolSubtype").enumValueIndex = (int)subtype;
            profile.FindPropertyRelative("requiredMaterialHardnessScore").floatValue = hardness;
            profile.FindPropertyRelative("requiredTier").intValue = tier;
            profile.FindPropertyRelative("baseHarvestTime").floatValue = time;
            profile.FindPropertyRelative("requiresCorrectToolFlag").boolValue = true;
            SerializedProperty drops = profile.FindPropertyRelative("simpleDrops");
            drops.arraySize = 1;
            drops.GetArrayElementAtIndex(0).FindPropertyRelative("itemDefId").FindPropertyRelative("value").stringValue = drop.ItemDefId.Value;
            drops.GetArrayElementAtIndex(0).FindPropertyRelative("quantity").FindPropertyRelative("value").longValue = drop.Quantity.Value;
            SerializedProperty materialTags = profile.FindPropertyRelative("materialTags");
            materialTags.arraySize = tags.Length;
            for (int i = 0; i < tags.Length; i++)
            {
                materialTags.GetArrayElementAtIndex(i).stringValue = tags[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void ConfigureIconProfile(IconGenerationProfile profile)
        {
            SerializedObject so = new SerializedObject(profile);
            SetIfExists(so, "outputSize", 256);
            SetIfExists(so, "transparentBackground", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void SetDatabaseDefinitions(ItemDefinitionDatabase database, params ItemDefinition[] definitions)
        {
            SerializedObject so = new SerializedObject(database);
            SerializedProperty list = so.FindProperty("itemDefinitions");
            list.arraySize = definitions.Length;
            for (int i = 0; i < definitions.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            database.RebuildLookup();
            EditorUtility.SetDirty(database);
        }

        private static void SetAffixDatabaseDefinitions(ItemAffixDefinitionDatabase database, ModifierDefinition[] modifiers, EnchantmentDefinition[] enchantments)
        {
            SerializedObject so = new SerializedObject(database);
            SerializedProperty modifierList = so.FindProperty("modifierDefinitions");
            modifierList.arraySize = modifiers.Length;
            for (int i = 0; i < modifiers.Length; i++)
            {
                modifierList.GetArrayElementAtIndex(i).objectReferenceValue = modifiers[i];
            }

            SerializedProperty enchantmentList = so.FindProperty("enchantmentDefinitions");
            enchantmentList.arraySize = enchantments.Length;
            for (int i = 0; i < enchantments.Length; i++)
            {
                enchantmentList.GetArrayElementAtIndex(i).objectReferenceValue = enchantments[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            database.RebuildLookup();
            EditorUtility.SetDirty(database);
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = $"{MaterialRoot}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateSkyboxMaterial()
        {
            string path = $"{MaterialRoot}/M_Quest3Demo_Skybox.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Skybox/Procedural") ?? Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_SkyTint"))
            {
                material.SetColor("_SkyTint", new Color(0.42f, 0.57f, 0.72f, 1f));
            }
            else if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", new Color(0.42f, 0.57f, 0.72f, 1f));
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateWorldItemPrefab(string name, PrimitiveType primitiveType, Vector3 scale, Material material)
        {
            string path = $"{PrefabRoot}/{name}.prefab";
            GameObject root = GameObject.CreatePrimitive(primitiveType);
            root.name = name;
            root.transform.localScale = scale;
            root.GetComponent<Renderer>().sharedMaterial = material;
            Rigidbody rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 1f;
            root.AddComponent<WorldItemIdentity>();
            root.AddComponent<WorldItemView>();
            TryAddComponentByTypeName(root, "HurricaneVR.Framework.Core.HVRGrabbable");
            GameObject prefab = SavePrefab(root, path);
            return prefab;
        }

        private static GameObject CreateToolPrefab(string name, Material material, HarvestingDomain domain, HarvestingSubtype subtype)
        {
            string path = $"{PrefabRoot}/{name}.prefab";
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            root.transform.localScale = new Vector3(0.12f, 0.12f, 0.9f);
            root.GetComponent<Renderer>().sharedMaterial = material;
            root.AddComponent<Rigidbody>();
            root.AddComponent<WorldItemIdentity>();
            WorldItemView view = root.AddComponent<WorldItemView>();
            TryAddComponentByTypeName(root, "HurricaneVR.Framework.Core.HVRGrabbable");
            GameObject hitZone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hitZone.name = subtype + "_HarvestHitZone";
            hitZone.transform.SetParent(root.transform, false);
            hitZone.transform.localPosition = new Vector3(0f, 0f, 0.45f);
            hitZone.transform.localScale = new Vector3(1.6f, 1.6f, 0.3f);
            Collider collider = hitZone.GetComponent<Collider>();
            collider.isTrigger = true;
            HarvestToolHitZoneComponent zone = hitZone.AddComponent<HarvestToolHitZoneComponent>();
            AssignSerialized(zone, ("worldItemView", view), ("itemDefinitionDatabase", null), ("affixDefinitionDatabase", null));
            return SavePrefab(root, path);
        }

        private static GameObject CreateSwordPrefab(string name, Material material)
        {
            string path = $"{PrefabRoot}/{name}.prefab";
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            root.transform.localScale = new Vector3(0.12f, 0.08f, 1.1f);
            root.GetComponent<Renderer>().sharedMaterial = material;
            root.AddComponent<Rigidbody>();
            root.AddComponent<WorldItemIdentity>();
            WorldItemView view = root.AddComponent<WorldItemView>();
            TryAddComponentByTypeName(root, "HurricaneVR.Framework.Core.HVRGrabbable");
            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "Blade_DamageZone";
            blade.transform.SetParent(root.transform, false);
            blade.transform.localPosition = new Vector3(0f, 0f, 0.2f);
            blade.transform.localScale = new Vector3(1.3f, 1.3f, 0.75f);
            Collider bladeCollider = blade.GetComponent<Collider>();
            bladeCollider.isTrigger = true;
            MeleeDamageZoneComponent zone = blade.AddComponent<MeleeDamageZoneComponent>();
            AssignSerialized(zone, ("worldItemView", view), ("damageZoneId", "blade"));
            return SavePrefab(root, path);
        }

        private static GameObject CreatePlacedPrefab(string name, PrimitiveType primitiveType, Vector3 scale, Material material)
        {
            string path = $"{PrefabRoot}/{name}.prefab";
            GameObject root = GameObject.CreatePrimitive(primitiveType);
            root.name = name;
            root.transform.localScale = scale;
            root.GetComponent<Renderer>().sharedMaterial = material;
            root.AddComponent<PlacedObjectIdentity>();
            return SavePrefab(root, path);
        }

        private static GameObject CreateFrameworkPlacedPrefab(string name, FrameworkPieceKind kind, Vector3 scale, Material material)
        {
            GameObject prefab = CreatePlacedPrefab(name, PrimitiveType.Cube, scale, material);
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            FrameworkStructurePiece piece = instance.GetComponent<FrameworkStructurePiece>() ?? instance.AddComponent<FrameworkStructurePiece>();
            piece.Bind(name, kind);
            if (kind == FrameworkPieceKind.Foundation)
            {
                CreateSnapPoint("Snap_North", instance.transform, new Vector3(0f, 0.55f, 0.55f), piece);
                CreateSnapPoint("Snap_South", instance.transform, new Vector3(0f, 0.55f, -0.55f), piece);
            }

            return SavePrefab(instance, $"{PrefabRoot}/{name}.prefab");
        }

        private static GameObject CreateElectricalPlacedPrefab(string name, Type componentType, Material material)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            root.transform.localScale = new Vector3(0.65f, 0.5f, 0.65f);
            root.GetComponent<Renderer>().sharedMaterial = material;
            root.AddComponent<PlacedObjectIdentity>();
            Component component = root.AddComponent(componentType);
            ElectricalNode input = CreateElectricalNode("InputNode", root.transform, new Vector3(-0.45f, 0f, 0f), ElectricalNodeRole.Input, ElectricalNodeKind.Generic);
            ElectricalNode output = CreateElectricalNode("OutputNode", root.transform, new Vector3(0.45f, 0f, 0f), ElectricalNodeRole.Output, ElectricalNodeKind.Generic);
            if (component is ElectricalGenerator)
            {
                AssignSerialized(output, ("roles", (int)ElectricalNodeRole.Output), ("nodeKind", (int)ElectricalNodeKind.GeneratorOutput));
                UnityEngine.Object.DestroyImmediate(input.gameObject);
            }
            else if (component is ElectricalSwitch electricalSwitch)
            {
                AssignSerialized(electricalSwitch, ("inputNode", input), ("outputNode", output));
            }
            else if (component is ElectricalDiode electricalDiode)
            {
                AssignSerialized(electricalDiode, ("inputNode", input), ("outputNode", output));
            }

            return SavePrefab(root, $"{PrefabRoot}/{name}.prefab");
        }

        private static ElectricalNode CreateElectricalNode(string name, Transform parent, Vector3 localPosition, ElectricalNodeRole roles, ElectricalNodeKind kind)
        {
            GameObject nodeObject = new GameObject(name);
            nodeObject.transform.SetParent(parent, false);
            nodeObject.transform.localPosition = localPosition;
            ElectricalNode node = nodeObject.AddComponent<ElectricalNode>();
            AssignSerialized(node, ("roles", (int)roles), ("nodeKind", (int)kind), ("maxWireDistance", 8f));
            return node;
        }

        private static FrameworkSnapPoint CreateSnapPoint(string name, Transform parent, Vector3 localPosition, FrameworkStructurePiece owner)
        {
            GameObject snap = new GameObject(name);
            snap.transform.SetParent(parent, false);
            snap.transform.localPosition = localPosition;
            FrameworkSnapPoint snapPoint = snap.AddComponent<FrameworkSnapPoint>();
            snapPoint.BindOwner(owner);
            return snapPoint;
        }

        private static GameObject SavePrefab(GameObject instance, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static Dictionary<ItemDefId, Sprite> CreateGeneratedIcons()
        {
            Dictionary<ItemDefId, Color> colors = new Dictionary<ItemDefId, Color>
            {
                [WoodId] = new Color(0.55f, 0.32f, 0.15f),
                [StoneId] = Color.gray,
                [CopperOreId] = new Color(0.8f, 0.39f, 0.16f),
                [FishId] = new Color(0.08f, 0.65f, 0.95f),
                [CopperPickaxeId] = new Color(0.85f, 0.45f, 0.18f),
                [CopperAxeId] = new Color(0.75f, 0.35f, 0.18f),
                [FishingTrapId] = new Color(0.45f, 0.28f, 0.1f),
                [CopperSwordId] = new Color(0.9f, 0.45f, 0.2f),
                [CopperHelmetId] = new Color(0.85f, 0.42f, 0.18f),
                [RubyRingId] = new Color(0.75f, 0.05f, 0.18f),
                [WoodFoundationId] = new Color(0.45f, 0.26f, 0.12f),
                [WoodWallId] = new Color(0.5f, 0.3f, 0.14f),
                [WoodChairId] = new Color(0.52f, 0.31f, 0.15f),
                [GeneratorId] = new Color(0.28f, 0.34f, 0.38f),
                [SwitchId] = new Color(0.22f, 0.42f, 0.48f),
                [DiodeId] = new Color(0.32f, 0.25f, 0.48f),
                [WireSpoolId] = new Color(0.16f, 0.16f, 0.16f)
            };

            Dictionary<ItemDefId, Sprite> icons = new Dictionary<ItemDefId, Sprite>();
            foreach (KeyValuePair<ItemDefId, Color> entry in colors)
            {
                string path = $"{IconRoot}/{SafeFileName(entry.Key.Value)}_generated.png";
                Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                Color[] pixels = Enumerable.Repeat(new Color(0f, 0f, 0f, 0f), 64 * 64).ToArray();
                for (int y = 8; y < 56; y++)
                {
                    for (int x = 8; x < 56; x++)
                    {
                        pixels[y * 64 + x] = entry.Value;
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }

                icons[entry.Key] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            return icons;
        }

        private static T CreateOrUpdateAsset<T>(string fileName) where T : ScriptableObject
        {
            string path = $"{DataRoot}/{fileName}";
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                UnityEngine.Object existingMainAsset = AssetDatabase.LoadMainAssetAtPath(path);
                if (existingMainAsset != null || File.Exists(path))
                {
                    if (!AssetDatabase.DeleteAsset(path))
                    {
                        FileUtil.DeleteFileOrDirectory(path);
                        FileUtil.DeleteFileOrDirectory(path + ".meta");
                        AssetDatabase.ImportAsset(DataRoot, ImportAssetOptions.ForceUpdate);
                    }
                }

                asset = ScriptableObject.CreateInstance<T>();
                asset.name = Path.GetFileNameWithoutExtension(fileName);
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        private static void DeleteInvalidGeneratedAssets()
        {
            bool deletedAny = false;
            deletedAny |= DeleteInvalidGeneratedAsset<HarvestableProfileDefinition>("Quest3Demo_TreeHarvest.asset");
            deletedAny |= DeleteInvalidGeneratedAsset<HarvestableProfileDefinition>("Quest3Demo_RockHarvest.asset");
            deletedAny |= DeleteInvalidGeneratedAsset<HarvestableProfileDefinition>("Quest3Demo_FishingCatch.asset");

            if (deletedAny)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
        }

        private static bool DeleteInvalidGeneratedAsset<T>(string fileName) where T : UnityEngine.Object
        {
            string path = $"{DataRoot}/{fileName}";
            if (!File.Exists(path) || AssetDatabase.LoadAssetAtPath<T>(path) != null)
            {
                return false;
            }

            FileUtil.DeleteFileOrDirectory(path);
            FileUtil.DeleteFileOrDirectory(path + ".meta");
            return true;
        }

        private static void SetStackSeeds(SerializedProperty property, IReadOnlyList<Quest3DemoStackSeed> seeds)
        {
            property.arraySize = seeds.Count;
            for (int i = 0; i < seeds.Count; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("itemDefId").FindPropertyRelative("value").stringValue = seeds[i].ItemDefId.Value;
                element.FindPropertyRelative("quantity").FindPropertyRelative("value").longValue = seeds[i].Quantity.Value;
            }
        }

        private static void SetInstanceSeeds(SerializedProperty property, IReadOnlyList<Quest3DemoInstanceSeed> seeds)
        {
            property.arraySize = seeds.Count;
            for (int i = 0; i < seeds.Count; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("itemDefId").FindPropertyRelative("value").stringValue = seeds[i].ItemDefId.Value;
                element.FindPropertyRelative("itemInstanceId").FindPropertyRelative("value").stringValue = seeds[i].ItemInstanceId.Value;
                element.FindPropertyRelative("modifierId").FindPropertyRelative("value").stringValue = seeds[i].ModifierId.Value;
                element.FindPropertyRelative("enchantmentId").FindPropertyRelative("value").stringValue = seeds[i].EnchantmentId.Value;
                element.FindPropertyRelative("enchantmentLevel").intValue = seeds[i].EnchantmentLevel;
                element.FindPropertyRelative("rollSeed").intValue = seeds[i].RollSeed;
                element.FindPropertyRelative("equipOnStart").boolValue = seeds[i].EquipOnStart;
                element.FindPropertyRelative("equipmentSlotId").stringValue = seeds[i].EquipmentSlotId;
            }
        }

        private static void AddSceneToBuildSettings(string scenePath, int desiredIndex)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(s => s != null && !string.Equals(s.path, scenePath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            EditorBuildSettingsScene newScene = new EditorBuildSettingsScene(scenePath, true);
            int index = Mathf.Clamp(desiredIndex, 0, scenes.Count);
            scenes.Insert(index, newScene);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "_Project");
            EnsureFolder("Assets/_Project", "Data");
            EnsureFolder("Assets/_Project/Data", "Quest3Demo");
            EnsureFolder("Assets/_Project", "Prefabs");
            EnsureFolder("Assets/_Project/Prefabs", "Quest3Demo");
            EnsureFolder("Assets/_Project", "GeneratedIcons");
            EnsureFolder("Assets/_Project/GeneratedIcons", "Quest3Demo");
            EnsureFolder("Assets/_Project", "Materials");
            EnsureFolder("Assets/_Project/Materials", "Quest3Demo");
            EnsureFolder("Assets/_Project", "Scenes");
            EnsureFolder("Docs", "Scenes");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void CreateLabel(Transform parent, string text, Vector3 position)
        {
            GameObject label = new GameObject("Label_" + SafeFileName(text));
            label.transform.SetParent(parent, false);
            label.transform.position = position;
            label.transform.localScale = Vector3.one * 0.04f;
            TextMesh textMesh = label.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.fontSize = 34;
            textMesh.anchor = TextAnchor.MiddleCenter;
        }

        private static void AssignSerialized(UnityEngine.Object target, params (string fieldName, object value)[] fields)
        {
            SerializedObject so = new SerializedObject(target);
            for (int i = 0; i < fields.Length; i++)
            {
                SerializedProperty property = so.FindProperty(fields[i].fieldName);
                if (property == null)
                {
                    continue;
                }

                object value = fields[i].value;
                switch (property.propertyType)
                {
                    case SerializedPropertyType.ObjectReference:
                        property.objectReferenceValue = value as UnityEngine.Object;
                        break;
                    case SerializedPropertyType.Boolean:
                        property.boolValue = value is bool boolValue && boolValue;
                        break;
                    case SerializedPropertyType.String:
                        property.stringValue = value as string ?? string.Empty;
                        break;
                    case SerializedPropertyType.Integer:
                        property.intValue = Convert.ToInt32(value);
                        break;
                    case SerializedPropertyType.Float:
                        property.floatValue = Convert.ToSingle(value);
                        break;
                    case SerializedPropertyType.Enum:
                        property.intValue = Convert.ToInt32(value);
                        break;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetSerializedBool(SerializedObject so, string propertyName, bool value, string label, QuestSettingsReport report)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Boolean)
            {
                property.boolValue = value;
                report.AppliedSettings.Add(label);
            }
            else
            {
                report.UnavailableSettings.Add(label);
            }
        }

        private static void SetSerializedEnum(SerializedObject so, string propertyName, int value, string label, QuestSettingsReport report)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Enum)
            {
                property.enumValueIndex = Mathf.Clamp(value, 0, Math.Max(0, property.enumDisplayNames.Length - 1));
                report.AppliedSettings.Add(label);
            }
            else
            {
                report.UnavailableSettings.Add(label);
            }
        }

        private static void SetIfExists(SerializedObject so, string propertyName, object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                property.intValue = Convert.ToInt32(value);
            }
            else if (property.propertyType == SerializedPropertyType.Boolean)
            {
                property.boolValue = Convert.ToBoolean(value);
            }
        }

        private static void EnableOpenXrFeature(UnityEngine.Object feature)
        {
            PropertyInfo enabledProperty = feature.GetType().GetProperty("enabled", BindingFlags.Instance | BindingFlags.Public);
            enabledProperty?.SetValue(feature, true);
            EditorUtility.SetDirty(feature);
        }

        private static Array InvokeArray(UnityEngine.Object target, string methodName)
        {
            MethodInfo method = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(candidate =>
                    candidate.Name == methodName &&
                    !candidate.IsGenericMethodDefinition &&
                    candidate.GetParameters().Length == 0 &&
                    candidate.ReturnType.IsArray);
            return method?.Invoke(target, null) as Array;
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void TryAddComponentByTypeName(GameObject gameObject, string fullName)
        {
            Type type = FindType(fullName);
            if (type != null && gameObject.GetComponent(type) == null)
            {
                gameObject.AddComponent(type);
            }
        }

        private static string SafeFileName(string value)
        {
            string safe = StableIdUtility.Normalize(value);
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(invalid, '_');
            }

            return safe.Replace(' ', '_').Replace('.', '_').Replace('/', '_');
        }

        private sealed class DemoAssetCatalog
        {
            public Material WoodMaterial;
            public Material StoneMaterial;
            public Material CopperMaterial;
            public Material GrassMaterial;
            public Material MetalMaterial;
            public Material RubyMaterial;
            public Material PortalMaterial;
            public GameObject WoodPhysicalPrefab;
            public GameObject StonePhysicalPrefab;
            public GameObject CopperOrePhysicalPrefab;
            public GameObject FishPhysicalPrefab;
            public GameObject PickaxePrefab;
            public GameObject AxePrefab;
            public GameObject FishingTrapPrefab;
            public GameObject SwordPrefab;
            public GameObject HelmetPrefab;
            public GameObject RingPrefab;
            public GameObject FoundationPlacedPrefab;
            public GameObject WallPlacedPrefab;
            public GameObject ChairPlacedPrefab;
            public GameObject GeneratorPlacedPrefab;
            public GameObject SwitchPlacedPrefab;
            public GameObject DiodePlacedPrefab;
            public GameObject WireSpoolPhysicalPrefab;
            public ItemDefinitionDatabase ItemDatabase;
            public ItemAffixDefinitionDatabase AffixDatabase;
            public EquipmentLoadoutConfig LoadoutConfig;
            public HarvestableProfileDefinition TreeHarvestProfile;
            public HarvestableProfileDefinition RockHarvestProfile;
            public HarvestableProfileDefinition FishingHarvestProfile;
            public IconGenerationProfile IconProfile;
        }

        private sealed class QuestSettingsReport
        {
            public readonly List<string> AppliedSettings = new List<string>();
            public readonly List<string> UnavailableSettings = new List<string>();
            public string QualityMsaa = string.Empty;
            public string AndroidGraphicsApi = string.Empty;
            public string OpenXrStatus = string.Empty;
            public string ControllerProfilesStatus = string.Empty;
            public string TargetDeviceStatus = string.Empty;
            public string HdrpMsaaStatus = string.Empty;
        }

        private readonly struct EnchantmentStatEffectData
        {
            public EnchantmentStatEffectData(StatId statId, StatModifierOperation operation, float baseValue, float valuePerLevel)
            {
                StatId = statId;
                Operation = operation;
                BaseValue = baseValue;
                ValuePerLevel = valuePerLevel;
            }

            public StatId StatId { get; }
            public StatModifierOperation Operation { get; }
            public float BaseValue { get; }
            public float ValuePerLevel { get; }
        }
    }
}
