using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VRGame.Items
{
    [Serializable]
    public sealed class ItemCategoryPath : ISerializationCallbackReceiver
    {
        private static readonly string[] EmptySegments = new string[0];

        [SerializeField]
        private List<string> segments = new List<string>();

        [NonSerialized]
        private string normalizedPath;

        [NonSerialized]
        private bool cacheDirty = true;

        public IReadOnlyList<string> Segments
        {
            get { return segments != null ? (IReadOnlyList<string>)segments : EmptySegments; }
        }

        public int Depth
        {
            get { return segments != null ? segments.Count : 0; }
        }

        public bool IsEmpty
        {
            get { return Depth == 0 || string.IsNullOrEmpty(NormalizedPath); }
        }

        public bool IsValid
        {
            get
            {
                if (segments == null || segments.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < segments.Count; i++)
                {
                    if (!IsValidSegment(segments[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public string NormalizedPath
        {
            get
            {
                if (cacheDirty)
                {
                    normalizedPath = BuildNormalizedPath();
                    cacheDirty = false;
                }

                return normalizedPath;
            }
        }

        public static ItemCategoryPath FromPath(string path)
        {
            ItemCategoryPath categoryPath = new ItemCategoryPath();
            categoryPath.SetFromPath(path);
            return categoryPath;
        }

        public void SetFromPath(string path)
        {
            EnsureSegments();
            segments.Clear();

            if (string.IsNullOrWhiteSpace(path))
            {
                MarkDirty();
                return;
            }

            string[] rawSegments = path.Split(new[] { '>' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < rawSegments.Length; i++)
            {
                string segment = NormalizeSegment(rawSegments[i]);
                if (!string.IsNullOrEmpty(segment))
                {
                    segments.Add(segment);
                }
            }

            MarkDirty();
        }

        public bool StartsWith(ItemCategoryPath parent)
        {
            if (parent == null || parent.Depth == 0)
            {
                return true;
            }

            if (Depth < parent.Depth)
            {
                return false;
            }

            for (int i = 0; i < parent.Depth; i++)
            {
                if (!StableIdUtility.EqualsNormalized(segments[i], parent.segments[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public bool EqualsPath(ItemCategoryPath other)
        {
            if (other == null)
            {
                return false;
            }

            return StableIdUtility.EqualsNormalized(NormalizedPath, other.NormalizedPath);
        }

        public override string ToString()
        {
            return NormalizedPath;
        }

        public void OnBeforeSerialize()
        {
            NormalizeSegmentsInPlace();
        }

        public void OnAfterDeserialize()
        {
            MarkDirty();
        }

        public static bool IsValidSegment(string segment)
        {
            string normalized = NormalizeSegment(segment);
            return !string.IsNullOrEmpty(normalized) && normalized.IndexOf('>') < 0;
        }

        public static string NormalizeSegment(string segment)
        {
            return string.IsNullOrWhiteSpace(segment) ? string.Empty : segment.Trim();
        }

        private string BuildNormalizedPath()
        {
            if (segments == null || segments.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < segments.Count; i++)
            {
                string segment = NormalizeSegment(segments[i]);
                if (string.IsNullOrEmpty(segment))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(" > ");
                }

                builder.Append(segment);
            }

            return builder.ToString();
        }

        private void EnsureSegments()
        {
            if (segments == null)
            {
                segments = new List<string>();
            }
        }

        private void NormalizeSegmentsInPlace()
        {
            EnsureSegments();

            for (int i = segments.Count - 1; i >= 0; i--)
            {
                string normalized = NormalizeSegment(segments[i]);
                if (string.IsNullOrEmpty(normalized))
                {
                    segments.RemoveAt(i);
                }
                else
                {
                    segments[i] = normalized;
                }
            }

            MarkDirty();
        }

        private void MarkDirty()
        {
            cacheDirty = true;
        }
    }
}
