using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VRGame.Items;

namespace VRGame.Runtime
{
    public sealed class HarvestHitResult
    {
        public HarvestHitResult(
            bool success,
            bool harvested,
            HarvestToolValidationResult validationResult,
            string message,
            IReadOnlyList<InventoryOperationResult> dropResults)
        {
            Success = success;
            Harvested = harvested;
            ValidationResult = validationResult;
            Message = message ?? string.Empty;
            DropResults = dropResults ?? System.Array.Empty<InventoryOperationResult>();
        }

        public bool Success { get; }

        public bool Harvested { get; }

        public HarvestToolValidationResult ValidationResult { get; }

        public string Message { get; }

        public IReadOnlyList<InventoryOperationResult> DropResults { get; }

        public static HarvestHitResult Failed(HarvestToolValidationResult validationResult, string message)
        {
            return new HarvestHitResult(false, false, validationResult, message, System.Array.Empty<InventoryOperationResult>());
        }
    }

    [DisallowMultipleComponent]
    public sealed class Harvestable : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField]
        private HarvestableProfileDefinition profileDefinition = null;

        [SerializeField]
        private HarvestableProfile inlineProfile = new HarvestableProfile();

        [Header("Services")]
        [SerializeField]
        private ItemDefinitionDatabase itemDefinitionDatabase = null;

        [SerializeField]
        private ItemAffixDefinitionDatabase affixDefinitionDatabase = null;

        [Tooltip("Optional MonoBehaviour that implements IPlayerInventoryStateProvider.")]
        [SerializeField]
        private MonoBehaviour inventoryStateProviderBehaviour = null;

        [Header("Runtime")]
        [SerializeField]
        private bool requireHeldWorldItem = true;

        [SerializeField]
        private bool deactivateWhenHarvested = true;

        [SerializeField]
        private bool logFailures = true;

        [SerializeField]
        private UnityEvent onHarvestProgress = new UnityEvent();

        [SerializeField]
        private UnityEvent onHarvested = new UnityEvent();

        [SerializeField]
        private UnityEvent onHarvestFailed = new UnityEvent();

        private readonly List<InventoryOperationResult> reusableDropResults = new List<InventoryOperationResult>();
        private IPlayerInventoryStateProvider inventoryStateProvider;
        private IPlayerInventoryStateProvider runtimeInventoryStateProvider;
        private HarvestableProfile runtimeProfile;
        private float currentProgress;
        private int successfulHitCount;
        private bool harvested;

        public float CurrentProgress
        {
            get { return Mathf.Max(0f, currentProgress); }
        }

        public int SuccessfulHitCount
        {
            get { return successfulHitCount; }
        }

        public bool Harvested
        {
            get { return harvested; }
        }

        public HarvestableProfile Profile
        {
            get
            {
                if (runtimeProfile != null)
                {
                    return runtimeProfile;
                }

                if (profileDefinition != null)
                {
                    return profileDefinition.Profile;
                }

                inlineProfile ??= new HarvestableProfile();
                return inlineProfile;
            }
        }

        public void BindRuntime(
            ItemDefinitionDatabase newItemDefinitionDatabase,
            ItemAffixDefinitionDatabase newAffixDefinitionDatabase,
            IPlayerInventoryStateProvider newInventoryStateProvider)
        {
            itemDefinitionDatabase = newItemDefinitionDatabase;
            affixDefinitionDatabase = newAffixDefinitionDatabase;
            runtimeInventoryStateProvider = newInventoryStateProvider;
            inventoryStateProvider = newInventoryStateProvider;
        }

        public void BindRuntime(
            HarvestableProfile newProfile,
            ItemDefinitionDatabase newItemDefinitionDatabase,
            ItemAffixDefinitionDatabase newAffixDefinitionDatabase,
            IPlayerInventoryStateProvider newInventoryStateProvider)
        {
            runtimeProfile = newProfile;
            BindRuntime(newItemDefinitionDatabase, newAffixDefinitionDatabase, newInventoryStateProvider);
        }

        public HarvestHitResult TryHarvestHit(WorldItemView heldToolView, float hitStrength = 1f)
        {
            reusableDropResults.Clear();
            if (harvested)
            {
                return HarvestHitResult.Failed(
                    HarvestToolValidationResult.Failed(HarvestValidationFailureReason.InvalidHarvestableProfile, "Harvestable target has already been harvested."),
                    "Harvestable target has already been harvested.");
            }

            if (!TryResolveHeldTool(heldToolView, out ItemDefinition toolDefinition, out ItemInstanceState toolInstance, out HarvestToolValidationResult runtimeFailure))
            {
                return Fail(runtimeFailure);
            }

            PlayerInventoryState inventoryState = ResolveInventoryState();
            HarvestToolValidationResult validation = HarvestingToolValidationService.ValidateToolForHarvest(new HarvestToolValidationInput(
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                toolDefinition,
                toolInstance,
                Profile));

            if (!validation.Success)
            {
                return Fail(validation);
            }

            HarvestToolStats toolStats = validation.ToolStats;
            float progressRequired = Mathf.Max(0.0001f, Profile.BaseHarvestTime);
            float progressAmount = Mathf.Max(0.01f, hitStrength) * Mathf.Max(0.01f, toolStats != null ? toolStats.HarvestSpeed : 1f);
            currentProgress += progressAmount;
            successfulHitCount++;
            onHarvestProgress.Invoke();

            if (currentProgress < progressRequired)
            {
                return new HarvestHitResult(true, false, validation, $"Harvest progress {currentProgress:0.##}/{progressRequired:0.##}.", System.Array.Empty<InventoryOperationResult>());
            }

            HarvestHitResult harvestResult = CompleteHarvest(validation);
            if (!harvestResult.Success)
            {
                return harvestResult;
            }

            harvested = true;
            onHarvested.Invoke();
            if (deactivateWhenHarvested)
            {
                gameObject.SetActive(false);
            }

            return harvestResult;
        }

        public void ResetHarvestProgress()
        {
            currentProgress = 0f;
            successfulHitCount = 0;
            harvested = false;
            if (deactivateWhenHarvested && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        private HarvestHitResult CompleteHarvest(HarvestToolValidationResult validation)
        {
            PlayerInventoryState inventoryState = ResolveInventoryState();
            if (inventoryState == null)
            {
                return Fail(HarvestToolValidationResult.Failed(HarvestValidationFailureReason.InvalidHarvestableProfile, "Cannot add harvest drops without an inventory state.", validation.ToolStats));
            }

            if (itemDefinitionDatabase == null)
            {
                return Fail(HarvestToolValidationResult.Failed(HarvestValidationFailureReason.InvalidHarvestableProfile, "Cannot add harvest drops without an item definition database.", validation.ToolStats));
            }

            IReadOnlyList<HarvestDropEntry> drops = Profile.SimpleDrops;
            bool addedAnyDrop = false;
            for (int i = 0; i < drops.Count; i++)
            {
                HarvestDropEntry drop = drops[i];
                if (drop == null || !drop.IsValid)
                {
                    continue;
                }

                InventoryOperationResult dropResult = PlayerInventoryOperations.AddStack(inventoryState, itemDefinitionDatabase, drop.ItemDefId, drop.Quantity);
                reusableDropResults.Add(dropResult);
                if (!dropResult.Success)
                {
                    return new HarvestHitResult(false, false, validation, dropResult.Message, reusableDropResults.ToArray());
                }

                addedAnyDrop = true;
            }

            string message = addedAnyDrop
                ? "Harvest complete; simple drops were added to inventory."
                : "Harvest complete; no simple drops were configured.";
            return new HarvestHitResult(true, true, validation, message, reusableDropResults.ToArray());
        }

        private HarvestHitResult Fail(HarvestToolValidationResult validation)
        {
            if (logFailures && validation != null)
            {
                Debug.Log($"Harvest failed: {validation.FailureReason} - {validation.Message}", this);
            }

            onHarvestFailed.Invoke();
            return HarvestHitResult.Failed(validation, validation != null ? validation.Message : "Harvest failed.");
        }

        private bool TryResolveHeldTool(
            WorldItemView heldToolView,
            out ItemDefinition toolDefinition,
            out ItemInstanceState toolInstance,
            out HarvestToolValidationResult failure)
        {
            toolDefinition = null;
            toolInstance = null;
            failure = null;

            if (heldToolView == null || heldToolView.Identity == null)
            {
                failure = HarvestToolValidationResult.Failed(HarvestValidationFailureReason.MissingToolDefinition, "Held tool world item is missing.");
                return false;
            }

            if (requireHeldWorldItem && heldToolView.Identity.LifecycleState != ItemLifecycleState.HeldInWorld)
            {
                failure = HarvestToolValidationResult.Failed(HarvestValidationFailureReason.InvalidHeldToolState, $"Held tool must be HeldInWorld. Current state: {heldToolView.Identity.LifecycleState}.");
                return false;
            }

            toolDefinition = heldToolView.BoundDefinition;
            toolInstance = heldToolView.BoundInstance;
            WorldItemIdentity identity = heldToolView.Identity;

            if (toolDefinition == null && itemDefinitionDatabase != null)
            {
                itemDefinitionDatabase.TryGet(identity.ItemDefId, out toolDefinition);
            }

            PlayerInventoryState inventoryState = ResolveInventoryState();
            if (toolInstance == null &&
                inventoryState != null &&
                !identity.ItemInstanceId.IsEmpty &&
                inventoryState.TryGetInstance(identity.ItemInstanceId, out ItemInstanceState resolvedInstance))
            {
                toolInstance = resolvedInstance;
            }

            if (toolDefinition == null)
            {
                failure = HarvestToolValidationResult.Failed(HarvestValidationFailureReason.MissingToolDefinition, $"Unknown held tool definition '{identity.ItemDefId}'.");
                return false;
            }

            return true;
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

        private void OnValidate()
        {
            inlineProfile ??= new HarvestableProfile();
        }
    }
}
