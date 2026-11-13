namespace PathPlusPlus.Core
{
    public interface ISessionInfo
    {
        /// <summary>True when this process has an elevated administrator token.</summary>
        bool IsElevated { get; }

        /// <summary>
        /// True when this process runs as a different account from the person signed in to the desktop —
        /// e.g. a standard user approved a UAC prompt with an IT admin's credentials. In that case HKCU
        /// belongs to the admin, so user-scope changes would land in the wrong profile.
        /// </summary>
        bool IsDifferentUserThanDesktop { get; }

        string UserName { get; }
    }
}
