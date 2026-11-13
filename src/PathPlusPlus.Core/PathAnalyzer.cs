using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PathPlusPlus.Core
{
    [Flags]
    public enum EntryStatus
    {
        Ok = 0,
        Empty = 1,
        Missing = 2,
        Duplicate = 4,
        AlsoInOtherScope = 8,
        NotAbsolute = 16,
        UnresolvedVariable = 32,
    }

    public sealed class EntryInfo
    {
        public EntryInfo(int index, string text, EntryStatus status, string expanded)
        {
            Index = index;
            Text = text;
            Status = status;
            Expanded = expanded;
        }

        public int Index { get; }
        public string Text { get; }
        public string Expanded { get; }
        public EntryStatus Status { get; }

        public string Describe(EnvScope scope)
        {
            if (Status == EntryStatus.Ok) return "OK";
            var parts = new List<string>();
            if ((Status & EntryStatus.Empty) != 0) parts.Add("Empty entry");
            if ((Status & EntryStatus.Missing) != 0) parts.Add("Folder not found");
            if ((Status & EntryStatus.UnresolvedVariable) != 0) parts.Add("Unknown %variable%");
            if ((Status & EntryStatus.NotAbsolute) != 0) parts.Add("Relative path");
            if ((Status & EntryStatus.Duplicate) != 0) parts.Add("Duplicate");
            if ((Status & EntryStatus.AlsoInOtherScope) != 0)
                parts.Add(scope == EnvScope.User ? "Also in system PATH" : "Also in user PATH");
            return string.Join(", ", parts);
        }
    }

    /// <summary>Flags problems in PATH entries (missing folders, duplicates, etc.).</summary>
    public static class PathAnalyzer
    {
        public static IList<EntryInfo> Analyze(IReadOnlyList<string> segments, IEnumerable<string> otherScopeSegments, Func<string, bool> directoryExists = null)
        {
            directoryExists = directoryExists ?? Directory.Exists;
            var otherKeys = new HashSet<string>(
                (otherScopeSegments ?? Enumerable.Empty<string>()).Select(PathNormalizer.Key).Where(k => k != null),
                StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<EntryInfo>(segments.Count);

            for (int i = 0; i < segments.Count; i++)
            {
                string text = segments[i];
                string key = PathNormalizer.Key(text);
                EntryStatus status = EntryStatus.Ok;
                string expanded = null;

                if (key == null)
                {
                    status = EntryStatus.Empty;
                }
                else
                {
                    expanded = PathNormalizer.Expand(text);
                    if (!seen.Add(key)) status |= EntryStatus.Duplicate;
                    if (otherKeys.Contains(key)) status |= EntryStatus.AlsoInOtherScope;

                    if (PathNormalizer.HasUnresolvedVariable(text))
                    {
                        status |= EntryStatus.UnresolvedVariable;
                    }
                    else if (!PathNormalizer.IsFullyQualified(expanded))
                    {
                        status |= EntryStatus.NotAbsolute;
                    }
                    else if (!SafeExists(directoryExists, expanded))
                    {
                        status |= EntryStatus.Missing;
                    }
                }

                result.Add(new EntryInfo(i, text, status, expanded));
            }

            return result;
        }

        private static bool SafeExists(Func<string, bool> exists, string path)
        {
            try
            {
                return exists(path);
            }
            catch (Exception)
            {
                return true; // Can't tell (e.g. offline network share) — don't flag it.
            }
        }
    }
}
