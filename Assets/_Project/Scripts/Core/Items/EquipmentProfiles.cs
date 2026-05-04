using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    public enum EquipmentFamily
    {
        None,
        Armor,
        Accessory,
        Weapon,
        Tool,
        Custom
    }

    public enum EquipmentSlotKind
    {
        Head,
        Shoulders,
        Gauntlets,
        Chest,
        Leggings,
        Boots,
        Wings,
        CapeCloak,
        Amulet,
        Ring,
        Custom
    }

    [Serializable]
    public sealed class EquipmentSlotReference
    {
        [SerializeField]
        private EquipmentSlotKind slotKind = EquipmentSlotKind.Custom;

        [SerializeField]
        private string slotId = string.Empty;

        [SerializeField]
        private bool indexedSlot = false;

        [Min(0)]
        [SerializeField]
        private int minIndex = 0;

        [Min(0)]
        [SerializeField]
        private int maxIndex = 0;

        public EquipmentSlotKind SlotKind
        {
            get { return slotKind; }
        }

        public string SlotId
        {
            get
            {
                if (slotKind == EquipmentSlotKind.Custom)
                {
                    return StableIdUtility.Normalize(slotId);
                }

                return EquipmentSlotIdUtility.GetDefaultSlotId(slotKind);
            }
        }

        public bool IndexedSlot
        {
            get { return indexedSlot || slotKind == EquipmentSlotKind.Ring; }
        }

        public int MinIndex
        {
            get { return Mathf.Max(0, minIndex); }
        }

        public int MaxIndex
        {
            get { return Mathf.Max(MinIndex, maxIndex); }
        }

        public bool IsValid
        {
            get { return StableIdUtility.IsValid(SlotId); }
        }
    }

    public static class EquipmentSlotIdUtility
    {
        public const int MaxRingSlotCount = 10;

        public static string GetDefaultSlotId(EquipmentSlotKind slotKind)
        {
            switch (slotKind)
            {
                case EquipmentSlotKind.Head:
                    return "equipment.head";
                case EquipmentSlotKind.Shoulders:
                    return "equipment.shoulders";
                case EquipmentSlotKind.Gauntlets:
                    return "equipment.gauntlets";
                case EquipmentSlotKind.Chest:
                    return "equipment.chest";
                case EquipmentSlotKind.Leggings:
                    return "equipment.leggings";
                case EquipmentSlotKind.Boots:
                    return "equipment.boots";
                case EquipmentSlotKind.Wings:
                    return "equipment.wings";
                case EquipmentSlotKind.CapeCloak:
                    return "equipment.cape_cloak";
                case EquipmentSlotKind.Amulet:
                    return "equipment.amulet";
                case EquipmentSlotKind.Ring:
                    return "equipment.ring";
                case EquipmentSlotKind.Custom:
                default:
                    return string.Empty;
            }
        }

        public static string GetGeneratedRingSlotId(int zeroBasedIndex)
        {
            int clampedIndex = Mathf.Clamp(zeroBasedIndex, 0, MaxRingSlotCount - 1);
            return $"equipment.ring_{clampedIndex + 1:00}";
        }

        public static string GetGeneratedRingDisplayName(int zeroBasedIndex)
        {
            int clampedIndex = Mathf.Clamp(zeroBasedIndex, 0, MaxRingSlotCount - 1);
            return $"Ring {clampedIndex + 1:00}";
        }
    }

    [Serializable]
    public sealed class EquipmentProfile
    {
        [SerializeField]
        private EquipmentFamily family = EquipmentFamily.None;

        [Tooltip("Armor and accessories usually equip to the loadout. Held tools and weapons can stay false.")]
        [SerializeField]
        private bool canEquipToLoadout = false;

        [Tooltip("Weapons and tools should generally stay true because held VR items are separate from equipment slots.")]
        [SerializeField]
        private bool canBeHeldAsItem = true;

        [SerializeField]
        private bool canHaveModifiers = true;

        [SerializeField]
        private bool canHaveEnchantments = true;

        [Min(0)]
        [SerializeField]
        private int socketLimit = 0;

        [SerializeField]
        private List<EquipmentSlotReference> compatibleSlots = new List<EquipmentSlotReference>();

        public EquipmentFamily Family
        {
            get { return family; }
        }

        public bool CanEquipToLoadout
        {
            get { return canEquipToLoadout; }
        }

        public bool CanBeHeldAsItem
        {
            get { return canBeHeldAsItem; }
        }

        public bool CanHaveModifiers
        {
            get { return canHaveModifiers; }
        }

        public bool CanHaveEnchantments
        {
            get { return canHaveEnchantments; }
        }

        public int SocketLimit
        {
            get { return Mathf.Max(0, socketLimit); }
        }

        public IReadOnlyList<EquipmentSlotReference> CompatibleSlots
        {
            get { return compatibleSlots ?? (IReadOnlyList<EquipmentSlotReference>)Array.Empty<EquipmentSlotReference>(); }
        }
    }
}
