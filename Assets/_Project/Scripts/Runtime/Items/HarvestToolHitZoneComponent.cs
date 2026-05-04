using System.Collections.Generic;
using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class HarvestToolHitZoneComponent : MonoBehaviour
    {
        [Header("Tool Binding")]
        [SerializeField]
        private WorldItemView worldItemView = null;

        [SerializeField]
        private ItemDefinitionDatabase itemDefinitionDatabase = null;

        [SerializeField]
        private ItemAffixDefinitionDatabase affixDefinitionDatabase = null;

        [Tooltip("Optional MonoBehaviour that implements IPlayerInventoryStateProvider.")]
        [SerializeField]
        private MonoBehaviour inventoryStateProviderBehaviour = null;

        [Header("Hit Rules")]
        [SerializeField]
        private LayerMask harvestableLayers = ~0;

        [Min(0f)]
        [SerializeField]
        private float minimumHitVelocity = 0.25f;

        [Min(0f)]
        [SerializeField]
        private float perTargetHitCooldownSeconds = 0.2f;

        [Min(0f)]
        [SerializeField]
        private float hitStrengthScale = 1f;

        [Header("Motion")]
        [SerializeField]
        private Rigidbody velocityRigidbody = null;

        [SerializeField]
        private bool logFailures = true;

        private readonly Dictionary<Harvestable, float> nextAllowedHitTimeByTarget = new Dictionary<Harvestable, float>();
        private IPlayerInventoryStateProvider inventoryStateProvider;
        private IPlayerInventoryStateProvider runtimeInventoryStateProvider;
        private Vector3 previousPosition;
        private Vector3 sampledVelocity;
        private HarvestHitResult lastHitResult;

        public HarvestHitResult LastHitResult
        {
            get { return lastHitResult; }
        }

        public void BindRuntime(
            WorldItemView newWorldItemView,
            ItemDefinitionDatabase newItemDefinitionDatabase,
            ItemAffixDefinitionDatabase newAffixDefinitionDatabase,
            IPlayerInventoryStateProvider newInventoryStateProvider)
        {
            worldItemView = newWorldItemView;
            itemDefinitionDatabase = newItemDefinitionDatabase;
            affixDefinitionDatabase = newAffixDefinitionDatabase;
            runtimeInventoryStateProvider = newInventoryStateProvider;
            inventoryStateProvider = newInventoryStateProvider;
        }

        public HarvestHitResult TryHarvest(Harvestable harvestable, float hitVelocity)
        {
            if (harvestable == null)
            {
                return null;
            }

            if (hitVelocity < minimumHitVelocity)
            {
                lastHitResult = HarvestHitResult.Failed(
                    HarvestToolValidationResult.Failed(HarvestValidationFailureReason.InvalidHeldToolState, $"Harvest hit velocity {hitVelocity:0.##} is below minimum {minimumHitVelocity:0.##}."),
                    "Harvest hit was too slow.");
                LogFailure(lastHitResult);
                return lastHitResult;
            }

            if (IsTargetOnCooldown(harvestable))
            {
                lastHitResult = HarvestHitResult.Failed(
                    HarvestToolValidationResult.Failed(HarvestValidationFailureReason.InvalidHeldToolState, "Harvestable target is still on hit cooldown."),
                    "Harvestable target is still on hit cooldown.");
                return lastHitResult;
            }

            harvestable.BindRuntime(itemDefinitionDatabase, affixDefinitionDatabase, ResolveInventoryProvider());
            float hitStrength = Mathf.Max(0.01f, hitVelocity * Mathf.Max(0.01f, hitStrengthScale));
            lastHitResult = harvestable.TryHarvestHit(worldItemView, hitStrength);
            if (lastHitResult != null && lastHitResult.Success)
            {
                SetTargetCooldown(harvestable);
            }
            else
            {
                LogFailure(lastHitResult);
            }

            return lastHitResult;
        }

        public void ClearHitCooldowns()
        {
            nextAllowedHitTimeByTarget.Clear();
        }

        private void Awake()
        {
            ResolveSceneReferences();
            previousPosition = transform.position;
        }

        private void OnValidate()
        {
            ResolveSceneReferences();
            minimumHitVelocity = Mathf.Max(0f, minimumHitVelocity);
            perTargetHitCooldownSeconds = Mathf.Max(0f, perTargetHitCooldownSeconds);
            hitStrengthScale = Mathf.Max(0f, hitStrengthScale);
        }

        private void FixedUpdate()
        {
            float deltaTime = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : Time.deltaTime;
            if (deltaTime > 0f)
            {
                sampledVelocity = (transform.position - previousPosition) / deltaTime;
            }

            previousPosition = transform.position;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHarvestCollider(other, Vector3.zero);
        }

        private void OnTriggerStay(Collider other)
        {
            TryHarvestCollider(other, Vector3.zero);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryHarvestCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryHarvestCollision(collision);
        }

        private void TryHarvestCollider(Collider other, Vector3 fallbackVelocity)
        {
            if (other == null || !LayerIsAllowed(other.gameObject.layer))
            {
                return;
            }

            Harvestable harvestable = other.GetComponentInParent<Harvestable>();
            if (harvestable == null)
            {
                return;
            }

            TryHarvest(harvestable, ResolveVelocity(fallbackVelocity).magnitude);
        }

        private void TryHarvestCollision(Collision collision)
        {
            if (collision == null || collision.collider == null || !LayerIsAllowed(collision.collider.gameObject.layer))
            {
                return;
            }

            Harvestable harvestable = collision.collider.GetComponentInParent<Harvestable>();
            if (harvestable == null)
            {
                return;
            }

            TryHarvest(harvestable, ResolveVelocity(collision.relativeVelocity).magnitude);
        }

        private Vector3 ResolveVelocity(Vector3 collisionRelativeVelocity)
        {
            if (collisionRelativeVelocity.sqrMagnitude > 0.0001f)
            {
                return collisionRelativeVelocity;
            }

            if (velocityRigidbody != null)
            {
                return velocityRigidbody.linearVelocity;
            }

            return sampledVelocity;
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

        private void ResolveSceneReferences()
        {
            if (worldItemView == null)
            {
                worldItemView = GetComponentInParent<WorldItemView>();
            }

            if (velocityRigidbody == null)
            {
                velocityRigidbody = GetComponentInParent<Rigidbody>();
            }
        }

        private bool IsTargetOnCooldown(Harvestable harvestable)
        {
            return harvestable != null &&
                   nextAllowedHitTimeByTarget.TryGetValue(harvestable, out float nextAllowedTime) &&
                   Time.time < nextAllowedTime;
        }

        private void SetTargetCooldown(Harvestable harvestable)
        {
            if (harvestable != null && perTargetHitCooldownSeconds > 0f)
            {
                nextAllowedHitTimeByTarget[harvestable] = Time.time + perTargetHitCooldownSeconds;
            }
        }

        private bool LayerIsAllowed(int layer)
        {
            return (harvestableLayers.value & (1 << layer)) != 0;
        }

        private void LogFailure(HarvestHitResult result)
        {
            if (logFailures && result != null && !result.Success)
            {
                Debug.Log($"Harvest tool hit failed: {result.Message}", this);
            }
        }
    }
}
