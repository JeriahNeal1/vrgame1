using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    public enum ReforgeSourceType
    {
        NPC,
        Station,
        ManualSmithing,
        QuestReward,
        LootRoll,
        AdminDebug
    }

    [Serializable]
    public sealed class ReforgeContext
    {
        [SerializeField]
        private ReforgeSourceType sourceType = ReforgeSourceType.Station;

        [SerializeField]
        private int skillLevel = 0;

        [SerializeField]
        private string stationId = string.Empty;

        [SerializeField]
        private ItemDefId toolUsedId = default;

        [SerializeField]
        private List<ItemDefId> consumedMaterialIds = new List<ItemDefId>();

        [SerializeField]
        private int randomSeed = 0;

        [SerializeField]
        private float qualityBonus = 0f;

        [SerializeField]
        private List<DefinitionIdReference> allowedModifierPoolOverride = new List<DefinitionIdReference>();

        public ReforgeContext()
        {
        }

        public ReforgeContext(ReforgeSourceType sourceType, int randomSeed)
        {
            this.sourceType = sourceType;
            this.randomSeed = randomSeed;
        }

        public ReforgeContext(
            ReforgeSourceType sourceType,
            int skillLevel,
            string stationId,
            ItemDefId toolUsedId,
            IReadOnlyList<ItemDefId> consumedMaterialIds,
            int randomSeed,
            float qualityBonus,
            IReadOnlyList<DefinitionIdReference> allowedModifierPoolOverride)
        {
            this.sourceType = sourceType;
            this.skillLevel = Mathf.Max(0, skillLevel);
            this.stationId = StableIdUtility.Normalize(stationId);
            this.toolUsedId = toolUsedId;
            this.randomSeed = randomSeed;
            this.qualityBonus = Mathf.Max(0f, qualityBonus);
            this.consumedMaterialIds = consumedMaterialIds != null
                ? new List<ItemDefId>(consumedMaterialIds)
                : new List<ItemDefId>();
            this.allowedModifierPoolOverride = allowedModifierPoolOverride != null
                ? new List<DefinitionIdReference>(allowedModifierPoolOverride)
                : new List<DefinitionIdReference>();
        }

        public ReforgeSourceType SourceType
        {
            get { return sourceType; }
        }

        public int SkillLevel
        {
            get { return Mathf.Max(0, skillLevel); }
        }

        public string StationId
        {
            get { return StableIdUtility.Normalize(stationId); }
        }

        public ItemDefId ToolUsedId
        {
            get { return toolUsedId; }
        }

        public IReadOnlyList<ItemDefId> ConsumedMaterialIds
        {
            get { return consumedMaterialIds ?? (IReadOnlyList<ItemDefId>)Array.Empty<ItemDefId>(); }
        }

        public int RandomSeed
        {
            get { return randomSeed; }
        }

        public float QualityBonus
        {
            get { return qualityBonus; }
        }

        public IReadOnlyList<DefinitionIdReference> AllowedModifierPoolOverride
        {
            get { return allowedModifierPoolOverride ?? (IReadOnlyList<DefinitionIdReference>)Array.Empty<DefinitionIdReference>(); }
        }

        public bool HasModifierPoolOverride
        {
            get { return CountValidReferences(AllowedModifierPoolOverride) > 0; }
        }

        private static int CountValidReferences(IReadOnlyList<DefinitionIdReference> references)
        {
            if (references == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < references.Count; i++)
            {
                if (references[i].IsValid)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public enum GemEnchantmentApplyBehavior
    {
        ApplyOrUpgrade,
        ApplyOnly,
        UpgradeOnly,
        ReplaceExisting
    }

    [Serializable]
    public sealed class GemEnchantmentContext
    {
        [SerializeField]
        private ItemDefId gemItemId = default;

        [SerializeField]
        private ItemInstanceId targetItemInstanceId = default;

        [SerializeField]
        private List<DefinitionIdReference> enchantmentPool = new List<DefinitionIdReference>();

        [SerializeField]
        private float skillBonus = 0f;

        [SerializeField]
        private float stationBonus = 0f;

        [SerializeField]
        private int randomSeed = 0;

        [SerializeField]
        private GemEnchantmentApplyBehavior behavior = GemEnchantmentApplyBehavior.ApplyOrUpgrade;

        public GemEnchantmentContext()
        {
        }

        public GemEnchantmentContext(ItemDefId gemItemId, ItemInstanceId targetItemInstanceId, int randomSeed)
        {
            this.gemItemId = gemItemId;
            this.targetItemInstanceId = targetItemInstanceId;
            this.randomSeed = randomSeed;
        }

        public GemEnchantmentContext(
            ItemDefId gemItemId,
            ItemInstanceId targetItemInstanceId,
            IReadOnlyList<DefinitionIdReference> enchantmentPool,
            float skillBonus,
            float stationBonus,
            int randomSeed,
            GemEnchantmentApplyBehavior behavior)
        {
            this.gemItemId = gemItemId;
            this.targetItemInstanceId = targetItemInstanceId;
            this.enchantmentPool = enchantmentPool != null
                ? new List<DefinitionIdReference>(enchantmentPool)
                : new List<DefinitionIdReference>();
            this.skillBonus = Mathf.Max(0f, skillBonus);
            this.stationBonus = Mathf.Max(0f, stationBonus);
            this.randomSeed = randomSeed;
            this.behavior = behavior;
        }

        public ItemDefId GemItemId
        {
            get { return gemItemId; }
        }

        public ItemInstanceId TargetItemInstanceId
        {
            get { return targetItemInstanceId; }
        }

        public IReadOnlyList<DefinitionIdReference> EnchantmentPool
        {
            get { return enchantmentPool ?? (IReadOnlyList<DefinitionIdReference>)Array.Empty<DefinitionIdReference>(); }
        }

        public float SkillBonus
        {
            get { return skillBonus; }
        }

        public float StationBonus
        {
            get { return stationBonus; }
        }

        public int RandomSeed
        {
            get { return randomSeed; }
        }

        public GemEnchantmentApplyBehavior Behavior
        {
            get { return behavior; }
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
    }
}
