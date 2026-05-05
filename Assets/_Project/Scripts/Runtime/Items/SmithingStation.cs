using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VRGame.Items;

namespace VRGame.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SmithingStation : MonoBehaviour
    {
        [Header("Definitions")]
        [SerializeField]
        private string stationId = "smithing.station";

        [SerializeField]
        private ItemDefinitionDatabase itemDefinitionDatabase = null;

        [SerializeField]
        private ItemAffixDefinitionDatabase affixDefinitionDatabase = null;

        [Tooltip("Optional MonoBehaviour implementing IPlayerInventoryStateProvider.")]
        [SerializeField]
        private MonoBehaviour inventoryStateProviderBehaviour = null;

        [Header("Smithing")]
        [Min(0)]
        [SerializeField]
        private int smithingSkillLevel = 0;

        [Min(0.01f)]
        [SerializeField]
        private float minimumStrikeVelocity = 1.25f;

        [Min(0.01f)]
        [SerializeField]
        private float maxQualityStrikeVelocity = 5f;

        [Min(1)]
        [SerializeField]
        private int strikesRequiredToForge = 3;

        [Min(0f)]
        [SerializeField]
        private float qualityBonusPerPerfectStrike = 0.2f;

        [Min(0f)]
        [SerializeField]
        private float maxAccumulatedQualityBonus = 0.75f;

        [Min(0f)]
        [SerializeField]
        private float strikeCooldownSeconds = 0.2f;

        [SerializeField]
        private bool autoForgeWhenStrikeCountReached = true;

        [SerializeField]
        private bool completeForgeWhenTargetRemoved = false;

        [Tooltip("References can point to individual modifier IDs or ModifierSetDefinition IDs in the affix database.")]
        [SerializeField]
        private List<DefinitionIdReference> allowedModifierPoolOverride = new List<DefinitionIdReference>();

        [Header("Gem Enchanting")]
        [SerializeField]
        private List<GemEnchantmentProfileDefinition> gemProfiles = new List<GemEnchantmentProfileDefinition>();

        [SerializeField]
        private bool autoProcessGemTriggerEntries = false;

        [Header("Placement")]
        [SerializeField]
        private Transform targetItemAnchor = null;

        [SerializeField]
        private Transform gemAnchor = null;

        [Header("Feedback")]
        [SerializeField]
        private ParticleSystem strikeSparks = null;

        [SerializeField]
        private AudioSource audioSource = null;

        [SerializeField]
        private AudioClip strikeClip = null;

        [SerializeField]
        private AudioClip successClip = null;

        [SerializeField]
        private UnityEvent onValidStrike = new UnityEvent();

        [SerializeField]
        private UnityEvent onForgeSucceeded = new UnityEvent();

        [SerializeField]
        private UnityEvent onGemInserted = new UnityEvent();

        [SerializeField]
        private bool verboseLogs = true;

        private IPlayerInventoryStateProvider inventoryStateProvider;
        private WorldItemView currentTargetItem;
        private int strikeCount;
        private float accumulatedQualityBonus;
        private float lastStrikeTime = -999f;
        private ItemInstanceId lastHammerInstanceId;
        private ItemDefId lastHammerDefId;
        private float lastNormalizedForce;
        private int lastStrikeSeed;

        public WorldItemView CurrentTargetItem
        {
            get { return currentTargetItem; }
        }

        public int StrikeCount
        {
            get { return strikeCount; }
        }

        public float AccumulatedQualityBonus
        {
            get { return accumulatedQualityBonus; }
        }

        public void BindRuntime(
            ItemDefinitionDatabase itemDatabase,
            ItemAffixDefinitionDatabase affixDatabase,
            IPlayerInventoryStateProvider provider)
        {
            itemDefinitionDatabase = itemDatabase != null ? itemDatabase : itemDefinitionDatabase;
            affixDefinitionDatabase = affixDatabase != null ? affixDatabase : affixDefinitionDatabase;
            inventoryStateProvider = provider ?? inventoryStateProvider;
        }

        public InventoryOperationResult NotifyItemReleasedOnStation(WorldItemView worldItemView)
        {
            if (worldItemView == null)
            {
                return Failed(InventoryOperationType.Validate, InventoryFailureReason.InvalidWorldItem, "Released world item is null.");
            }

            if (TryFindGemProfile(worldItemView.Identity.ItemDefId, out _))
            {
                return NotifyGemReleasedOnStation(worldItemView);
            }

            return TrySetTargetItem(worldItemView);
        }

        public InventoryOperationResult NotifyGemReleasedOnStation(WorldItemView gemWorldItem)
        {
            if (gemWorldItem == null)
            {
                return Failed(InventoryOperationType.ApplyGemEnchantment, InventoryFailureReason.InvalidWorldItem, "Gem world item is null.");
            }

            if (currentTargetItem == null || currentTargetItem.Identity.ItemInstanceId.IsEmpty)
            {
                return Failed(InventoryOperationType.ApplyGemEnchantment, InventoryFailureReason.InvalidItemInstanceId, "No valid target equipment item is on the smithing station.");
            }

            if (!TryFindGemProfile(gemWorldItem.Identity.ItemDefId, out GemEnchantmentProfileDefinition gemProfile))
            {
                return Failed(InventoryOperationType.ApplyGemEnchantment, InventoryFailureReason.UnknownItemDefinition, $"No gem enchantment profile is registered for '{gemWorldItem.Identity.ItemDefId}'.");
            }

            PlayerInventoryState inventoryState = ResolveInventoryState();
            InventoryOperationResult result = SmithingService.ApplyGemProfileFromWorldGem(
                inventoryState,
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                gemProfile,
                currentTargetItem.Identity.ItemInstanceId,
                CreateSeed(gemWorldItem.Identity.ItemDefId.Value),
                accumulatedQualityBonus,
                smithingSkillLevel,
                out EnchantmentId appliedEnchantmentId);

            if (!result.Success)
            {
                LogWarning($"Gem insert failed: {result.FailureReason}: {result.Message}");
                return result;
            }

            SnapWorldItem(gemWorldItem, gemAnchor);
            DestroyConsumedWorldItem(gemWorldItem);
            onGemInserted.Invoke();
            PlaySuccessFeedback();
            Log($"Gem insert applied enchantment '{appliedEnchantmentId}' to '{currentTargetItem.Identity.ItemInstanceId}'.");
            return result;
        }

        public InventoryOperationResult NotifyHammerStrike(WorldItemView hammerWorldItem, float hitVelocity, Vector3 hitPoint)
        {
            if (currentTargetItem == null || currentTargetItem.Identity.ItemInstanceId.IsEmpty)
            {
                return Failed(InventoryOperationType.RerollModifier, InventoryFailureReason.InvalidItemInstanceId, "No valid equipment item is on the smithing station.");
            }

            if (Time.time - lastStrikeTime < strikeCooldownSeconds)
            {
                return Failed(InventoryOperationType.RerollModifier, InventoryFailureReason.InvalidManifestationRequest, "Smithing strike ignored due to cooldown.");
            }

            if (hammerWorldItem == null)
            {
                return Failed(InventoryOperationType.RerollModifier, InventoryFailureReason.InvalidWorldItem, "Hammer world item is null.");
            }

            ItemDefinition hammerDefinition = ResolveDefinition(hammerWorldItem);
            if (!IsSmithingHammer(hammerDefinition))
            {
                return Failed(InventoryOperationType.RerollModifier, InventoryFailureReason.ItemDefinitionMismatch, $"World item '{hammerWorldItem.Identity.ItemDefId}' is not a smithing hammer.");
            }

            if (hitVelocity < minimumStrikeVelocity)
            {
                return Failed(InventoryOperationType.RerollModifier, InventoryFailureReason.InvalidManifestationRequest, $"Hammer strike velocity {hitVelocity:0.00} is below required {minimumStrikeVelocity:0.00}.");
            }

            lastStrikeTime = Time.time;
            lastHammerInstanceId = hammerWorldItem.Identity.ItemInstanceId;
            lastHammerDefId = hammerWorldItem.Identity.ItemDefId;
            lastNormalizedForce = Mathf.InverseLerp(minimumStrikeVelocity, Mathf.Max(minimumStrikeVelocity, maxQualityStrikeVelocity), hitVelocity);
            lastStrikeSeed = CreateSeed($"{lastHammerDefId.Value}:{strikeCount}:{hitVelocity:0.000}");
            strikeCount++;
            accumulatedQualityBonus = Mathf.Min(
                maxAccumulatedQualityBonus,
                accumulatedQualityBonus + lastNormalizedForce * Mathf.Max(0f, qualityBonusPerPerfectStrike));

            if (strikeSparks != null)
            {
                strikeSparks.transform.position = hitPoint;
                strikeSparks.Play();
            }

            PlayClip(strikeClip);
            onValidStrike.Invoke();
            Log($"Smithing strike {strikeCount}/{strikesRequiredToForge} accepted. Quality bonus: {accumulatedQualityBonus:0.00}.");

            if (autoForgeWhenStrikeCountReached && strikeCount >= strikesRequiredToForge)
            {
                return CompleteForge();
            }

            return InventoryOperationResult.Succeeded(InventoryOperationType.Validate, ResolveRevision(), "Smithing strike accepted.");
        }

        public InventoryOperationResult CompleteForge()
        {
            if (currentTargetItem == null || currentTargetItem.Identity.ItemInstanceId.IsEmpty)
            {
                return Failed(InventoryOperationType.RerollModifier, InventoryFailureReason.InvalidItemInstanceId, "No valid equipment item is on the smithing station.");
            }

            if (lastHammerDefId.IsEmpty)
            {
                return Failed(InventoryOperationType.RerollModifier, InventoryFailureReason.InvalidItemDefinitionId, "No valid smithing hammer strike has been recorded.");
            }

            SmithingStrikeRecord strikeRecord = new SmithingStrikeRecord(
                currentTargetItem.Identity.ItemInstanceId,
                lastHammerInstanceId,
                lastHammerDefId,
                stationId,
                lastNormalizedForce,
                1f,
                1f,
                strikeCount,
                lastStrikeSeed);

            ReforgeContext context = SmithingService.CreateManualSmithingContext(
                stationId,
                smithingSkillLevel,
                lastHammerDefId,
                Array.Empty<ItemDefId>(),
                lastStrikeSeed,
                accumulatedQualityBonus,
                allowedModifierPoolOverride);

            InventoryOperationResult result = SmithingService.ApplySmithingStrike(
                ResolveInventoryState(),
                itemDefinitionDatabase,
                affixDefinitionDatabase,
                strikeRecord,
                context,
                out ModifierId appliedModifierId);

            if (!result.Success)
            {
                LogWarning($"Forge failed: {result.FailureReason}: {result.Message}");
                return result;
            }

            ResetStrikeSequence();
            onForgeSucceeded.Invoke();
            PlaySuccessFeedback();
            Log($"Forge applied modifier '{appliedModifierId}' to '{currentTargetItem.Identity.ItemInstanceId}'.");
            return result;
        }

        public void ClearTargetItem(WorldItemView worldItemView)
        {
            if (worldItemView == null || currentTargetItem != worldItemView)
            {
                return;
            }

            if (completeForgeWhenTargetRemoved && strikeCount > 0)
            {
                CompleteForge();
            }

            currentTargetItem = null;
            ResetStrikeSequence();
        }

        private void Awake()
        {
            ResolveInventoryProvider();
        }

        private void OnTriggerEnter(Collider other)
        {
            WorldItemView worldItemView = other != null ? other.GetComponentInParent<WorldItemView>() : null;
            if (worldItemView == null)
            {
                return;
            }

            if (TryFindGemProfile(worldItemView.Identity.ItemDefId, out _) && autoProcessGemTriggerEntries)
            {
                NotifyGemReleasedOnStation(worldItemView);
                return;
            }

            if (currentTargetItem == null)
            {
                TrySetTargetItem(worldItemView);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            WorldItemView worldItemView = other != null ? other.GetComponentInParent<WorldItemView>() : null;
            if (worldItemView != null && currentTargetItem == worldItemView)
            {
                ClearTargetItem(worldItemView);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null || currentTargetItem == null)
            {
                return;
            }

            WorldItemView hammerWorldItem = collision.collider != null ? collision.collider.GetComponentInParent<WorldItemView>() : null;
            if (hammerWorldItem == null)
            {
                return;
            }

            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            NotifyHammerStrike(hammerWorldItem, collision.relativeVelocity.magnitude, point);
        }

        private InventoryOperationResult TrySetTargetItem(WorldItemView worldItemView)
        {
            if (worldItemView == null || worldItemView.Identity.ItemInstanceId.IsEmpty)
            {
                return Failed(InventoryOperationType.Validate, InventoryFailureReason.InvalidWorldItem, "Smithing target must be a manifested item instance.");
            }

            ItemDefinition definition = ResolveDefinition(worldItemView);
            if (definition == null)
            {
                return Failed(InventoryOperationType.Validate, InventoryFailureReason.UnknownItemDefinition, $"Unknown smithing target definition '{worldItemView.Identity.ItemDefId}'.");
            }

            if (!definition.IsEquipment || definition.ResolvedStackPolicy.IsStackable)
            {
                return Failed(InventoryOperationType.Validate, InventoryFailureReason.ItemMustBeEquipment, $"Smithing target '{definition.ItemDefId}' must be unstackable equipment.");
            }

            if (IsSmithingHammer(definition))
            {
                return Failed(InventoryOperationType.Validate, InventoryFailureReason.ItemDefinitionMismatch, "The smithing hammer is the strike tool, not the current forge target.");
            }

            if (ResolveInventoryState() == null || !ResolveInventoryState().TryGetInstance(worldItemView.Identity.ItemInstanceId, out _))
            {
                return Failed(InventoryOperationType.Validate, InventoryFailureReason.UnknownItemInstance, $"Smithing target instance '{worldItemView.Identity.ItemInstanceId}' is not in player inventory state.");
            }

            currentTargetItem = worldItemView;
            ResetStrikeSequence();
            SnapWorldItem(worldItemView, targetItemAnchor);
            Log($"Smithing target set to '{worldItemView.Identity.ItemInstanceId}'.");
            return InventoryOperationResult.Succeeded(InventoryOperationType.Validate, ResolveRevision(), "Smithing target accepted.");
        }

        private bool TryFindGemProfile(ItemDefId gemItemDefId, out GemEnchantmentProfileDefinition gemProfile)
        {
            IReadOnlyList<GemEnchantmentProfileDefinition> profiles = gemProfiles ?? (IReadOnlyList<GemEnchantmentProfileDefinition>)Array.Empty<GemEnchantmentProfileDefinition>();
            for (int i = 0; i < profiles.Count; i++)
            {
                GemEnchantmentProfileDefinition profile = profiles[i];
                if (profile != null && profile.MatchesGem(gemItemDefId))
                {
                    gemProfile = profile;
                    return true;
                }
            }

            gemProfile = null;
            return false;
        }

        private ItemDefinition ResolveDefinition(WorldItemView worldItemView)
        {
            if (worldItemView == null)
            {
                return null;
            }

            if (worldItemView.BoundDefinition != null)
            {
                return worldItemView.BoundDefinition;
            }

            return itemDefinitionDatabase != null && itemDefinitionDatabase.TryGet(worldItemView.Identity.ItemDefId, out ItemDefinition definition)
                ? definition
                : null;
        }

        private static bool IsSmithingHammer(ItemDefinition definition)
        {
            return definition != null &&
                   definition.HasFlag(ItemFlags.Tool) &&
                   definition.HasToolProfile &&
                   definition.ToolProfile != null &&
                   definition.ToolProfile.HarvestingType == HarvestingDomain.ConstructionArchitecture &&
                   definition.ToolProfile.ToolSubtype == HarvestingSubtype.Hammer;
        }

        private PlayerInventoryState ResolveInventoryState()
        {
            IPlayerInventoryStateProvider provider = ResolveInventoryProvider();
            return provider != null ? provider.InventoryState : null;
        }

        private IPlayerInventoryStateProvider ResolveInventoryProvider()
        {
            if (inventoryStateProvider != null)
            {
                return inventoryStateProvider;
            }

            inventoryStateProvider = inventoryStateProviderBehaviour as IPlayerInventoryStateProvider;
            if (inventoryStateProvider != null)
            {
                return inventoryStateProvider;
            }

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerInventoryStateProvider provider)
                {
                    inventoryStateProvider = provider;
                    inventoryStateProviderBehaviour = behaviours[i];
                    return inventoryStateProvider;
                }
            }

            return null;
        }

        private void SnapWorldItem(WorldItemView worldItemView, Transform anchor)
        {
            if (worldItemView == null || anchor == null)
            {
                return;
            }

            Rigidbody itemRigidbody = worldItemView.GetComponent<Rigidbody>();
            if (itemRigidbody != null)
            {
                itemRigidbody.linearVelocity = Vector3.zero;
                itemRigidbody.angularVelocity = Vector3.zero;
            }

            worldItemView.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        }

        private void DestroyConsumedWorldItem(WorldItemView worldItemView)
        {
            if (worldItemView == null)
            {
                return;
            }

            worldItemView.NotifyDestroyed();
            if (Application.isPlaying)
            {
                Destroy(worldItemView.gameObject);
            }
            else
            {
                DestroyImmediate(worldItemView.gameObject);
            }
        }

        private int CreateSeed(string salt)
        {
            unchecked
            {
                int seed = stationId != null ? StableIdUtility.GetNormalizedHashCode(stationId) : 0;
                seed = (seed * 397) ^ (salt != null ? salt.GetHashCode() : 0);
                seed = (seed * 397) ^ strikeCount;
                seed = (seed * 397) ^ ResolveRevision().GetHashCode();
                return seed == 0 ? 1 : seed;
            }
        }

        private long ResolveRevision()
        {
            PlayerInventoryState inventoryState = ResolveInventoryState();
            return inventoryState != null ? inventoryState.Revision : 0;
        }

        private InventoryOperationResult Failed(InventoryOperationType operationType, InventoryFailureReason reason, string message)
        {
            return InventoryOperationResult.Failed(operationType, reason, message, ResolveRevision());
        }

        private void ResetStrikeSequence()
        {
            strikeCount = 0;
            accumulatedQualityBonus = 0f;
            lastHammerInstanceId = default;
            lastHammerDefId = default;
            lastNormalizedForce = 0f;
            lastStrikeSeed = 0;
        }

        private void PlaySuccessFeedback()
        {
            if (strikeSparks != null)
            {
                strikeSparks.Play();
            }

            PlayClip(successClip);
        }

        private void PlayClip(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private void Log(string message)
        {
            if (verboseLogs)
            {
                Debug.Log(message, this);
            }
        }

        private void LogWarning(string message)
        {
            if (verboseLogs)
            {
                Debug.LogWarning(message, this);
            }
        }
    }
}
