using UnityEditor;
using UnityEngine;
using VRGame.Items;
using VRGame.Runtime;

namespace VRGame.Manifestation.Editor
{
    public static class VRInventoryUISelfCheckMenu
    {
        [MenuItem("Tools/VRGame/Items/Run VR Inventory UI Self Checks")]
        public static void RunVRInventoryUISelfChecks()
        {
            GameObject woodPrefab = CreateWorldPrefab("SelfCheck_UI_WoodPrefab");
            GameObject swordPrefab = CreateWorldPrefab("SelfCheck_UI_CopperSwordPrefab");
            GameObject helmetPrefab = CreateWorldPrefab("SelfCheck_UI_CopperHelmetPrefab");
            GameObject ringPrefab = CreateWorldPrefab("SelfCheck_UI_CopperRingPrefab");

            ItemDefinition wood = CreateItemDefinition(
                "resource.wood",
                ItemFlags.Resource | ItemFlags.Material | ItemFlags.CanBeHeld | ItemFlags.CanBeManifested,
                "Resource > Natural",
                woodPrefab);
            ItemDefinition copperSword = CreateItemDefinition(
                "equipment.weapon.copper_sword",
                ItemFlags.Equipment | ItemFlags.Weapon | ItemFlags.CanBeHeld | ItemFlags.CanBeManifested,
                "Equipment > Weapon > Melee > True Melee",
                swordPrefab);
            ItemDefinition copperHelmet = CreateItemDefinition(
                "equipment.armor.copper_helmet",
                ItemFlags.Equipment | ItemFlags.Armor | ItemFlags.CanBeHeld | ItemFlags.CanBeManifested | ItemFlags.CanBeEquipped,
                "Equipment > Armor > Head",
                helmetPrefab);
            ItemDefinition copperRing = CreateItemDefinition(
                "equipment.accessory.copper_ring",
                ItemFlags.Equipment | ItemFlags.Accessory | ItemFlags.CanBeHeld | ItemFlags.CanBeManifested | ItemFlags.CanBeEquipped,
                "Equipment > Accessory > Ring",
                ringPrefab);

            ItemDefinitionDatabase database = CreateDatabase(wood, copperSword, copperHelmet, copperRing);
            EquipmentLoadoutConfig loadoutConfig = CreateLoadoutConfig(1);
            PlayerInventoryState inventoryState = new PlayerInventoryState("vr_inventory_ui_self_check_player");

            int failures = 0;

            failures += ExpectSuccess(
                "Inventory capacity is not slot-limited",
                PlayerInventoryOperations.AddStack(inventoryState, database, ItemDefId.FromString("resource.wood"), StackQuantity.FromLong(1000000)));

            failures += ExpectSuccess(
                "Create Copper Sword instance",
                PlayerInventoryOperations.CreateItemInstance(inventoryState, database, ItemDefId.FromString("equipment.weapon.copper_sword"), ItemInstanceId.FromString("ui_self_check_sword"), out ItemInstanceId swordInstanceId));
            failures += ExpectSuccess(
                "Create Copper Helmet instance",
                PlayerInventoryOperations.CreateItemInstance(inventoryState, database, ItemDefId.FromString("equipment.armor.copper_helmet"), ItemInstanceId.FromString("ui_self_check_helmet"), out ItemInstanceId helmetInstanceId));
            failures += ExpectSuccess(
                "Create second Copper Helmet instance",
                PlayerInventoryOperations.CreateItemInstance(inventoryState, database, ItemDefId.FromString("equipment.armor.copper_helmet"), ItemInstanceId.FromString("ui_self_check_helmet_reject"), out ItemInstanceId rejectedHelmetInstanceId));
            failures += ExpectSuccess(
                "Create Copper Ring instance",
                PlayerInventoryOperations.CreateItemInstance(inventoryState, database, ItemDefId.FromString("equipment.accessory.copper_ring"), ItemInstanceId.FromString("ui_self_check_ring"), out ItemInstanceId ringInstanceId));

            GameObject serviceObject = new GameObject("SelfCheck_VRInventoryUI");
            DefaultHandItemSpawner spawner = serviceObject.AddComponent<DefaultHandItemSpawner>();
            ItemManifestationService manifestationService = serviceObject.AddComponent<ItemManifestationService>();
            ManifestationPortal portal = serviceObject.AddComponent<ManifestationPortal>();
            VRInventoryUIController controller = serviceObject.AddComponent<VRInventoryUIController>();
            AssignSpawner(manifestationService, spawner);

            TestInventoryProvider provider = new TestInventoryProvider(inventoryState);
            controller.BindRuntime(provider, database, loadoutConfig, manifestationService, portal);

            failures += ExpectTrue(
                "Three-panel UI creates category, list, portal, and equipment surfaces",
                controller.DisplayedInventoryItemCount >= 4 &&
                controller.DisplayedEquipmentSlotCount >= 10);

            controller.SetCategoryFilter(InventoryUiCategory.Resources);
            failures += ExpectTrue(
                "Resource category shows Wood stack",
                controller.DisplayedInventoryItemCount == 1 &&
                controller.TryGetVisibleEntry(0, out InventoryUiEntry resourceEntry) &&
                resourceEntry.Selection.IsStack);

            controller.SelectStack("resource.wood");
            ItemManifestationResult woodManifest = controller.RequestManifestSelectedItem("right");
            failures += ExpectSuccess("Can select Wood and manifest it", woodManifest.InventoryResult);
            failures += ExpectTrue(
                "Manifested Wood represents quantity 1",
                woodManifest.WorldItemView != null &&
                woodManifest.WorldItemView.Identity != null &&
                woodManifest.WorldItemView.Identity.StackQuantity == StackQuantity.One);
            failures += ExpectSuccess(
                "Returning Wood restores inventory state",
                manifestationService.ReturnToInventory(inventoryState, database, woodManifest.Reservation.RequestId));

            controller.SetCategoryFilter(InventoryUiCategory.Weapons);
            controller.SelectItemInstance(swordInstanceId.Value);
            ItemManifestationResult swordManifest = controller.RequestManifestSelectedItem("right");
            failures += ExpectSuccess("Can select a sword instance and manifest it", swordManifest.InventoryResult);
            failures += ExpectSuccess(
                "Returning sword restores exact instance",
                manifestationService.ReturnToInventory(inventoryState, database, swordManifest.Reservation.RequestId));

            string headSlotId = EquipmentSlotIdUtility.GetDefaultSlotId(EquipmentSlotKind.Head);
            string ringSlotId = EquipmentSlotIdUtility.GetGeneratedRingSlotId(0);
            string secondRingSlotId = EquipmentSlotIdUtility.GetGeneratedRingSlotId(1);

            failures += ExpectTrue(
                "Ring count is configurable",
                loadoutConfig.TryGetSlot(ringSlotId, out _) && !loadoutConfig.TryGetSlot(secondRingSlotId, out _));

            controller.SetCategoryFilter(InventoryUiCategory.Armor);
            controller.SelectItemInstance(helmetInstanceId.Value);
            ItemManifestationResult helmetManifest = controller.RequestManifestSelectedItem("right");
            failures += ExpectSuccess("Manifest helmet for Head slot", helmetManifest.InventoryResult);
            failures += ExpectSuccess(
                "Can equip helmet to Head",
                controller.NotifyHeldItemReleaseOverSlot(helmetManifest.WorldItemView, headSlotId));
            failures += ExpectTrue(
                "Equipment panel updates after helmet equip",
                controller.TryGetEquipmentSlotView(headSlotId, out EquipmentSlotView headView) &&
                headView.EquippedDefinition == copperHelmet);

            controller.SelectItemInstance(rejectedHelmetInstanceId.Value);
            ItemManifestationResult rejectedHelmetManifest = controller.RequestManifestSelectedItem("right");
            failures += ExpectSuccess("Manifest second helmet for invalid Ring release", rejectedHelmetManifest.InventoryResult);
            failures += ExpectFailure(
                "Cannot equip helmet to Ring",
                controller.NotifyHeldItemReleaseOverSlot(rejectedHelmetManifest.WorldItemView, ringSlotId));
            failures += ExpectSuccess(
                "Invalid release does not consume held helmet",
                manifestationService.ReturnToInventory(inventoryState, database, rejectedHelmetManifest.Reservation.RequestId));

            controller.SetCategoryFilter(InventoryUiCategory.Accessories);
            controller.SelectItemInstance(ringInstanceId.Value);
            ItemManifestationResult ringManifest = controller.RequestManifestSelectedItem("right");
            failures += ExpectSuccess("Manifest ring for Ring_01", ringManifest.InventoryResult);
            failures += ExpectSuccess(
                "Can equip ring to a ring slot",
                controller.NotifyHeldItemReleaseOverSlot(ringManifest.WorldItemView, ringSlotId));
            failures += ExpectTrue(
                "Equipment panel updates after ring equip",
                controller.TryGetEquipmentSlotView(ringSlotId, out EquipmentSlotView ringView) &&
                ringView.EquippedDefinition == copperRing);

            failures += ExpectSuccess(
                "Unequip through equipment panel controller restores inventory state",
                controller.UnequipSlot(headSlotId));
            failures += ExpectTrue(
                "Equipment panel updates after unequip",
                controller.TryGetEquipmentSlotView(headSlotId, out EquipmentSlotView emptyHeadView) &&
                emptyHeadView.EquippedDefinition == null);

            DestroyTemporaryObjects(
                serviceObject,
                wood,
                copperSword,
                copperHelmet,
                copperRing,
                database,
                loadoutConfig,
                woodPrefab,
                swordPrefab,
                helmetPrefab,
                ringPrefab);

            if (failures == 0)
            {
                Debug.Log("VR inventory UI self checks passed.");
            }
            else
            {
                Debug.LogError($"VR inventory UI self checks failed with {failures} failure(s).");
            }
        }

