using System;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace PathPlusPlus.Core
{
    public sealed class WindowsSessionInfo : ISessionInfo
    {
        private readonly Lazy<bool> _elevated;
        private readonly Lazy<bool> _differentUser;

        public WindowsSessionInfo()
        {
            _elevated = new Lazy<bool>(ComputeElevated);
            _differentUser = new Lazy<bool>(ComputeDifferentUser);
        }

        public bool IsElevated => _elevated.Value;

        public bool IsDifferentUserThanDesktop => _differentUser.Value;

        public string UserName => Environment.UserName;

        private static bool ComputeElevated()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private static bool ComputeDifferentUser()
        {
            SecurityIdentifier desktop = GetDesktopUserSid();
            if (desktop == null) return false; // Unknown (no shell running, e.g. during setup) — don't block.
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                return identity.User != null && !identity.User.Equals(desktop);
            }
        }

        /// <summary>The account that owns the desktop shell (explorer.exe) in this session.</summary>
        internal static SecurityIdentifier GetDesktopUserSid()
        {
            IntPtr shell = GetShellWindow();
            if (shell == IntPtr.Zero) return null;
            if (GetWindowThreadProcessId(shell, out uint pid) == 0) return null;
            if (pid == 0) return null;

            IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (process == IntPtr.Zero) return null;
            try
            {
                if (!OpenProcessToken(process, TOKEN_QUERY, out IntPtr token)) return null;
                try
                {
                    using (var identity = new WindowsIdentity(token))
                    {
                        return identity.User;
                    }
                }
                finally
                {
                    CloseHandle(token);
                }
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                CloseHandle(process);
            }
        }

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint TOKEN_QUERY = 0x0008;

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
