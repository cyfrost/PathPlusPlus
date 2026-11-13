using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PathPlusPlus.Core;

namespace PathPlusPlus.Cli
{
    /// <summary>The pathpp command-line tool. Returns process exit codes (see <see cref="EnvError"/>).</summary>
    internal sealed class CliApp
    {
        internal const int ExitOk = 0;
        internal const int ExitUsage = 2;

        private readonly OperationRunner _runner;
        private readonly TextWriter _out;
        private readonly TextWriter _err;
        private readonly Func<string, bool> _directoryExists;
        private readonly Func<string, string> _getFullPath;

        public CliApp(OperationRunner runner, TextWriter output, TextWriter error,
            Func<string, bool> directoryExists = null, Func<string, string> getFullPath = null)
        {
            _runner = runner;
            _out = output;
            _err = error;
            _directoryExists = directoryExists ?? Directory.Exists;
            _getFullPath = getFullPath ?? Path.GetFullPath;
        }

        private EnvironmentService Service => _runner.Service;

        public int Run(string[] args)
        {
            CliArgs a;
            try
            {
                a = CliArgs.Parse(args);
                if (a.Has("--version")) return Version();
                if (a.Command == null || a.Command == "help")
                {
                    _out.WriteLine(Usage);
                    return a.Command == null && !a.Has("--help") ? ExitUsage : ExitOk;
                }
                if (a.Has("--help"))
                {
                    _out.WriteLine(Usage);
                    return ExitOk;
                }

                switch (a.Command)
                {
                    case "add": return Add(a);
                    case "remove":
                    case "rm": return Remove(a);
                    case "list":
                    case "ls": return List(a);
                    case "dedupe": return Dedupe(a);
                    case "get": return Get(a);
                    case "set": return Set(a);
                    case "unset": return Unset(a);
                    case "vars": return Vars(a);
                    case "backups": return Backups(a);
                    case "restore": return Restore(a);
                    case "version": return Version();
                    default: throw new CliUsageException("Unknown command '" + a.Command + "'. Run 'pathpp help' for the list of commands.");
                }
            }
            catch (CliUsageException ex)
            {
                _err.WriteLine("pathpp: " + ex.Message);
                return ExitUsage;
            }
            catch (EnvOperationException ex)
            {
                _err.WriteLine("pathpp: " + ex.Message);
                return (int)ex.Error;
            }
        }

        // ------------------------------------------------------------------ commands

        private int Add(CliArgs a)
        {
            a.AllowOnly("--user", "--machine", "--prepend", "--force");
            a.ExpectPositionals(1, "add <folder> [--machine] [--prepend] [--force]");
            EnvScope scope = Scope(a);
            string folder = ResolveFolder(a.Positionals[0]);

            if (!a.Has("--force") && folder.IndexOf('%') < 0 && !_directoryExists(folder))
            {
                throw new EnvOperationException(EnvError.InvalidArgument, "Folder not found: " + folder + " (use --force to add it anyway).");
            }

            return Report(_runner.Run(EnvRequest.AddToPath(scope, folder, a.Has("--prepend"))), pathChanged: true);
        }

        private int Remove(CliArgs a)
        {
            a.AllowOnly("--user", "--machine", "--all");
            a.ExpectPositionals(1, "remove <folder> [--machine | --all]");
            string folder = ResolveFolder(a.Positionals[0]);

            if (!a.Has("--all")) return Report(_runner.Run(EnvRequest.RemoveFromPath(Scope(a), folder)), pathChanged: true);
            if (a.Has("--user") || a.Has("--machine")) throw new CliUsageException("--all can't be combined with --user or --machine.");

            int user = Report(_runner.Run(EnvRequest.RemoveFromPath(EnvScope.User, folder)), pathChanged: false);
            int machine = Report(_runner.Run(EnvRequest.RemoveFromPath(EnvScope.Machine, folder)), pathChanged: false);
            int worst = user != ExitOk ? user : machine;
            if (worst == ExitOk) _out.WriteLine(NewTerminalHint);
            return worst;
        }