        private static int ExpectSuccess(string label, InventoryOperationResult result)
        {
            if (result != null && result.Success)
            {
                Debug.Log($"PASS: {label}");
                return 0;
            }

            Debug.LogError($"FAIL: {label} - {FormatResult(result)}");
            return 1;
        }

        private static int ExpectFailure(string label, InventoryOperationResult result)
        {
            if (result != null && !result.Success)
            {
                Debug.Log($"PASS: {label}");
                return 0;
            }

            Debug.LogError($"FAIL: {label} - expected failure but got {FormatResult(result)}");
            return 1;
        }

        private static int ExpectTrue(string label, bool condition)
        {
            if (condition)
            {
                Debug.Log($"PASS: {label}");
                return 0;
            }

            Debug.LogError($"FAIL: {label}");
            return 1;
        }

        private static string FormatResult(InventoryOperationResult result)
        {
            if (result == null)
            {
                return "null result";
            }

            return $"{result.FailureReason}: {result.Message}";
        }

        private static void AssignSpawner(ItemManifestationService manifestationService, DefaultHandItemSpawner spawner)
        {
            SerializedObject serializedObject = new SerializedObject(manifestationService);
            serializedObject.FindProperty("handItemSpawnerBehaviour").objectReferenceValue = spawner;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateWorldPrefab(string name)
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.name = name;
            prefab.AddComponent<Rigidbody>();
            prefab.AddComponent<WorldItemIdentity>();
            prefab.AddComponent<WorldItemView>();
            return prefab;
        }

