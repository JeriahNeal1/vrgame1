using System;
using UnityEngine;
using UnityEngine.Events;
using VRGame.Items;

namespace VRGame.Runtime
{
    [DisallowMultipleComponent]
    public sealed class WorldItemView : MonoBehaviour
    {
        [SerializeField]
        private WorldItemIdentity identity;

        [SerializeField]
        private bool renameObjectOnBind = true;

        [SerializeField]
        private bool verboseLifecycleLogs = false;

        [SerializeField]
        private string debugDisplayName = string.Empty;

        [Header("Lifecycle Events")]
        [SerializeField]
        private UnityEvent onManifested = new UnityEvent();

        [SerializeField]
        private UnityEvent onGrabbed = new UnityEvent();

        [SerializeField]
        private UnityEvent onReleased = new UnityEvent();

        [SerializeField]
        private UnityEvent onDropped = new UnityEvent();

        [SerializeField]
        private UnityEvent onReturnedToInventory = new UnityEvent();

        [SerializeField]
        private UnityEvent onDestroyed = new UnityEvent();

        [NonSerialized]
        private ItemDefinition boundDefinition;

        [NonSerialized]
        private ItemInstanceState boundInstance;

        private bool destroyedEventRaised;

        public event Action<WorldItemView> Manifested;

        public event Action<WorldItemView> Grabbed;

        public event Action<WorldItemView> Released;

        public event Action<WorldItemView> Dropped;

        public event Action<WorldItemView> ReturnedToInventory;

        public event Action<WorldItemView> Destroyed;

        public WorldItemIdentity Identity
        {
            get
            {
                EnsureIdentity();
                return identity;
            }
        }

        public ItemDefinition BoundDefinition
        {
            get { return boundDefinition; }
        }

        public ItemInstanceState BoundInstance
        {
            get { return boundInstance; }
        }

        public string DebugDisplayName
        {
            get { return debugDisplayName ?? string.Empty; }
        }

        public void Bind(WorldItemBinding binding)
        {
            if (binding == null)
            {
                Debug.LogWarning($"{nameof(WorldItemView)} on '{name}' received a null binding.", this);
                return;
            }

            EnsureIdentity();
            boundDefinition = binding.ItemDefinition;
            boundInstance = binding.ItemInstance;
            debugDisplayName = binding.DisplayName;

            identity.Bind(
                binding.WorldItemId,
                binding.ManifestationRequestId,
                binding.OwnerId,
                binding.ItemDefId,
                binding.ItemInstanceId,
                binding.Quantity,
                binding.LifecycleState,
                binding.RuntimeBindingId);

            if (renameObjectOnBind && !string.IsNullOrEmpty(debugDisplayName))
            {
                gameObject.name = debugDisplayName;
            }
        }

        public void NotifyManifested()
        {
            SetLifecycle(ItemLifecycleState.HeldInWorld);
            LogLifecycle(nameof(NotifyManifested));
            onManifested.Invoke();
            Manifested?.Invoke(this);
        }

        public void NotifyGrabbed()
        {
            SetLifecycle(ItemLifecycleState.HeldInWorld);
            LogLifecycle(nameof(NotifyGrabbed));
            onGrabbed.Invoke();
            Grabbed?.Invoke(this);
        }

        public void NotifyReleased()
        {
            LogLifecycle(nameof(NotifyReleased));
            onReleased.Invoke();
            Released?.Invoke(this);
        }

        public void NotifyDropped()
        {
            SetLifecycle(ItemLifecycleState.DroppedInWorld);
            LogLifecycle(nameof(NotifyDropped));
            onDropped.Invoke();
            Dropped?.Invoke(this);
        }

        public void NotifyReturnedToInventory()
        {
            SetLifecycle(ItemLifecycleState.InInventory);
            LogLifecycle(nameof(NotifyReturnedToInventory));
            onReturnedToInventory.Invoke();
            ReturnedToInventory?.Invoke(this);
        }

        public void NotifyDestroyed()
        {
            if (destroyedEventRaised)
            {
                return;
            }

            destroyedEventRaised = true;
            SetLifecycle(ItemLifecycleState.Destroyed);
            LogLifecycle(nameof(NotifyDestroyed));
            onDestroyed.Invoke();
            Destroyed?.Invoke(this);
        }

        private void Awake()
        {
            EnsureIdentity();
        }

        private void OnValidate()
        {
            if (identity == null)
            {
                identity = GetComponent<WorldItemIdentity>();
            }
        }

        private void OnDestroy()
        {
            NotifyDestroyed();
        }

        private void SetLifecycle(ItemLifecycleState lifecycleState)
        {
            EnsureIdentity();
            identity.SetLifecycleState(lifecycleState);
        }

        private void EnsureIdentity()
        {
            if (identity == null)
            {
                identity = GetComponent<WorldItemIdentity>();
            }

            if (identity == null)
            {
                identity = gameObject.AddComponent<WorldItemIdentity>();
            }
        }

        private void LogLifecycle(string eventName)
        {
            if (verboseLifecycleLogs)
            {
                Debug.Log($"{nameof(WorldItemView)} {eventName}: {Identity.ItemDefId} / {Identity.ItemInstanceId} / {Identity.ManifestationRequestId}", this);
            }
        }
    }

    public sealed class WorldItemBinding
    {
        public string WorldItemId { get; set; }

        public string ManifestationRequestId { get; set; }

        public string RuntimeBindingId { get; set; }

        public string OwnerId { get; set; }

        public ItemDefId ItemDefId { get; set; }

        public ItemInstanceId ItemInstanceId { get; set; }

        public StackQuantity Quantity { get; set; } = StackQuantity.One;

        public ItemLifecycleState LifecycleState { get; set; } = ItemLifecycleState.ManifestingFromPortal;

        public ItemDefinition ItemDefinition { get; set; }

        public ItemInstanceState ItemInstance { get; set; }

        public string DisplayName
        {
            get
            {
                if (ItemDefinition == null)
                {
                    return ItemDefId.Value;
                }

                return ItemInstanceId.IsEmpty
                    ? ItemDefinition.DisplayName
                    : $"{ItemDefinition.DisplayName} [{ItemInstanceId.Value}]";
            }
        }
    }
}
