using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    [Serializable]
    public sealed class PlayerInventoryState
    {
        [SerializeField]
        private string ownerId = string.Empty;

        [SerializeField]
        private long revision = 0;

        [SerializeField]
        private List<InventoryStackRecord> stackLedger = new List<InventoryStackRecord>();

        [SerializeField]
        private List<ItemInstanceState> itemInstances = new List<ItemInstanceState>();

        [SerializeField]
        private EquipmentLoadoutState equipmentLoadout = new EquipmentLoadoutState();

        [SerializeField]
        private List<HeldWorldItemReference> heldWorldItemReferences = new List<HeldWorldItemReference>();

        public PlayerInventoryState()
        {
        }

        public PlayerInventoryState(string ownerId)
        {
            this.ownerId = StableIdUtility.Normalize(ownerId);
        }

        public string OwnerId
        {
            get { return StableIdUtility.Normalize(ownerId); }
        }

        public long Revision
        {
            get { return Math.Max(0, revision); }
        }

        public IReadOnlyList<InventoryStackRecord> StackLedger
        {
            get { return stackLedger ?? (IReadOnlyList<InventoryStackRecord>)Array.Empty<InventoryStackRecord>(); }
        }

        public IReadOnlyList<ItemInstanceState> ItemInstances
        {
            get { return itemInstances ?? (IReadOnlyList<ItemInstanceState>)Array.Empty<ItemInstanceState>(); }
        }

        public EquipmentLoadoutState EquipmentLoadout
        {
            get
            {
                equipmentLoadout ??= new EquipmentLoadoutState();
                return equipmentLoadout;
            }
        }

        public IReadOnlyList<HeldWorldItemReference> HeldWorldItemReferences
        {
            get { return heldWorldItemReferences ?? (IReadOnlyList<HeldWorldItemReference>)Array.Empty<HeldWorldItemReference>(); }
        }

        public bool TryGetStack(ItemDefId itemDefId, out InventoryStackRecord stackRecord)
        {
            int index = FindStackIndex(itemDefId);
            if (index < 0)
            {
                stackRecord = null;
                return false;
            }

            stackRecord = stackLedger[index];
            return true;
        }

        public bool TryGetInstance(ItemInstanceId itemInstanceId, out ItemInstanceState itemInstance)
        {
            int index = FindInstanceIndex(itemInstanceId);
            if (index < 0)
            {
                itemInstance = null;
                return false;
            }

            itemInstance = itemInstances[index];
            return true;
        }

        internal int FindStackIndex(ItemDefId itemDefId)
        {
            EnsureLists();
            if (itemDefId.IsEmpty)
            {
                return -1;
            }

            for (int i = 0; i < stackLedger.Count; i++)
            {
                InventoryStackRecord record = stackLedger[i];
                if (record != null && record.ItemDefId == itemDefId)
                {
                    return i;
                }
            }

            return -1;
        }

        internal int FindInstanceIndex(ItemInstanceId itemInstanceId)
        {
            EnsureLists();
            if (itemInstanceId.IsEmpty)
            {
                return -1;
            }

            for (int i = 0; i < itemInstances.Count; i++)
            {
                ItemInstanceState record = itemInstances[i];
                if (record != null && record.ItemInstanceId == itemInstanceId)
                {
                    return i;
                }
            }

            return -1;
        }

        internal InventoryStackRecord AddStackRecord(ItemDefId itemDefId, StackQuantity quantity)
        {
            EnsureLists();
            InventoryStackRecord stackRecord = new InventoryStackRecord(itemDefId, quantity);
            stackLedger.Add(stackRecord);
            return stackRecord;
        }

        internal void RemoveStackAt(int index)
        {
            EnsureLists();
            if (index >= 0 && index < stackLedger.Count)
            {
                stackLedger.RemoveAt(index);
            }
        }

        internal void AddItemInstance(ItemInstanceState itemInstance)
        {
            EnsureLists();
            if (itemInstance != null)
            {
                itemInstance.EnsureLists();
                itemInstances.Add(itemInstance);
            }
        }

        internal void IncrementRevision()
        {
            revision = revision == long.MaxValue ? long.MaxValue : revision + 1;
        }

        internal void EnsureLists()
        {
            stackLedger ??= new List<InventoryStackRecord>();
            itemInstances ??= new List<ItemInstanceState>();
            equipmentLoadout ??= new EquipmentLoadoutState();
            heldWorldItemReferences ??= new List<HeldWorldItemReference>();
            equipmentLoadout.EnsureList();

            for (int i = 0; i < itemInstances.Count; i++)
            {
                itemInstances[i]?.EnsureLists();
            }
        }
    }
}
