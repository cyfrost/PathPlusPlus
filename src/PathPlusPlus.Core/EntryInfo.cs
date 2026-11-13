using System.Collections.Generic;

namespace PathPlusPlus.Core
{
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
            if (Status == EntryStatus.Ok) return string.Empty;
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
}
