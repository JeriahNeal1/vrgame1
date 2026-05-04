using UnityEditor;
using UnityEngine;
using VRGame.Items;
using VRGame.Runtime;

namespace VRGame.Manifestation.Editor
{
    public static class HarvestingSelfCheckMenu
    {
        [MenuItem("Tools/VRGame/Items/Run Harvesting Self Checks")]
        public static void RunHarvestingSelfChecks()
        {
            ItemDefinition stone = CreateResourceDefinition("resource.stone", "Resource > Natural");
            ItemDefinition oakWood = CreateResourceDefinition("resource.oak_wood", "Resource > Natural");
            ItemDefinition copperPickaxe = CreateToolDefinition(
                "equipment.tool.copper_pickaxe",
                "Equipment > Tool > Mining > Pickaxe",
                HarvestingDomain.Mining,
                HarvestingSubtype.Pickaxe,
                2f,
                1,
                1f);
            ItemDefinition copperAxe = CreateToolDefinition(
                "equipment.tool.copper_axe",
                "Equipment > Tool > Lumber > Axe",
                HarvestingDomain.Lumber,
                HarvestingSubtype.Axe,
                2f,
                1,
                1f);
            ItemDefinition weakPickaxe = CreateToolDefinition(
                "equipment.tool.weak_pickaxe",
                "Equipment > Tool > Mining > Pickaxe",
                HarvestingDomain.Mining,
                HarvestingSubtype.Pickaxe,
                1f,
                1,
                1f);

            ItemDefinitionDatabase itemDatabase = CreateDatabase(stone, oakWood, copperPickaxe, copperAxe, weakPickaxe);
            ModifierDefinition hardenedModifier = CreateModifierDefinition(
                "modifier.hardened_i",
                new StatModifierSpec(StatIds.ToolHardness, StatModifierOperation.Flat, 1.25f));
            ItemAffixDefinitionDatabase affixDatabase = CreateAffixDatabase(new[] { hardenedModifier });

            HarvestableProfileDefinition stoneProfile = CreateHarvestableProfile(
                "harvestable.stone",
                HarvestingDomain.Mining,
                HarvestingSubtype.Pickaxe,
                2f,
                1,
                1f,
                stone.ItemDefId,
                3);
            HarvestableProfileDefinition oakProfile = CreateHarvestableProfile(
                "harvestable.oak_wood",
                HarvestingDomain.Lumber,
                HarvestingSubtype.Axe,
                1f,
                1,
                1f,
                oakWood.ItemDefId,
                5);

            PlayerInventoryState inventoryState = new PlayerInventoryState("harvesting_self_check_player");
            SelfCheckInventoryProvider provider = new SelfCheckInventoryProvider(inventoryState);
            int failures = 0;

            WorldItemView pickaxeWorld = CreateHeldToolWorldItem("SelfCheck_CopperPickaxe", inventoryState, itemDatabase, copperPickaxe, "self_check_copper_pickaxe", out _);
            WorldItemView axeWorld = CreateHeldToolWorldItem("SelfCheck_CopperAxe", inventoryState, itemDatabase, copperAxe, "self_check_copper_axe", out _);
            WorldItemView weakPickaxeWorld = CreateHeldToolWorldItem("SelfCheck_WeakPickaxe", inventoryState, itemDatabase, weakPickaxe, "self_check_weak_pickaxe", out ItemInstanceId weakPickaxeInstanceId);
            WorldItemView hardenedWeakPickaxeWorld = CreateHeldToolWorldItem("SelfCheck_HardenedWeakPickaxe", inventoryState, itemDatabase, weakPickaxe, "self_check_hardened_weak_pickaxe", out ItemInstanceId hardenedWeakPickaxeInstanceId);

            failures += ExpectSuccess(
                "Apply Hardened modifier to weak pickaxe",
                ItemAffixService.ApplyModifier(inventoryState, itemDatabase, affixDatabase, hardenedWeakPickaxeInstanceId, hardenedModifier.ModifierId, 2468));

            Harvestable stoneTargetForHitZone = CreateHarvestable("SelfCheck_Stone_HitZone", stoneProfile, itemDatabase, affixDatabase, provider);
            HarvestToolHitZoneComponent pickaxeHitZone = CreateHarvestToolHitZone(pickaxeWorld, itemDatabase, affixDatabase, provider);
            HarvestHitResult pickaxeStoneResult = pickaxeHitZone.TryHarvest(stoneTargetForHitZone, 1.2f);
            failures += ExpectHarvested("Copper Pickaxe mines Stone", pickaxeStoneResult);
            failures += ExpectTrue(
                "Stone drops are added to inventory",
                PlayerInventoryOperations.HasStack(inventoryState, stone.ItemDefId, StackQuantity.FromLong(3)));

            Harvestable oakTargetForAxe = CreateHarvestable("SelfCheck_OakWood_Axe", oakProfile, itemDatabase, affixDatabase, provider);
            HarvestHitResult axeWoodResult = oakTargetForAxe.TryHarvestHit(axeWorld, 1f);
            failures += ExpectHarvested("Copper Axe chops Oak Wood", axeWoodResult);
            failures += ExpectTrue(
                "Oak Wood drops are added to inventory",
                PlayerInventoryOperations.HasStack(inventoryState, oakWood.ItemDefId, StackQuantity.FromLong(5)));

            Harvestable oakTargetForPickaxeReject = CreateHarvestable("SelfCheck_OakWood_PickaxeReject", oakProfile, itemDatabase, affixDatabase, provider);
            HarvestHitResult pickaxeWoodResult = oakTargetForPickaxeReject.TryHarvestHit(pickaxeWorld, 1f);
            failures += ExpectFailure(
                "Pickaxe cannot chop Oak Wood",
                pickaxeWoodResult,
                HarvestValidationFailureReason.HarvestingTypeMismatch);

            Harvestable stoneTargetForAxeReject = CreateHarvestable("SelfCheck_Stone_AxeReject", stoneProfile, itemDatabase, affixDatabase, provider);
            HarvestHitResult axeStoneResult = stoneTargetForAxeReject.TryHarvestHit(axeWorld, 1f);
            failures += ExpectFailure(
                "Axe cannot mine Stone",
                axeStoneResult,
                HarvestValidationFailureReason.HarvestingTypeMismatch);

            Harvestable stoneTargetForWeakReject = CreateHarvestable("SelfCheck_Stone_WeakReject", stoneProfile, itemDatabase, affixDatabase, provider);
            HarvestHitResult weakPickaxeStoneResult = stoneTargetForWeakReject.TryHarvestHit(weakPickaxeWorld, 1f);
            failures += ExpectFailure(
                "Tool below hardness requirement fails",
                weakPickaxeStoneResult,
                HarvestValidationFailureReason.ToolHardnessTooLow);

            Harvestable stoneTargetForHardenedPass = CreateHarvestable("SelfCheck_Stone_HardenedPass", stoneProfile, itemDatabase, affixDatabase, provider);
            HarvestHitResult hardenedPickaxeStoneResult = stoneTargetForHardenedPass.TryHarvestHit(hardenedWeakPickaxeWorld, 1f);
            failures += ExpectHarvested("Hardened modifier can raise effective hardness enough to pass", hardenedPickaxeStoneResult);

            failures += ExpectTrue(
                "Tools do not lose durability",
                typeof(ItemInstanceState).GetField("durability", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) == null);

            DestroyTemporaryObjects(
                pickaxeHitZone.gameObject,
                stoneTargetForHitZone.gameObject,
                oakTargetForAxe.gameObject,
                oakTargetForPickaxeReject.gameObject,
                stoneTargetForAxeReject.gameObject,
                stoneTargetForWeakReject.gameObject,
                stoneTargetForHardenedPass.gameObject,
                pickaxeWorld.gameObject,
                axeWorld.gameObject,
                weakPickaxeWorld.gameObject,
                hardenedWeakPickaxeWorld.gameObject,
                stone,
                oakWood,
                copperPickaxe,
                copperAxe,
                weakPickaxe,
                itemDatabase,
                hardenedModifier,
                affixDatabase,
                stoneProfile,
                oakProfile);

            if (failures == 0)
            {
                Debug.Log("Harvesting self checks passed.");
            }
            else
            {
                Debug.LogError($"Harvesting self checks failed with {failures} failure(s).");
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

        private static int ExpectHarvested(string label, HarvestHitResult result)
        {
            if (result != null && result.Success && result.Harvested)
            {
                Debug.Log($"PASS: {label}");
                return 0;
            }

            Debug.LogError($"FAIL: {label} - {FormatHarvestResult(result)}");
            return 1;
        }

        private static int ExpectFailure(string label, HarvestHitResult result, HarvestValidationFailureReason expectedReason)
        {
            if (result != null &&
                !result.Success &&
                result.ValidationResult != null &&
                result.ValidationResult.FailureReason == expectedReason)
            {
                Debug.Log($"PASS: {label}");
                return 0;
            }

            Debug.LogError($"FAIL: {label} - expected {expectedReason}, got {FormatHarvestResult(result)}");
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

        private static string FormatHarvestResult(HarvestHitResult result)
        {
            if (result == null)
            {
                return "null result";
            }

            string reason = result.ValidationResult != null ? result.ValidationResult.FailureReason.ToString() : "NoValidation";
            return $"{reason}: {result.Message}";
        }

        private static ItemDefinition CreateResourceDefinition(string itemDefId, string categoryPath)
        {
            return CreateItemDefinition(itemDefId, ItemFlags.Resource | ItemFlags.Material | ItemFlags.CanBeHarvested, categoryPath);
        }

        private static ItemDefinition CreateToolDefinition(
            string itemDefId,
            string categoryPath,
            HarvestingDomain harvestingDomain,
            HarvestingSubtype harvestingSubtype,
            float hardness,
            int tier,
            float harvestSpeed)
        {
            ItemDefinition itemDefinition = CreateItemDefinition(
                itemDefId,
                ItemFlags.Equipment | ItemFlags.Tool | ItemFlags.CanBeHeld | ItemFlags.CanBeManifested,
                categoryPath);

            SerializedObject serializedObject = new SerializedObject(itemDefinition);
            serializedObject.FindProperty("hasToolProfile").boolValue = true;
            SerializedProperty toolProfile = serializedObject.FindProperty("toolProfile");
            toolProfile.FindPropertyRelative("harvestingType").enumValueIndex = (int)harvestingDomain;
            toolProfile.FindPropertyRelative("toolSubtype").enumValueIndex = (int)harvestingSubtype;
            toolProfile.FindPropertyRelative("baseMaterialHardnessScore").floatValue = hardness;
            toolProfile.FindPropertyRelative("toolTier").intValue = tier;
            toolProfile.FindPropertyRelative("harvestSpeed").floatValue = harvestSpeed;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return itemDefinition;
        }

        private static ItemDefinition CreateItemDefinition(string itemDefId, ItemFlags flags, string categoryPath)
        {
            ItemDefinition itemDefinition = ScriptableObject.CreateInstance<ItemDefinition>();
            itemDefinition.name = itemDefId;

            SerializedObject serializedObject = new SerializedObject(itemDefinition);
            serializedObject.FindProperty("itemDefId").FindPropertyRelative("value").stringValue = itemDefId;
            serializedObject.FindProperty("displayName").stringValue = itemDefId;
            serializedObject.FindProperty("flags").intValue = (int)flags;
            SetCategoryPath(serializedObject.FindProperty("categoryPath").FindPropertyRelative("segments"), categoryPath);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return itemDefinition;
        }

        private static HarvestableProfileDefinition CreateHarvestableProfile(
            string profileName,
            HarvestingDomain requiredDomain,
            HarvestingSubtype requiredSubtype,
            float requiredHardness,
            int requiredTier,
            float baseHarvestTime,
            ItemDefId dropItemDefId,
            long dropQuantity)
        {
            HarvestableProfileDefinition profileDefinition = ScriptableObject.CreateInstance<HarvestableProfileDefinition>();
            profileDefinition.name = profileName;

            SerializedObject serializedObject = new SerializedObject(profileDefinition);
            SerializedProperty profile = serializedObject.FindProperty("profile");
            profile.FindPropertyRelative("requiredHarvestingType").enumValueIndex = (int)requiredDomain;
            profile.FindPropertyRelative("requiredToolSubtype").enumValueIndex = (int)requiredSubtype;
            profile.FindPropertyRelative("requiredMaterialHardnessScore").floatValue = requiredHardness;
            profile.FindPropertyRelative("requiredTier").intValue = requiredTier;
            profile.FindPropertyRelative("baseHarvestTime").floatValue = baseHarvestTime;
            profile.FindPropertyRelative("requiresCorrectToolFlag").boolValue = true;

            SerializedProperty simpleDrops = profile.FindPropertyRelative("simpleDrops");
            simpleDrops.arraySize = 1;
            SerializedProperty drop = simpleDrops.GetArrayElementAtIndex(0);
            drop.FindPropertyRelative("itemDefId").FindPropertyRelative("value").stringValue = dropItemDefId.Value;
            drop.FindPropertyRelative("quantity").FindPropertyRelative("value").longValue = dropQuantity;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return profileDefinition;
        }

        private static ModifierDefinition CreateModifierDefinition(string modifierId, StatModifierSpec statModifier)
        {
            ModifierDefinition modifierDefinition = ScriptableObject.CreateInstance<ModifierDefinition>();
            modifierDefinition.name = modifierId;

            SerializedObject serializedObject = new SerializedObject(modifierDefinition);
            serializedObject.FindProperty("modifierId").FindPropertyRelative("value").stringValue = modifierId;
            serializedObject.FindProperty("displayName").stringValue = modifierId;
            serializedObject.FindProperty("rarity").intValue = 1;
            serializedObject.FindProperty("weight").floatValue = 1f;
            serializedObject.FindProperty("exclusiveGroup").stringValue = "modifier.treatment";
            SerializedProperty allowedFilter = serializedObject.FindProperty("allowedItemFilter");
            allowedFilter.FindPropertyRelative("requiredFlags").intValue = (int)(ItemFlags.Equipment | ItemFlags.Tool);
            SetCategoryPath(GetArrayElementOrCreate(allowedFilter.FindPropertyRelative("categoryFilters"), 0).FindPropertyRelative("segments"), "Equipment > Tool");
            allowedFilter.FindPropertyRelative("includeCategoryDescendants").boolValue = true;
            SetStatModifiers(serializedObject.FindProperty("statModifiers"), new[] { statModifier });
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return modifierDefinition;
        }

        private static ItemDefinitionDatabase CreateDatabase(params ItemDefinition[] itemDefinitions)
        {
            ItemDefinitionDatabase database = ScriptableObject.CreateInstance<ItemDefinitionDatabase>();
            database.name = "HarvestingSelfCheckItemDatabase";

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

        private static ItemAffixDefinitionDatabase CreateAffixDatabase(ModifierDefinition[] modifiers)
        {
            ItemAffixDefinitionDatabase database = ScriptableObject.CreateInstance<ItemAffixDefinitionDatabase>();
            database.name = "HarvestingSelfCheckAffixDatabase";

            SerializedObject serializedObject = new SerializedObject(database);
            SerializedProperty modifiersProperty = serializedObject.FindProperty("modifierDefinitions");
            modifiersProperty.arraySize = modifiers.Length;
            for (int i = 0; i < modifiers.Length; i++)
            {
                modifiersProperty.GetArrayElementAtIndex(i).objectReferenceValue = modifiers[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            database.RebuildLookup();
            return database;
        }

        private static Harvestable CreateHarvestable(
            string objectName,
            HarvestableProfileDefinition profileDefinition,
            ItemDefinitionDatabase itemDatabase,
            ItemAffixDefinitionDatabase affixDatabase,
            IPlayerInventoryStateProvider provider)
        {
            GameObject harvestableObject = new GameObject(objectName);
            harvestableObject.AddComponent<BoxCollider>();
            Harvestable harvestable = harvestableObject.AddComponent<Harvestable>();
            harvestable.BindRuntime(profileDefinition.Profile, itemDatabase, affixDatabase, provider);
            return harvestable;
        }

        private static HarvestToolHitZoneComponent CreateHarvestToolHitZone(
            WorldItemView worldItemView,
            ItemDefinitionDatabase itemDatabase,
            ItemAffixDefinitionDatabase affixDatabase,
            IPlayerInventoryStateProvider provider)
        {
            GameObject hitZoneObject = new GameObject("SelfCheck_HarvestHitZone");
            hitZoneObject.transform.SetParent(worldItemView.transform, false);
            hitZoneObject.AddComponent<BoxCollider>().isTrigger = true;
            HarvestToolHitZoneComponent hitZone = hitZoneObject.AddComponent<HarvestToolHitZoneComponent>();
            hitZone.BindRuntime(worldItemView, itemDatabase, affixDatabase, provider);
            return hitZone;
        }

        private static WorldItemView CreateHeldToolWorldItem(
            string objectName,
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDatabase,
            ItemDefinition toolDefinition,
            string itemInstanceId,
            out ItemInstanceId createdInstanceId)
        {
            PlayerInventoryOperations.CreateItemInstance(inventoryState, itemDatabase, toolDefinition.ItemDefId, ItemInstanceId.FromString(itemInstanceId), out createdInstanceId);
            PlayerInventoryOperations.MoveInstanceToState(inventoryState, createdInstanceId, ItemLifecycleState.HeldInWorld);
            inventoryState.TryGetInstance(createdInstanceId, out ItemInstanceState itemInstance);

            GameObject toolObject = new GameObject(objectName);
            toolObject.AddComponent<Rigidbody>().useGravity = false;
            WorldItemView worldItemView = toolObject.AddComponent<WorldItemView>();
            worldItemView.Bind(new WorldItemBinding
            {
                WorldItemId = objectName,
                RuntimeBindingId = objectName + "_binding",
                OwnerId = inventoryState.OwnerId,
                ItemDefId = toolDefinition.ItemDefId,
                ItemInstanceId = createdInstanceId,
                Quantity = StackQuantity.One,
                LifecycleState = ItemLifecycleState.HeldInWorld,
                ItemDefinition = toolDefinition,
                ItemInstance = itemInstance
            });

            return worldItemView;
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

        private static void SetStatModifiers(SerializedProperty modifiersProperty, StatModifierSpec[] statModifiers)
        {
            if (modifiersProperty == null || !modifiersProperty.isArray || statModifiers == null)
            {
                return;
            }

            modifiersProperty.arraySize = statModifiers.Length;
            for (int i = 0; i < statModifiers.Length; i++)
            {
                SerializedProperty modifierProperty = modifiersProperty.GetArrayElementAtIndex(i);
                modifierProperty.FindPropertyRelative("statId").FindPropertyRelative("value").stringValue = statModifiers[i].StatId.Value;
                modifierProperty.FindPropertyRelative("operation").enumValueIndex = (int)statModifiers[i].Operation;
                modifierProperty.FindPropertyRelative("value").floatValue = statModifiers[i].Value;
                modifierProperty.FindPropertyRelative("sourceId").stringValue = "harvesting_self_check";
                modifierProperty.FindPropertyRelative("order").intValue = 0;
            }
        }

        private static SerializedProperty GetArrayElementOrCreate(SerializedProperty arrayProperty, int index)
        {
            if (arrayProperty == null || !arrayProperty.isArray)
            {
                return null;
            }

            while (arrayProperty.arraySize <= index)
            {
                arrayProperty.InsertArrayElementAtIndex(arrayProperty.arraySize);
            }

            return arrayProperty.GetArrayElementAtIndex(index);
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

        private readonly struct StatModifierSpec
        {
            public StatModifierSpec(StatId statId, StatModifierOperation operation, float value)
            {
                StatId = statId;
                Operation = operation;
                Value = value;
            }

            public StatId StatId { get; }

            public StatModifierOperation Operation { get; }

            public float Value { get; }
        }

        private sealed class SelfCheckInventoryProvider : IPlayerInventoryStateProvider
        {
            public SelfCheckInventoryProvider(PlayerInventoryState inventoryState)
            {
                InventoryState = inventoryState;
            }

            public PlayerInventoryState InventoryState { get; }
        }
    }
}
