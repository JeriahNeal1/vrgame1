using System;
using UnityEngine;

namespace VRGame.Items
{
    public enum ItemStackPolicyMode
    {
        DefaultByItemFlags,
        AlwaysUnstackable,
        InfinitelyStackable,
        LimitedStack
    }

    [Serializable]
    public sealed class ItemStackPolicy
    {
        [SerializeField]
        private ItemStackPolicyMode mode = ItemStackPolicyMode.DefaultByItemFlags;

        [Min(1)]
        [SerializeField]
        private int maxStackSize = 1;

        public ItemStackPolicyMode Mode
        {
            get { return mode; }
        }

        public int MaxStackSize
        {
            get { return Mathf.Max(1, maxStackSize); }
        }

        public ResolvedItemStackPolicy Resolve(ItemFlags flags)
        {
            switch (mode)
            {
                case ItemStackPolicyMode.AlwaysUnstackable:
                    return ResolvedItemStackPolicy.Unstackable;
                case ItemStackPolicyMode.InfinitelyStackable:
                    return ResolvedItemStackPolicy.Infinite;
                case ItemStackPolicyMode.LimitedStack:
                    return ResolvedItemStackPolicy.Limited(MaxStackSize);
                case ItemStackPolicyMode.DefaultByItemFlags:
                default:
                    return (flags & ItemFlags.Equipment) != 0
                        ? ResolvedItemStackPolicy.Unstackable
                        : ResolvedItemStackPolicy.Infinite;
            }
        }

        public bool IsExplicitlyStackable
        {
            get
            {
                return mode == ItemStackPolicyMode.InfinitelyStackable ||
                       (mode == ItemStackPolicyMode.LimitedStack && MaxStackSize > 1);
            }
        }
    }

    public readonly struct ResolvedItemStackPolicy
    {
        public static readonly ResolvedItemStackPolicy Unstackable = new ResolvedItemStackPolicy(false, false, 1);
        public static readonly ResolvedItemStackPolicy Infinite = new ResolvedItemStackPolicy(true, true, 0);

        public ResolvedItemStackPolicy(bool isStackable, bool isInfinite, int maxStackSize)
        {
            IsStackable = isStackable;
            IsInfinite = isInfinite;
            MaxStackSize = maxStackSize;
        }

        public bool IsStackable { get; }

        public bool IsInfinite { get; }

        public int MaxStackSize { get; }

        public static ResolvedItemStackPolicy Limited(int maxStackSize)
        {
            int clampedMax = Mathf.Max(1, maxStackSize);
            return clampedMax <= 1
                ? Unstackable
                : new ResolvedItemStackPolicy(true, false, clampedMax);
        }
    }
}
