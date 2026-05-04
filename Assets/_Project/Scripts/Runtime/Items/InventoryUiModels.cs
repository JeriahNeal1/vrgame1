using System;
using UnityEngine;
using VRGame.Items;

namespace VRGame.Runtime
{
    public enum InventoryUiCategory
    {
        All,
        Equipment,
        Weapons,
        Armor,
        Accessories,
        Tools,
        Resources,
        Placeables,
        Electrical,
        Crafting,
        Favorites
    }

    public enum InventorySelectionKind
    {
        None,
        Stack,
        ItemInstance
    }

    [Serializable]
    public struct InventoryItemSelection : IEquatable<InventoryItemSelection>
    {
        [SerializeField]
        private InventorySelectionKind kind;

        [SerializeField]
        private ItemDefId itemDefId;

        [SerializeField]
        private ItemInstanceId itemInstanceId;

        [SerializeField]
        private StackQuantity quantity;

        public InventoryItemSelection(InventorySelectionKind kind, ItemDefId itemDefId, ItemInstanceId itemInstanceId, StackQuantity quantity)
        {
            this.kind = kind;
            this.itemDefId = itemDefId;
            this.itemInstanceId = itemInstanceId;
            this.quantity = quantity.IsPositive ? quantity : StackQuantity.One;
        }

        public InventorySelectionKind Kind
        {
            get { return kind; }
        }

        public ItemDefId ItemDefId
        {
            get { return itemDefId; }
        }

        public ItemInstanceId ItemInstanceId
        {
            get { return itemInstanceId; }
        }

        public StackQuantity Quantity
        {
            get { return quantity.IsPositive ? quantity : StackQuantity.One; }
        }

        public bool IsStack
        {
            get { return kind == InventorySelectionKind.Stack && !itemDefId.IsEmpty; }
        }

        public bool IsItemInstance
        {
            get { return kind == InventorySelectionKind.ItemInstance && !itemInstanceId.IsEmpty; }
        }

        public bool IsValid
        {
            get { return IsStack || IsItemInstance; }
        }

        public static InventoryItemSelection None
        {
            get { return new InventoryItemSelection(InventorySelectionKind.None, default, default, StackQuantity.Zero); }
        }

        public static InventoryItemSelection ForStack(ItemDefId itemDefId, StackQuantity quantity)
        {
            return new InventoryItemSelection(InventorySelectionKind.Stack, itemDefId, default, quantity);
        }

        public static InventoryItemSelection ForItemInstance(ItemDefId itemDefId, ItemInstanceId itemInstanceId)
        {
            return new InventoryItemSelection(InventorySelectionKind.ItemInstance, itemDefId, itemInstanceId, StackQuantity.One);
        }

        public bool Equals(InventoryItemSelection other)
        {
            return kind == other.kind &&
                   itemDefId == other.itemDefId &&
                   itemInstanceId == other.itemInstanceId;
        }

        public override bool Equals(object obj)
        {
            return obj is InventoryItemSelection other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)kind;
                hash = (hash * 397) ^ itemDefId.GetHashCode();
                hash = (hash * 397) ^ itemInstanceId.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(InventoryItemSelection left, InventoryItemSelection right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(InventoryItemSelection left, InventoryItemSelection right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class InventoryUiEntry
    {
        public InventoryItemSelection Selection { get; set; }

        public ItemDefinition ItemDefinition { get; set; }

        public ItemInstanceState ItemInstance { get; set; }

        public StackQuantity Quantity { get; set; } = StackQuantity.One;

        public string DisplayName
        {
            get
            {
                if (ItemDefinition == null)
                {
                    return Selection.ItemDefId.Value;
                }

                return ItemDefinition.DisplayName;
            }
        }

        public string DetailText
        {
            get
            {
                if (Selection.IsStack)
                {
                    return $"x{Quantity.Value}";
                }

                if (ItemInstance != null)
                {
                    return ItemInstance.LifecycleState.ToString();
                }

                return string.Empty;
            }
        }

        public Sprite Icon
        {
            get { return ItemDefinition != null ? ItemDefinition.GeneratedIcon : null; }
        }

        public bool CanManifest
        {
            get
            {
                return ItemDefinition != null &&
                       ItemDefinition.IsManifestable &&
                       ItemDefinition.WorldPrefab != null &&
                       (Selection.IsStack && Quantity.IsPositive ||
                        Selection.IsItemInstance && ItemInstance != null && ItemInstance.LifecycleState == ItemLifecycleState.InInventory);
            }
        }
    }
}
