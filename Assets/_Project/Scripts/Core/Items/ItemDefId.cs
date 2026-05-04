using System;
using UnityEngine;

namespace VRGame.Items
{
    [Serializable]
    public struct ItemDefId : IEquatable<ItemDefId>
    {
        [SerializeField]
        private string value;

        public ItemDefId(string value)
        {
            this.value = StableIdUtility.Normalize(value);
        }

        public string Value
        {
            get { return StableIdUtility.Normalize(value); }
        }

        public bool IsEmpty
        {
            get { return string.IsNullOrEmpty(Value); }
        }

        public bool IsValid
        {
            get { return StableIdUtility.IsValid(Value); }
        }

        public static ItemDefId FromString(string value)
        {
            return new ItemDefId(value);
        }

        public bool Equals(ItemDefId other)
        {
            return StableIdUtility.EqualsNormalized(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is ItemDefId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StableIdUtility.GetNormalizedHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(ItemDefId left, ItemDefId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ItemDefId left, ItemDefId right)
        {
            return !left.Equals(right);
        }

        public static implicit operator string(ItemDefId id)
        {
            return id.Value;
        }
    }
}
