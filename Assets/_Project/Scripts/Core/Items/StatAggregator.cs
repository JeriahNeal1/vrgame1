using System;
using System.Collections.Generic;

namespace VRGame.Items
{
    public static class StatAggregator
    {
        public static void RecalculateEquipmentStats(
            StatBlock baseStats,
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            StatBlock outputStats,
            IReadOnlyList<IItemInstanceStatModifierProvider> extensionProviders = null,
            bool clearDirtyFlag = true)
        {
            RecalculateEquipmentStats(
                baseStats,
                inventoryState,
                itemDefinitionDatabase,
                null,
                outputStats,
                extensionProviders,
                clearDirtyFlag);
        }

        public static void RecalculateEquipmentStats(
            StatBlock baseStats,
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            StatBlock outputStats,
            IReadOnlyList<IItemInstanceStatModifierProvider> extensionProviders = null,
            bool clearDirtyFlag = true)
        {
            if (outputStats == null)
            {
                return;
            }

            outputStats.CopyFrom(baseStats);

            if (inventoryState == null || itemDefinitionDatabase == null)
            {
                return;
            }

            List<StatModifier> modifiers = new List<StatModifier>();
            IReadOnlyList<EquipmentSlotAssignment> equippedSlots = inventoryState.EquipmentLoadout.EquippedSlots;
            for (int i = 0; i < equippedSlots.Count; i++)
            {
                EquipmentSlotAssignment assignment = equippedSlots[i];
                if (assignment == null || assignment.ItemInstanceId.IsEmpty)
                {
                    continue;
                }

                if (!inventoryState.TryGetInstance(assignment.ItemInstanceId, out ItemInstanceState itemInstance) || itemInstance == null)
                {
                    continue;
                }

                if (!itemDefinitionDatabase.TryGet(itemInstance.ItemDefId, out ItemDefinition itemDefinition) || itemDefinition == null)
                {
                    continue;
                }

                AddValidModifiers(itemDefinition.BaseStatModifiers, modifiers);
                AddAffixModifiers(itemInstance, affixDefinitionDatabase, modifiers);

                if (extensionProviders != null)
                {
                    for (int providerIndex = 0; providerIndex < extensionProviders.Count; providerIndex++)
                    {
                        extensionProviders[providerIndex]?.AddStatModifiers(itemInstance, itemDefinition, modifiers);
                    }
                }
            }

            ApplyModifiers(outputStats, modifiers);

            if (clearDirtyFlag)
            {
                inventoryState.EquipmentLoadout.ClearStatsDirty();
            }
        }

        public static void RecalculateItemInstanceStats(
            StatBlock baseStats,
            ItemInstanceState itemInstance,
            ItemDefinition itemDefinition,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            StatBlock outputStats,
            IReadOnlyList<IItemInstanceStatModifierProvider> extensionProviders = null)
        {
            if (outputStats == null)
            {
                return;
            }

            outputStats.CopyFrom(baseStats);
            if (itemDefinition == null)
            {
                return;
            }

            List<StatModifier> modifiers = new List<StatModifier>();
            AddValidModifiers(itemDefinition.BaseStatModifiers, modifiers);
            AddAffixModifiers(itemInstance, affixDefinitionDatabase, modifiers);

            if (extensionProviders != null)
            {
                for (int providerIndex = 0; providerIndex < extensionProviders.Count; providerIndex++)
                {
                    extensionProviders[providerIndex]?.AddStatModifiers(itemInstance, itemDefinition, modifiers);
                }
            }

            ApplyModifiers(outputStats, modifiers);
        }

        public static void ApplyModifiers(StatBlock statBlock, IReadOnlyList<StatModifier> modifiers)
        {
            if (statBlock == null || modifiers == null || modifiers.Count == 0)
            {
                return;
            }

            Dictionary<string, StatAccumulator> accumulators = new Dictionary<string, StatAccumulator>(StableIdUtility.Comparer);

            IReadOnlyList<StatValueRecord> baseValues = statBlock.Values;
            for (int i = 0; i < baseValues.Count; i++)
            {
                StatValueRecord baseValue = baseValues[i];
                if (baseValue != null && !baseValue.StatId.IsEmpty)
                {
                    GetAccumulator(accumulators, baseValue.StatId).BaseValue = baseValue.Value;
                }
            }

            List<StatModifier> sortedModifiers = new List<StatModifier>(modifiers);
            sortedModifiers.Sort(CompareModifiers);

            for (int i = 0; i < sortedModifiers.Count; i++)
            {
                StatModifier modifier = sortedModifiers[i];
                if (modifier == null || !modifier.IsValid)
                {
                    continue;
                }

                StatAccumulator accumulator = GetAccumulator(accumulators, modifier.StatId);
                accumulator.Apply(modifier);
            }

            foreach (StatAccumulator accumulator in accumulators.Values)
            {
                statBlock.SetValue(accumulator.StatId, accumulator.Evaluate());
            }
        }