        private int List(CliArgs a)
        {
            a.AllowOnly("--user", "--machine", "--plain");
            a.ExpectPositionals(0, "list [--user | --machine] [--plain]");
            var scopes = new List<EnvScope>();
            if (a.Has("--user") || !a.Has("--machine")) scopes.Add(EnvScope.User);
            if (a.Has("--machine") || !a.Has("--user")) scopes.Add(EnvScope.Machine);

            PathList user = Service.GetPath(EnvScope.User);
            PathList machine = Service.GetPath(EnvScope.Machine);
            bool plain = a.Has("--plain");

            foreach (EnvScope scope in scopes)
            {
                PathList list = scope == EnvScope.User ? user : machine;
                PathList other = scope == EnvScope.User ? machine : user;
                if (plain)
                {
                    foreach (string entry in list.Entries) _out.WriteLine(PathNormalizer.Clean(entry));
                    continue;
                }

                _out.WriteLine((scope == EnvScope.User ? "User PATH" : "System PATH") + "  (" + scope.RegistryLocation() + ")");
                IList<EntryInfo> infos = PathAnalyzer.Analyze(list.Segments, other.Segments, _directoryExists);
                int n = 0;
                foreach (EntryInfo info in infos)
                {
                    if (info.Status == EntryStatus.Empty) continue;
                    n++;
                    string note = info.Status == EntryStatus.Ok ? string.Empty : "   <- " + info.Describe(scope);
                    _out.WriteLine(string.Format("  {0,3}. {1}{2}", n, info.Text, note));
                }
                if (n == 0) _out.WriteLine("  (empty)");
                _out.WriteLine();
            }
            return ExitOk;
        }

        private int Dedupe(CliArgs a)
        {
            a.AllowOnly("--user", "--machine");
            a.ExpectPositionals(0, "dedupe [--user | --machine]");
            return Report(_runner.Run(EnvRequest.DedupePath(Scope(a))), pathChanged: true);
        }

        private int Get(CliArgs a)
        {
            a.AllowOnly("--user", "--machine");
            a.ExpectPositionals(1, "get <NAME> [--user | --machine]");
            EnvScope scope = Scope(a);
            RawValue value = Service.GetVariable(scope, a.Positionals[0]);
            if (value == null)
            {
                _err.WriteLine("pathpp: " + a.Positionals[0] + " is not defined in the " + scope.DisplayName() + " environment.");
                return (int)EnvError.NotFound;
            }
            _out.WriteLine(value.Value);
            return ExitOk;
        }

        private int Set(CliArgs a)
        {
            a.AllowOnly("--user", "--machine", "--force");
            a.ExpectPositionals(2, "set <NAME> <value> [--machine] [--force]");
            return Report(_runner.Run(EnvRequest.SetVariable(Scope(a), a.Positionals[0], a.Positionals[1], a.Has("--force"))), pathChanged: false);
        }

        private int Unset(CliArgs a)
        {
            a.AllowOnly("--user", "--machine", "--force");
            a.ExpectPositionals(1, "unset <NAME> [--machine] [--force]");
            return Report(_runner.Run(EnvRequest.DeleteVariable(Scope(a), a.Positionals[0], a.Has("--force"))), pathChanged: false);
        }

        private int Vars(CliArgs a)
        {
            a.AllowOnly("--user", "--machine");
            a.ExpectPositionals(0, "vars [--user | --machine]");
            EnvScope scope = Scope(a);
            foreach (string name in Service.GetVariableNames(scope).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                RawValue v = Service.GetVariable(scope, name);
                if (v != null) _out.WriteLine(name + "=" + v.Value);
            }
            return ExitOk;
        }

        private int Backups(CliArgs a)
        {
            a.AllowOnly();
            a.ExpectPositionals(0, "backups");
            IList<BackupInfo> backups = Service.Backups.List();
            if (backups.Count == 0)
            {
                _out.WriteLine("No backups yet.");
                return ExitOk;
            }
            for (int i = 0; i < backups.Count; i++)
            {
                BackupInfo b = backups[i];
                _out.WriteLine(string.Format("  {0,3}. {1:yyyy-MM-dd HH:mm:ss}  {2,-6}  {3}{4}",
                    i + 1, b.TimestampUtc.ToLocalTime(), b.Scope.DisplayName(), b.Name, b.Existed ? string.Empty : "  (did not exist)"));
                _out.WriteLine("       " + b.File);
            }
            _out.WriteLine();
            _out.WriteLine("Restore with: pathpp restore <number | file>");
            return ExitOk;
        }

