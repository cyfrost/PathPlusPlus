using System;

namespace PathPlusPlus.Core
{
    /// <summary>Where an environment variable lives.</summary>
    public enum EnvScope
    {
        /// <summary>HKCU\Environment — the signed-in user only. Never needs elevation.</summary>
        User,

        /// <summary>HKLM\...\Session Manager\Environment — all users. Needs administrator rights.</summary>
        Machine,
    }

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

    /// <summary>Categories of failure, mapped to CLI exit codes and UI messages.</summary>
    public enum EnvError
    {
        None = 0,
        Failed = 1,
        InvalidArgument = 2,
        NotFound = 3,
        Cancelled = 4,
        NotPermitted = 5,
        Conflict = 6,
    }

    /// <summary>An expected, user-explainable failure of an environment operation.</summary>
    [Serializable]
    public sealed class EnvOperationException : Exception
    {
        public EnvOperationException(EnvError error, string message) : base(message)
        {
            Error = error;
        }

        public EnvOperationException(EnvError error, string message, Exception inner) : base(message, inner)
        {
            Error = error;
        }

        public EnvError Error { get; }
    }
}
