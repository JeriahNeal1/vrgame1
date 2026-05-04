using UnityEditor;
using UnityEngine;
using VRGame.Items;
using VRGame.Runtime;

namespace VRGame.Manifestation.Editor
{
    public static class MeleeCombatSelfCheckMenu
    {
        [MenuItem("Tools/VRGame/Items/Run Melee Combat Self Checks")]
        public static void RunMeleeCombatSelfChecks()
        {
            GameObject swordPrefab = CreateWorldPrefab("SelfCheck_MeleeSwordPrefab");
            ItemDefinition copperSword = CreateMeleeWeaponDefinition(
                "equipment.weapon.copper_sword",
                "Equipment > Weapon > Melee > True Melee",
                swordPrefab,
                10f,
                0f,
                2f,
                1f,
                true,
                1f,
                0.5f,
                "blade",
                1f,
                0f);
            ItemDefinition copperRing = CreateItemDefinition(
                "equipment.accessory.copper_ring",
                ItemFlags.Equipment | ItemFlags.Accessory | ItemFlags.CanBeEquipped,
                "Equipment > Accessory > Ring",
                null,
                new StatModifierSpec(StatIds.MeleeDamage, StatModifierOperation.Flat, 4f));

            ItemDefinitionDatabase itemDatabase = CreateDatabase(copperSword, copperRing);
            ModifierDefinition sharpModifier = CreateModifierDefinition(
                "modifier.sharp",
                new StatModifierSpec(StatIds.MeleeDamage, StatModifierOperation.Flat, 5f));
            EnchantmentDefinition emberEnchantment = CreateEnchantmentDefinition(
                "enchantment.ember",
                new EnchantmentStatEffectSpec(StatIds.MeleeDamage, StatModifierOperation.Flat, 2f, 0f));
            ItemAffixDefinitionDatabase affixDatabase = CreateAffixDatabase(new[] { sharpModifier }, new[] { emberEnchantment });
            EquipmentLoadoutConfig loadoutConfig = CreateLoadoutConfig(1);

            PlayerInventoryState inventoryState = new PlayerInventoryState("melee_self_check_player");
            int failures = 0;

            failures += ExpectSuccess(
                "Create held sword instance",
                PlayerInventoryOperations.CreateItemInstance(inventoryState, itemDatabase, copperSword.ItemDefId, ItemInstanceId.FromString("self_check_copper_sword"), out ItemInstanceId swordInstanceId));
            failures += ExpectSuccess(
                "Move sword instance to HeldInWorld",
                PlayerInventoryOperations.MoveInstanceToState(inventoryState, swordInstanceId, ItemLifecycleState.HeldInWorld));
            failures += ExpectSuccess(
                "Apply melee damage modifier to sword",
                ItemAffixService.ApplyModifier(inventoryState, itemDatabase, affixDatabase, swordInstanceId, sharpModifier.ModifierId, 1234));
            failures += ExpectSuccess(
                "Apply melee enchantment to sword",
                ItemAffixService.ApplyEnchantment(inventoryState, itemDatabase, affixDatabase, swordInstanceId, emberEnchantment.EnchantmentId, 1, 2345));

            inventoryState.TryGetInstance(swordInstanceId, out ItemInstanceState swordInstance);

            GameObject weaponObject = new GameObject("SelfCheck_MeleeWeapon");
            Rigidbody weaponRigidbody = weaponObject.AddComponent<Rigidbody>();
            weaponRigidbody.useGravity = false;
            WorldItemView worldItemView = weaponObject.AddComponent<WorldItemView>();
            SelfCheckMeleeHitHandler hitHandler = weaponObject.AddComponent<SelfCheckMeleeHitHandler>();
            worldItemView.Bind(new WorldItemBinding
            {
                WorldItemId = "self_check_world_sword",
                RuntimeBindingId = "self_check_melee_binding",
                OwnerId = inventoryState.OwnerId,
                ItemDefId = copperSword.ItemDefId,
                ItemInstanceId = swordInstanceId,
                Quantity = StackQuantity.One,
                LifecycleState = ItemLifecycleState.HeldInWorld,
                ItemDefinition = copperSword,
                ItemInstance = swordInstance
            });

            GameObject zoneObject = new GameObject("Blade");
            zoneObject.transform.SetParent(weaponObject.transform, false);
            BoxCollider zoneCollider = zoneObject.AddComponent<BoxCollider>();
            zoneCollider.isTrigger = true;
            MeleeDamageZoneComponent damageZone = zoneObject.AddComponent<MeleeDamageZoneComponent>();
            damageZone.BindRuntime(worldItemView, itemDatabase, affixDatabase, new SelfCheckInventoryProvider(inventoryState));

            GameObject firstDummyObject = CreateDummy("SelfCheck_Dummy_Fast", out BoxCollider firstDummyCollider, out MeleeDamageDummy firstDummy);
            DamageResult fastHit = damageZone.TryHit(firstDummy, firstDummyCollider, Vector3.zero, Vector3.back, new Vector3(2f, 0f, 0f));
            failures += ExpectDamageAccepted("Sword hit above velocity threshold damages dummy", fastHit);
            float damageWithoutRing = firstDummy.LastDamageContext != null ? firstDummy.LastDamageContext.DamageAmount : 0f;
            failures += ExpectTrue(
                "Modifier and enchantment stats affect held weapon damage",
                damageWithoutRing > 10f);
            failures += ExpectTrue(
                "OnMeleeHit action hook is called",
                hitHandler.HitCount == 1 && hitHandler.LastContext != null);

            DamageResult repeatedHit = damageZone.TryHit(firstDummy, firstDummyCollider, Vector3.zero, Vector3.back, new Vector3(2f, 0f, 0f));
            failures += ExpectDamageRejected("Same swing cannot hit same target every frame", repeatedHit);
            failures += ExpectTrue(
                "Cooldown prevents repeated damage application",
                firstDummy.ReceivedHitCount == 1);

            GameObject slowDummyObject = CreateDummy("SelfCheck_Dummy_Slow", out BoxCollider slowDummyCollider, out MeleeDamageDummy slowDummy);
            DamageResult slowHit = damageZone.TryHit(slowDummy, slowDummyCollider, Vector3.zero, Vector3.back, new Vector3(0.25f, 0f, 0f));
            failures += ExpectDamageRejected("Slow touch does not damage dummy", slowHit);
            failures += ExpectTrue(
                "Slow touch leaves dummy health unchanged",
                slowDummy.ReceivedHitCount == 0 && Mathf.Approximately(slowDummy.CurrentHealth, slowDummy.MaxHealth));

            failures += ExpectSuccess(
                "Create ring instance",
                PlayerInventoryOperations.CreateItemInstance(inventoryState, itemDatabase, copperRing.ItemDefId, ItemInstanceId.FromString("self_check_copper_ring"), out ItemInstanceId ringInstanceId));
            failures += ExpectSuccess(
                "Equip ring for attacker melee stat bonus",
                EquipmentService.Equip(inventoryState, itemDatabase, loadoutConfig, ringInstanceId, EquipmentSlotIdUtility.GetGeneratedRingSlotId(0)));

            damageZone.ClearHitCooldowns();
            GameObject statDummyObject = CreateDummy("SelfCheck_Dummy_Stats", out BoxCollider statDummyCollider, out MeleeDamageDummy statDummy);
            DamageResult statHit = damageZone.TryHit(statDummy, statDummyCollider, Vector3.zero, Vector3.back, new Vector3(2f, 0f, 0f));
            failures += ExpectDamageAccepted("Sword hit with attacker melee stats damages dummy", statHit);
            failures += ExpectTrue(
                "Melee stat modifiers affect damage",
                statDummy.LastDamageContext != null && statDummy.LastDamageContext.DamageAmount > damageWithoutRing);

            failures += ExpectTrue(
                "Tools and melee weapons still have no durability field",
                typeof(ItemInstanceState).GetField("durability", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) == null);

            DestroyTemporaryObjects(
                firstDummyObject,
                slowDummyObject,
                statDummyObject,
                weaponObject,
                swordPrefab,
                copperSword,
                copperRing,
                itemDatabase,
                sharpModifier,
                emberEnchantment,
                affixDatabase,
                loadoutConfig);

            if (failures == 0)
            {
                Debug.Log("Melee combat self checks passed.");
            }
            else
            {
                Debug.LogError($"Melee combat self checks failed with {failures} failure(s).");
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

        private static int ExpectDamageAccepted(string label, DamageResult result)
        {
            if (result != null && result.Accepted && result.AppliedDamage > 0f)
            {
                Debug.Log($"PASS: {label}");
                return 0;
            }

            Debug.LogError($"FAIL: {label} - {FormatDamageResult(result)}");
            return 1;
        }

        private static int ExpectDamageRejected(string label, DamageResult result)
        {
            if (result != null && !result.Accepted)
            {
                Debug.Log($"PASS: {label}");
                return 0;
            }

            Debug.LogError($"FAIL: {label} - expected rejected damage but got {FormatDamageResult(result)}");
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

        private static string FormatDamageResult(DamageResult result)
        {
            if (result == null)
            {
                return "null result";
            }

            return $"{result.Message} ({result.AppliedDamage:0.##})";
        }

        private static GameObject CreateDummy(string objectName, out BoxCollider collider, out MeleeDamageDummy dummy)
        {
            GameObject dummyObject = new GameObject(objectName);
            collider = dummyObject.AddComponent<BoxCollider>();
            dummy = dummyObject.AddComponent<MeleeDamageDummy>();
            dummy.ResetHealth();
            return dummyObject;
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

        private static ItemDefinition CreateMeleeWeaponDefinition(
            string itemDefId,
            string categoryPath,
            GameObject worldPrefab,
            float baseDamage,
            float critChance,
            float knockback,
            float swingSpeed,
            bool trueMelee,
            float minimumHitVelocity,
            float hitCooldownSeconds,
            string damageZoneId,
            float damageZoneMultiplier,
            float damageZoneMinimumVelocityOverride)
        {
            ItemDefinition itemDefinition = CreateItemDefinition(
                itemDefId,
                ItemFlags.Equipment | ItemFlags.Weapon | ItemFlags.CanBeHeld | ItemFlags.CanBeManifested,
                categoryPath,
                worldPrefab);

            SerializedObject serializedObject = new SerializedObject(itemDefinition);
            serializedObject.FindProperty("hasWeaponProfile").boolValue = true;
            serializedObject.FindProperty("hasMeleeWeaponProfile").boolValue = true;

            SerializedProperty weaponProfile = serializedObject.FindProperty("weaponProfile");
            weaponProfile.FindPropertyRelative("family").enumValueIndex = (int)WeaponFamily.Melee;
            weaponProfile.FindPropertyRelative("heldItem").boolValue = true;
            weaponProfile.FindPropertyRelative("occupiesEquipmentSlot").boolValue = false;

            SerializedProperty meleeProfile = serializedObject.FindProperty("meleeWeaponProfile");
            meleeProfile.FindPropertyRelative("baseDamage").floatValue = baseDamage;
            meleeProfile.FindPropertyRelative("critChance").floatValue = critChance;
            meleeProfile.FindPropertyRelative("knockback").floatValue = knockback;
            meleeProfile.FindPropertyRelative("swingSpeed").floatValue = swingSpeed;
            meleeProfile.FindPropertyRelative("trueMelee").boolValue = trueMelee;
            meleeProfile.FindPropertyRelative("minimumHitVelocity").floatValue = minimumHitVelocity;
            meleeProfile.FindPropertyRelative("hitCooldownSeconds").floatValue = hitCooldownSeconds;

            SerializedProperty damageZones = meleeProfile.FindPropertyRelative("damageZones");
            damageZones.arraySize = 1;
            SerializedProperty zone = damageZones.GetArrayElementAtIndex(0);
            zone.FindPropertyRelative("zoneId").stringValue = damageZoneId;
            zone.FindPropertyRelative("damageMultiplier").floatValue = damageZoneMultiplier;
            zone.FindPropertyRelative("minimumHitVelocityOverride").floatValue = damageZoneMinimumVelocityOverride;

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return itemDefinition;
        }

        private static ItemDefinition CreateItemDefinition(string itemDefId, ItemFlags flags, string categoryPath, GameObject worldPrefab = null, params StatModifierSpec[] statModifiers)
        {
            ItemDefinition itemDefinition = ScriptableObject.CreateInstance<ItemDefinition>();
            itemDefinition.name = itemDefId;

            SerializedObject serializedObject = new SerializedObject(itemDefinition);
            serializedObject.FindProperty("itemDefId").FindPropertyRelative("value").stringValue = itemDefId;
            serializedObject.FindProperty("displayName").stringValue = itemDefId;
            serializedObject.FindProperty("flags").intValue = (int)flags;
            serializedObject.FindProperty("worldPrefab").objectReferenceValue = worldPrefab;
            SetCategoryPath(serializedObject.FindProperty("categoryPath").FindPropertyRelative("segments"), categoryPath);
            SetStatModifiers(serializedObject.FindProperty("baseStatModifiers"), statModifiers);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            return itemDefinition;
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
            serializedObject.FindProperty("exclusiveGroup").stringValue = "modifier.primary";
            SerializedProperty allowedFilter = serializedObject.FindProperty("allowedItemFilter");
            allowedFilter.FindPropertyRelative("requiredFlags").intValue = (int)(ItemFlags.Equipment | ItemFlags.Weapon);
            SetCategoryPath(GetArrayElementOrCreate(allowedFilter.FindPropertyRelative("categoryFilters"), 0).FindPropertyRelative("segments"), "Equipment > Weapon");
            allowedFilter.FindPropertyRelative("includeCategoryDescendants").boolValue = true;
            SetStatModifiers(serializedObject.FindProperty("statModifiers"), new[] { statModifier });
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            return modifierDefinition;
        }

        private static EnchantmentDefinition CreateEnchantmentDefinition(string enchantmentId, EnchantmentStatEffectSpec statEffect)
        {
            EnchantmentDefinition enchantmentDefinition = ScriptableObject.CreateInstance<EnchantmentDefinition>();
            enchantmentDefinition.name = enchantmentId;

            SerializedObject serializedObject = new SerializedObject(enchantmentDefinition);
            serializedObject.FindProperty("enchantmentId").FindPropertyRelative("value").stringValue = enchantmentId;
            serializedObject.FindProperty("displayName").stringValue = enchantmentId;
            serializedObject.FindProperty("maxLevel").intValue = 1;
            SerializedProperty allowedFilter = serializedObject.FindProperty("allowedItemFilter");
            allowedFilter.FindPropertyRelative("requiredFlags").intValue = (int)(ItemFlags.Equipment | ItemFlags.Weapon);
            SetCategoryPath(GetArrayElementOrCreate(allowedFilter.FindPropertyRelative("categoryFilters"), 0).FindPropertyRelative("segments"), "Equipment > Weapon");
            allowedFilter.FindPropertyRelative("includeCategoryDescendants").boolValue = true;
            SetEnchantmentStatEffects(serializedObject.FindProperty("statEffectsPerLevel"), new[] { statEffect });
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            return enchantmentDefinition;
        }

        private static ItemDefinitionDatabase CreateDatabase(params ItemDefinition[] itemDefinitions)
        {
            ItemDefinitionDatabase database = ScriptableObject.CreateInstance<ItemDefinitionDatabase>();
            database.name = "MeleeCombatSelfCheckItemDatabase";

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
            database.name = "MeleeCombatSelfCheckAffixDatabase";

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
            config.name = "MeleeCombatSelfCheckLoadoutConfig";

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
                modifierProperty.FindPropertyRelative("sourceId").stringValue = "melee_self_check";
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
                effectProperty.FindPropertyRelative("sourceId").stringValue = "melee_self_check";
                effectProperty.FindPropertyRelative("order").intValue = 0;
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

        private sealed class SelfCheckInventoryProvider : IPlayerInventoryStateProvider
        {
            public SelfCheckInventoryProvider(PlayerInventoryState inventoryState)
            {
                InventoryState = inventoryState;
            }

            public PlayerInventoryState InventoryState { get; }
        }
    }

    internal sealed class SelfCheckMeleeHitHandler : MonoBehaviour, IMeleeHitActionHandler
    {
        public int HitCount { get; private set; }

        public MeleeHitActionContext LastContext { get; private set; }

        public void OnMeleeHit(MeleeHitActionContext context)
        {
            HitCount++;
            LastContext = context;
        }
    }
}
