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
}
