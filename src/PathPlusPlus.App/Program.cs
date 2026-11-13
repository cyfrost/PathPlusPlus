using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using PathPlusPlus.Core;

namespace PathPlusPlus.App
{
    internal static class Program
    {
        internal const string Usage =
            "PathPlusPlus.exe                              Open the PATH editor\n" +
            "PathPlusPlus.exe --add-to-path [--parent] <path>\n" +
            "PathPlusPlus.exe --remove-from-path [--parent] <path>\n" +
            "PathPlusPlus.exe --new-variable [--parent] <path>\n" +
            "PathPlusPlus.exe --env-dialog                 Open the Windows dialog\n" +
            "PathPlusPlus.exe --register-shell [--all-users]\n" +
            "PathPlusPlus.exe --unregister-shell [--all-users]\n\n" +
            "--parent uses the folder containing <path>.\n" +
            "For scripting, use pathpp.exe (run 'pathpp help').";

        [STAThread]
        private static int Main(string[] args)
        {
            // Elevated helper for system-wide changes (no UI).
            if (ElevatedWorker.IsWorkerInvocation(args))
            {
                return ElevatedWorker.Run(args, EnvironmentServices.CreateService(), new WindowsSessionInfo());
            }

            // Installer hooks (no UI, exit code only).
            if (args.Length > 0 && (args[0] == "--register-shell" || args[0] == "--unregister-shell"))
            {
                return RunShellRegistration(args);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Ui.Error(null, "Unexpected error: " + e.Exception.Message);

            try
            {
                return Dispatch(args);
            }
            catch (Exception ex)
            {
                Ui.Error(null, "Unexpected error: " + ex.Message);
                return 1;
            }
        }

        private static int Dispatch(string[] args)
        {
            OperationRunner runner = EnvironmentServices.CreateRunner();
            if (args.Length == 0)
            {
                using (SingleInstance editor = SingleInstance.TryAcquire("PathPlusPlus.Editor"))
                {
                    Application.Run(new MainForm(runner));
                }
                return 0;
            }

            string command = args[0].ToLowerInvariant();
            if (command == "--env-dialog")
            {
                Ui.OpenWindowsEnvironmentDialog();
                return 0;
            }
            if (command == "--help" || command == "-h" || command == "/?")
            {
                Ui.Info(null, Usage);
                return 0;
            }

            string error = ResolveTarget(args, File.Exists, out string target);
            if (error != null)
            {
                Ui.Error(null, error + "\n\n" + Usage);
                return 2;
            }

            switch (command)
            {
                case "--add-to-path":
                    Application.Run(new AddToPathDialog(runner, target));
                    return 0;
                case "--remove-from-path":
                    RemoveFromPathDialog.Run(runner, target);
                    return 0;
                default:
                    Application.Run(new NewVariableDialog(runner, target));
                    return 0;
            }
        }

        /// <summary>
        /// Works out which path a dialog should use from an Explorer-style invocation.
        /// PATH holds folders, so for PATH commands a file (or --parent) resolves to its containing folder.
        /// Returns an error message, or null on success.
        /// </summary>
        internal static string ResolveTarget(string[] args, Func<string, bool> fileExists, out string target)
        {
            target = null;
            string command = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;
            bool forPath = command == "--add-to-path" || command == "--remove-from-path";
            if (!forPath && command != "--new-variable") return "Unknown option '" + (args.Length > 0 ? args[0] : string.Empty) + "'.";

            List<string> rest = args.Skip(1).ToList();
            bool parent = rest.Remove("--parent");
            if (rest.Count != 1) return "Expected one path after " + args[0] + ".";

            string path = PathNormalizer.SanitizeArgument(rest[0]);
            if (string.IsNullOrEmpty(path)) return "No path was given.";

            if (parent || (forPath && fileExists(path)))
                target = PathNormalizer.ParentDirectory(path);
            else
                target = forPath ? PathNormalizer.TrimTrailingSeparators(path) : path;
            return null;
        }

        private static int RunShellRegistration(string[] args)
        {
            bool allUsers = args.Skip(1).Any(a => string.Equals(a, "--all-users", StringComparison.OrdinalIgnoreCase));
            try
            {
                if (args[0] == "--register-shell")
                    ShellIntegration.Register(allUsers, Application.ExecutablePath);
                else
                    ShellIntegration.Unregister(allUsers);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ex is UnauthorizedAccessException || ex is System.Security.SecurityException ? (int)EnvError.NotPermitted : 1;
            }
        }
    }

    /// <summary>Keeps a single PATH editor open: a second launch brings the first one forward.</summary>
    internal sealed class SingleInstance : IDisposable
    {
        private readonly Mutex _mutex;

        private SingleInstance(Mutex mutex)
        {
            _mutex = mutex;
        }

        public static SingleInstance TryAcquire(string name)
        {
            var mutex = new Mutex(true, @"Local\" + name, out bool created);
            if (!created)
            {
                mutex.Dispose();
                BringExistingToFront();
                Environment.Exit(0);
            }
            return new SingleInstance(mutex);
        }

        private static void BringExistingToFront()
        {
            var current = System.Diagnostics.Process.GetCurrentProcess();
            foreach (var p in System.Diagnostics.Process.GetProcessesByName(current.ProcessName))
            {
                if (p.Id == current.Id || p.MainWindowHandle == IntPtr.Zero) continue;
                if (!p.MainWindowTitle.StartsWith("Path++ - PATH editor", StringComparison.Ordinal)) continue;
                ShowWindow(p.MainWindowHandle, 9 /* SW_RESTORE */);
                SetForegroundWindow(p.MainWindowHandle);
                break;
            }
        }

        public void Dispose()
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not owned.
            }
            _mutex.Dispose();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int cmd);
    }
}
