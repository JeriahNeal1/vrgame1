using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    [Serializable]
    public sealed class AffixRollRange
    {
        [SerializeField]
        private string key = string.Empty;

        [SerializeField]
        private float minValue = 0f;

        [SerializeField]
        private float maxValue = 0f;

        [Min(0)]
        [SerializeField]
        private int decimalPlaces = 2;

        public string Key
        {
            get { return StableIdUtility.Normalize(key); }
        }

        public float MinValue
        {
            get { return Mathf.Min(minValue, maxValue); }
        }

        public float MaxValue
        {
            get { return Mathf.Max(minValue, maxValue); }
        }

        public int DecimalPlaces
        {
            get { return Mathf.Clamp(decimalPlaces, 0, 6); }
        }

        public bool IsValid
        {
            get { return StableIdUtility.IsValid(Key); }
        }

        public ItemInstanceVariableRecord Roll(System.Random random)
        {
            float t = random != null ? (float)random.NextDouble() : 0f;
            float value = Mathf.Lerp(MinValue, MaxValue, t);
            float scale = Mathf.Pow(10f, DecimalPlaces);
            float rounded = Mathf.Round(value * scale) / scale;
            return new ItemInstanceVariableRecord(Key, rounded.ToString("G9", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    [CreateAssetMenu(menuName = "VRGame/Items/Modifier Definition", fileName = "ModifierDefinition")]
    public sealed class ModifierDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private ModifierId modifierId = default;

        [SerializeField]
        private string displayName = string.Empty;

        [TextArea]
        [SerializeField]
        private string description = string.Empty;

        [Header("Roll Selection")]
        [Min(0)]
        [SerializeField]
        private int rarity = 0;

        [Min(0f)]
        [SerializeField]
        private float weight = 1f;

        [SerializeField]
        private string exclusiveGroup = "modifier.primary";

        [Header("Applicability")]
        [SerializeField]
        private ItemApplicabilityFilter allowedItemFilter = new ItemApplicabilityFilter();

        [SerializeField]
        private ItemApplicabilityFilter blockedItemFilter = new ItemApplicabilityFilter();

        [Header("Effects")]
        [SerializeField]
        private List<StatModifier> statModifiers = new List<StatModifier>();

        [SerializeField]
        private List<AffixRollRange> rollRanges = new List<AffixRollRange>();

        [Header("Future Action Hooks")]
        [SerializeField]
        private List<DefinitionIdReference> actionPresetReferences = new List<DefinitionIdReference>();

        public ModifierId ModifierId
        {
            get { return modifierId; }
        }

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim(); }
        }

        public string Description
        {
            get { return description ?? string.Empty; }
        }

        public int Rarity
        {
            get { return Mathf.Max(0, rarity); }
        }

        public float Weight
        {
            get { return Mathf.Max(0f, weight); }
        }

        public string ExclusiveGroup
        {
            get { return StableIdUtility.Normalize(exclusiveGroup); }
        }

        public ItemApplicabilityFilter AllowedItemFilter
        {
            get { return allowedItemFilter; }
        }

        public ItemApplicabilityFilter BlockedItemFilter
        {
            get { return blockedItemFilter; }
        }

        public IReadOnlyList<StatModifier> StatModifiers
        {
            get { return statModifiers ?? (IReadOnlyList<StatModifier>)Array.Empty<StatModifier>(); }
        }

        public IReadOnlyList<AffixRollRange> RollRanges
        {
            get { return rollRanges ?? (IReadOnlyList<AffixRollRange>)Array.Empty<AffixRollRange>(); }
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

        public ModifierInstanceRecord CreateRecord(int rollSeed)
        {
            return new ModifierInstanceRecord(modifierId, rollSeed, RollRolledValues(rollSeed));
        }

        public IReadOnlyList<ItemAffixDefinitionValidationIssue> ValidateDefinition()
        {
            return ItemAffixDefinitionValidator.Validate(this);
        }

        private List<ItemInstanceVariableRecord> RollRolledValues(int rollSeed)
        {
            List<ItemInstanceVariableRecord> records = new List<ItemInstanceVariableRecord>();
            IReadOnlyList<AffixRollRange> ranges = RollRanges;
            if (ranges.Count == 0)
            {
                return records;
            }

            System.Random random = new System.Random(rollSeed);
            for (int i = 0; i < ranges.Count; i++)
            {
                AffixRollRange range = ranges[i];
                if (range != null && range.IsValid)
                {
                    records.Add(range.Roll(random));
                }
            }

            return records;
        }

        private void OnValidate()
        {
            rarity = Mathf.Max(0, rarity);
            weight = Mathf.Max(0f, weight);
            allowedItemFilter ??= new ItemApplicabilityFilter();
            blockedItemFilter ??= new ItemApplicabilityFilter();
            statModifiers ??= new List<StatModifier>();
            rollRanges ??= new List<AffixRollRange>();
            actionPresetReferences ??= new List<DefinitionIdReference>();
        }
    }
}
