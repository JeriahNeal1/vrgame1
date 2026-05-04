using System.Collections.Generic;
using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class MeleeDamageZoneComponent : MonoBehaviour
    {
        [Header("Weapon Binding")]
        [SerializeField]
        private WorldItemView worldItemView = null;

        [SerializeField]
        private ItemDefinitionDatabase itemDefinitionDatabase = null;

        [SerializeField]
        private ItemAffixDefinitionDatabase affixDefinitionDatabase = null;

        [Tooltip("Optional MonoBehaviour that implements IPlayerInventoryStateProvider for attacker equipment stats and held instance lookup.")]
        [SerializeField]
        private MonoBehaviour inventoryStateProviderBehaviour = null;

        [Header("Damage Zone")]
        [SerializeField]
        private string damageZoneId = "blade";

        [SerializeField]
        private bool requireHeldWorldItem = true;

        [SerializeField]
        private LayerMask targetLayers = ~0;

        [Header("Motion")]
        [SerializeField]
        private Rigidbody velocityRigidbody = null;

        [SerializeField]
        private float velocityDamageScale = 0.1f;

        [SerializeField]
        private float maxVelocityMultiplier = 2f;

        [Header("Attacker Stats")]
        [SerializeField]
        private StatBlock attackerBaseStats = new StatBlock();

        [Header("Events")]
        [SerializeField]
        private DamageContextEvent onMeleeHit = new DamageContextEvent();

        [SerializeField]
        private DamageContextEvent onMeleeHitRejected = new DamageContextEvent();

        private readonly Dictionary<string, float> nextAllowedHitTimeByTarget = new Dictionary<string, float>(StableIdUtility.Comparer);
        private readonly List<IMeleeHitActionHandler> reusableActionHandlers = new List<IMeleeHitActionHandler>();
        private readonly StatBlock resolvedAttackerStats = new StatBlock();

        private IPlayerInventoryStateProvider inventoryStateProvider;
        private IPlayerInventoryStateProvider runtimeInventoryStateProvider;
        private Vector3 previousPosition;
        private Vector3 sampledVelocity;

        public string DamageZoneId
        {
            get { return StableIdUtility.Normalize(damageZoneId); }
        }

        public StatBlock AttackerBaseStats
        {
            get
            {
                attackerBaseStats ??= new StatBlock();
                return attackerBaseStats;
            }
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

        public DamageResult TryHit(IDamageable damageable, Collider targetCollider, Vector3 hitPoint, Vector3 hitNormal, Vector3 sourceVelocity)
        {
            if (damageable == null)
            {
                return Reject(null, "No damageable target was found.");
            }

            if (targetCollider != null && !LayerIsAllowed(targetCollider.gameObject.layer))
            {
                return Reject(null, $"Target layer '{targetCollider.gameObject.layer}' is not in the melee damage mask.");
            }

            if (targetCollider != null && worldItemView != null && targetCollider.transform.IsChildOf(worldItemView.transform))
            {
                return Reject(null, "Ignored collision with the source weapon.");
            }

            string targetKey = GetTargetKey(damageable);
            if (IsTargetOnCooldown(targetKey))
            {
                return Reject(null, $"Target '{targetKey}' is still on melee hit cooldown.");
            }

            if (!TryResolveWeapon(out ItemDefinition itemDefinition, out ItemInstanceState itemInstance, out string rejectMessage))
            {
                return Reject(null, rejectMessage);
            }

            BuildAttackerStats();

            MeleeDamageCalculationResult calculation = MeleeDamageCalculator.Calculate(new MeleeDamageCalculationInput(
                itemDefinition,
                itemInstance,
                affixDefinitionDatabase,
                resolvedAttackerStats,
                DamageZoneId,
                sourceVelocity.magnitude,
                Random.value,
                velocityDamageScale,
                maxVelocityMultiplier));

            DamageContext context = CreateDamageContext(itemDefinition, itemInstance, calculation, hitPoint, hitNormal, sourceVelocity);
            if (!calculation.Success)
            {
                return Reject(context, calculation.FailureReason.ToString());
            }

            if (!damageable.CanReceiveDamage(context))
            {
                return Reject(context, $"Target '{targetKey}' rejected melee damage.");
            }

            DamageResult result = damageable.ApplyDamage(context);
            if (result == null || !result.Accepted)
            {
                return Reject(context, result != null ? result.Message : $"Target '{targetKey}' returned a null damage result.");
            }

            SetTargetCooldown(targetKey, calculation.HitCooldownSeconds);
            onMeleeHit.Invoke(context);
            DispatchMeleeHitActions(context, result);
            return result;
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
            velocityDamageScale = Mathf.Max(0f, velocityDamageScale);
            maxVelocityMultiplier = Mathf.Max(1f, maxVelocityMultiplier);
            attackerBaseStats ??= new StatBlock();
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
            TryHitCollider(other, Vector3.zero);
        }

        private void OnTriggerStay(Collider other)
        {
            TryHitCollider(other, Vector3.zero);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryHitCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryHitCollision(collision);
        }

        private void TryHitCollider(Collider other, Vector3 fallbackNormal)
        {
            if (other == null)
            {
                return;
            }

            IDamageable damageable = ResolveDamageable(other);
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 velocity = ResolveVelocity(Vector3.zero);
            TryHit(damageable, other, hitPoint, fallbackNormal, velocity);
        }

        private void TryHitCollision(Collision collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            ContactPoint contact = collision.contactCount > 0 ? collision.GetContact(0) : default;
            Vector3 hitPoint = collision.contactCount > 0 ? contact.point : collision.collider.ClosestPoint(transform.position);
            Vector3 hitNormal = collision.contactCount > 0 ? contact.normal : Vector3.zero;
            Vector3 velocity = ResolveVelocity(collision.relativeVelocity);
            TryHit(ResolveDamageable(collision.collider), collision.collider, hitPoint, hitNormal, velocity);
        }

        private bool TryResolveWeapon(out ItemDefinition itemDefinition, out ItemInstanceState itemInstance, out string rejectMessage)
        {
            itemDefinition = null;
            itemInstance = null;
            rejectMessage = string.Empty;

            ResolveSceneReferences();
            if (worldItemView == null || worldItemView.Identity == null)
            {
                rejectMessage = "Melee damage zone has no world item binding.";
                return false;
            }

            if (requireHeldWorldItem && worldItemView.Identity.LifecycleState != ItemLifecycleState.HeldInWorld)
            {
                rejectMessage = $"World item must be HeldInWorld before it can deal melee damage. Current state: {worldItemView.Identity.LifecycleState}.";
                return false;
            }

            itemDefinition = worldItemView.BoundDefinition;
            itemInstance = worldItemView.BoundInstance;
            WorldItemIdentity identity = worldItemView.Identity;

            if (itemDefinition == null && itemDefinitionDatabase != null)
            {
                itemDefinitionDatabase.TryGet(identity.ItemDefId, out itemDefinition);
            }

            PlayerInventoryState inventoryState = ResolveInventoryState();
            if (itemInstance == null &&
                inventoryState != null &&
                !identity.ItemInstanceId.IsEmpty &&
                inventoryState.TryGetInstance(identity.ItemInstanceId, out ItemInstanceState resolvedInstance))
            {
                itemInstance = resolvedInstance;
            }

            if (itemDefinition == null)
            {
                rejectMessage = $"Unknown melee item definition '{identity.ItemDefId}'.";
                return false;
            }

            if (itemDefinition.MeleeWeaponProfile == null)
            {
                rejectMessage = $"Item definition '{itemDefinition.ItemDefId}' has no melee weapon profile.";
                return false;
            }

            return true;
        }

        private DamageContext CreateDamageContext(
            ItemDefinition itemDefinition,
            ItemInstanceState itemInstance,
            MeleeDamageCalculationResult calculation,
            Vector3 hitPoint,
            Vector3 hitNormal,
            Vector3 sourceVelocity)
        {
            WorldItemIdentity identity = worldItemView != null ? worldItemView.Identity : null;
            Vector3 direction = sourceVelocity.sqrMagnitude > 0.0001f
                ? sourceVelocity.normalized
                : hitNormal.sqrMagnitude > 0.0001f ? -hitNormal.normalized : transform.forward;

            return new DamageContext
            {
                AttackerId = identity != null ? identity.OwnerId : string.Empty,
                AttackerObject = worldItemView != null ? worldItemView.gameObject : gameObject,
                SourceItemIdentity = identity,
                SourceItemDefinition = itemDefinition,
                SourceItemInstance = itemInstance,
                ItemDefId = itemDefinition != null ? itemDefinition.ItemDefId : default,
                ItemInstanceId = itemInstance != null ? itemInstance.ItemInstanceId : identity != null ? identity.ItemInstanceId : default,
                DamageZoneId = DamageZoneId,
                DamageType = calculation.TrueMelee ? DamageType.TrueMelee : DamageType.Melee,
                DamageAmount = calculation.Damage,
                Critical = calculation.Critical,
                Knockback = calculation.Knockback,
                HitPoint = hitPoint,
                HitDirection = direction,
                SourceVelocity = sourceVelocity,
                HitVelocity = sourceVelocity.magnitude,
                VelocityMultiplier = calculation.VelocityMultiplier,
                MinimumHitVelocity = calculation.MinimumHitVelocity,
                HitCooldownSeconds = calculation.HitCooldownSeconds
            };
        }

        private DamageResult Reject(DamageContext context, string message)
        {
            if (context != null)
            {
                onMeleeHitRejected.Invoke(context);
            }

            return DamageResult.Rejected(message);
        }

        private void BuildAttackerStats()
        {
            PlayerInventoryState inventoryState = ResolveInventoryState();
            if (inventoryState != null && itemDefinitionDatabase != null)
            {
                StatAggregator.RecalculateEquipmentStats(
                    AttackerBaseStats,
                    inventoryState,
                    itemDefinitionDatabase,
                    affixDefinitionDatabase,
                    resolvedAttackerStats,
                    null,
                    false);
            }
            else
            {
                resolvedAttackerStats.CopyFrom(AttackerBaseStats);
            }
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

        private bool IsTargetOnCooldown(string targetKey)
        {
            return !string.IsNullOrEmpty(targetKey) &&
                   nextAllowedHitTimeByTarget.TryGetValue(targetKey, out float nextAllowedTime) &&
                   Time.time < nextAllowedTime;
        }

        private void SetTargetCooldown(string targetKey, float cooldownSeconds)
        {
            if (!string.IsNullOrEmpty(targetKey) && cooldownSeconds > 0f)
            {
                nextAllowedHitTimeByTarget[targetKey] = Time.time + cooldownSeconds;
            }
        }

        private void DispatchMeleeHitActions(DamageContext damageContext, DamageResult damageResult)
        {
            reusableActionHandlers.Clear();
            CollectActionHandlers(worldItemView != null ? worldItemView.gameObject : gameObject, reusableActionHandlers);

            MeleeHitActionContext context = new MeleeHitActionContext(damageContext, damageResult);
            for (int i = 0; i < reusableActionHandlers.Count; i++)
            {
                reusableActionHandlers[i]?.OnMeleeHit(context);
            }
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

        private bool LayerIsAllowed(int layer)
        {
            return (targetLayers.value & (1 << layer)) != 0;
        }

        private static IDamageable ResolveDamageable(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDamageable damageable)
                {
                    return damageable;
                }
            }

            return null;
        }

        private static string GetTargetKey(IDamageable damageable)
        {
            if (damageable == null)
            {
                return string.Empty;
            }

            string damageableId = StableIdUtility.Normalize(damageable.DamageableId);
            if (!string.IsNullOrEmpty(damageableId))
            {
                return damageableId;
            }

            return damageable is Component component
                ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(component).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : damageable.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void CollectActionHandlers(GameObject sourceObject, List<IMeleeHitActionHandler> target)
        {
            if (sourceObject == null || target == null)
            {
                return;
            }

            MonoBehaviour[] behaviours = sourceObject.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IMeleeHitActionHandler actionHandler && !target.Contains(actionHandler))
                {
                    target.Add(actionHandler);
                }
            }

            behaviours = sourceObject.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IMeleeHitActionHandler actionHandler && !target.Contains(actionHandler))
                {
                    target.Add(actionHandler);
                }
            }
        }
    }
}
