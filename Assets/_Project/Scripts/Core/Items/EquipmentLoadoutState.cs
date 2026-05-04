using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    [Serializable]
    public sealed class EquipmentLoadoutState
    {
        [SerializeField]
        private List<EquipmentSlotAssignment> equippedSlots = new List<EquipmentSlotAssignment>();

        [SerializeField]
        private bool statsDirty = true;

        [SerializeField]
        private long statsRevision = 0;

        public IReadOnlyList<EquipmentSlotAssignment> EquippedSlots
        {
            get { return equippedSlots ?? (IReadOnlyList<EquipmentSlotAssignment>)Array.Empty<EquipmentSlotAssignment>(); }
        }

        public bool StatsDirty
        {
            get { return statsDirty; }
        }

        public long StatsRevision
        {
            get { return Math.Max(0, statsRevision); }
        }

        public bool TryGetEquippedItem(string slotId, out ItemInstanceId itemInstanceId)
        {
            int index = FindSlotIndex(slotId);
            if (index < 0)
            {
                itemInstanceId = default;
                return false;
            }

            itemInstanceId = equippedSlots[index].ItemInstanceId;
            return !itemInstanceId.IsEmpty;
        }

        public bool IsSlotEmpty(string slotId)
        {
            return !TryGetEquippedItem(slotId, out _);
        }

        internal void SetEquippedItem(string slotId, ItemInstanceId itemInstanceId)
        {
            EnsureList();
            string normalizedSlotId = StableIdUtility.Normalize(slotId);
            int index = FindSlotIndex(normalizedSlotId);
            if (index < 0)
            {
                equippedSlots.Add(new EquipmentSlotAssignment(normalizedSlotId, itemInstanceId));
            }
            else
            {
                equippedSlots[index].SetItemInstanceId(itemInstanceId);
            }

            MarkStatsDirty();
        }

        internal bool ClearSlot(string slotId, out ItemInstanceId previousItemInstanceId)
        {
            EnsureList();
            int index = FindSlotIndex(slotId);
            if (index < 0)
            {
                previousItemInstanceId = default;
                return false;
            }

            previousItemInstanceId = equippedSlots[index].ItemInstanceId;
            equippedSlots.RemoveAt(index);
            MarkStatsDirty();
            return !previousItemInstanceId.IsEmpty;
        }

        internal void MarkStatsDirty()
        {
            statsDirty = true;
            statsRevision = statsRevision == long.MaxValue ? long.MaxValue : statsRevision + 1;
        }

        public void ClearStatsDirty()
        {
            statsDirty = false;
        }

        internal int FindSlotIndex(string slotId)
        {
            EnsureList();
            string normalizedSlotId = StableIdUtility.Normalize(slotId);
            if (string.IsNullOrEmpty(normalizedSlotId))
            {
                return -1;
            }

            for (int i = 0; i < equippedSlots.Count; i++)
            {
                EquipmentSlotAssignment assignment = equippedSlots[i];
                if (assignment != null && StableIdUtility.EqualsNormalized(assignment.SlotId, normalizedSlotId))
                {
                    return i;
                }
            }

            return -1;
        }

        internal void EnsureList()
        {
            equippedSlots ??= new List<EquipmentSlotAssignment>();
        }
    }

    [Serializable]
    public sealed class EquipmentSlotAssignment
    {
        [SerializeField]
        private string slotId = string.Empty;

        [SerializeField]
        private ItemInstanceId itemInstanceId = default;

        public EquipmentSlotAssignment(string slotId, ItemInstanceId itemInstanceId)
        {
            this.slotId = StableIdUtility.Normalize(slotId);
            this.itemInstanceId = itemInstanceId;
        }

        public string SlotId
        {
            get { return StableIdUtility.Normalize(slotId); }
        }

        public ItemInstanceId ItemInstanceId
        {
            get { return itemInstanceId; }
        }

        internal void SetItemInstanceId(ItemInstanceId newItemInstanceId)
        {
            itemInstanceId = newItemInstanceId;
        }
    }
}
