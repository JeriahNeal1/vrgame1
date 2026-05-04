using System;
using UnityEngine;

namespace VRGame.Items
{
    [Serializable]
    public struct ModifierId : IEquatable<ModifierId>
    {
        [SerializeField]
        private string value;

        public ModifierId(string value)
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

        public static ModifierId FromString(string value)
        {
            return new ModifierId(value);
        }

        public bool Equals(ModifierId other)
        {
            return StableIdUtility.EqualsNormalized(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is ModifierId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StableIdUtility.GetNormalizedHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(ModifierId left, ModifierId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ModifierId left, ModifierId right)
        {
            return !left.Equals(right);
        }

        public static implicit operator string(ModifierId id)
        {
            return id.Value;
        }
    }

    [Serializable]
    public struct EnchantmentId : IEquatable<EnchantmentId>
    {
        [SerializeField]
        private string value;

        public EnchantmentId(string value)
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

        public static EnchantmentId FromString(string value)
        {
            return new EnchantmentId(value);
        }

        public bool Equals(EnchantmentId other)
        {
            return StableIdUtility.EqualsNormalized(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is EnchantmentId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StableIdUtility.GetNormalizedHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(EnchantmentId left, EnchantmentId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EnchantmentId left, EnchantmentId right)
        {
            return !left.Equals(right);
        }

        public static implicit operator string(EnchantmentId id)
        {
            return id.Value;
        }
    }
}
