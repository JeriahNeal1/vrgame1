using UnityEditor;
using UnityEngine;

namespace VRGame.Items.Editor
{
    public static class PlayerInventorySelfCheckMenu
    {
        [MenuItem("Tools/VRGame/Items/Run Inventory Self Checks")]
        public static void RunInventorySelfChecks()
        {
            ItemDefinition wood = CreateItemDefinition("resource.wood", ItemFlags.Resource | ItemFlags.Material | ItemFlags.CanBeManifested, "Resource > Natural");
            ItemDefinition stone = CreateItemDefinition("resource.stone", ItemFlags.Resource | ItemFlags.Material | ItemFlags.CanBeManifested, "Resource > Natural");
            ItemDefinition copperSword = CreateItemDefinition("equipment.weapon.copper_sword", ItemFlags.Equipment | ItemFlags.Weapon | ItemFlags.CanBeHeld | ItemFlags.CanBeManifested, "Equipment > Weapon > Melee > True Melee");
            ItemDefinition helmet = CreateItemDefinition(
                "equipment.armor.copper_helmet",
                ItemFlags.Equipment | ItemFlags.Armor | ItemFlags.CanBeEquipped | ItemFlags.CanBeManifested,
                "Equipment > Armor > Head",
                new StatModifierSpec(StatIds.Defense, StatModifierOperation.Flat, 5f));
            ItemDefinition ring = CreateItemDefinition(
                "equipment.accessory.copper_ring",
                ItemFlags.Equipment | ItemFlags.Accessory | ItemFlags.CanBeEquipped | ItemFlags.CanBeManifested,
                "Equipment > Accessory > Ring",
                new StatModifierSpec(StatIds.MeleeDamage, StatModifierOperation.Flat, 2f));

            ItemDefinitionDatabase database = CreateDatabase(wood, stone, copperSword, helmet, ring);

            ModifierDefinition sharpModifier = CreateModifierDefinition(
                "modifier.sharp",
                ItemFlags.Equipment | ItemFlags.Weapon,
                "Equipment > Weapon",
                "modifier.primary",
                new StatModifierSpec(StatIds.MeleeDamage, StatModifierOperation.Flat, 4f));
            ModifierDefinition sturdyModifier = CreateModifierDefinition(
                "modifier.sturdy",
                ItemFlags.Equipment | ItemFlags.Armor,
                "Equipment > Armor",
                "modifier.primary",
                new StatModifierSpec(StatIds.Defense, StatModifierOperation.Flat, 3f));
            EnchantmentDefinition flaringEnchantment = CreateEnchantmentDefinition(
                "enchantment.flaring",
                ItemFlags.Equipment,
                "Equipment",
                3,
                "elemental_damage",
                new EnchantmentStatEffectSpec(StatIds.MeleeDamage, StatModifierOperation.Flat, 1f, 1f));
            EnchantmentDefinition freezingEnchantment = CreateEnchantmentDefinition(
                "enchantment.freezing",
                ItemFlags.Equipment,
                "Equipment",
                3,
                "elemental_damage",
                new EnchantmentStatEffectSpec(StatIds.MeleeDamage, StatModifierOperation.Flat, 1f, 0.5f));
            ItemAffixDefinitionDatabase affixDatabase = CreateAffixDatabase(
                new[] { sharpModifier, sturdyModifier },
                new[] { flaringEnchantment, freezingEnchantment });

            EquipmentLoadoutConfig loadoutConfig = CreateLoadoutConfig(1);
            PlayerInventoryState inventoryState = new PlayerInventoryState("self_check_player");

            int failures = 0;

            failures += ExpectSuccess(
                "Add 1000000 wood",
                PlayerInventoryOperations.AddStack(inventoryState, database, ItemDefId.FromString("resource.wood"), StackQuantity.FromLong(1000000)));

            failures += ExpectTrue(
                "Wood stack contains 1000000",
                PlayerInventoryOperations.HasStack(inventoryState, ItemDefId.FromString("resource.wood"), StackQuantity.FromLong(1000000)));

            failures += ExpectSuccess(
                "Remove 250000 wood",
                PlayerInventoryOperations.RemoveStack(inventoryState, database, ItemDefId.FromString("resource.wood"), StackQuantity.FromLong(250000)));

            failures += ExpectTrue(
                "Wood stack contains remaining 750000",
                PlayerInventoryOperations.HasStack(inventoryState, ItemDefId.FromString("resource.wood"), StackQuantity.FromLong(750000)));

            failures += ExpectFailure(
                "Reject removing more wood than available",
                PlayerInventoryOperations.RemoveStack(inventoryState, database, ItemDefId.FromString("resource.wood"), StackQuantity.FromLong(750001)));

            failures += ExpectSuccess(
                "Create unstackable equipment instance",
                PlayerInventoryOperations.CreateItemInstance(inventoryState, database, ItemDefId.FromString("equipment.weapon.copper_sword"), ItemInstanceId.FromString("test_copper_sword_instance"), out ItemInstanceId copperSwordInstanceId));

            failures += ExpectFailure(
                "Reject modifier application to Wood",
                PlayerInventoryOperations.CanApplyModifier(database, ItemDefId.FromString("resource.wood")));

            failures += ExpectFailure(
                "Reject enchantment application to Stone",
                PlayerInventoryOperations.CanApplyEnchantment(database, ItemDefId.FromString("resource.stone")));

            failures += ExpectSuccess(
                "Apply valid modifier to Copper Sword",
                ItemAffixService.ApplyModifier(inventoryState, database, affixDatabase, copperSwordInstanceId, ModifierId.FromString("modifier.sharp"), 1234));

            failures += ExpectTrue(
                "Copper Sword stores modifier as runtime ID record",
                inventoryState.TryGetInstance(copperSwordInstanceId, out ItemInstanceState copperSwordInstance) &&
                copperSwordInstance.HasModifier(ModifierId.FromString("modifier.sharp")));

            failures += ExpectSuccess(
                "Reroll modifier through ReforgeContext",
                ItemAffixService.RerollModifier(
                    inventoryState,
                    database,
                    affixDatabase,
                    copperSwordInstanceId,
                    new ReforgeContext(ReforgeSourceType.AdminDebug, 5678),
                    out ModifierId rerolledModifierId));

            failures += ExpectTrue(
                "Reroll returned a valid modifier ID",
                !rerolledModifierId.IsEmpty);

            failures += ExpectSuccess(
                "Move instance from InInventory to HeldInWorld",
                PlayerInventoryOperations.MoveInstanceToState(inventoryState, copperSwordInstanceId, ItemLifecycleState.HeldInWorld));

            failures += ExpectTrue(
                "Moved instance reports HeldInWorld",
                inventoryState.TryGetInstance(copperSwordInstanceId, out ItemInstanceState movedInstance) &&
                movedInstance.LifecycleState == ItemLifecycleState.HeldInWorld);

            failures += ExpectTrue(
                "Tools and item instances have no durability field",
                typeof(ItemInstanceState).GetField("durability", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) == null);

            failures += ExpectSuccess(
                "Create helmet instance",
                PlayerInventoryOperations.CreateItemInstance(inventoryState, database, ItemDefId.FromString("equipment.armor.copper_helmet"), ItemInstanceId.FromString("test_copper_helmet_instance"), out ItemInstanceId helmetInstanceId));

            failures += ExpectSuccess(
                "Create ring instance",
                PlayerInventoryOperations.CreateItemInstance(inventoryState, database, ItemDefId.FromString("equipment.accessory.copper_ring"), ItemInstanceId.FromString("test_copper_ring_instance"), out ItemInstanceId ringInstanceId));

            string headSlotId = EquipmentSlotIdUtility.GetDefaultSlotId(EquipmentSlotKind.Head);
            string ringSlotId = EquipmentSlotIdUtility.GetGeneratedRingSlotId(0);
            string missingRingSlotId = EquipmentSlotIdUtility.GetGeneratedRingSlotId(1);

            failures += ExpectSuccess(
                "Can equip helmet into Head",
                EquipmentService.CanEquip(inventoryState, database, loadoutConfig, helmetInstanceId, headSlotId));

            failures += ExpectFailure(
                "Cannot equip helmet into Ring_01",
                EquipmentService.CanEquip(inventoryState, database, loadoutConfig, helmetInstanceId, ringSlotId));

            failures += ExpectSuccess(
                "Can equip ring into Ring_01",
                EquipmentService.CanEquip(inventoryState, database, loadoutConfig, ringInstanceId, ringSlotId));

            failures += ExpectTrue(
                "Ring count is configurable",
                loadoutConfig.TryGetSlot(ringSlotId, out _) && !loadoutConfig.TryGetSlot(missingRingSlotId, out _));

            inventoryState.EquipmentLoadout.ClearStatsDirty();
            failures += ExpectSuccess(
                "Equip helmet from inventory without held item state",
                EquipmentService.Equip(inventoryState, database, loadoutConfig, helmetInstanceId, headSlotId));

            failures += ExpectTrue(
                "Equipping marks stats dirty",
                inventoryState.EquipmentLoadout.StatsDirty);

            failures += ExpectSuccess(
                "Equip ring into Ring_01",
                EquipmentService.Equip(inventoryState, database, loadoutConfig, ringInstanceId, ringSlotId));

            failures += ExpectSuccess(
                "Apply valid modifier to equipped helmet",
                ItemAffixService.ApplyModifier(inventoryState, database, affixDatabase, helmetInstanceId, ModifierId.FromString("modifier.sturdy"), 2222));

            failures += ExpectSuccess(
                "Apply enchantment to valid equipment",
                ItemAffixService.ApplyEnchantment(inventoryState, database, affixDatabase, ringInstanceId, EnchantmentId.FromString("enchantment.flaring"), 2, 3333));

            failures += ExpectFailure(
                "Reject conflicting enchantments",
                ItemAffixService.ApplyEnchantment(inventoryState, database, affixDatabase, ringInstanceId, EnchantmentId.FromString("enchantment.freezing"), 1, 4444));

            StatBlock baseStats = new StatBlock();
            baseStats.SetValue(StatIds.Defense, 10f);
            baseStats.SetValue(StatIds.MeleeDamage, 3f);

            StatBlock aggregatedStats = new StatBlock();
            StatAggregator.RecalculateEquipmentStats(baseStats, inventoryState, database, affixDatabase, aggregatedStats);

            failures += ExpectTrue(
                "Stat aggregation applies base + equipment + modifier/enchantment stats",
                Mathf.Approximately(aggregatedStats.GetValue(StatIds.Defense), 18f) &&
                Mathf.Approximately(aggregatedStats.GetValue(StatIds.MeleeDamage), 7f));

            DestroyTemporaryObjects(
                wood,
                stone,
                copperSword,
                helmet,
                ring,
                database,
                sharpModifier,
                sturdyModifier,
                flaringEnchantment,
                freezingEnchantment,
                affixDatabase,
                loadoutConfig);

            if (failures == 0)
            {
                Debug.Log("Inventory self checks passed.");
            }
            else
            {
                Debug.LogError($"Inventory self checks failed with {failures} failure(s).");
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

        private static ItemDefinition CreateItemDefinition(string itemDefId, ItemFlags flags, string categoryPath, params StatModifierSpec[] statModifiers)
        {
            ItemDefinition itemDefinition = ScriptableObject.CreateInstance<ItemDefinition>();
            itemDefinition.name = itemDefId;

            SerializedObject serializedObject = new SerializedObject(itemDefinition);
            serializedObject.FindProperty("itemDefId").FindPropertyRelative("value").stringValue = itemDefId;
            serializedObject.FindProperty("displayName").stringValue = itemDefId;
            serializedObject.FindProperty("flags").intValue = (int)flags;
            SetCategoryPath(serializedObject.FindProperty("categoryPath").FindPropertyRelative("segments"), categoryPath);
            SetStatModifiers(serializedObject.FindProperty("baseStatModifiers"), statModifiers);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            return itemDefinition;
        }

        private static ModifierDefinition CreateModifierDefinition(string modifierId, ItemFlags requiredFlags, string categoryPath, string exclusiveGroup, StatModifierSpec statModifier)
        {
            ModifierDefinition modifierDefinition = ScriptableObject.CreateInstance<ModifierDefinition>();
            modifierDefinition.name = modifierId;

            SerializedObject serializedObject = new SerializedObject(modifierDefinition);
            serializedObject.FindProperty("modifierId").FindPropertyRelative("value").stringValue = modifierId;
            serializedObject.FindProperty("displayName").stringValue = modifierId;
            serializedObject.FindProperty("rarity").intValue = 1;
            serializedObject.FindProperty("weight").floatValue = 1f;
            serializedObject.FindProperty("exclusiveGroup").stringValue = exclusiveGroup;
            SerializedProperty allowedFilter = serializedObject.FindProperty("allowedItemFilter");
            allowedFilter.FindPropertyRelative("requiredFlags").intValue = (int)requiredFlags;
            SetCategoryPath(GetArrayElementOrCreate(allowedFilter.FindPropertyRelative("categoryFilters"), 0).FindPropertyRelative("segments"), categoryPath);
            allowedFilter.FindPropertyRelative("includeCategoryDescendants").boolValue = true;
            SetStatModifiers(serializedObject.FindProperty("statModifiers"), new[] { statModifier });
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            return modifierDefinition;
        }

        private static EnchantmentDefinition CreateEnchantmentDefinition(string enchantmentId, ItemFlags requiredFlags, string categoryPath, int maxLevel, string conflictGroup, EnchantmentStatEffectSpec statEffect)
        {
            EnchantmentDefinition enchantmentDefinition = ScriptableObject.CreateInstance<EnchantmentDefinition>();
            enchantmentDefinition.name = enchantmentId;

            SerializedObject serializedObject = new SerializedObject(enchantmentDefinition);
            serializedObject.FindProperty("enchantmentId").FindPropertyRelative("value").stringValue = enchantmentId;
            serializedObject.FindProperty("displayName").stringValue = enchantmentId;
            serializedObject.FindProperty("maxLevel").intValue = maxLevel;
            SerializedProperty conflictGroups = serializedObject.FindProperty("conflictGroups");
            conflictGroups.arraySize = 1;
            conflictGroups.GetArrayElementAtIndex(0).stringValue = conflictGroup;
            SerializedProperty allowedFilter = serializedObject.FindProperty("allowedItemFilter");
            allowedFilter.FindPropertyRelative("requiredFlags").intValue = (int)requiredFlags;
            SetCategoryPath(GetArrayElementOrCreate(allowedFilter.FindPropertyRelative("categoryFilters"), 0).FindPropertyRelative("segments"), categoryPath);
            allowedFilter.FindPropertyRelative("includeCategoryDescendants").boolValue = true;
            SetEnchantmentStatEffects(serializedObject.FindProperty("statEffectsPerLevel"), new[] { statEffect });
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            return enchantmentDefinition;
        }

        private static ItemDefinitionDatabase CreateDatabase(params ItemDefinition[] itemDefinitions)
        {
            ItemDefinitionDatabase database = ScriptableObject.CreateInstance<ItemDefinitionDatabase>();
            database.name = "InventorySelfCheckDatabase";

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

        private static ItemAffixDefinitionDatabase CreateAffixDatabase(ModifierDefinition[] modifiers, EnchantmentDefinition[] enchantments)
        {
            ItemAffixDefinitionDatabase database = ScriptableObject.CreateInstance<ItemAffixDefinitionDatabase>();
            database.name = "InventorySelfCheckAffixDatabase";

            SerializedObject serializedObject = new SerializedObject(database);
            SerializedProperty modifiersProperty = serializedObject.FindProperty("modifierDefinitions");
            modifiersProperty.arraySize = modifiers.Length;
            for (int i = 0; i < modifiers.Length; i++)
            {
                modifiersProperty.GetArrayElementAtIndex(i).objectReferenceValue = modifiers[i];
            }

            SerializedProperty enchantmentsProperty = serializedObject.FindProperty("enchantmentDefinitions");
            enchantmentsProperty.arraySize = enchantments.Length;
            for (int i = 0; i < enchantments.Length; i++)
            {
                enchantmentsProperty.GetArrayElementAtIndex(i).objectReferenceValue = enchantments[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            database.RebuildLookup();
            return database;
        }

        private static EquipmentLoadoutConfig CreateLoadoutConfig(int ringSlotCount)
        {
            EquipmentLoadoutConfig config = ScriptableObject.CreateInstance<EquipmentLoadoutConfig>();
            config.name = "InventorySelfCheckLoadoutConfig";

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
                modifierProperty.FindPropertyRelative("sourceId").stringValue = "self_check";
                modifierProperty.FindPropertyRelative("order").intValue = 0;
            }
        }

        private static void SetEnchantmentStatEffects(SerializedProperty effectsProperty, EnchantmentStatEffectSpec[] statEffects)
        {
            if (effectsProperty == null || !effectsProperty.isArray || statEffects == null)
            {
                return;
            }

            effectsProperty.arraySize = statEffects.Length;
            for (int i = 0; i < statEffects.Length; i++)
            {
                SerializedProperty effectProperty = effectsProperty.GetArrayElementAtIndex(i);
                effectProperty.FindPropertyRelative("statId").FindPropertyRelative("value").stringValue = statEffects[i].StatId.Value;
                effectProperty.FindPropertyRelative("operation").enumValueIndex = (int)statEffects[i].Operation;
                effectProperty.FindPropertyRelative("baseValue").floatValue = statEffects[i].BaseValue;
                effectProperty.FindPropertyRelative("valuePerLevel").floatValue = statEffects[i].ValuePerLevel;
                effectProperty.FindPropertyRelative("sourceId").stringValue = "self_check";
                effectProperty.FindPropertyRelative("order").intValue = 0;
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

        private readonly struct EnchantmentStatEffectSpec
        {
            public EnchantmentStatEffectSpec(StatId statId, StatModifierOperation operation, float baseValue, float valuePerLevel)
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
    }
}
