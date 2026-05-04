using System;
using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    public sealed class WorldItemIdentity : MonoBehaviour
    {
        [SerializeField]
        private string worldItemId = string.Empty;

        [SerializeField]
        private string manifestationRequestId = string.Empty;

        [SerializeField]
        private string ownerId = string.Empty;

        [SerializeField]
        private ItemDefId itemDefId = default;

        [SerializeField]
        private ItemInstanceId itemInstanceId = default;

        [SerializeField]
        private StackQuantity stackQuantity = StackQuantity.One;

        [SerializeField]
        private ItemLifecycleState lifecycleState = ItemLifecycleState.DroppedInWorld;

        [SerializeField]
        private string runtimeBindingId = string.Empty;

        public string WorldItemId
        {
            get { return StableIdUtility.Normalize(worldItemId); }
        }

        public string ManifestationRequestId
        {
            get { return StableIdUtility.Normalize(manifestationRequestId); }
        }

        public string OwnerId
        {
            get { return StableIdUtility.Normalize(ownerId); }
        }

        public ItemDefId ItemDefId
        {
            get { return itemDefId; }
        }

        public ItemInstanceId ItemInstanceId
        {
            get { return itemInstanceId; }
        }

        public StackQuantity StackQuantity
        {
            get { return stackQuantity.IsPositive ? stackQuantity : StackQuantity.One; }
        }

        public ItemLifecycleState LifecycleState
        {
            get { return lifecycleState; }
        }

        public string RuntimeBindingId
        {
            get { return StableIdUtility.Normalize(runtimeBindingId); }
        }

        public bool HasItemInstance
        {
            get { return !itemInstanceId.IsEmpty; }
        }

        public void Bind(
            string newWorldItemId,
            string newManifestationRequestId,
            string newOwnerId,
            ItemDefId newItemDefId,
            ItemInstanceId newItemInstanceId,
            StackQuantity newStackQuantity,
            ItemLifecycleState newLifecycleState,
            string newRuntimeBindingId)
        {
            worldItemId = StableIdUtility.IsValid(newWorldItemId) ? StableIdUtility.Normalize(newWorldItemId) : CreateWorldItemId();
            manifestationRequestId = StableIdUtility.Normalize(newManifestationRequestId);
            ownerId = StableIdUtility.Normalize(newOwnerId);
            itemDefId = newItemDefId;
            itemInstanceId = newItemInstanceId;
            stackQuantity = newStackQuantity.IsPositive ? newStackQuantity : StackQuantity.One;
            lifecycleState = newLifecycleState;
            runtimeBindingId = StableIdUtility.Normalize(newRuntimeBindingId);
        }

        public void SetLifecycleState(ItemLifecycleState newLifecycleState)
        {
            lifecycleState = newLifecycleState;
        }

        public static string CreateWorldItemId()
        {
            return "world_item_" + Guid.NewGuid().ToString("N");
        }

        private void Reset()
        {
            if (string.IsNullOrWhiteSpace(worldItemId))
            {
                worldItemId = CreateWorldItemId();
            }

            if (!stackQuantity.IsPositive)
            {
                stackQuantity = StackQuantity.One;
            }
        }
    }
}
