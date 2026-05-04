using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    [CreateAssetMenu(menuName = "VRGame/Items/Modifier Set Definition", fileName = "ModifierSetDefinition")]
    public sealed class ModifierSetDefinition : ScriptableObject
    {
        [SerializeField]
        private string modifierSetId = string.Empty;

        [SerializeField]
        private string displayName = string.Empty;

        [TextArea]
        [SerializeField]
        private string description = string.Empty;

        [SerializeField]
        private List<ModifierDefinition> modifiers = new List<ModifierDefinition>();

        public string ModifierSetId
        {
            get { return StableIdUtility.Normalize(modifierSetId); }
        }

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim(); }
        }

        public string Description
        {
            get { return description ?? string.Empty; }
        }

        public IReadOnlyList<ModifierDefinition> Modifiers
        {
            get { return modifiers ?? (IReadOnlyList<ModifierDefinition>)Array.Empty<ModifierDefinition>(); }
        }

        private void OnValidate()
        {
            modifiers ??= new List<ModifierDefinition>();
        }
    }
}
