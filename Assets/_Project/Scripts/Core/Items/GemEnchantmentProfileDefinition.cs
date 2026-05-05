using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    [CreateAssetMenu(menuName = "VRGame/Items/Gem Enchantment Profile", fileName = "GemEnchantmentProfile")]
    public sealed class GemEnchantmentProfileDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private string profileId = string.Empty;

        [SerializeField]
        private string displayName = string.Empty;

        [TextArea]
        [SerializeField]
        private string description = string.Empty;

        [Header("Gem")]
        [SerializeField]
        private ItemDefId gemItemDefId = default;

        [SerializeField]
        private StackQuantity consumedQuantity = StackQuantity.One;

        [Header("Enchantment")]
        [SerializeField]
        private GemEnchantmentApplyBehavior behavior = GemEnchantmentApplyBehavior.ApplyOrUpgrade;

        [Tooltip("References can point to individual enchantment IDs or EnchantmentSetDefinition IDs in the affix database.")]
        [SerializeField]
        private List<DefinitionIdReference> enchantmentPool = new List<DefinitionIdReference>();

        [Header("Quality")]
        [Min(0f)]
        [SerializeField]
        private float skillBonus = 0f;

        [Min(0f)]
        [SerializeField]
        private float stationBonus = 0f;

        public string ProfileId
        {
            get { return StableIdUtility.Normalize(profileId); }
        }

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim(); }
        }

        public string Description
        {
            get { return description ?? string.Empty; }
        }

        public ItemDefId GemItemDefId
        {
            get { return gemItemDefId; }
        }

        public StackQuantity ConsumedQuantity
        {
            get { return consumedQuantity.IsPositive ? consumedQuantity : StackQuantity.One; }
        }

        public GemEnchantmentApplyBehavior Behavior
        {
            get { return behavior; }
        }

        public IReadOnlyList<DefinitionIdReference> EnchantmentPool
        {
            get { return enchantmentPool ?? (IReadOnlyList<DefinitionIdReference>)Array.Empty<DefinitionIdReference>(); }
        }

        public float SkillBonus
        {
            get { return Mathf.Max(0f, skillBonus); }
        }

        public float StationBonus
        {
            get { return Mathf.Max(0f, stationBonus); }
        }

        public bool IsValid
        {
            get { return StableIdUtility.IsValid(ProfileId) && gemItemDefId.IsValid && HasEnchantmentPool; }
        }

        public bool HasEnchantmentPool
        {
            get
            {
                IReadOnlyList<DefinitionIdReference> pool = EnchantmentPool;
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i].IsValid)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool MatchesGem(ItemDefId itemDefId)
        {
            return gemItemDefId == itemDefId;
        }

        public GemEnchantmentContext CreateContext(ItemInstanceId targetItemInstanceId, int randomSeed, float extraStationBonus = 0f, float extraSkillBonus = 0f)
        {
            return new GemEnchantmentContext(
                gemItemDefId,
                targetItemInstanceId,
                EnchantmentPool,
                SkillBonus + Mathf.Max(0f, extraSkillBonus),
                StationBonus + Mathf.Max(0f, extraStationBonus),
                randomSeed,
                behavior);
        }

        private void OnValidate()
        {
            profileId = StableIdUtility.Normalize(profileId);
            enchantmentPool ??= new List<DefinitionIdReference>();
            if (!consumedQuantity.IsPositive)
            {
                consumedQuantity = StackQuantity.One;
            }
        }
    }
}
