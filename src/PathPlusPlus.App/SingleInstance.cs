using System;
using System.Diagnostics;
using System.Threading;

namespace PathPlusPlus.App
{
    /// <summary>Keeps a single PATH editor open: a second launch brings the first one forward.</summary>
    internal sealed class SingleInstance : IDisposable
    {
        private readonly Mutex _mutex;

        private SingleInstance(Mutex mutex)
        {
            _mutex = mutex;
        }

        /// <summary>Returns null (after activating the existing window) when another editor is already open.</summary>
        public static SingleInstance TryAcquire(string name, string windowTitle)
        {
            var mutex = new Mutex(true, @"Local\" + name, out bool created);
            if (created) return new SingleInstance(mutex);

            mutex.Dispose();
            BringToFront(windowTitle);
            return null;
        }

        public void Dispose()
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not owned by this thread; nothing to release.
            }
            _mutex.Dispose();
        }

        private static void BringToFront(string windowTitle)
        {
            using (Process current = Process.GetCurrentProcess())
            {
                foreach (Process p in Process.GetProcessesByName(current.ProcessName))
                {
                    using (p)
                    {
                        if (p.Id == current.Id || p.MainWindowHandle == IntPtr.Zero) continue;
                        if (!p.MainWindowTitle.StartsWith(windowTitle, StringComparison.Ordinal)) continue;
                        NativeMethods.ShowWindow(p.MainWindowHandle, NativeMethods.SW_RESTORE);
                        NativeMethods.SetForegroundWindow(p.MainWindowHandle);
                        return;
                    }
                }
            }
        }
    }
}
