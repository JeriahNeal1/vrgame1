using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    [CreateAssetMenu(menuName = "VRGame/Items/Equipment Slot Definition", fileName = "EquipmentSlotDefinition")]
    public sealed class EquipmentSlotDefinition : ScriptableObject
    {
        [SerializeField]
        private string slotId = string.Empty;

        [SerializeField]
        private string displayName = string.Empty;

        [SerializeField]
        private List<ItemCategoryPath> allowedCategoryFilters = new List<ItemCategoryPath>();

        [SerializeField]
        private ItemFlags allowedItemFlags = ItemFlags.Equipment;

        [SerializeField]
        private ItemFlags rejectedItemFlags = ItemFlags.None;

        [SerializeField]
        private int sortOrder = 0;

        [SerializeField]
        private bool isAccessorySlot = false;

        [SerializeField]
        private EquipmentSlotKind slotKind = EquipmentSlotKind.Custom;

        public string SlotId
        {
            get { return StableIdUtility.Normalize(slotId); }
        }

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim(); }
        }

        public IReadOnlyList<ItemCategoryPath> AllowedCategoryFilters
        {
            get { return allowedCategoryFilters ?? (IReadOnlyList<ItemCategoryPath>)Array.Empty<ItemCategoryPath>(); }
        }

        public ItemFlags AllowedItemFlags
        {
            get { return allowedItemFlags; }
        }

        public ItemFlags RejectedItemFlags
        {
            get { return rejectedItemFlags; }
        }

        public int SortOrder
        {
            get { return sortOrder; }
        }

        public bool IsAccessorySlot
        {
            get { return isAccessorySlot; }
        }

        public EquipmentSlotKind SlotKind
        {
            get { return slotKind; }
        }

        public EquipmentRuntimeSlot ToRuntimeSlot()
        {
            return new EquipmentRuntimeSlot(
                SlotId,
                DisplayName,
                AllowedCategoryFilters,
                AllowedItemFlags,
                RejectedItemFlags,
                SortOrder,
                IsAccessorySlot,
                SlotKind,
                false);
        }

        private void OnValidate()
        {
            allowedCategoryFilters ??= new List<ItemCategoryPath>();
        }
    }

    public sealed class EquipmentRuntimeSlot
    {
        private readonly List<ItemCategoryPath> allowedCategoryFilters;

        public EquipmentRuntimeSlot(
            string slotId,
            string displayName,
            IReadOnlyList<ItemCategoryPath> allowedCategoryFilters,
            ItemFlags allowedItemFlags,
            ItemFlags rejectedItemFlags,
            int sortOrder,
            bool isAccessorySlot,
            EquipmentSlotKind slotKind,
            bool generated)
        {
            SlotId = StableIdUtility.Normalize(slotId);
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? SlotId : displayName.Trim();
            this.allowedCategoryFilters = allowedCategoryFilters != null
                ? new List<ItemCategoryPath>(allowedCategoryFilters)
                : new List<ItemCategoryPath>();
            AllowedItemFlags = allowedItemFlags;
            RejectedItemFlags = rejectedItemFlags;
            SortOrder = sortOrder;
            IsAccessorySlot = isAccessorySlot;
            SlotKind = slotKind;
            Generated = generated;
        }

        public string SlotId { get; }

        public string DisplayName { get; }

        public IReadOnlyList<ItemCategoryPath> AllowedCategoryFilters
        {
            get { return allowedCategoryFilters; }
        }

        public ItemFlags AllowedItemFlags { get; }

        public ItemFlags RejectedItemFlags { get; }

        public int SortOrder { get; }

        public bool IsAccessorySlot { get; }

        public EquipmentSlotKind SlotKind { get; }

        public bool Generated { get; }

        public bool IsValid
        {
            get { return StableIdUtility.IsValid(SlotId); }
        }

        public bool Allows(ItemDefinition itemDefinition)
        {
            if (itemDefinition == null)
            {
                return false;
            }

            if (AllowedItemFlags != ItemFlags.None && (itemDefinition.Flags & AllowedItemFlags) != AllowedItemFlags)
            {
                return false;
            }

            if (RejectedItemFlags != ItemFlags.None && (itemDefinition.Flags & RejectedItemFlags) != 0)
            {
                return false;
            }

            if (allowedCategoryFilters.Count == 0)
            {
                return true;
            }

            ItemCategoryPath itemCategory = itemDefinition.CategoryPath;
            if (itemCategory == null || itemCategory.IsEmpty)
            {
                return false;
            }

            for (int i = 0; i < allowedCategoryFilters.Count; i++)
            {
                ItemCategoryPath filter = allowedCategoryFilters[i];
                if (filter != null && !filter.IsEmpty && itemCategory.StartsWith(filter))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
