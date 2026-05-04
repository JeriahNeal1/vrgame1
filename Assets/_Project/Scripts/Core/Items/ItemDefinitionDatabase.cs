using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    [CreateAssetMenu(menuName = "VRGame/Items/Item Definition Database", fileName = "ItemDefinitionDatabase")]
    public sealed class ItemDefinitionDatabase : ScriptableObject
    {
        [SerializeField]
        private List<ItemDefinition> itemDefinitions = new List<ItemDefinition>();

        [NonSerialized]
        private Dictionary<string, ItemDefinition> lookupById;

        [NonSerialized]
        private bool lookupDirty = true;

        public IReadOnlyList<ItemDefinition> ItemDefinitions
        {
            get { return itemDefinitions ?? (IReadOnlyList<ItemDefinition>)Array.Empty<ItemDefinition>(); }
        }

        public bool TryGet(ItemDefId itemDefId, out ItemDefinition itemDefinition)
        {
            EnsureLookup();
            if (itemDefId.IsEmpty)
            {
                itemDefinition = null;
                return false;
            }

            return lookupById.TryGetValue(itemDefId.Value, out itemDefinition);
        }

        public bool TryGet(string itemDefId, out ItemDefinition itemDefinition)
        {
            return TryGet(ItemDefId.FromString(itemDefId), out itemDefinition);
        }

        public ItemDefinition GetOrNull(ItemDefId itemDefId)
        {
            return TryGet(itemDefId, out ItemDefinition itemDefinition) ? itemDefinition : null;
        }

        public bool Contains(ItemDefId itemDefId)
        {
            return TryGet(itemDefId, out _);
        }

        public IEnumerable<ItemDefinition> FindByFlags(ItemFlags requiredFlags, bool requireAllFlags = true)
        {
            IReadOnlyList<ItemDefinition> definitions = ItemDefinitions;
            for (int i = 0; i < definitions.Count; i++)
            {
                ItemDefinition itemDefinition = definitions[i];
                if (itemDefinition == null)
                {
                    continue;
                }

                bool matches = requiredFlags == ItemFlags.None ||
                               (requireAllFlags
                                   ? (itemDefinition.Flags & requiredFlags) == requiredFlags
                                   : (itemDefinition.Flags & requiredFlags) != 0);

                if (matches)
                {
                    yield return itemDefinition;
                }
            }
        }

        public IEnumerable<ItemDefinition> FindByCategory(ItemCategoryPath categoryPath, bool includeDescendants = true)
        {
            IReadOnlyList<ItemDefinition> definitions = ItemDefinitions;
            for (int i = 0; i < definitions.Count; i++)
            {
                ItemDefinition itemDefinition = definitions[i];
                if (itemDefinition == null)
                {
                    continue;
                }

                ItemCategoryPath itemCategory = itemDefinition.CategoryPath;
                if (categoryPath == null || categoryPath.IsEmpty)
                {
                    yield return itemDefinition;
                }
                else if (includeDescendants && itemCategory != null && itemCategory.StartsWith(categoryPath))
                {
                    yield return itemDefinition;
                }
                else if (!includeDescendants && itemCategory != null && itemCategory.EqualsPath(categoryPath))
                {
                    yield return itemDefinition;
                }
            }
        }

        public IReadOnlyList<ItemDefinitionValidationIssue> ValidateDefinitions()
        {
            return ItemDefinitionValidator.ValidateDatabase(this);
        }

        public void RebuildLookup()
        {
            if (lookupById == null)
            {
                lookupById = new Dictionary<string, ItemDefinition>(StableIdUtility.Comparer);
            }
            else
            {
                lookupById.Clear();
            }

            IReadOnlyList<ItemDefinition> definitions = ItemDefinitions;
            for (int i = 0; i < definitions.Count; i++)
            {
                ItemDefinition itemDefinition = definitions[i];
                if (itemDefinition == null || itemDefinition.ItemDefId.IsEmpty)
                {
                    continue;
                }

                string id = itemDefinition.ItemDefId.Value;
                if (!lookupById.ContainsKey(id))
                {
                    lookupById.Add(id, itemDefinition);
                }
            }

            lookupDirty = false;
        }

        private void OnValidate()
        {
            lookupDirty = true;
            itemDefinitions ??= new List<ItemDefinition>();
        }

        private void EnsureLookup()
        {
            if (lookupDirty || lookupById == null)
            {
                RebuildLookup();
            }
        }
    }
}
