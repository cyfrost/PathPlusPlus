using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            catch (Exception ex) when (!(ex is OutOfMemoryException))
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
                using (SingleInstance instance = SingleInstance.TryAcquire("PathPlusPlus.Editor", Ui.Title(Ui.EditorTitle)))
                {
                    if (instance == null) return 0;
                    using (var editor = new MainForm(runner))
                    {
                        Application.Run(editor);
                    }
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
                    using (var dialog = new AddToPathDialog(runner, target)) Application.Run(dialog);
                    return 0;
                case "--remove-from-path":
                    RemoveFromPathDialog.Run(runner, target);
                    return 0;
                default:
                    using (var dialog = new NewVariableDialog(runner, target)) Application.Run(dialog);
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
            {
                target = PathNormalizer.ParentDirectory(path);
            }
            else
            {
                target = forPath ? PathNormalizer.TrimTrailingSeparators(path) : path;
            }
            return null;
        }

        private static int RunShellRegistration(string[] args)
        {
            bool allUsers = args.Skip(1).Any(a => string.Equals(a, "--all-users", StringComparison.OrdinalIgnoreCase));
            try
            {
                if (args[0] == "--register-shell")
                {
                    ShellIntegration.Register(allUsers, Application.ExecutablePath);
                }
                else
                {
                    ShellIntegration.Unregister(allUsers);
                }
                return 0;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            {
                return (int)EnvError.NotPermitted;
            }
            catch (IOException)
            {
                return (int)EnvError.Failed;
            }
        }
    }
}
