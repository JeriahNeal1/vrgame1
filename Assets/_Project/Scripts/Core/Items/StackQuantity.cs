using System;
using UnityEngine;

namespace VRGame.Items
{
    // A signed long keeps common inventory operations cheap while still supporting factory-scale stacks.
    // If the economy later needs astronomical quantities, keep this value object API and swap the backing
    // storage to string-backed chunks, decimal mantissa/exponent, or an arbitrary-precision type at the boundary.
    [Serializable]
    public struct StackQuantity : IEquatable<StackQuantity>, IComparable<StackQuantity>
    {
        public const long MaxPracticalValue = long.MaxValue;

        public static readonly StackQuantity Zero = new StackQuantity(0);
        public static readonly StackQuantity One = new StackQuantity(1);
        public static readonly StackQuantity MaxPractical = new StackQuantity(MaxPracticalValue);

        [SerializeField]
        private long value;

        public StackQuantity(long value)
        {
            this.value = Math.Max(0, value);
        }

        public long Value
        {
            get { return Math.Max(0, value); }
        }

        public bool IsZero
        {
            get { return Value == 0; }
        }

        public bool IsPositive
        {
            get { return Value > 0; }
        }

        public static StackQuantity FromLong(long value)
        {
            return new StackQuantity(value);
        }

        public static bool TryCreate(long value, out StackQuantity quantity)
        {
            if (value < 0)
            {
                quantity = Zero;
                return false;
            }

            quantity = new StackQuantity(value);
            return true;
        }

        public bool TryAdd(StackQuantity other, out StackQuantity result)
        {
            if (MaxPracticalValue - Value < other.Value)
            {
                result = MaxPractical;
                return false;
            }

            result = new StackQuantity(Value + other.Value);
            return true;
        }

        public bool TrySubtract(StackQuantity other, out StackQuantity result)
        {
            if (Value < other.Value)
            {
                result = Zero;
                return false;
            }

            result = new StackQuantity(Value - other.Value);
            return true;
        }

        public bool Equals(StackQuantity other)
        {
            return Value == other.Value;
        }

        public int CompareTo(StackQuantity other)
        {
            return Value.CompareTo(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is StackQuantity other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(StackQuantity left, StackQuantity right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StackQuantity left, StackQuantity right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(StackQuantity left, StackQuantity right)
        {
            return left.Value < right.Value;
        }

        public static bool operator >(StackQuantity left, StackQuantity right)
        {
            return left.Value > right.Value;
        }

        public static bool operator <=(StackQuantity left, StackQuantity right)
        {
            return left.Value <= right.Value;
        }

        public static bool operator >=(StackQuantity left, StackQuantity right)
        {
            return left.Value >= right.Value;
        }
    }
}