        private int Restore(CliArgs a)
        {
            a.AllowOnly();
            a.ExpectPositionals(1, "restore <number | backup-file>");
            string target = a.Positionals[0];
            BackupInfo info;
            if (int.TryParse(target, out int index))
            {
                IList<BackupInfo> backups = Service.Backups.List();
                if (index < 1 || index > backups.Count) throw new CliUsageException("There is no backup number " + index + ". Run 'pathpp backups'.");
                info = backups[index - 1];
            }
            else
            {
                try
                {
                    info = Service.Backups.Load(target);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException || ex is FormatException || ex is ArgumentException)
                {
                    throw new EnvOperationException(EnvError.InvalidArgument, "Can't read backup: " + ex.Message);
                }
            }
            return Report(_runner.Run(EnvRequest.Restore(info.Scope, info.File)), pathChanged: EnvValidation.IsPathName(info.Name));
        }

        private int Version()
        {
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            _out.WriteLine("pathpp " + v.Major + "." + v.Minor + "." + v.Build);
            return ExitOk;
        }

        // ------------------------------------------------------------------ helpers

        private const string NewTerminalHint = "Open a new terminal window to use the updated PATH.";

        private int Report(EnvResponse response, bool pathChanged)
        {
            if (!response.Success)
            {
                _err.WriteLine("pathpp: " + response.Message);
                return (int)response.Error;
            }
            _out.WriteLine(response.Message);
            if (pathChanged && response.Changed) _out.WriteLine(NewTerminalHint);
            return ExitOk;
        }

        private static EnvScope Scope(CliArgs a)
        {
            if (a.Has("--user") && a.Has("--machine")) throw new CliUsageException("Choose either --user or --machine, not both.");
            return a.Has("--machine") ? EnvScope.Machine : EnvScope.User;
        }

        /// <summary>Makes relative folders absolute (so "pathpp add ." works) and strips trailing slashes.</summary>
        internal string ResolveFolder(string argument)
        {
            string folder = PathNormalizer.SanitizeArgument(argument);
            if (folder.Length == 0) throw new CliUsageException("The folder is empty.");
            if (folder.IndexOf('%') < 0 && !PathNormalizer.IsFullyQualified(folder))
            {
                try
                {
                    folder = _getFullPath(folder);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
                {
                    throw new CliUsageException("Invalid folder '" + argument + "': " + ex.Message);
                }
            }
            return PathNormalizer.TrimTrailingSeparators(folder.Replace('/', '\\'));
        }

        internal const string Usage =
@"Path++ - manage PATH and environment variables safely.

Usage: pathpp <command> [arguments] [options]

PATH commands
  add <folder>        Add a folder to PATH (skipped if it's already there)
      --machine         system PATH for all users (asks for admin approval)
      --prepend         put it first so it wins over other folders
      --force           add even if the folder doesn't exist yet
  remove <folder>     Remove every copy of a folder from PATH
      --machine         system PATH;  --all  both user and system
  list                Show user and system PATH with problems flagged
      --user | --machine   only one of them;  --plain  entries only
  dedupe              Remove duplicate and empty entries  (--machine)

Variable commands
  get <NAME>          Print a variable's raw value         (--machine)
  set <NAME> <value>  Create or update a variable          (--machine)
  unset <NAME>        Delete a variable                    (--machine)
  vars                List variables                       (--machine)

Safety
  backups             List automatic backups (taken before every change)
  restore <n | file>  Restore a backup

Without --machine, commands act on your user environment (no admin needed).
Changes take effect in programs started afterwards (open a new terminal).

Exit codes: 0 ok, 1 failed, 2 invalid input, 3 not found, 4 cancelled,
            5 not permitted, 6 changed by another program";
    }
}
