using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    public enum HarvestingDomain
    {
        None,
        Combat,
        Mining,
        Lumber,
        ConstructionArchitecture,
        Fishing
    }

    public enum HarvestingSubtype
    {
        None,
        Melee,
        Magic,
        Summoner,
        Ranged,
        Pickaxe,
        Drill,
        Axe,
        Chainsaw,
        Hammer,
        Jackhammer,
        FishingRod,
        Trap
    }

    [Serializable]
    public sealed class ToolProfile
    {
        [SerializeField]
        private HarvestingDomain harvestingType = HarvestingDomain.None;

        [SerializeField]
        private HarvestingSubtype toolSubtype = HarvestingSubtype.None;

        [Min(0f)]
        [SerializeField]
        private float baseMaterialHardnessScore = 0f;

        [Min(0)]
        [SerializeField]
        private int toolTier = 0;

        [Min(0f)]
        [SerializeField]
        private float harvestSpeed = 1f;

        [SerializeField]
        private List<string> effectiveMaterialTags = new List<string>();

        [SerializeField]
        private List<string> effectiveCategoryTags = new List<string>();

        public HarvestingDomain HarvestingType
        {
            get { return harvestingType; }
        }

        public HarvestingSubtype ToolSubtype
        {
            get { return toolSubtype; }
        }

        public float BaseMaterialHardnessScore
        {
            get { return Mathf.Max(0f, baseMaterialHardnessScore); }
        }

        public int ToolTier
        {
            get { return Mathf.Max(0, toolTier); }
        }

        public float HarvestSpeed
        {
            get { return Mathf.Max(0f, harvestSpeed); }
        }

        public IReadOnlyList<string> EffectiveMaterialTags
        {
            get { return effectiveMaterialTags ?? (IReadOnlyList<string>)Array.Empty<string>(); }
        }

        public IReadOnlyList<string> EffectiveCategoryTags
        {
            get { return effectiveCategoryTags ?? (IReadOnlyList<string>)Array.Empty<string>(); }
        }
    }

    [Serializable]
    public sealed class HarvestingProfile
    {
        [SerializeField]
        private HarvestingDomain requiredHarvestingType = HarvestingDomain.None;

        [SerializeField]
        private List<HarvestingSubtype> allowedToolSubtypes = new List<HarvestingSubtype>();

        [Min(0f)]
        [SerializeField]
        private float materialHardnessScore = 0f;

        [SerializeField]
        private List<DefinitionIdReference> outputItemDefinitions = new List<DefinitionIdReference>();

        [SerializeField]
        private List<string> harvestableTags = new List<string>();

        public HarvestingDomain RequiredHarvestingType
        {
            get { return requiredHarvestingType; }
        }

        public IReadOnlyList<HarvestingSubtype> AllowedToolSubtypes
        {
            get { return allowedToolSubtypes ?? (IReadOnlyList<HarvestingSubtype>)Array.Empty<HarvestingSubtype>(); }
        }

        public float MaterialHardnessScore
        {
            get { return Mathf.Max(0f, materialHardnessScore); }
        }

        public IReadOnlyList<DefinitionIdReference> OutputItemDefinitions
        {
            get { return outputItemDefinitions ?? (IReadOnlyList<DefinitionIdReference>)Array.Empty<DefinitionIdReference>(); }
        }

        public IReadOnlyList<string> HarvestableTags
        {
            get { return harvestableTags ?? (IReadOnlyList<string>)Array.Empty<string>(); }
        }
    }
}
