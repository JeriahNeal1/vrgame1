using System.Collections.Generic;
using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    public sealed class DebugInventoryStateProvider : MonoBehaviour, IPlayerInventoryStateProvider
    {
        [SerializeField]
        private string ownerId = "debug_player";

        [SerializeField]
        private PlayerInventoryState inventoryState;

        [Header("Optional Debug Seed")]
        [SerializeField]
        private ItemDefinitionDatabase itemDefinitionDatabase = null;

        [SerializeField]
        private List<DebugStackSeed> initialStacks = new List<DebugStackSeed>();

        private bool seeded;

        public PlayerInventoryState InventoryState
        {
            get
            {
                EnsureInventoryState();
                SeedOnce();
                return inventoryState;
            }
        }

        private void Awake()
        {
            EnsureInventoryState();
            SeedOnce();
        }

        private void EnsureInventoryState()
        {
            inventoryState ??= new PlayerInventoryState(ownerId);
        }

        private void SeedOnce()
        {
            if (seeded || itemDefinitionDatabase == null)
            {
                return;
            }

            seeded = true;
            for (int i = 0; i < initialStacks.Count; i++)
            {
                DebugStackSeed seed = initialStacks[i];
                if (seed != null && seed.ItemDefId.IsValid && seed.Quantity.IsPositive)
                {
                    PlayerInventoryOperations.AddStack(inventoryState, itemDefinitionDatabase, seed.ItemDefId, seed.Quantity);
                }
            }
        }
    }

    [System.Serializable]
    public sealed class DebugStackSeed
    {
        [SerializeField]
        private ItemDefId itemDefId = default;

        [SerializeField]
        private StackQuantity quantity = StackQuantity.One;

        public ItemDefId ItemDefId
        {
            get { return itemDefId; }
        }

        public StackQuantity Quantity
        {
            get { return quantity.IsPositive ? quantity : StackQuantity.One; }
        }
    }
}
