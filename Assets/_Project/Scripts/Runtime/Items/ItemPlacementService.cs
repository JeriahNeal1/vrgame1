using System;
using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    public sealed class ItemPlacementService : MonoBehaviour
    {
        [Header("Services")]
        [SerializeField]
        private ItemDefinitionDatabase itemDefinitionDatabase = null;

        [Tooltip("Optional MonoBehaviour that implements IPlayerInventoryStateProvider.")]
        [SerializeField]
        private MonoBehaviour inventoryStateProviderBehaviour = null;

        [Header("Parents")]
        [SerializeField]
        private Transform placedObjectParent = null;

        [SerializeField]
        private Transform previewObjectParent = null;

        [Header("Debug")]
        [SerializeField]
        private bool verboseLogging = false;

        private IPlayerInventoryStateProvider inventoryStateProvider;
        private IPlayerInventoryStateProvider runtimeInventoryStateProvider;
        private GameObject activePreviewObject;
        private ItemDefId activePreviewItemDefId;

        public GameObject ActivePreviewObject
        {
            get { return activePreviewObject; }
        }

        public void BindRuntime(ItemDefinitionDatabase database, IPlayerInventoryStateProvider provider)
        {
            itemDefinitionDatabase = database;
            runtimeInventoryStateProvider = provider;
            inventoryStateProvider = provider;
        }

        public PlacementResult ShowPreview(ItemDefId itemDefId, PlacementPose pose, FrameworkSnapPoint snapPoint = null, WorldItemView requiredToolView = null)
        {
            PlacementResult validation = ValidatePlacement(itemDefId, pose, snapPoint, requiredToolView);
            if (!validation.Success)
            {
                DestroyPreview();
                return validation;
            }

            itemDefinitionDatabase.TryGet(itemDefId, out ItemDefinition itemDefinition);
            PlaceableProfile profile = itemDefinition.PlaceableProfile;
            GameObject prefab = profile.PreviewPrefab;
            if (prefab == null)
            {
                DestroyPreview();
                return PlacementResult.Failed(PlacementFailureReason.MissingPlacedPrefab, $"Placeable item '{itemDefId}' has no preview or placed prefab.", itemDefId, profile.ConsumedItemQuantity);
            }

            if (activePreviewObject == null || activePreviewItemDefId != itemDefId)
            {
                DestroyPreview();
                activePreviewObject = Instantiate(prefab, previewObjectParent);
                activePreviewObject.name = $"{itemDefinition.DisplayName}_PlacementPreview";
                ConfigurePreviewObject(activePreviewObject);
                activePreviewItemDefId = itemDefId;
            }

            ApplyPose(activePreviewObject.transform, profile, pose, snapPoint);
            return PlacementResult.Succeeded(itemDefId, StackQuantity.Zero, "Placement preview updated.", null, null, activePreviewObject, snapPoint);
        }

        public PlacementResult TryPlace(ItemDefId itemDefId, PlacementPose pose, FrameworkSnapPoint snapPoint = null, WorldItemView requiredToolView = null)
        {
            PlacementResult validation = ValidatePlacement(itemDefId, pose, snapPoint, requiredToolView);
            if (!validation.Success)
            {
                return validation;
            }

            PlayerInventoryState inventoryState = ResolveInventoryState();
            itemDefinitionDatabase.TryGet(itemDefId, out ItemDefinition itemDefinition);
            PlaceableProfile profile = itemDefinition.PlaceableProfile;
            GameObject placedPrefab = profile.PlacedPrefab;
            if (placedPrefab == null)
            {
                return PlacementResult.Failed(PlacementFailureReason.MissingPlacedPrefab, $"Placeable item '{itemDefId}' has no placed prefab.", itemDefId, profile.ConsumedItemQuantity);
            }

            InventoryOperationResult consumeResult = PlayerInventoryOperations.RemoveStack(inventoryState, itemDefinitionDatabase, itemDefId, profile.ConsumedItemQuantity);
            if (!consumeResult.Success)
            {
                return PlacementResult.Failed(PlacementFailureReason.InventoryConsumeFailed, consumeResult.Message, itemDefId, profile.ConsumedItemQuantity, consumeResult);
            }

            GameObject placedObject = null;
            try
            {
                placedObject = Instantiate(placedPrefab, placedObjectParent);
                placedObject.name = itemDefinition.DisplayName;
                ApplyPose(placedObject.transform, profile, pose, snapPoint);
                BindPlacedObject(placedObject, inventoryState, itemDefId, profile, profile.ConsumedItemQuantity);
                ConfigureFrameworkPiece(placedObject, profile);

                if (profile.PlacementMode == PlacementMode.FrameworkSnap && snapPoint != null)
                {
                    snapPoint.MarkOccupied(true);
                }

                Log($"Placed '{itemDefId}' at {placedObject.transform.position}.");
                return PlacementResult.Succeeded(itemDefId, profile.ConsumedItemQuantity, $"Placed '{itemDefId}'.", consumeResult, placedObject, null, snapPoint);
            }
            catch (Exception exception)
            {
                if (placedObject != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(placedObject);
                    }
                    else
                    {
                        DestroyImmediate(placedObject);
                    }
                }

                PlayerInventoryOperations.AddStack(inventoryState, itemDefinitionDatabase, itemDefId, profile.ConsumedItemQuantity);
                if (profile.PlacementMode == PlacementMode.FrameworkSnap && snapPoint != null)
                {
                    snapPoint.MarkOccupied(false);
                }

                return PlacementResult.Failed(PlacementFailureReason.SpawnFailed, exception.Message, itemDefId, profile.ConsumedItemQuantity, consumeResult);
            }
        }

        public PlacementResult ValidatePlacement(ItemDefId itemDefId, PlacementPose pose, FrameworkSnapPoint snapPoint = null, WorldItemView requiredToolView = null)
        {
            PlacementResult definitionResult = ValidatePlaceableDefinition(itemDefId, out ItemDefinition itemDefinition, out PlaceableProfile profile);
            if (!definitionResult.Success)
            {
                return definitionResult;
            }

            if (!IsValidPose(pose))
            {
                return PlacementResult.Failed(PlacementFailureReason.InvalidPlacementPose, "Placement pose is missing or contains non-finite values.", itemDefId, profile.ConsumedItemQuantity);
            }

            PlacementResult toolResult = ValidateRequiredTool(profile, requiredToolView, itemDefId);
            if (!toolResult.Success)
            {
                return toolResult;
            }

            PlacementResult modeResult = ValidatePlacementMode(profile, pose, snapPoint, itemDefId);
            if (!modeResult.Success)
            {
                return modeResult;
            }

            PlacementResult collisionResult = ValidateCollision(profile, pose, snapPoint, itemDefId);
            if (!collisionResult.Success)
            {
                return collisionResult;
            }

            return PlacementResult.Succeeded(itemDefId, StackQuantity.Zero, $"Placement is valid for '{itemDefinition.DisplayName}'.", null, null, null, snapPoint);
        }

        public void DestroyPreview()
        {
            if (activePreviewObject != null)
            {
                Destroy(activePreviewObject);
            }

            activePreviewObject = null;
            activePreviewItemDefId = default;
        }

        private PlacementResult ValidatePlaceableDefinition(ItemDefId itemDefId, out ItemDefinition itemDefinition, out PlaceableProfile profile)
        {
            itemDefinition = null;
            profile = null;
            PlayerInventoryState inventoryState = ResolveInventoryState();
            if (inventoryState == null)
            {
                return PlacementResult.Failed(PlacementFailureReason.InvalidInventoryState, "Player inventory state is null.", itemDefId);
            }

            if (itemDefinitionDatabase == null)
            {
                return PlacementResult.Failed(PlacementFailureReason.InvalidItemDefinitionDatabase, "Item definition database is null.", itemDefId);
            }

            if (itemDefId.IsEmpty)
            {
                return PlacementResult.Failed(PlacementFailureReason.InvalidItemDefinitionId, "Item definition ID is empty.");
            }

            if (!itemDefinitionDatabase.TryGet(itemDefId, out itemDefinition) || itemDefinition == null)
            {
                return PlacementResult.Failed(PlacementFailureReason.UnknownItemDefinition, $"Unknown item definition '{itemDefId}'.", itemDefId);
            }

            if (!itemDefinition.HasFlag(ItemFlags.Placeable))
            {
                return PlacementResult.Failed(PlacementFailureReason.ItemNotPlaceable, $"Item definition '{itemDefId}' is not flagged Placeable.", itemDefId);
            }

            profile = itemDefinition.PlaceableProfile;
            if (!itemDefinition.HasPlaceableProfile || profile == null)
            {
                return PlacementResult.Failed(PlacementFailureReason.MissingPlaceableProfile, $"Item definition '{itemDefId}' has no placeable profile.", itemDefId);
            }

            if (!itemDefinition.ResolvedStackPolicy.IsStackable)
            {
                return PlacementResult.Failed(PlacementFailureReason.ItemNotPlaceable, $"Placeable item '{itemDefId}' must be stackable inventory data for placement consumption.", itemDefId, profile.ConsumedItemQuantity);
            }

            if (!PlayerInventoryOperations.HasStack(inventoryState, itemDefId, profile.ConsumedItemQuantity))
            {
                return PlacementResult.Failed(PlacementFailureReason.InsufficientStack, $"Inventory does not contain {profile.ConsumedItemQuantity} of '{itemDefId}'.", itemDefId, profile.ConsumedItemQuantity);
            }

            return PlacementResult.Succeeded(itemDefId, StackQuantity.Zero, "Placeable definition is valid.");
        }

        private PlacementResult ValidateRequiredTool(PlaceableProfile profile, WorldItemView requiredToolView, ItemDefId itemDefId)
        {
            PlacementToolRequirement toolRequirement = profile.RequiredTool;
            if (!toolRequirement.RequiresTool)
            {
                return PlacementResult.Succeeded(itemDefId, StackQuantity.Zero, "No placement tool required.");
            }

            if (requiredToolView == null)
            {
                return PlacementResult.Failed(PlacementFailureReason.RequiredToolMissing, "Placement requires a matching held tool.", itemDefId, profile.ConsumedItemQuantity);
            }

            ItemDefinition toolDefinition = requiredToolView.BoundDefinition;
            if (toolDefinition == null && itemDefinitionDatabase != null)
            {
                itemDefinitionDatabase.TryGet(requiredToolView.Identity.ItemDefId, out toolDefinition);
            }

            if (toolDefinition == null || !toolDefinition.HasToolProfile || toolDefinition.ToolProfile == null)
            {
                return PlacementResult.Failed(PlacementFailureReason.RequiredToolMismatch, "Held item is not a tool with a tool profile.", itemDefId, profile.ConsumedItemQuantity);
            }

            ToolProfile toolProfile = toolDefinition.ToolProfile;
            bool matchesDomain = toolRequirement.RequiredHarvestingType == HarvestingDomain.None || toolProfile.HarvestingType == toolRequirement.RequiredHarvestingType;
            bool matchesSubtype = toolRequirement.RequiredToolSubtype == HarvestingSubtype.None || toolProfile.ToolSubtype == toolRequirement.RequiredToolSubtype;
            bool matchesTier = toolProfile.ToolTier >= toolRequirement.RequiredToolTier;
            if (!matchesDomain || !matchesSubtype || !matchesTier)
            {
                return PlacementResult.Failed(PlacementFailureReason.RequiredToolMismatch, "Held tool does not match the placeable tool requirement.", itemDefId, profile.ConsumedItemQuantity);
            }

            return PlacementResult.Succeeded(itemDefId, StackQuantity.Zero, "Held tool satisfies placement requirement.");
        }

        private PlacementResult ValidatePlacementMode(PlaceableProfile profile, PlacementPose pose, FrameworkSnapPoint snapPoint, ItemDefId itemDefId)
        {
            switch (profile.PlacementMode)
            {
                case PlacementMode.FrameworkSnap:
                    return ValidateFrameworkPlacement(profile, pose, snapPoint, itemDefId);
                case PlacementMode.FreeFurniture:
                case PlacementMode.ElectricalDevice:
                case PlacementMode.Machine:
                case PlacementMode.Decoration:
                    if (profile.SurfaceSnapMode == PlacementSurfaceSnapMode.Required && !pose.HasSurface)
                    {
                        return PlacementResult.Failed(PlacementFailureReason.GroundRequired, "Placement requires a valid surface.", itemDefId, profile.ConsumedItemQuantity);
                    }

                    return PlacementResult.Succeeded(itemDefId, StackQuantity.Zero, "Free placement mode is valid.");
                case PlacementMode.Wire:
                    return PlacementResult.Failed(PlacementFailureReason.MissingPlacedPrefab, "Wire placement is handled by WireToolAction node connections, not direct prefab placement.", itemDefId, profile.ConsumedItemQuantity);
                default:
                    return PlacementResult.Failed(PlacementFailureReason.ItemNotPlaceable, $"Unsupported placement mode '{profile.PlacementMode}'.", itemDefId, profile.ConsumedItemQuantity);
            }
        }

        private PlacementResult ValidateFrameworkPlacement(PlaceableProfile profile, PlacementPose pose, FrameworkSnapPoint snapPoint, ItemDefId itemDefId)
        {
            if (profile.FrameworkPieceKind == FrameworkPieceKind.Foundation)
            {
                if (!pose.HasSurface)
                {
                    return PlacementResult.Failed(PlacementFailureReason.GroundRequired, "Foundation placement requires a valid ground surface.", itemDefId, profile.ConsumedItemQuantity);
                }

                if (!IsLayerInMask(pose.SurfaceObject.layer, profile.ValidGroundLayers))
                {
                    return PlacementResult.Failed(PlacementFailureReason.GroundRequired, "Foundation surface is not on a valid ground layer.", itemDefId, profile.ConsumedItemQuantity);
                }

                return PlacementResult.Succeeded(itemDefId, StackQuantity.Zero, "Foundation can be placed on valid ground.");
            }

            if (snapPoint == null)
            {
                return PlacementResult.Failed(PlacementFailureReason.SnapPointRequired, $"{profile.FrameworkPieceKind} placement requires a framework snap point.", itemDefId, profile.ConsumedItemQuantity);
            }

            if (!snapPoint.CanAccept(profile, pose.Position, out string snapReason))
            {
                return PlacementResult.Failed(PlacementFailureReason.InvalidSnapTarget, snapReason, itemDefId, profile.ConsumedItemQuantity);
            }

            return PlacementResult.Succeeded(itemDefId, StackQuantity.Zero, "Framework snap placement is valid.", null, null, null, snapPoint);
        }

        private PlacementResult ValidateCollision(PlaceableProfile profile, PlacementPose pose, FrameworkSnapPoint snapPoint, ItemDefId itemDefId)
        {
            PlacementCollisionRules collisionRules = profile.CollisionRules;
            if (!collisionRules.RequireNoBlockingOverlap)
            {
                return PlacementResult.Succeeded(itemDefId, StackQuantity.Zero, "Collision clearance check disabled.");
            }

            Vector3 extents = collisionRules.BoundsExtents + Vector3.one * collisionRules.Padding;
            if (extents.sqrMagnitude <= 0.0001f)
            {
                return PlacementResult.Succeeded(itemDefId, StackQuantity.Zero, "Collision bounds are empty.");
            }

            Vector3 center = pose.Position + pose.Rotation * collisionRules.BoundsCenter;
            QueryTriggerInteraction triggerInteraction = collisionRules.IgnoreTriggerColliders
                ? QueryTriggerInteraction.Ignore
                : QueryTriggerInteraction.Collide;
            Collider[] colliders = Physics.OverlapBox(center, extents, pose.Rotation, collisionRules.BlockingLayers, triggerInteraction);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || collider == pose.SurfaceCollider)
                {
                    continue;
                }

                if (snapPoint != null && snapPoint.OwnerPiece != null && collider.transform.IsChildOf(snapPoint.OwnerPiece.transform))
                {
                    continue;
                }

                return PlacementResult.Failed(PlacementFailureReason.PlacementBlocked, $"Placement overlaps blocking collider '{collider.name}'.", itemDefId, profile.ConsumedItemQuantity);
            }

            return PlacementResult.Succeeded(itemDefId, StackQuantity.Zero, "Placement collision clearance is valid.");
        }

        private void ApplyPose(Transform target, PlaceableProfile profile, PlacementPose pose, FrameworkSnapPoint snapPoint)
        {
            if (target == null)
            {
                return;
            }

            Vector3 position = snapPoint != null && profile.PlacementMode == PlacementMode.FrameworkSnap
                ? snapPoint.transform.position
                : pose.Position;
            Quaternion rotation = ResolveRotation(profile, pose, snapPoint);
            target.SetPositionAndRotation(position, rotation);
        }

        private Quaternion ResolveRotation(PlaceableProfile profile, PlacementPose pose, FrameworkSnapPoint snapPoint)
        {
            PlacementRotationRules rotationRules = profile.RotationRules;
            switch (rotationRules.RotationMode)
            {
                case PlacementRotationMode.Fixed:
                    return Quaternion.identity;
                case PlacementRotationMode.AlignToSurface:
                    return Quaternion.FromToRotation(Vector3.up, pose.SurfaceNormal.normalized == Vector3.zero ? Vector3.up : pose.SurfaceNormal.normalized) * pose.Rotation;
                case PlacementRotationMode.MatchSnapPoint:
                    return snapPoint != null ? snapPoint.transform.rotation : pose.Rotation;
                case PlacementRotationMode.SnapYaw:
                    if (rotationRules.YawSnapDegrees > 0f)
                    {
                        Vector3 euler = pose.Rotation.eulerAngles;
                        euler.y = Mathf.Round(euler.y / rotationRules.YawSnapDegrees) * rotationRules.YawSnapDegrees;
                        return Quaternion.Euler(euler);
                    }

                    return pose.Rotation;
                case PlacementRotationMode.FreeYaw:
                default:
                    return pose.Rotation;
            }
        }

        private void ConfigurePreviewObject(GameObject previewObject)
        {
            Collider[] colliders = previewObject.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            Rigidbody[] rigidbodies = previewObject.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                rigidbodies[i].isKinematic = true;
                rigidbodies[i].useGravity = false;
                rigidbodies[i].detectCollisions = false;
            }
        }

        private static void BindPlacedObject(GameObject placedObject, PlayerInventoryState inventoryState, ItemDefId itemDefId, PlaceableProfile profile, StackQuantity quantity)
        {
            PlacedObjectIdentity identity = placedObject.GetComponent<PlacedObjectIdentity>();
            if (identity == null)
            {
                identity = placedObject.AddComponent<PlacedObjectIdentity>();
            }

            identity.Bind(inventoryState != null ? inventoryState.OwnerId : string.Empty, itemDefId, profile.PlacementMode, quantity, "place_" + Guid.NewGuid().ToString("N"));
        }

        private static void ConfigureFrameworkPiece(GameObject placedObject, PlaceableProfile profile)
        {
            if (profile.PlacementMode != PlacementMode.FrameworkSnap)
            {
                return;
            }

            FrameworkStructurePiece piece = placedObject.GetComponent<FrameworkStructurePiece>();
            if (piece == null)
            {
                piece = placedObject.AddComponent<FrameworkStructurePiece>();
            }

            piece.Bind(placedObject.name, profile.FrameworkPieceKind);
        }

        private static bool IsValidPose(PlacementPose pose)
        {
            if (pose == null)
            {
                return false;
            }

            Vector3 position = pose.Position;
            Quaternion rotation = pose.Rotation;
            return IsFinite(position.x) &&
                   IsFinite(position.y) &&
                   IsFinite(position.z) &&
                   IsFinite(rotation.x) &&
                   IsFinite(rotation.y) &&
                   IsFinite(rotation.z) &&
                   IsFinite(rotation.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsLayerInMask(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        private PlayerInventoryState ResolveInventoryState()
        {
            IPlayerInventoryStateProvider provider = ResolveInventoryProvider();
            return provider != null ? provider.InventoryState : null;
        }

        private IPlayerInventoryStateProvider ResolveInventoryProvider()
        {
            if (runtimeInventoryStateProvider != null)
            {
                inventoryStateProvider = runtimeInventoryStateProvider;
                return inventoryStateProvider;
            }

            if (inventoryStateProvider != null)
            {
                return inventoryStateProvider;
            }

            inventoryStateProvider = inventoryStateProviderBehaviour as IPlayerInventoryStateProvider;
            return inventoryStateProvider;
        }

        private void OnDestroy()
        {
            DestroyPreview();
        }

        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log(message, this);
            }
        }
    }
}
