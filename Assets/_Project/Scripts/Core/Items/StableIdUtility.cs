using System;

namespace VRGame.Items
{
    public static class StableIdUtility
    {
        public static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static bool IsValid(string value)
        {
            return !string.IsNullOrWhiteSpace(Normalize(value));
        }

        public static bool EqualsNormalized(string left, string right)
        {
            return Comparer.Equals(Normalize(left), Normalize(right));
        }

        public static int GetNormalizedHashCode(string value)
        {
            return Comparer.GetHashCode(Normalize(value));
        }
    }
}
