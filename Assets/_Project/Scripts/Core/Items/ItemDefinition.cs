using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    [CreateAssetMenu(menuName = "VRGame/Items/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private ItemDefId itemDefId = default;

        [SerializeField]
        private string displayName;

        [TextArea]
        [SerializeField]
        private string description = string.Empty;

        [SerializeField]
        private ItemCategoryPath categoryPath = new ItemCategoryPath();

        [Header("Behavior")]
        [SerializeField]
        private ItemFlags flags = ItemFlags.None;

        [SerializeField]
        private ItemStackPolicy stackPolicy = new ItemStackPolicy();

        [Header("Stats")]
        [SerializeField]
        private List<StatModifier> baseStatModifiers = new List<StatModifier>();

        [Header("Presentation And World")]
        [Tooltip("Physical world prefab used for drops, VR manifestation, and editor icon generation.")]
        [SerializeField]
        private GameObject worldPrefab = null;

        [SerializeField]
        private Sprite generatedIcon = null;

        [Header("Optional Profiles")]
        [SerializeField]
        private bool hasEquipmentProfile = false;

        [SerializeField]
        private EquipmentProfile equipmentProfile = new EquipmentProfile();

        [SerializeField]
        private bool hasWeaponProfile = false;

        [SerializeField]
        private WeaponProfile weaponProfile = new WeaponProfile();

        [SerializeField]
        private bool hasMeleeWeaponProfile = false;

        [SerializeField]
        private MeleeWeaponProfile meleeWeaponProfile = new MeleeWeaponProfile();

        [SerializeField]
        private bool hasToolProfile = false;

        [SerializeField]
        private ToolProfile toolProfile = new ToolProfile();

        [SerializeField]
        private bool hasHarvestingProfile = false;

        [SerializeField]
        private HarvestingProfile harvestingProfile = new HarvestingProfile();

        [SerializeField]
        private bool hasPlaceableProfile = false;

        [SerializeField]
        private PlaceableProfile placeableProfile = new PlaceableProfile();

        [Header("Future Data-Driven Hooks")]
        [SerializeField]
        private List<DefinitionIdReference> actionPresetReferences = new List<DefinitionIdReference>();

        [SerializeField]
        private List<DefinitionIdReference> allowedModifierPoolReferences = new List<DefinitionIdReference>();

        [SerializeField]
        private List<DefinitionIdReference> allowedEnchantmentPoolReferences = new List<DefinitionIdReference>();

        public ItemDefId ItemDefId
        {
            get { return itemDefId; }
        }

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim(); }
        }

        public string Description
        {
            get { return description ?? string.Empty; }
        }

        public ItemCategoryPath CategoryPath
        {
            get { return categoryPath; }
        }

        public ItemFlags Flags
        {
            get { return flags; }
        }

        public ItemStackPolicy StackPolicy
        {
            get { return stackPolicy; }
        }

        public ResolvedItemStackPolicy ResolvedStackPolicy
        {
            get { return (stackPolicy ?? new ItemStackPolicy()).Resolve(flags); }
        }

        public IReadOnlyList<StatModifier> BaseStatModifiers
        {
            get { return baseStatModifiers ?? (IReadOnlyList<StatModifier>)Array.Empty<StatModifier>(); }
        }

        public GameObject WorldPrefab
        {
            get { return worldPrefab; }
        }

        public Sprite GeneratedIcon
        {
            get { return generatedIcon; }
        }

        public bool HasEquipmentProfile
        {
            get { return hasEquipmentProfile; }
        }

        public EquipmentProfile EquipmentProfile
        {
            get { return hasEquipmentProfile ? equipmentProfile : null; }
        }

        public bool HasWeaponProfile
        {
            get { return hasWeaponProfile; }
        }

        public WeaponProfile WeaponProfile
        {
            get { return hasWeaponProfile ? weaponProfile : null; }
        }

        public bool HasMeleeWeaponProfile
        {
            get { return hasMeleeWeaponProfile; }
        }

        public MeleeWeaponProfile MeleeWeaponProfile
        {
            get { return hasMeleeWeaponProfile ? meleeWeaponProfile : null; }
        }

        public bool HasToolProfile
        {
            get { return hasToolProfile; }
        }

        public ToolProfile ToolProfile
        {
            get { return hasToolProfile ? toolProfile : null; }
        }

        public bool HasHarvestingProfile
        {
            get { return hasHarvestingProfile; }
        }

        public HarvestingProfile HarvestingProfile
        {
            get { return hasHarvestingProfile ? harvestingProfile : null; }
        }

        public bool HasPlaceableProfile
        {
            get { return hasPlaceableProfile; }
        }

        public PlaceableProfile PlaceableProfile
        {
            get { return hasPlaceableProfile ? placeableProfile : null; }
        }

        public IReadOnlyList<DefinitionIdReference> ActionPresetReferences
        {
            get { return actionPresetReferences ?? (IReadOnlyList<DefinitionIdReference>)Array.Empty<DefinitionIdReference>(); }
        }

        public IReadOnlyList<DefinitionIdReference> AllowedModifierPoolReferences
        {
            get { return allowedModifierPoolReferences ?? (IReadOnlyList<DefinitionIdReference>)Array.Empty<DefinitionIdReference>(); }
        }

        public IReadOnlyList<DefinitionIdReference> AllowedEnchantmentPoolReferences
        {
            get { return allowedEnchantmentPoolReferences ?? (IReadOnlyList<DefinitionIdReference>)Array.Empty<DefinitionIdReference>(); }
        }

        public bool IsEquipment
        {
            get { return HasFlag(ItemFlags.Equipment); }
        }

        public bool IsManifestable
        {
            get { return HasFlag(ItemFlags.CanBeManifested); }
        }

        public bool CanHaveModifierOrEnchantmentPools
        {
            get { return IsEquipment; }
        }

        public bool HasModifierOrEnchantmentPools
        {
            get { return CountValidReferences(AllowedModifierPoolReferences) > 0 || CountValidReferences(AllowedEnchantmentPoolReferences) > 0; }
        }

        public bool HasFlag(ItemFlags itemFlag)
        {
            return (flags & itemFlag) == itemFlag;
        }

        private void Reset()
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = name;
            }
        }

        private void OnValidate()
        {
            categoryPath ??= new ItemCategoryPath();
            stackPolicy ??= new ItemStackPolicy();
            baseStatModifiers ??= new List<StatModifier>();
            equipmentProfile ??= new EquipmentProfile();
            weaponProfile ??= new WeaponProfile();
            meleeWeaponProfile ??= new MeleeWeaponProfile();
            toolProfile ??= new ToolProfile();
            harvestingProfile ??= new HarvestingProfile();
            placeableProfile ??= new PlaceableProfile();
            actionPresetReferences ??= new List<DefinitionIdReference>();
            allowedModifierPoolReferences ??= new List<DefinitionIdReference>();
            allowedEnchantmentPoolReferences ??= new List<DefinitionIdReference>();
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
}