        private static void AddAffixModifiers(
            ItemInstanceState itemInstance,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            List<StatModifier> target)
        {
            if (itemInstance == null || affixDefinitionDatabase == null || target == null)
            {
                return;
            }

            IReadOnlyList<ModifierInstanceRecord> modifierRecords = itemInstance.Modifiers;
            for (int i = 0; i < modifierRecords.Count; i++)
            {
                ModifierInstanceRecord modifierRecord = modifierRecords[i];
                if (modifierRecord == null || modifierRecord.ModifierId.IsEmpty)
                {
                    continue;
                }

                if (affixDefinitionDatabase.TryGetModifier(modifierRecord.ModifierId, out ModifierDefinition modifierDefinition) &&
                    modifierDefinition != null)
                {
                    AddValidModifiers(modifierDefinition.StatModifiers, target);
                }
            }

            IReadOnlyList<EnchantmentInstanceRecord> enchantmentRecords = itemInstance.Enchantments;
            for (int i = 0; i < enchantmentRecords.Count; i++)
            {
                EnchantmentInstanceRecord enchantmentRecord = enchantmentRecords[i];
                if (enchantmentRecord == null || enchantmentRecord.EnchantmentId.IsEmpty)
                {
                    continue;
                }

                if (affixDefinitionDatabase.TryGetEnchantment(enchantmentRecord.EnchantmentId, out EnchantmentDefinition enchantmentDefinition) &&
                    enchantmentDefinition != null)
                {
                    enchantmentDefinition.AddStatModifiersForLevel(enchantmentRecord.Level, target);
                }
            }
        }

        private static void AddValidModifiers(IReadOnlyList<StatModifier> source, List<StatModifier> target)
        {
            if (source == null || target == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                StatModifier modifier = source[i];
                if (modifier != null && modifier.IsValid)
                {
                    target.Add(modifier);
                }
            }
        }

        private static int CompareModifiers(StatModifier left, StatModifier right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            int orderCompare = left.Order.CompareTo(right.Order);
            if (orderCompare != 0)
            {
                return orderCompare;
            }

            return string.Compare(left.SourceId, right.SourceId, StringComparison.OrdinalIgnoreCase);
        }

        private static StatAccumulator GetAccumulator(Dictionary<string, StatAccumulator> accumulators, StatId statId)
        {
            string key = statId.Value;
            if (!accumulators.TryGetValue(key, out StatAccumulator accumulator))
            {
                accumulator = new StatAccumulator(statId);
                accumulators.Add(key, accumulator);
            }

            return accumulator;
        }

        private sealed class StatAccumulator
        {
            private float flat;
            private float additivePercent;
            private float multiplicativeFactor = 1f;
            private bool hasOverride;
            private float overrideValue;
            private bool hasMinClamp;
            private float minClamp;
            private bool hasMaxClamp;
            private float maxClamp;

            public StatAccumulator(StatId statId)
            {
                StatId = statId;
            }

            public StatId StatId { get; }

            public float BaseValue { get; set; }

            public void Apply(StatModifier modifier)
            {
                switch (modifier.Operation)
                {
                    case StatModifierOperation.Flat:
                        flat += modifier.Value;
                        break;
                    case StatModifierOperation.AdditivePercent:
                        additivePercent += modifier.Value;
                        break;
                    case StatModifierOperation.MultiplicativePercent:
                        multiplicativeFactor *= 1f + modifier.Value;
                        break;
                    case StatModifierOperation.Override:
                        hasOverride = true;
                        overrideValue = modifier.Value;
                        break;
                    case StatModifierOperation.MinClamp:
                        minClamp = hasMinClamp ? Math.Max(minClamp, modifier.Value) : modifier.Value;
                        hasMinClamp = true;
                        break;
                    case StatModifierOperation.MaxClamp:
                        maxClamp = hasMaxClamp ? Math.Min(maxClamp, modifier.Value) : modifier.Value;
                        hasMaxClamp = true;
                        break;
                }
            }

            public float Evaluate()
            {
                float value = hasOverride
                    ? overrideValue
                    : (BaseValue + flat) * (1f + additivePercent) * multiplicativeFactor;

                if (hasMinClamp)
                {
                    value = Math.Max(minClamp, value);
                }

                if (hasMaxClamp)
                {
                    value = Math.Min(maxClamp, value);
                }

                return value;
            }
        }
    }
}
