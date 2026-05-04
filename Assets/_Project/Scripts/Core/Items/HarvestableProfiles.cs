using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    [Serializable]
    public sealed class HarvestDropEntry
    {
        [SerializeField]
        private ItemDefId itemDefId = default;

        [SerializeField]
        private StackQuantity quantity = StackQuantity.One;

        public HarvestDropEntry()
        {
        }

        public HarvestDropEntry(ItemDefId itemDefId, StackQuantity quantity)
        {
            this.itemDefId = itemDefId;
            this.quantity = quantity.IsPositive ? quantity : StackQuantity.One;
        }

        public ItemDefId ItemDefId
        {
            get { return itemDefId; }
        }

        public StackQuantity Quantity
        {
            get { return quantity.IsPositive ? quantity : StackQuantity.One; }
        }

        public bool IsValid
        {
            get { return !itemDefId.IsEmpty && Quantity.IsPositive; }
        }
    }

    [Serializable]
    public sealed class HarvestableProfile
    {
        [Header("Tool Requirements")]
        [SerializeField]
        private HarvestingDomain requiredHarvestingType = HarvestingDomain.Mining;

        [SerializeField]
        private HarvestingSubtype requiredToolSubtype = HarvestingSubtype.Pickaxe;

        [Min(0f)]
        [SerializeField]
        private float requiredMaterialHardnessScore = 0f;

        [Min(0)]
        [SerializeField]
        private int requiredTier = 0;

        [Min(0f)]
        [SerializeField]
        private float baseHarvestTime = 1f;

        [SerializeField]
        private bool requiresCorrectToolFlag = true;

        [Header("Drops")]
        [SerializeField]
        private DefinitionIdReference dropTableReference = new DefinitionIdReference();

        [SerializeField]
        private List<HarvestDropEntry> simpleDrops = new List<HarvestDropEntry>();

        [Header("Material Tags")]
        [SerializeField]
        private List<string> materialTags = new List<string>();

        [SerializeField]
        private List<string> categoryTags = new List<string>();

        public HarvestingDomain RequiredHarvestingType
        {
            get { return requiredHarvestingType; }
        }

        public HarvestingSubtype RequiredToolSubtype
        {
            get { return requiredToolSubtype; }
        }

        public float RequiredMaterialHardnessScore
        {
            get { return Mathf.Max(0f, requiredMaterialHardnessScore); }
        }

        public int RequiredTier
        {
            get { return Mathf.Max(0, requiredTier); }
        }

        public float BaseHarvestTime
        {
            get { return Mathf.Max(0f, baseHarvestTime); }
        }

        public bool RequiresCorrectToolFlag
        {
            get { return requiresCorrectToolFlag; }
        }

        public DefinitionIdReference DropTableReference
        {
            get { return dropTableReference; }
        }

        public IReadOnlyList<HarvestDropEntry> SimpleDrops
        {
            get { return simpleDrops ?? (IReadOnlyList<HarvestDropEntry>)Array.Empty<HarvestDropEntry>(); }
        }

        public IReadOnlyList<string> MaterialTags
        {
            get { return materialTags ?? (IReadOnlyList<string>)Array.Empty<string>(); }
        }

        public IReadOnlyList<string> CategoryTags
        {
            get { return categoryTags ?? (IReadOnlyList<string>)Array.Empty<string>(); }
        }

        public bool HasAnyDrops
        {
            get
            {
                IReadOnlyList<HarvestDropEntry> drops = SimpleDrops;
                for (int i = 0; i < drops.Count; i++)
                {
                    if (drops[i] != null && drops[i].IsValid)
                    {
                        return true;
                    }
                }

                return DropTableReference.IsValid;
            }
        }
    }

    [CreateAssetMenu(menuName = "VRGame/Items/Harvestable Profile", fileName = "HarvestableProfile")]
    public sealed class HarvestableProfileDefinition : ScriptableObject
    {
        [SerializeField]
        private HarvestableProfile profile = new HarvestableProfile();

        public HarvestableProfile Profile
        {
            get
            {
                profile ??= new HarvestableProfile();
                return profile;
            }
        }

        private void OnValidate()
        {
            profile ??= new HarvestableProfile();
        }
    }
}
