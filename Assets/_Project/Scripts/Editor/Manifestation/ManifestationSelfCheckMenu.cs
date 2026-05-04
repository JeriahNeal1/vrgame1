using UnityEditor;
using UnityEngine;
using VRGame.Items;
using VRGame.Runtime;

namespace VRGame.Manifestation.Editor
{
    public static class ManifestationSelfCheckMenu
    {
        [MenuItem("Tools/VRGame/Items/Run Manifestation Self Checks")]
        public static void RunManifestationSelfChecks()
        {
            GameObject woodPrefab = CreateWorldPrefab("SelfCheck_WoodPrefab");
            GameObject swordPrefab = CreateWorldPrefab("SelfCheck_CopperSwordPrefab");

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

            ItemDefinitionDatabase database = CreateDatabase(wood, copperSword);

            GameObject serviceObject = new GameObject("SelfCheck_ManifestationService");
            DefaultHandItemSpawner spawner = serviceObject.AddComponent<DefaultHandItemSpawner>();
            ItemManifestationService manifestationService = serviceObject.AddComponent<ItemManifestationService>();
            SerializedObject serviceSerialized = new SerializedObject(manifestationService);
            serviceSerialized.FindProperty("handItemSpawnerBehaviour").objectReferenceValue = spawner;
            serviceSerialized.ApplyModifiedPropertiesWithoutUndo();

            PlayerInventoryState inventoryState = new PlayerInventoryState("manifestation_self_check");
            int failures = 0;

            failures += ExpectSuccess(
                "Seed one wood",
                PlayerInventoryOperations.AddStack(inventoryState, database, ItemDefId.FromString("resource.wood"), StackQuantity.One));

            ItemManifestationResult woodManifest = manifestationService.ManifestStack(inventoryState, database, ItemDefId.FromString("resource.wood"), "right");
            failures += ExpectSuccess("Manifest Wood from inventory as quantity 1", woodManifest.InventoryResult);
            failures += ExpectTrue(
                "Manifesting Wood removes one from stack ledger",
                !PlayerInventoryOperations.HasStack(inventoryState, ItemDefId.FromString("resource.wood"), StackQuantity.One));

            ItemManifestationResult duplicateWoodManifest = manifestationService.ManifestStack(inventoryState, database, ItemDefId.FromString("resource.wood"), "right");
            failures += ExpectFailure("Repeated Wood portal call cannot duplicate missing stack", duplicateWoodManifest.InventoryResult);

            failures += ExpectSuccess(
                "Returning Wood restores inventory state",
                manifestationService.ReturnToInventory(inventoryState, database, woodManifest.Reservation.RequestId));
            failures += ExpectTrue(
                "Wood stack restored after return",
                PlayerInventoryOperations.HasStack(inventoryState, ItemDefId.FromString("resource.wood"), StackQuantity.One));

            ItemManifestationResult droppedWoodManifest = manifestationService.ManifestStack(inventoryState, database, ItemDefId.FromString("resource.wood"), "right");
            failures += ExpectSuccess("Manifest Wood for drop", droppedWoodManifest.InventoryResult);
            failures += ExpectSuccess(
                "Dropping Wood leaves inventory state consistent",
                manifestationService.DropManifestedItem(inventoryState, droppedWoodManifest.Reservation.RequestId));
            failures += ExpectTrue(
                "Dropped Wood is not duplicated back into inventory",
                !PlayerInventoryOperations.HasStack(inventoryState, ItemDefId.FromString("resource.wood"), StackQuantity.One));

            failures += ExpectSuccess(
                "Create Copper Sword item instance",
                PlayerInventoryOperations.CreateItemInstance(inventoryState, database, ItemDefId.FromString("equipment.weapon.copper_sword"), ItemInstanceId.FromString("self_check_copper_sword"), out ItemInstanceId swordInstanceId));

            ItemManifestationResult swordManifest = manifestationService.ManifestItemInstance(inventoryState, database, swordInstanceId, "right");
            failures += ExpectSuccess("Manifest Copper Sword as a specific item instance", swordManifest.InventoryResult);
            failures += ExpectTrue(
                "Copper Sword instance moves to HeldInWorld",
                inventoryState.TryGetInstance(swordInstanceId, out ItemInstanceState heldSword) &&
                heldSword.LifecycleState == ItemLifecycleState.HeldInWorld);

            ItemManifestationResult duplicateSwordManifest = manifestationService.ManifestItemInstance(inventoryState, database, swordInstanceId, "right");
            failures += ExpectFailure("Repeated Copper Sword portal call cannot duplicate held instance", duplicateSwordManifest.InventoryResult);

            failures += ExpectSuccess(
                "Returning Copper Sword restores item instance to inventory",
                manifestationService.ReturnToInventory(inventoryState, database, swordManifest.Reservation.RequestId));
            failures += ExpectTrue(
                "Copper Sword instance returns to InInventory",
                inventoryState.TryGetInstance(swordInstanceId, out ItemInstanceState returnedSword) &&
                returnedSword.LifecycleState == ItemLifecycleState.InInventory);

            ItemManifestationResult swordDropManifest = manifestationService.ManifestItemInstance(inventoryState, database, swordInstanceId, "right");
            failures += ExpectSuccess("Manifest Copper Sword for drop", swordDropManifest.InventoryResult);
            failures += ExpectSuccess(
                "Dropping Copper Sword leaves exact instance in DroppedInWorld",
                manifestationService.DropManifestedItem(inventoryState, swordDropManifest.Reservation.RequestId));
            failures += ExpectTrue(
                "Dropped Copper Sword instance state is DroppedInWorld",
                inventoryState.TryGetInstance(swordInstanceId, out ItemInstanceState droppedSword) &&
                droppedSword.LifecycleState == ItemLifecycleState.DroppedInWorld);

            failures += ExpectTrue(
                "Core inventory assembly remains Hurricane-independent",
                typeof(PlayerInventoryState).Assembly.GetType("HurricaneVR.Framework.Core.HVRGrabbable") == null);

            DestroyWorldItemIfPresent(woodManifest);
            DestroyWorldItemIfPresent(duplicateWoodManifest);
            DestroyWorldItemIfPresent(droppedWoodManifest);
            DestroyWorldItemIfPresent(swordManifest);
            DestroyWorldItemIfPresent(duplicateSwordManifest);
            DestroyWorldItemIfPresent(swordDropManifest);

            Object.DestroyImmediate(serviceObject);
            Object.DestroyImmediate(wood);
            Object.DestroyImmediate(copperSword);
            Object.DestroyImmediate(database);
            Object.DestroyImmediate(woodPrefab);
            Object.DestroyImmediate(swordPrefab);

            if (failures == 0)
            {
                Debug.Log("Manifestation self checks passed.");
            }
            else
            {
                Debug.LogError($"Manifestation self checks failed with {failures} failure(s).");
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
            database.name = "ManifestationSelfCheckDatabase";

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

        private static void DestroyWorldItemIfPresent(ItemManifestationResult result)
        {
            if (result != null && result.WorldItemView != null)
            {
                Object.DestroyImmediate(result.WorldItemView.gameObject);
            }
        }
    }
}
