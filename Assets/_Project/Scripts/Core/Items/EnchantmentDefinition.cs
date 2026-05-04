using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    [Serializable]
    public sealed class EnchantmentStatEffect
    {
        [SerializeField]
        private StatId statId = default;

        [SerializeField]
        private StatModifierOperation operation = StatModifierOperation.Flat;

        [SerializeField]
        private float baseValue = 0f;

        [SerializeField]
        private float valuePerLevel = 0f;

        [SerializeField]
        private string sourceId = string.Empty;

        [SerializeField]
        private int order = 0;

        public StatId StatId
        {
            get { return statId; }
        }

        public StatModifierOperation Operation
        {
            get { return operation; }
        }

        public float BaseValue
        {
            get { return baseValue; }
        }

        public float ValuePerLevel
        {
            get { return valuePerLevel; }
        }

        public string SourceId
        {
            get { return StableIdUtility.Normalize(sourceId); }
        }

        public int Order
        {
            get { return order; }
        }

        public bool IsValid
        {
            get { return !statId.IsEmpty; }
        }

        public StatModifier CreateModifier(int level, string fallbackSourceId)
        {
            int clampedLevel = Mathf.Max(1, level);
            float value = baseValue + (valuePerLevel * (clampedLevel - 1));
            string resolvedSourceId = StableIdUtility.IsValid(sourceId) ? sourceId : fallbackSourceId;
            return new StatModifier(statId, operation, value, resolvedSourceId, order);
        }
    }

    [CreateAssetMenu(menuName = "VRGame/Items/Enchantment Definition", fileName = "EnchantmentDefinition")]
    public sealed class EnchantmentDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private EnchantmentId enchantmentId = default;

        [SerializeField]
        private string displayName = string.Empty;

        [TextArea]
        [SerializeField]
        private string description = string.Empty;

        [Header("Rules")]
        [Min(1)]
        [SerializeField]
        private int maxLevel = 1;

        [SerializeField]
        private bool canBeRemoved = true;

        [SerializeField]
        private List<string> conflictGroups = new List<string>();

        [Header("Applicability")]
        [SerializeField]
        private ItemApplicabilityFilter allowedItemFilter = new ItemApplicabilityFilter();

        [SerializeField]
        private ItemApplicabilityFilter blockedItemFilter = new ItemApplicabilityFilter();

        [Header("Effects")]
        [SerializeField]
        private List<EnchantmentStatEffect> statEffectsPerLevel = new List<EnchantmentStatEffect>();

        [Header("Future Action Hooks")]
        [SerializeField]
        private List<DefinitionIdReference> actionPresetReferences = new List<DefinitionIdReference>();

        public EnchantmentId EnchantmentId
        {
            get { return enchantmentId; }
        }

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim(); }
        }

        public string Description
        {
            get { return description ?? string.Empty; }
        }

        public int MaxLevel
        {
            get { return Mathf.Max(1, maxLevel); }
        }

        public bool CanBeRemoved
        {
            get { return canBeRemoved; }
        }

        public IReadOnlyList<string> ConflictGroups
        {
            get { return conflictGroups ?? (IReadOnlyList<string>)Array.Empty<string>(); }
        }

        public ItemApplicabilityFilter AllowedItemFilter
        {
            get { return allowedItemFilter; }
        }

        public ItemApplicabilityFilter BlockedItemFilter
        {
            get { return blockedItemFilter; }
        }

        public IReadOnlyList<EnchantmentStatEffect> StatEffectsPerLevel
        {
            get { return statEffectsPerLevel ?? (IReadOnlyList<EnchantmentStatEffect>)Array.Empty<EnchantmentStatEffect>(); }
        }

        public IReadOnlyList<DefinitionIdReference> ActionPresetReferences
        {
            get { return actionPresetReferences ?? (IReadOnlyList<DefinitionIdReference>)Array.Empty<DefinitionIdReference>(); }
        }

        public bool CanApplyTo(ItemDefinition itemDefinition)
        {
            return itemDefinition != null &&
                   itemDefinition.IsEquipment &&
                   (allowedItemFilter == null || allowedItemFilter.Allows(itemDefinition)) &&
                   (blockedItemFilter == null || !blockedItemFilter.Blocks(itemDefinition));
        }

        public EnchantmentInstanceRecord CreateRecord(int level, int rollSeed)
        {
            return new EnchantmentInstanceRecord(enchantmentId, Mathf.Clamp(level, 1, MaxLevel), rollSeed, Array.Empty<ItemInstanceVariableRecord>());
        }

        public void AddStatModifiersForLevel(int level, IList<StatModifier> target)
        {
            if (target == null)
            {
                return;
            }

            IReadOnlyList<EnchantmentStatEffect> effects = StatEffectsPerLevel;
            for (int i = 0; i < effects.Count; i++)
            {
                EnchantmentStatEffect effect = effects[i];
                if (effect != null && effect.IsValid)
                {
                    target.Add(effect.CreateModifier(Mathf.Clamp(level, 1, MaxLevel), enchantmentId.Value));
                }
            }
        }

        public bool SharesConflictGroupWith(EnchantmentDefinition other)
        {
            if (other == null)
            {
                return false;
            }

            IReadOnlyList<string> groups = ConflictGroups;
            IReadOnlyList<string> otherGroups = other.ConflictGroups;
            for (int i = 0; i < groups.Count; i++)
            {
                string group = StableIdUtility.Normalize(groups[i]);
                if (string.IsNullOrEmpty(group))
                {
                    continue;
                }

                for (int j = 0; j < otherGroups.Count; j++)
                {
                    if (StableIdUtility.EqualsNormalized(group, otherGroups[j]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public IReadOnlyList<ItemAffixDefinitionValidationIssue> ValidateDefinition()
        {
            return ItemAffixDefinitionValidator.Validate(this);
        }

        private void OnValidate()
        {
            maxLevel = Mathf.Max(1, maxLevel);
            allowedItemFilter ??= new ItemApplicabilityFilter();
            blockedItemFilter ??= new ItemApplicabilityFilter();
            conflictGroups ??= new List<string>();
            statEffectsPerLevel ??= new List<EnchantmentStatEffect>();
            actionPresetReferences ??= new List<DefinitionIdReference>();

            for (int i = conflictGroups.Count - 1; i >= 0; i--)
            {
                string normalized = StableIdUtility.Normalize(conflictGroups[i]);
                if (string.IsNullOrEmpty(normalized))
                {
                    conflictGroups.RemoveAt(i);
                }
                else
                {
                    conflictGroups[i] = normalized;
                }
            }
        }
    }
}
