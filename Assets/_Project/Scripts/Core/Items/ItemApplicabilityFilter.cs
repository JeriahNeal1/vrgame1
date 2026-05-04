using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    [Serializable]
    public sealed class ItemApplicabilityFilter
    {
        [SerializeField]
        private ItemFlags requiredFlags = ItemFlags.None;

        [Tooltip("If set, at least one of these flags must be present.")]
        [SerializeField]
        private ItemFlags anyOfFlags = ItemFlags.None;

        [SerializeField]
        private ItemFlags rejectedFlags = ItemFlags.None;

        [SerializeField]
        private List<ItemCategoryPath> categoryFilters = new List<ItemCategoryPath>();

        [SerializeField]
        private bool includeCategoryDescendants = true;

        public ItemFlags RequiredFlags
        {
            get { return requiredFlags; }
        }

        public ItemFlags AnyOfFlags
        {
            get { return anyOfFlags; }
        }

        public ItemFlags RejectedFlags
        {
            get { return rejectedFlags; }
        }

        public IReadOnlyList<ItemCategoryPath> CategoryFilters
        {
            get { return categoryFilters ?? (IReadOnlyList<ItemCategoryPath>)Array.Empty<ItemCategoryPath>(); }
        }

        public bool IncludeCategoryDescendants
        {
            get { return includeCategoryDescendants; }
        }

        public bool HasRules
        {
            get
            {
                return requiredFlags != ItemFlags.None ||
                       anyOfFlags != ItemFlags.None ||
                       rejectedFlags != ItemFlags.None ||
                       CountValidCategoryFilters() > 0;
            }
        }

        public bool Allows(ItemDefinition itemDefinition)
        {
            return Matches(itemDefinition, true);
        }

        public bool Blocks(ItemDefinition itemDefinition)
        {
            return Matches(itemDefinition, false);
        }

        public bool Matches(ItemDefinition itemDefinition, bool emptyFilterMatches)
        {
            if (itemDefinition == null)
            {
                return false;
            }

            if (!HasRules)
            {
                return emptyFilterMatches;
            }

            ItemFlags itemFlags = itemDefinition.Flags;
            if (requiredFlags != ItemFlags.None && (itemFlags & requiredFlags) != requiredFlags)
            {
                return false;
            }

            if (anyOfFlags != ItemFlags.None && (itemFlags & anyOfFlags) == 0)
            {
                return false;
            }

            if (rejectedFlags != ItemFlags.None && (itemFlags & rejectedFlags) != 0)
            {
                return false;
            }

            if (CountValidCategoryFilters() == 0)
            {
                return true;
            }

            ItemCategoryPath itemCategory = itemDefinition.CategoryPath;
            if (itemCategory == null || itemCategory.IsEmpty)
            {
                return false;
            }

            IReadOnlyList<ItemCategoryPath> filters = CategoryFilters;
            for (int i = 0; i < filters.Count; i++)
            {
                ItemCategoryPath filter = filters[i];
                if (filter == null || filter.IsEmpty)
                {
                    continue;
                }

                if (includeCategoryDescendants && itemCategory.StartsWith(filter))
                {
                    return true;
                }

                if (!includeCategoryDescendants && itemCategory.EqualsPath(filter))
                {
                    return true;
                }
            }

            return false;
        }

        private int CountValidCategoryFilters()
        {
            IReadOnlyList<ItemCategoryPath> filters = CategoryFilters;
            int count = 0;
            for (int i = 0; i < filters.Count; i++)
            {
                if (filters[i] != null && !filters[i].IsEmpty)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
