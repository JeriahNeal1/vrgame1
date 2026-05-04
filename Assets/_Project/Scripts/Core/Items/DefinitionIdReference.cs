using System;
using UnityEngine;

namespace VRGame.Items
{
    [Serializable]
    public struct DefinitionIdReference : IEquatable<DefinitionIdReference>
    {
        [SerializeField]
        private string id;

        [SerializeField]
        private string note;

        public DefinitionIdReference(string id, string note = "")
        {
            this.id = StableIdUtility.Normalize(id);
            this.note = note ?? string.Empty;
        }

        public string Id
        {
            get { return StableIdUtility.Normalize(id); }
        }

        public string Note
        {
            get { return note ?? string.Empty; }
        }

        public bool IsValid
        {
            get { return StableIdUtility.IsValid(Id); }
        }

        public bool Equals(DefinitionIdReference other)
        {
            return StableIdUtility.EqualsNormalized(Id, other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is DefinitionIdReference other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StableIdUtility.GetNormalizedHashCode(Id);
        }

        public override string ToString()
        {
            return Id;
        }
    }
}
