using System;
using System.Collections.Generic;
using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    public enum ManifestationSourceKind
    {
        Stack,
        ItemInstance
    }

    public enum ManifestationReservationState
    {
        Reserved,
        Spawned,
        Held,
        Dropped,
        ReturnedToInventory,
        CommittedToEquipment,
        Cancelled,
        Destroyed
    }

    [Serializable]
    public sealed class ManifestationReservation
    {
        [SerializeField]
        private string requestId = string.Empty;

        [SerializeField]
        private string playerId = string.Empty;

        [SerializeField]
        private ManifestationSourceKind sourceKind = ManifestationSourceKind.Stack;

        [SerializeField]
        private ItemDefId itemDefId = default;

        [SerializeField]
        private ItemInstanceId itemInstanceId = default;

        [SerializeField]
        private StackQuantity reservedQuantity = StackQuantity.One;

        [SerializeField]
        private string spawnedWorldItemId = string.Empty;

        [SerializeField]
        private string requestedHandId = string.Empty;

        [SerializeField]
        private ManifestationReservationState state = ManifestationReservationState.Reserved;

        [SerializeField]
        private long sourceInventoryRevision = 0;

        [SerializeField]
        private double createdAtRealtime = 0d;

        public ManifestationReservation()
        {
        }

        public ManifestationReservation(
            string requestId,
            string playerId,
            ManifestationSourceKind sourceKind,
            ItemDefId itemDefId,
            ItemInstanceId itemInstanceId,
            StackQuantity reservedQuantity,
            string requestedHandId,
            long sourceInventoryRevision)
        {
            this.requestId = StableIdUtility.IsValid(requestId) ? StableIdUtility.Normalize(requestId) : CreateRequestId();
            this.playerId = StableIdUtility.Normalize(playerId);
            this.sourceKind = sourceKind;
            this.itemDefId = itemDefId;
            this.itemInstanceId = itemInstanceId;
            this.reservedQuantity = reservedQuantity.IsPositive ? reservedQuantity : StackQuantity.One;
            this.requestedHandId = StableIdUtility.Normalize(requestedHandId);
            this.sourceInventoryRevision = Math.Max(0, sourceInventoryRevision);
            createdAtRealtime = Time.realtimeSinceStartupAsDouble;
        }

        public string RequestId
        {
            get { return StableIdUtility.Normalize(requestId); }
        }

        public string PlayerId
        {
            get { return StableIdUtility.Normalize(playerId); }
        }

        public ManifestationSourceKind SourceKind
        {
            get { return sourceKind; }
        }

        public ItemDefId ItemDefId
        {
            get { return itemDefId; }
        }

        public ItemInstanceId ItemInstanceId
        {
            get { return itemInstanceId; }
        }

        public StackQuantity ReservedQuantity
        {
            get { return reservedQuantity.IsPositive ? reservedQuantity : StackQuantity.One; }
        }

        public string SpawnedWorldItemId
        {
            get { return StableIdUtility.Normalize(spawnedWorldItemId); }
        }

        public string RequestedHandId
        {
            get { return StableIdUtility.Normalize(requestedHandId); }
        }

        public ManifestationReservationState State
        {
            get { return state; }
        }

        public long SourceInventoryRevision
        {
            get { return Math.Max(0, sourceInventoryRevision); }
        }

        public double CreatedAtRealtime
        {
            get { return Math.Max(0d, createdAtRealtime); }
        }

        public bool IsActive
        {
            get
            {
                return state == ManifestationReservationState.Reserved ||
                       state == ManifestationReservationState.Spawned ||
                       state == ManifestationReservationState.Held;
            }
        }

        internal void MarkSpawned(string worldItemId)
        {
            spawnedWorldItemId = StableIdUtility.Normalize(worldItemId);
            state = ManifestationReservationState.Spawned;
        }

        internal void MarkHeld()
        {
            state = ManifestationReservationState.Held;
        }

        internal void MarkDropped()
        {
            state = ManifestationReservationState.Dropped;
        }

        internal void MarkReturned()
        {
            state = ManifestationReservationState.ReturnedToInventory;
        }

        internal void MarkCommittedToEquipment()
        {
            state = ManifestationReservationState.CommittedToEquipment;
        }

        internal void MarkCancelled()
        {
            state = ManifestationReservationState.Cancelled;
        }

        internal void MarkDestroyed()
        {
            state = ManifestationReservationState.Destroyed;
        }

        public static string CreateRequestId()
        {
            return "manifest_" + Guid.NewGuid().ToString("N");
        }
    }

    [Serializable]
    public sealed class ManifestationReservationStore
    {
        [SerializeField]
        private List<ManifestationReservation> reservations = new List<ManifestationReservation>();

        private readonly Dictionary<string, WorldItemView> worldItemsByRequestId = new Dictionary<string, WorldItemView>(StableIdUtility.Comparer);

        public IReadOnlyList<ManifestationReservation> Reservations
        {
            get { return reservations ?? (IReadOnlyList<ManifestationReservation>)Array.Empty<ManifestationReservation>(); }
        }

        public ManifestationReservation AddReservation(ManifestationReservation reservation)
        {
            EnsureList();
            if (reservation != null && FindReservationIndex(reservation.RequestId) < 0)
            {
                reservations.Add(reservation);
            }

            return reservation;
        }

        public bool TryGetReservation(string requestId, out ManifestationReservation reservation)
        {
            int index = FindReservationIndex(requestId);
            if (index < 0)
            {
                reservation = null;
                return false;
            }

            reservation = reservations[index];
            return true;
        }

        public bool TryGetWorldItem(string requestId, out WorldItemView worldItemView)
        {
            string normalized = StableIdUtility.Normalize(requestId);
            if (string.IsNullOrEmpty(normalized))
            {
                worldItemView = null;
                return false;
            }

            if (worldItemsByRequestId.TryGetValue(normalized, out worldItemView) && worldItemView != null)
            {
                return true;
            }

            worldItemsByRequestId.Remove(normalized);
            worldItemView = null;
            return false;
        }

        public bool HasActiveReservationForInstance(ItemInstanceId itemInstanceId)
        {
            EnsureList();
            if (itemInstanceId.IsEmpty)
            {
                return false;
            }

            for (int i = 0; i < reservations.Count; i++)
            {
                ManifestationReservation reservation = reservations[i];
                if (reservation != null && reservation.IsActive && reservation.ItemInstanceId == itemInstanceId)
                {
                    return true;
                }
            }

            return false;
        }

        public void BindWorldItem(string requestId, WorldItemView worldItemView)
        {
            string normalized = StableIdUtility.Normalize(requestId);
            if (string.IsNullOrEmpty(normalized) || worldItemView == null)
            {
                return;
            }

            worldItemsByRequestId[normalized] = worldItemView;
        }

        public void ClearWorldItem(string requestId)
        {
            string normalized = StableIdUtility.Normalize(requestId);
            if (!string.IsNullOrEmpty(normalized))
            {
                worldItemsByRequestId.Remove(normalized);
            }
        }

        private int FindReservationIndex(string requestId)
        {
            EnsureList();
            string normalized = StableIdUtility.Normalize(requestId);
            if (string.IsNullOrEmpty(normalized))
            {
                return -1;
            }

            for (int i = 0; i < reservations.Count; i++)
            {
                ManifestationReservation reservation = reservations[i];
                if (reservation != null && StableIdUtility.EqualsNormalized(reservation.RequestId, normalized))
                {
                    return i;
                }
            }

            return -1;
        }

        private void EnsureList()
        {
            reservations ??= new List<ManifestationReservation>();
        }
    }

    public sealed class ItemManifestationResult
    {
        public ItemManifestationResult(InventoryOperationResult inventoryResult, ManifestationReservation reservation = null, WorldItemView worldItemView = null)
        {
            InventoryResult = inventoryResult;
            Reservation = reservation;
            WorldItemView = worldItemView;
        }

        public InventoryOperationResult InventoryResult { get; }

        public ManifestationReservation Reservation { get; }

        public WorldItemView WorldItemView { get; }

        public bool Success
        {
            get { return InventoryResult != null && InventoryResult.Success; }
        }

        public string Message
        {
            get { return InventoryResult != null ? InventoryResult.Message : string.Empty; }
        }
    }
}
