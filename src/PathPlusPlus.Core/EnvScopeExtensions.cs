using System;

namespace PathPlusPlus.Core
{
    public static class EnvScopeExtensions
    {
        /// <summary>Human-friendly name used in messages ("user" / "system").</summary>
        public static string DisplayName(this EnvScope scope) => scope == EnvScope.Machine ? "system" : "user";

        public static string RegistryLocation(this EnvScope scope) =>
            scope == EnvScope.Machine
                ? @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment"
                : @"HKCU\Environment";

        public static EnvScope Parse(string text)
        {
            if (string.Equals(text, "User", StringComparison.OrdinalIgnoreCase)) return EnvScope.User;
            if (string.Equals(text, "Machine", StringComparison.OrdinalIgnoreCase)) return EnvScope.Machine;
            throw new FormatException("Unknown scope '" + text + "'.");
        }
    }
}
