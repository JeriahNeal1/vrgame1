using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    public sealed class ItemManifestationService : MonoBehaviour, IHeldItemService
    {
        [SerializeField]
        private MonoBehaviour handItemSpawnerBehaviour = null;

        [SerializeField]
        private Transform defaultSpawnOrigin = null;

        [SerializeField]
        private Transform spawnedItemParent = null;

        [SerializeField]
        private bool destroyWorldObjectOnReturn = true;

        [SerializeField]
        private bool verboseLogging = false;

        [SerializeField]
        private ManifestationReservationStore reservationStore = new ManifestationReservationStore();

        private IVRHandItemSpawner cachedSpawner;

        public ManifestationReservationStore ReservationStore
        {
            get
            {
                reservationStore ??= new ManifestationReservationStore();
                return reservationStore;
            }
        }

        public ItemManifestationResult ManifestStack(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemDefId itemDefId,
            string requestedHandId)
        {
            InventoryOperationResult validation = ValidateManifestableDefinition(
                InventoryOperationType.ManifestStack,
                inventoryState,
                itemDefinitionDatabase,
                itemDefId,
                true,
                out ItemDefinition itemDefinition);

            if (!validation.Success)
            {
                return new ItemManifestationResult(validation);
            }

            if (!PlayerInventoryOperations.HasStack(inventoryState, itemDefId, StackQuantity.One))
            {
                return new ItemManifestationResult(InventoryOperationResult.Failed(
                    InventoryOperationType.ManifestStack,
                    InventoryFailureReason.InsufficientStack,
                    $"Inventory does not contain one '{itemDefId}' to manifest.",
                    inventoryState.Revision));
            }

            InventoryOperationResult reserveResult = PlayerInventoryOperations.RemoveStack(inventoryState, itemDefinitionDatabase, itemDefId, StackQuantity.One);
            if (!reserveResult.Success)
            {
                return new ItemManifestationResult(reserveResult);
            }

            ManifestationReservation reservation = new ManifestationReservation(
                ManifestationReservation.CreateRequestId(),
                inventoryState.OwnerId,
                ManifestationSourceKind.Stack,
                itemDefId,
                default,
                StackQuantity.One,
                requestedHandId,
                inventoryState.Revision);

            ReservationStore.AddReservation(reservation);

            WorldItemBinding binding = CreateBinding(reservation, itemDefinition, null, ItemLifecycleState.ManifestingFromPortal);
            if (!TrySpawn(binding, itemDefinition, null, requestedHandId, out WorldItemView worldItemView, out string spawnMessage))
            {
                PlayerInventoryOperations.AddStack(inventoryState, itemDefinitionDatabase, itemDefId, StackQuantity.One);
                reservation.MarkCancelled();
                return new ItemManifestationResult(InventoryOperationResult.Failed(
                    InventoryOperationType.ManifestStack,
                    InventoryFailureReason.SpawnFailed,
                    spawnMessage,
                    inventoryState.Revision), reservation);
            }

            CompleteSpawn(reservation, worldItemView);
            return new ItemManifestationResult(InventoryOperationResult
                .Succeeded(InventoryOperationType.ManifestStack, inventoryState.Revision, $"Manifested one '{itemDefId}' as request '{reservation.RequestId}'.")
                .WithChangedItemDefinition(itemDefId), reservation, worldItemView);
        }

        public ItemManifestationResult ManifestItemInstance(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemInstanceId itemInstanceId,
            string requestedHandId)
        {
            InventoryOperationResult validation = ValidateInventoryAndInstance(
                InventoryOperationType.ManifestItemInstance,
                inventoryState,
                itemDefinitionDatabase,
                itemInstanceId,
                out ItemInstanceState itemInstance,
                out ItemDefinition itemDefinition);

            if (!validation.Success)
            {
                return new ItemManifestationResult(validation);
            }

            if (!itemDefinition.IsManifestable)
            {
                return new ItemManifestationResult(InventoryOperationResult.Failed(
                    InventoryOperationType.ManifestItemInstance,
                    InventoryFailureReason.ItemNotManifestable,
                    $"Item definition '{itemDefinition.ItemDefId}' is not manifestable.",
                    inventoryState.Revision));
            }

            if (itemDefinition.WorldPrefab == null)
            {
                return new ItemManifestationResult(InventoryOperationResult.Failed(
                    InventoryOperationType.ManifestItemInstance,
                    InventoryFailureReason.MissingWorldPrefab,
                    $"Item definition '{itemDefinition.ItemDefId}' has no world prefab.",
                    inventoryState.Revision));
            }

            if (itemDefinition.ResolvedStackPolicy.IsStackable)
            {
                return new ItemManifestationResult(InventoryOperationResult.Failed(
                    InventoryOperationType.ManifestItemInstance,
                    InventoryFailureReason.ItemMustBeUnstackable,
                    $"Item definition '{itemDefinition.ItemDefId}' is stackable and should be manifested from the stack ledger.",
                    inventoryState.Revision));
            }

            if (itemInstance.LifecycleState != ItemLifecycleState.InInventory)
            {
                return new ItemManifestationResult(InventoryOperationResult.Failed(
                    InventoryOperationType.ManifestItemInstance,
                    InventoryFailureReason.ItemNotInInventory,
                    $"Item instance '{itemInstanceId}' must be InInventory before portal manifestation. Current state: {itemInstance.LifecycleState}.",
                    inventoryState.Revision));
            }

            if (ReservationStore.HasActiveReservationForInstance(itemInstanceId))
            {
                return new ItemManifestationResult(InventoryOperationResult.Failed(
                    InventoryOperationType.ManifestItemInstance,
                    InventoryFailureReason.ManifestationAlreadyActive,
                    $"Item instance '{itemInstanceId}' already has an active manifestation reservation.",
                    inventoryState.Revision));
            }

            InventoryOperationResult reserveResult = PlayerInventoryOperations.MoveInstanceToState(inventoryState, itemInstanceId, ItemLifecycleState.ManifestingFromPortal);
            if (!reserveResult.Success)
            {
                return new ItemManifestationResult(reserveResult);
            }

            ManifestationReservation reservation = new ManifestationReservation(
                ManifestationReservation.CreateRequestId(),
                inventoryState.OwnerId,
                ManifestationSourceKind.ItemInstance,
                itemInstance.ItemDefId,
                itemInstanceId,
                StackQuantity.One,
                requestedHandId,
                inventoryState.Revision);

            ReservationStore.AddReservation(reservation);

            WorldItemBinding binding = CreateBinding(reservation, itemDefinition, itemInstance, ItemLifecycleState.ManifestingFromPortal);
            if (!TrySpawn(binding, itemDefinition, itemInstance, requestedHandId, out WorldItemView worldItemView, out string spawnMessage))
            {
                PlayerInventoryOperations.MoveInstanceToState(inventoryState, itemInstanceId, ItemLifecycleState.InInventory);
                reservation.MarkCancelled();
                return new ItemManifestationResult(InventoryOperationResult.Failed(
                    InventoryOperationType.ManifestItemInstance,
                    InventoryFailureReason.SpawnFailed,
                    spawnMessage,
                    inventoryState.Revision), reservation);
            }

            InventoryOperationResult heldResult = PlayerInventoryOperations.MoveInstanceToState(inventoryState, itemInstanceId, ItemLifecycleState.HeldInWorld);
            if (!heldResult.Success)
            {
                DestroyWorldItem(worldItemView);
                reservation.MarkCancelled();
                return new ItemManifestationResult(heldResult, reservation);
            }

            CompleteSpawn(reservation, worldItemView);
            return new ItemManifestationResult(InventoryOperationResult
                .Succeeded(InventoryOperationType.ManifestItemInstance, inventoryState.Revision, $"Manifested item instance '{itemInstanceId}' as request '{reservation.RequestId}'.")
                .WithChangedItemDefinition(itemInstance.ItemDefId)
                .WithChangedItemInstance(itemInstanceId), reservation, worldItemView);
        }

        public InventoryOperationResult ReturnToInventory(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            string manifestationRequestId)
        {
            InventoryOperationResult validation = ValidateActiveReservation(
                InventoryOperationType.ReturnManifestedItem,
                inventoryState,
                manifestationRequestId,
                out ManifestationReservation reservation);

            if (!validation.Success)
            {
                return validation;
            }

            InventoryOperationResult inventoryResult;
            if (reservation.SourceKind == ManifestationSourceKind.Stack)
            {
                inventoryResult = PlayerInventoryOperations.AddStack(inventoryState, itemDefinitionDatabase, reservation.ItemDefId, reservation.ReservedQuantity);
            }
            else
            {
                inventoryResult = PlayerInventoryOperations.MoveInstanceToState(inventoryState, reservation.ItemInstanceId, ItemLifecycleState.InInventory);
            }

            if (!inventoryResult.Success)
            {
                return inventoryResult;
            }

            if (ReservationStore.TryGetWorldItem(reservation.RequestId, out WorldItemView worldItemView) && worldItemView != null)
            {
                worldItemView.NotifyReturnedToInventory();
                if (destroyWorldObjectOnReturn)
                {
                    DestroyWorldItem(worldItemView);
                }
            }

            ReservationStore.ClearWorldItem(reservation.RequestId);
            reservation.MarkReturned();

            Log($"Returned manifestation '{reservation.RequestId}' to inventory.");
            return InventoryOperationResult
                .Succeeded(InventoryOperationType.ReturnManifestedItem, inventoryState.Revision, $"Returned manifestation '{reservation.RequestId}' to inventory.")
                .WithChangedItemDefinition(reservation.ItemDefId)
                .WithChangedItemInstance(reservation.ItemInstanceId);
        }

        public InventoryOperationResult DropManifestedItem(PlayerInventoryState inventoryState, string manifestationRequestId)
        {
            InventoryOperationResult validation = ValidateActiveReservation(
                InventoryOperationType.DropManifestedItem,
                inventoryState,
                manifestationRequestId,
                out ManifestationReservation reservation);

            if (!validation.Success)
            {
                return validation;
            }

            InventoryOperationResult inventoryResult = InventoryOperationResult.Succeeded(InventoryOperationType.DropManifestedItem, inventoryState.Revision);
            if (reservation.SourceKind == ManifestationSourceKind.ItemInstance)
            {
                inventoryResult = PlayerInventoryOperations.MoveInstanceToState(inventoryState, reservation.ItemInstanceId, ItemLifecycleState.DroppedInWorld);
                if (!inventoryResult.Success)
                {
                    return inventoryResult;
                }
            }

            if (ReservationStore.TryGetWorldItem(reservation.RequestId, out WorldItemView worldItemView) && worldItemView != null)
            {
                worldItemView.NotifyDropped();
            }

            reservation.MarkDropped();

            Log($"Dropped manifestation '{reservation.RequestId}'.");
            return InventoryOperationResult
                .Succeeded(InventoryOperationType.DropManifestedItem, inventoryState.Revision, $"Dropped manifestation '{reservation.RequestId}'.")
                .WithChangedItemDefinition(reservation.ItemDefId)
                .WithChangedItemInstance(reservation.ItemInstanceId);
        }

        public InventoryOperationResult CommitManifestedItemAsEquipped(PlayerInventoryState inventoryState, string manifestationRequestId)
        {
            InventoryOperationResult validation = ValidateActiveReservation(
                InventoryOperationType.Equip,
                inventoryState,
                manifestationRequestId,
                out ManifestationReservation reservation);

            if (!validation.Success)
            {
                return validation;
            }

            if (reservation.SourceKind != ManifestationSourceKind.ItemInstance || reservation.ItemInstanceId.IsEmpty)
            {
                return InventoryOperationResult.Failed(
                    InventoryOperationType.Equip,
                    InventoryFailureReason.InvalidManifestationRequest,
                    $"Manifestation '{manifestationRequestId}' is not an item instance and cannot be committed as equipment.",
                    inventoryState.Revision);
            }

            if (ReservationStore.TryGetWorldItem(reservation.RequestId, out WorldItemView worldItemView) && worldItemView != null)
            {
                DestroyWorldItem(worldItemView);
            }

            ReservationStore.ClearWorldItem(reservation.RequestId);
            reservation.MarkCommittedToEquipment();

            Log($"Committed manifestation '{reservation.RequestId}' to equipment.");
            return InventoryOperationResult
                .Succeeded(InventoryOperationType.Equip, inventoryState.Revision, $"Committed manifestation '{reservation.RequestId}' to equipment.")
                .WithChangedItemDefinition(reservation.ItemDefId)
                .WithChangedItemInstance(reservation.ItemInstanceId);
        }

        public InventoryOperationResult CancelManifestation(
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            string manifestationRequestId)
        {
            return ReturnToInventory(inventoryState, itemDefinitionDatabase, manifestationRequestId);
        }

        public bool TryGetWorldItem(string manifestationRequestId, out WorldItemView worldItemView)
        {
            return ReservationStore.TryGetWorldItem(manifestationRequestId, out worldItemView);
        }

        private bool TrySpawn(
            WorldItemBinding binding,
            ItemDefinition itemDefinition,
            ItemInstanceState itemInstance,
            string requestedHandId,
            out WorldItemView worldItemView,
            out string message)
        {
            IVRHandItemSpawner spawner = ResolveSpawner();
            HandItemSpawnRequest request = new HandItemSpawnRequest
            {
                Binding = binding,
                ItemDefinition = itemDefinition,
                ItemInstance = itemInstance,
                Reservation = binding != null ? ReservationStore.TryGetReservation(binding.ManifestationRequestId, out ManifestationReservation reservation) ? reservation : null : null,
                RequestedHandId = requestedHandId,
                SpawnOrigin = defaultSpawnOrigin,
                OptionalParent = spawnedItemParent
            };

            if (spawner != null)
            {
                return spawner.TrySpawnIntoHand(request, out worldItemView, out message);
            }

            return FallbackSpawn(request, out worldItemView, out message);
        }

        private void CompleteSpawn(ManifestationReservation reservation, WorldItemView worldItemView)
        {
            reservation.MarkSpawned(worldItemView.Identity.WorldItemId);
            reservation.MarkHeld();
            ReservationStore.BindWorldItem(reservation.RequestId, worldItemView);
            worldItemView.Destroyed += OnWorldItemDestroyed;
            worldItemView.NotifyManifested();
            Log($"Manifestation '{reservation.RequestId}' spawned world item '{worldItemView.Identity.WorldItemId}'.");
        }

        private WorldItemBinding CreateBinding(
            ManifestationReservation reservation,
            ItemDefinition itemDefinition,
            ItemInstanceState itemInstance,
            ItemLifecycleState lifecycleState)
        {
            return new WorldItemBinding
            {
                WorldItemId = WorldItemIdentity.CreateWorldItemId(),
                ManifestationRequestId = reservation.RequestId,
                RuntimeBindingId = "binding_" + System.Guid.NewGuid().ToString("N"),
                OwnerId = reservation.PlayerId,
                ItemDefId = reservation.ItemDefId,
                ItemInstanceId = reservation.ItemInstanceId,
                Quantity = reservation.ReservedQuantity,
                LifecycleState = lifecycleState,
                ItemDefinition = itemDefinition,
                ItemInstance = itemInstance
            };
        }

        private InventoryOperationResult ValidateManifestableDefinition(
            InventoryOperationType operationType,
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemDefId itemDefId,
            bool requireStackable,
            out ItemDefinition itemDefinition)
        {
            itemDefinition = null;
            if (inventoryState == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidInventoryState, "Player inventory state is null.");
            }

            if (itemDefinitionDatabase == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidItemDefinitionDatabase, "Item definition database is null.", inventoryState.Revision);
            }

            if (itemDefId.IsEmpty)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidItemDefinitionId, "Item definition ID is empty.", inventoryState.Revision);
            }

            if (!itemDefinitionDatabase.TryGet(itemDefId, out itemDefinition) || itemDefinition == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.UnknownItemDefinition, $"Unknown item definition '{itemDefId}'.", inventoryState.Revision);
            }

            if (!itemDefinition.IsManifestable)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.ItemNotManifestable, $"Item definition '{itemDefId}' is not manifestable.", inventoryState.Revision);
            }

            if (itemDefinition.WorldPrefab == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.MissingWorldPrefab, $"Item definition '{itemDefId}' has no world prefab.", inventoryState.Revision);
            }

            if (requireStackable && !itemDefinition.ResolvedStackPolicy.IsStackable)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.ItemMustBeStackable, $"Item definition '{itemDefId}' is unstackable and should be manifested as an item instance.", inventoryState.Revision);
            }

            return InventoryOperationResult.Succeeded(operationType, inventoryState.Revision);
        }

        private InventoryOperationResult ValidateInventoryAndInstance(
            InventoryOperationType operationType,
            PlayerInventoryState inventoryState,
            ItemDefinitionDatabase itemDefinitionDatabase,
            ItemInstanceId itemInstanceId,
            out ItemInstanceState itemInstance,
            out ItemDefinition itemDefinition)
        {
            itemInstance = null;
            itemDefinition = null;

            if (inventoryState == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidInventoryState, "Player inventory state is null.");
            }

            if (itemDefinitionDatabase == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidItemDefinitionDatabase, "Item definition database is null.", inventoryState.Revision);
            }

            if (itemInstanceId.IsEmpty)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidItemInstanceId, "Item instance ID is empty.", inventoryState.Revision);
            }

            if (!inventoryState.TryGetInstance(itemInstanceId, out itemInstance) || itemInstance == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.UnknownItemInstance, $"Unknown item instance '{itemInstanceId}'.", inventoryState.Revision);
            }

            if (!itemDefinitionDatabase.TryGet(itemInstance.ItemDefId, out itemDefinition) || itemDefinition == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.UnknownItemDefinition, $"Unknown item definition '{itemInstance.ItemDefId}'.", inventoryState.Revision);
            }

            return InventoryOperationResult.Succeeded(operationType, inventoryState.Revision);
        }

        private InventoryOperationResult ValidateActiveReservation(
            InventoryOperationType operationType,
            PlayerInventoryState inventoryState,
            string manifestationRequestId,
            out ManifestationReservation reservation)
        {
            reservation = null;
            if (inventoryState == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.InvalidInventoryState, "Player inventory state is null.");
            }

            if (!ReservationStore.TryGetReservation(manifestationRequestId, out reservation) || reservation == null)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.UnknownManifestation, $"Unknown manifestation request '{manifestationRequestId}'.", inventoryState.Revision);
            }

            if (!reservation.IsActive)
            {
                return InventoryOperationResult.Failed(operationType, InventoryFailureReason.ManifestationAlreadyResolved, $"Manifestation request '{manifestationRequestId}' is already {reservation.State}.", inventoryState.Revision);
            }

            return InventoryOperationResult.Succeeded(operationType, inventoryState.Revision);
        }

        private IVRHandItemSpawner ResolveSpawner()
        {
            if (cachedSpawner != null)
            {
                return cachedSpawner;
            }

            if (handItemSpawnerBehaviour != null)
            {
                cachedSpawner = handItemSpawnerBehaviour as IVRHandItemSpawner;
                if (cachedSpawner == null)
                {
                    Debug.LogWarning($"Assigned hand item spawner '{handItemSpawnerBehaviour.name}' does not implement {nameof(IVRHandItemSpawner)}.", handItemSpawnerBehaviour);
                }
            }

            if (cachedSpawner == null)
            {
                cachedSpawner = GetComponent<IVRHandItemSpawner>();
            }

            return cachedSpawner;
        }

        private bool FallbackSpawn(HandItemSpawnRequest request, out WorldItemView worldItemView, out string message)
        {
            worldItemView = null;
            if (request == null || request.WorldPrefab == null)
            {
                message = "Fallback spawn request is missing a prefab.";
                return false;
            }

            Transform origin = request.SpawnOrigin != null ? request.SpawnOrigin : transform;
            GameObject spawned = Instantiate(request.WorldPrefab, origin.position, origin.rotation, request.OptionalParent);
            worldItemView = spawned.GetComponent<WorldItemView>();
            if (worldItemView == null)
            {
                worldItemView = spawned.AddComponent<WorldItemView>();
            }

            if (spawned.GetComponent<WorldItemIdentity>() == null)
            {
                spawned.AddComponent<WorldItemIdentity>();
            }

            worldItemView.Bind(request.Binding);
            message = "Spawned item with manifestation service fallback.";
            return true;
        }

        private void DestroyWorldItem(WorldItemView worldItemView)
        {
            if (worldItemView == null)
            {
                return;
            }

            worldItemView.Destroyed -= OnWorldItemDestroyed;
            if (Application.isPlaying)
            {
                Destroy(worldItemView.gameObject);
            }
            else
            {
                DestroyImmediate(worldItemView.gameObject);
            }
        }

        private void OnWorldItemDestroyed(WorldItemView worldItemView)
        {
            if (worldItemView == null || worldItemView.Identity == null)
            {
                return;
            }

            string requestId = worldItemView.Identity.ManifestationRequestId;
            if (ReservationStore.TryGetReservation(requestId, out ManifestationReservation reservation) && reservation != null && reservation.IsActive)
            {
                reservation.MarkDestroyed();
            }

            ReservationStore.ClearWorldItem(requestId);
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
