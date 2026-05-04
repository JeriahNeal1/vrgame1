using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    public enum PlacementFailureReason
    {
        None,
        InvalidInventoryState,
        InvalidItemDefinitionDatabase,
        InvalidItemDefinitionId,
        UnknownItemDefinition,
        ItemNotPlaceable,
        MissingPlaceableProfile,
        MissingPlacedPrefab,
        InvalidPlacementPose,
        InsufficientStack,
        RequiredToolMissing,
        RequiredToolMismatch,
        GroundRequired,
        SnapPointRequired,
        InvalidSnapTarget,
        PlacementBlocked,
        SpawnFailed,
        InventoryConsumeFailed
    }

    public sealed class PlacementPose
    {
        public Vector3 Position { get; set; }

        public Quaternion Rotation { get; set; } = Quaternion.identity;

        public Vector3 SurfaceNormal { get; set; } = Vector3.up;

        public Collider SurfaceCollider { get; set; }

        public bool HasSurface
        {
            get { return SurfaceCollider != null; }
        }

        public GameObject SurfaceObject
        {
            get { return SurfaceCollider != null ? SurfaceCollider.gameObject : null; }
        }

        public static PlacementPose FromTransform(Transform transform)
        {
            if (transform == null)
            {
                return new PlacementPose();
            }

            return new PlacementPose
            {
                Position = transform.position,
                Rotation = transform.rotation,
                SurfaceNormal = transform.up
            };
        }
    }

    public sealed class PlacementResult
    {
        private PlacementResult(
            bool success,
            PlacementFailureReason failureReason,
            string message,
            ItemDefId itemDefId,
            StackQuantity consumedQuantity,
            InventoryOperationResult inventoryResult = null,
            GameObject placedObject = null,
            GameObject previewObject = null,
            FrameworkSnapPoint snapPoint = null)
        {
            Success = success;
            FailureReason = failureReason;
            Message = message ?? string.Empty;
            ItemDefId = itemDefId;
            ConsumedQuantity = consumedQuantity;
            InventoryResult = inventoryResult;
            PlacedObject = placedObject;
            PreviewObject = previewObject;
            SnapPoint = snapPoint;
        }

        public bool Success { get; }

        public PlacementFailureReason FailureReason { get; }

        public string Message { get; }

        public ItemDefId ItemDefId { get; }

        public StackQuantity ConsumedQuantity { get; }

        public InventoryOperationResult InventoryResult { get; }

        public GameObject PlacedObject { get; }

        public GameObject PreviewObject { get; }

        public FrameworkSnapPoint SnapPoint { get; }

        public static PlacementResult Succeeded(
            ItemDefId itemDefId,
            StackQuantity consumedQuantity,
            string message,
            InventoryOperationResult inventoryResult = null,
            GameObject placedObject = null,
            GameObject previewObject = null,
            FrameworkSnapPoint snapPoint = null)
        {
            return new PlacementResult(true, PlacementFailureReason.None, message, itemDefId, consumedQuantity, inventoryResult, placedObject, previewObject, snapPoint);
        }

        public static PlacementResult Failed(PlacementFailureReason failureReason, string message, ItemDefId itemDefId = default, StackQuantity consumedQuantity = default, InventoryOperationResult inventoryResult = null)
        {
            return new PlacementResult(false, failureReason, message, itemDefId, consumedQuantity, inventoryResult);
        }
    }
}
