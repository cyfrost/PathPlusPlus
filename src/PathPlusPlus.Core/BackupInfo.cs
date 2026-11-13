using System;
using Microsoft.Win32;

namespace PathPlusPlus.Core
{
    /// <summary>A snapshot of a variable taken just before Path++ changed it.</summary>
    public sealed class BackupInfo
    {
        public string File { get; set; }
        public DateTime TimestampUtc { get; set; }
        public EnvScope Scope { get; set; }
        public string Name { get; set; }
        public bool Existed { get; set; }
        public RegistryValueKind Kind { get; set; }
        public string Value { get; set; }
    }
}