        private static ItemDefinition CreateItemDefinition(string itemDefId, ItemFlags flags, string categoryPath, GameObject worldPrefab)
        {
            ItemDefinition itemDefinition = ScriptableObject.CreateInstance<ItemDefinition>();
            itemDefinition.name = itemDefId;

            SerializedObject serializedObject = new SerializedObject(itemDefinition);
            serializedObject.FindProperty("itemDefId").FindPropertyRelative("value").stringValue = itemDefId;
            serializedObject.FindProperty("displayName").stringValue = itemDefId;
            serializedObject.FindProperty("flags").intValue = (int)flags;
            serializedObject.FindProperty("worldPrefab").objectReferenceValue = worldPrefab;
            SetCategoryPath(serializedObject.FindProperty("categoryPath").FindPropertyRelative("segments"), categoryPath);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            return itemDefinition;
        }

        private static ItemDefinitionDatabase CreateDatabase(params ItemDefinition[] itemDefinitions)
        {
            ItemDefinitionDatabase database = ScriptableObject.CreateInstance<ItemDefinitionDatabase>();
            database.name = "VRInventoryUISelfCheckDatabase";

            SerializedObject serializedObject = new SerializedObject(database);
            SerializedProperty definitionsProperty = serializedObject.FindProperty("itemDefinitions");
            definitionsProperty.arraySize = itemDefinitions.Length;

            for (int i = 0; i < itemDefinitions.Length; i++)
            {
                definitionsProperty.GetArrayElementAtIndex(i).objectReferenceValue = itemDefinitions[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            database.RebuildLookup();
            return database;
        }

        private static EquipmentLoadoutConfig CreateLoadoutConfig(int ringSlotCount)
        {
            EquipmentLoadoutConfig config = ScriptableObject.CreateInstance<EquipmentLoadoutConfig>();
            config.name = "VRInventoryUISelfCheckLoadoutConfig";

            SerializedObject serializedObject = new SerializedObject(config);
            serializedObject.FindProperty("includeDefaultBodySlots").boolValue = true;
            serializedObject.FindProperty("ringSlotCount").intValue = ringSlotCount;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            return config;
        }

        private static void SetCategoryPath(SerializedProperty segmentsProperty, string categoryPath)
        {
            if (segmentsProperty == null || !segmentsProperty.isArray)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(categoryPath))
            {
                segmentsProperty.arraySize = 0;
                return;
            }

            string[] segments = categoryPath.Split('>');
            segmentsProperty.arraySize = segments.Length;
            for (int i = 0; i < segments.Length; i++)
            {
                segmentsProperty.GetArrayElementAtIndex(i).stringValue = segments[i].Trim();
            }
        }

        private static void DestroyTemporaryObjects(params Object[] objects)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                {
                    Object.DestroyImmediate(objects[i]);
                }
            }
        }

        private sealed class TestInventoryProvider : IPlayerInventoryStateProvider
        {
            public TestInventoryProvider(PlayerInventoryState inventoryState)
            {
                InventoryState = inventoryState;
            }

            public PlayerInventoryState InventoryState { get; }
        }
    }
}
