using System;
using UnityEngine;

namespace VRGame.Items
{
    [Serializable]
    public struct ItemInstanceId : IEquatable<ItemInstanceId>
    {
        [SerializeField]
        private string value;

        public ItemInstanceId(string value)
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

        public static ItemInstanceId NewId()
        {
            return new ItemInstanceId("item_instance_" + Guid.NewGuid().ToString("N"));
        }

        public static ItemInstanceId FromString(string value)
        {
            return new ItemInstanceId(value);
        }

        public bool Equals(ItemInstanceId other)
        {
            return StableIdUtility.EqualsNormalized(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is ItemInstanceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StableIdUtility.GetNormalizedHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(ItemInstanceId left, ItemInstanceId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ItemInstanceId left, ItemInstanceId right)
        {
            return !left.Equals(right);
        }

        public static implicit operator string(ItemInstanceId id)
        {
            return id.Value;
        }
    }
}
