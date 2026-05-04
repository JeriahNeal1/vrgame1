using System;
using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlacedObjectIdentity : MonoBehaviour
    {
        [SerializeField]
        private string placedObjectId = string.Empty;

        [SerializeField]
        private string ownerId = string.Empty;

        [SerializeField]
        private ItemDefId itemDefId = default;

        [SerializeField]
        private PlacementMode placementMode = PlacementMode.FreeFurniture;

        [SerializeField]
        private StackQuantity sourceQuantity = StackQuantity.One;

        [SerializeField]
        private string placementTransactionId = string.Empty;

        public string PlacedObjectId
        {
            get { return StableIdUtility.Normalize(placedObjectId); }
        }

        public string OwnerId
        {
            get { return StableIdUtility.Normalize(ownerId); }
        }

        public ItemDefId ItemDefId
        {
            get { return itemDefId; }
        }

        public PlacementMode PlacementMode
        {
            get { return placementMode; }
        }

        public StackQuantity SourceQuantity
        {
            get { return sourceQuantity.IsPositive ? sourceQuantity : StackQuantity.One; }
        }

        public string PlacementTransactionId
        {
            get { return StableIdUtility.Normalize(placementTransactionId); }
        }

        public void Bind(string ownerId, ItemDefId itemDefId, PlacementMode placementMode, StackQuantity sourceQuantity, string placementTransactionId)
        {
            placedObjectId = CreatePlacedObjectId();
            this.ownerId = StableIdUtility.Normalize(ownerId);
            this.itemDefId = itemDefId;
            this.placementMode = placementMode;
            this.sourceQuantity = sourceQuantity.IsPositive ? sourceQuantity : StackQuantity.One;
            this.placementTransactionId = StableIdUtility.Normalize(placementTransactionId);
        }

        public static string CreatePlacedObjectId()
        {
            return "placed_" + Guid.NewGuid().ToString("N");
        }
    }
}
