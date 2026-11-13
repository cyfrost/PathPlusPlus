using System;
using System.Collections.Generic;
using System.Windows.Forms;
using PathPlusPlus.Core;

namespace PathPlusPlus.App
{
    /// <summary>Editor state for one PATH (user or system): the loaded value and the in-memory edits.</summary>
    internal sealed class ScopeTab
    {
        public ScopeTab(EnvScope scope, string title, bool readOnly)
        {
            Scope = scope;
            Title = title;
            ReadOnly = readOnly;
        }

        public EnvScope Scope { get; }

        public string Title { get; }

        public bool ReadOnly { get; }

        public TabPage Page { get; set; }

        public ListView List { get; set; }

        /// <summary>The raw value as loaded from the registry.</summary>
        public string Original { get; set; } = string.Empty;

        public List<string> Segments { get; set; } = new List<string>();

        public string Current => string.Join(";", Segments);

        public bool Dirty => !string.Equals(Current, Original, StringComparison.Ordinal);
    }
}
