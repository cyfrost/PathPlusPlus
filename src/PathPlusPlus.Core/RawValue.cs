using Microsoft.Win32;

namespace PathPlusPlus.Core
{
    /// <summary>A raw registry value: the unexpanded text plus its registry type.</summary>
    public sealed class RawValue
    {
        public RawValue(string value, RegistryValueKind kind)
        {
            Value = value ?? string.Empty;
            Kind = kind;
        }

        public string Value { get; }

        /// <summary>REG_SZ (String) or REG_EXPAND_SZ (ExpandString).</summary>
        public RegistryValueKind Kind { get; }
    }
}
