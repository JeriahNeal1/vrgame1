using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    [CreateAssetMenu(menuName = "VRGame/Items/Equipment Loadout Config", fileName = "EquipmentLoadoutConfig")]
    public sealed class EquipmentLoadoutConfig : ScriptableObject
    {
        [SerializeField]
        private bool includeDefaultBodySlots = true;

        [Range(0, EquipmentSlotIdUtility.MaxRingSlotCount)]
        [SerializeField]
        private int ringSlotCount = 2;

        [SerializeField]
        private List<EquipmentSlotDefinition> customSlots = new List<EquipmentSlotDefinition>();

        public bool IncludeDefaultBodySlots
        {
            get { return includeDefaultBodySlots; }
        }

        public int RingSlotCount
        {
            get { return Mathf.Clamp(ringSlotCount, 0, EquipmentSlotIdUtility.MaxRingSlotCount); }
        }

        public IReadOnlyList<EquipmentSlotDefinition> CustomSlots
        {
            get { return customSlots ?? (IReadOnlyList<EquipmentSlotDefinition>)Array.Empty<EquipmentSlotDefinition>(); }
        }

        public List<EquipmentRuntimeSlot> BuildRuntimeSlots()
        {
            List<EquipmentRuntimeSlot> slots = new List<EquipmentRuntimeSlot>();
            AddRuntimeSlots(slots);
            slots.Sort((left, right) => left.SortOrder != right.SortOrder
                ? left.SortOrder.CompareTo(right.SortOrder)
                : string.Compare(left.SlotId, right.SlotId, StringComparison.OrdinalIgnoreCase));
            return slots;
        }

        public void AddRuntimeSlots(List<EquipmentRuntimeSlot> target)
        {
            if (target == null)
            {
                return;
            }

            if (includeDefaultBodySlots)
            {
                AddDefaultBodySlots(target);
            }

            AddGeneratedRingSlots(RingSlotCount, target);

            IReadOnlyList<EquipmentSlotDefinition> slots = CustomSlots;
            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentSlotDefinition customSlot = slots[i];
                if (customSlot != null)
                {
                    target.Add(customSlot.ToRuntimeSlot());
                }
            }
        }

        public bool TryGetSlot(string slotId, out EquipmentRuntimeSlot slot)
        {
            string normalizedSlotId = StableIdUtility.Normalize(slotId);
            List<EquipmentRuntimeSlot> slots = BuildRuntimeSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                if (StableIdUtility.EqualsNormalized(slots[i].SlotId, normalizedSlotId))
                {
                    slot = slots[i];
                    return true;
                }
            }

            slot = null;
            return false;
        }

        public IReadOnlyList<EquipmentLoadoutConfigValidationIssue> ValidateConfig()
        {
            return EquipmentLoadoutConfigValidator.Validate(this);
        }

        public static void AddGeneratedRingSlots(int ringCount, List<EquipmentRuntimeSlot> target)
        {
            if (target == null)
            {
                return;
            }

            int clampedCount = Mathf.Clamp(ringCount, 0, EquipmentSlotIdUtility.MaxRingSlotCount);
            for (int i = 0; i < clampedCount; i++)
            {
                target.Add(new EquipmentRuntimeSlot(
                    EquipmentSlotIdUtility.GetGeneratedRingSlotId(i),
                    EquipmentSlotIdUtility.GetGeneratedRingDisplayName(i),
                    new[] { ItemCategoryPath.FromPath("Equipment > Accessory > Ring") },
                    ItemFlags.Equipment | ItemFlags.Accessory,
                    ItemFlags.None,
                    900 + i,
                    true,
                    EquipmentSlotKind.Ring,
                    true));
            }
        }

        public static void AddDefaultBodySlots(List<EquipmentRuntimeSlot> target)
        {
            if (target == null)
            {
                return;
            }

            AddDefaultSlot(target, EquipmentSlotKind.Head, "Head", "Equipment > Armor > Head", ItemFlags.Equipment | ItemFlags.Armor, 100, false);
            AddDefaultSlot(target, EquipmentSlotKind.Shoulders, "Shoulders", "Equipment > Armor > Shoulders", ItemFlags.Equipment | ItemFlags.Armor, 110, false);
            AddDefaultSlot(target, EquipmentSlotKind.Gauntlets, "Gauntlets", "Equipment > Armor > Gauntlets", ItemFlags.Equipment | ItemFlags.Armor, 120, false);
            AddDefaultSlot(target, EquipmentSlotKind.Chest, "Chest", "Equipment > Armor > Chest", ItemFlags.Equipment | ItemFlags.Armor, 130, false);
            AddDefaultSlot(target, EquipmentSlotKind.Leggings, "Leggings", "Equipment > Armor > Leggings", ItemFlags.Equipment | ItemFlags.Armor, 140, false);
            AddDefaultSlot(target, EquipmentSlotKind.Boots, "Boots", "Equipment > Armor > Boots", ItemFlags.Equipment | ItemFlags.Armor, 150, false);
            AddDefaultSlot(target, EquipmentSlotKind.Wings, "Wings", "Equipment > Accessory > Wings", ItemFlags.Equipment | ItemFlags.Accessory, 800, true);
            AddDefaultSlot(target, EquipmentSlotKind.CapeCloak, "Cape/Cloak", "Equipment > Accessory > Cape", ItemFlags.Equipment | ItemFlags.Accessory, 810, true);
            AddDefaultSlot(target, EquipmentSlotKind.Amulet, "Amulet", "Equipment > Accessory > Amulet", ItemFlags.Equipment | ItemFlags.Accessory, 820, true);
        }

        private static void AddDefaultSlot(
            List<EquipmentRuntimeSlot> target,
            EquipmentSlotKind slotKind,
            string displayName,
            string categoryPath,
            ItemFlags allowedFlags,
            int sortOrder,
            bool accessory)
        {
            target.Add(new EquipmentRuntimeSlot(
                EquipmentSlotIdUtility.GetDefaultSlotId(slotKind),
                displayName,
                new[] { ItemCategoryPath.FromPath(categoryPath) },
                allowedFlags,
                ItemFlags.None,
                sortOrder,
                accessory,
                slotKind,
                true));
        }

        private void OnValidate()
        {
            ringSlotCount = Mathf.Clamp(ringSlotCount, 0, EquipmentSlotIdUtility.MaxRingSlotCount);
            customSlots ??= new List<EquipmentSlotDefinition>();
        }
    }

    public enum EquipmentLoadoutConfigValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class EquipmentLoadoutConfigValidationIssue
    {
        public EquipmentLoadoutConfigValidationIssue(EquipmentLoadoutConfigValidationSeverity severity, string message)
        {
            Severity = severity;
            Message = message ?? string.Empty;
        }

        public EquipmentLoadoutConfigValidationSeverity Severity { get; }

        public string Message { get; }
    }

    public static class EquipmentLoadoutConfigValidator
    {
        public static List<EquipmentLoadoutConfigValidationIssue> Validate(EquipmentLoadoutConfig config)
        {
            List<EquipmentLoadoutConfigValidationIssue> issues = new List<EquipmentLoadoutConfigValidationIssue>();
            if (config == null)
            {
                issues.Add(new EquipmentLoadoutConfigValidationIssue(EquipmentLoadoutConfigValidationSeverity.Error, "Equipment loadout config is null."));
                return issues;
            }

            Dictionary<string, EquipmentRuntimeSlot> seenSlots = new Dictionary<string, EquipmentRuntimeSlot>(StableIdUtility.Comparer);
            List<EquipmentRuntimeSlot> runtimeSlots = config.BuildRuntimeSlots();
            for (int i = 0; i < runtimeSlots.Count; i++)
            {
                EquipmentRuntimeSlot slot = runtimeSlots[i];
                if (slot == null || !slot.IsValid)
                {
                    issues.Add(new EquipmentLoadoutConfigValidationIssue(EquipmentLoadoutConfigValidationSeverity.Warning, "Equipment slot has a missing or invalid slot ID."));
                    continue;
                }

                if (seenSlots.ContainsKey(slot.SlotId))
                {
                    issues.Add(new EquipmentLoadoutConfigValidationIssue(EquipmentLoadoutConfigValidationSeverity.Error, $"Duplicate equipment slot ID '{slot.SlotId}'."));
                }
                else
                {
                    seenSlots.Add(slot.SlotId, slot);
                }
            }

            return issues;
        }
    }
}
