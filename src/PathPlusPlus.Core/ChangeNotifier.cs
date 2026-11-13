using System;
using System.Runtime.InteropServices;

namespace PathPlusPlus.Core
{
    public interface IChangeNotifier
    {
        void EnvironmentChanged();
    }

    /// <summary>
    /// Broadcasts WM_SETTINGCHANGE("Environment") so Explorer and other programs reload the environment.
    /// Without this, new terminals opened from Explorer keep the old PATH until the user signs out.
    /// </summary>
    public sealed class SettingChangeBroadcaster : IChangeNotifier
    {
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, string lParam, uint flags, uint timeoutMs, out UIntPtr result);

        public void EnvironmentChanged()
        {
            SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, "Environment", SMTO_ABORTIFHUNG, 5000, out _);
        }
    }
}
