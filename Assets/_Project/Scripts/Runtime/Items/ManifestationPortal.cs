using UnityEngine;
using UnityEngine.Events;
using VRGame.Items;

namespace VRGame.Runtime
{
    public sealed class ManifestationPortal : MonoBehaviour
    {
        [Header("Services")]
        [SerializeField]
        private ItemManifestationService manifestationService;

        [SerializeField]
        private ItemDefinitionDatabase itemDefinitionDatabase = null;

        [Tooltip("Optional MonoBehaviour that implements IPlayerInventoryStateProvider.")]
        [SerializeField]
        private MonoBehaviour inventoryStateProviderBehaviour = null;

        [Header("Selected Item")]
        [SerializeField]
        private bool selectedItemIsInstance = false;

        [SerializeField]
        private ItemDefId selectedItemDefId = default;

        [SerializeField]
        private ItemInstanceId selectedItemInstanceId = default;

        [SerializeField]
        private string defaultHandId = "right";

        [Header("Debug")]
        [SerializeField]
        private bool logResults = true;

        [SerializeField]
        private UnityEvent onManifested = new UnityEvent();

        [SerializeField]
        private UnityEvent onManifestFailed = new UnityEvent();

        private IPlayerInventoryStateProvider inventoryStateProvider;
        private IPlayerInventoryStateProvider runtimeInventoryStateProvider;
        private string lastManifestationRequestId = string.Empty;

        public string LastManifestationRequestId
        {
            get { return StableIdUtility.Normalize(lastManifestationRequestId); }
        }

        public void BindRuntime(
            ItemManifestationService newManifestationService,
            ItemDefinitionDatabase newItemDefinitionDatabase,
            IPlayerInventoryStateProvider newInventoryStateProvider)
        {
            manifestationService = newManifestationService;
            itemDefinitionDatabase = newItemDefinitionDatabase;
            runtimeInventoryStateProvider = newInventoryStateProvider;
            inventoryStateProvider = newInventoryStateProvider;
        }

        public void SelectStack(ItemDefId itemDefId)
        {
            selectedItemIsInstance = false;
            selectedItemDefId = itemDefId;
            selectedItemInstanceId = default;
        }

        public void SelectItemInstance(ItemInstanceId itemInstanceId)
        {
            selectedItemIsInstance = true;
            selectedItemInstanceId = itemInstanceId;
            selectedItemDefId = default;
        }

        public void RequestManifestSelected()
        {
            RequestManifestSelected(defaultHandId);
        }

        public void RequestManifestSelected(string requestedHandId)
        {
            RequestManifestSelectedItem(requestedHandId);
        }

        public ItemManifestationResult RequestManifestSelectedItem()
        {
            return RequestManifestSelectedItem(defaultHandId);
        }

        public ItemManifestationResult RequestManifestSelectedItem(string requestedHandId)
        {
            if (!TryResolveDependencies(out PlayerInventoryState inventoryState))
            {
                onManifestFailed.Invoke();
                return new ItemManifestationResult(InventoryOperationResult.Failed(
                    selectedItemIsInstance ? InventoryOperationType.ManifestItemInstance : InventoryOperationType.ManifestStack,
                    InventoryFailureReason.InvalidInventoryState,
                    "Manifestation portal dependencies are not configured."));
            }

            ItemManifestationResult result = selectedItemIsInstance
                ? manifestationService.ManifestItemInstance(inventoryState, itemDefinitionDatabase, selectedItemInstanceId, requestedHandId)
                : manifestationService.ManifestStack(inventoryState, itemDefinitionDatabase, selectedItemDefId, requestedHandId);

            if (result.Success)
            {
                lastManifestationRequestId = result.Reservation != null ? result.Reservation.RequestId : string.Empty;
                Log($"Manifestation succeeded: {result.Message}");
                onManifested.Invoke();
            }
            else
            {
                Log($"Manifestation failed: {FormatResult(result.InventoryResult)}");
                onManifestFailed.Invoke();
            }

            return result;
        }

        public void ReturnLastManifestedItem()
        {
            if (!TryResolveDependencies(out PlayerInventoryState inventoryState) || string.IsNullOrEmpty(LastManifestationRequestId))
            {
                return;
            }

            InventoryOperationResult result = manifestationService.ReturnToInventory(inventoryState, itemDefinitionDatabase, LastManifestationRequestId);
            Log(FormatResult(result));
            if (result.Success)
            {
                lastManifestationRequestId = string.Empty;
            }
        }

        public void DropLastManifestedItem()
        {
            if (!TryResolveDependencies(out PlayerInventoryState inventoryState) || string.IsNullOrEmpty(LastManifestationRequestId))
            {
                return;
            }

            InventoryOperationResult result = manifestationService.DropManifestedItem(inventoryState, LastManifestationRequestId);
            Log(FormatResult(result));
            if (result.Success)
            {
                lastManifestationRequestId = string.Empty;
            }
        }

        private void Awake()
        {
            ResolveInventoryProvider();
            if (manifestationService == null)
            {
                manifestationService = GetComponent<ItemManifestationService>();
            }
        }

        private void OnValidate()
        {
            if (manifestationService == null)
            {
                manifestationService = GetComponent<ItemManifestationService>();
            }
        }

        private bool TryResolveDependencies(out PlayerInventoryState inventoryState)
        {
            inventoryState = null;

            if (manifestationService == null)
            {
                Debug.LogWarning($"{nameof(ManifestationPortal)} has no manifestation service.", this);
                return false;
            }

            if (itemDefinitionDatabase == null)
            {
                Debug.LogWarning($"{nameof(ManifestationPortal)} has no item definition database.", this);
                return false;
            }

            ResolveInventoryProvider();
            inventoryState = inventoryStateProvider != null ? inventoryStateProvider.InventoryState : null;
            if (inventoryState == null)
            {
                Debug.LogWarning($"{nameof(ManifestationPortal)} has no inventory state provider.", this);
                return false;
            }

            return true;
        }

        private void ResolveInventoryProvider()
        {
            if (runtimeInventoryStateProvider != null)
            {
                inventoryStateProvider = runtimeInventoryStateProvider;
                return;
            }

            if (inventoryStateProvider != null)
            {
                return;
            }

            inventoryStateProvider = inventoryStateProviderBehaviour as IPlayerInventoryStateProvider;
            if (inventoryStateProvider == null && inventoryStateProviderBehaviour != null)
            {
                Debug.LogWarning($"Assigned inventory provider '{inventoryStateProviderBehaviour.name}' does not implement {nameof(IPlayerInventoryStateProvider)}.", inventoryStateProviderBehaviour);
            }
        }

        private void Log(string message)
        {
            if (logResults)
            {
                Debug.Log(message, this);
            }
        }

        private static string FormatResult(InventoryOperationResult result)
        {
            if (result == null)
            {
                return "null result";
            }

            return result.Success ? result.Message : $"{result.FailureReason}: {result.Message}";
        }
    }
}
