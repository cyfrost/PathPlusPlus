using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace PathPlusPlus.Core
{
    /// <summary>
    /// Plain-text backups. User-scope backups go to %LOCALAPPDATA%\Path++\Backups; system-scope backups go to
    /// %ProgramData%\Path++\Backups, which is locked to administrators so a restore can't be tampered with.
    /// </summary>
    public sealed class FileBackupStore : IBackupStore
    {
        private const string Header = "Path++ backup v1";
        private const string Separator = "---";
        private readonly string _userDir;
        private readonly string _machineDir;
        private readonly int _keep;
        private readonly bool _secureMachineDir;

        public FileBackupStore(string userDir, string machineDir, int keep = 100, bool secureMachineDir = false)
        {
            _userDir = userDir;
            _machineDir = machineDir;
            _keep = keep;
            _secureMachineDir = secureMachineDir;
        }

        public static FileBackupStore CreateDefault()
        {
            string user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Path++", "Backups");
            string machine = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Path++", "Backups");
            return new FileBackupStore(user, machine, secureMachineDir: true);
        }

        public string GetDirectory(EnvScope scope) => scope == EnvScope.Machine ? _machineDir : _userDir;

        public string Save(EnvScope scope, string name, RawValue before)
        {
            string dir = GetDirectory(scope);
            Directory.CreateDirectory(dir);
            if (scope == EnvScope.Machine && _secureMachineDir) TrySecureMachineDirectory(dir);

            DateTime now = DateTime.UtcNow;
            string baseName = now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + scope + "-" + SafeFileName(name);
            string file = Path.Combine(dir, baseName + ".txt");
            for (int i = 2; System.IO.File.Exists(file); i++)
            {
                file = Path.Combine(dir, baseName + "-" + i + ".txt");
            }

            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');
            sb.Append("scope=").Append(scope).Append('\n');
            sb.Append("name=").Append(name).Append('\n');
            sb.Append("existed=").Append(before != null ? "true" : "false").Append('\n');
            sb.Append("kind=").Append(before?.Kind ?? RegistryValueKind.String).Append('\n');
            sb.Append("timestamp=").Append(now.ToString("o", CultureInfo.InvariantCulture)).Append('\n');
            sb.Append(Separator).Append('\n');
            sb.Append(before?.Value ?? string.Empty);

            System.IO.File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
            Prune(dir);
            return file;
        }

        public IList<BackupInfo> List()
        {
            var result = new List<BackupInfo>();
            foreach (string dir in new[] { _userDir, _machineDir })
            {
                if (!Directory.Exists(dir)) continue;
                string[] files;
                try
                {
                    files = Directory.GetFiles(dir, "*.txt");
                }
                catch (Exception)
                {
                    continue;
                }
                foreach (string file in files)
                {
                    try
                    {
                        result.Add(Load(file));
                    }
                    catch (Exception)
                    {
                        // Skip unreadable or foreign files.
                    }
                }
            }
            return result.OrderByDescending(b => b.TimestampUtc).ToList();
        }

        public BackupInfo Load(string file)
        {
            string text = System.IO.File.ReadAllText(file, Encoding.UTF8);
            string marker = "\n" + Separator + "\n";
            int split = text.IndexOf(marker, StringComparison.Ordinal);
            if (!text.StartsWith(Header + "\n", StringComparison.Ordinal) || split < 0)
            {
                throw new InvalidDataException("Not a Path++ backup file: " + file);
            }

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in text.Substring(0, split).Split('\n').Skip(1))
            {
                int eq = line.IndexOf('=');
                if (eq > 0) fields[line.Substring(0, eq)] = line.Substring(eq + 1);
            }

            return new BackupInfo
            {
                File = Path.GetFullPath(file),
                Scope = EnvScopeExtensions.Parse(Get(fields, "scope")),
                Name = Get(fields, "name"),
                Existed = string.Equals(Get(fields, "existed"), "true", StringComparison.OrdinalIgnoreCase),
                Kind = (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), Get(fields, "kind"), ignoreCase: true),
                TimestampUtc = DateTime.Parse(Get(fields, "timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime(),
                Value = text.Substring(split + marker.Length),
            };
        }

        public bool IsInStore(string file, EnvScope scope)
        {
            try
            {
                string dir = PathNormalizer.TrimTrailingSeparators(Path.GetFullPath(GetDirectory(scope))) + "\\";
                return Path.GetFullPath(file).StartsWith(dir, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string Get(Dictionary<string, string> fields, string key)
        {
            if (!fields.TryGetValue(key, out string value) || value == null)
            {
                throw new InvalidDataException("Backup file is missing '" + key + "'.");
            }
            return value;
        }

        private void Prune(string dir)
        {
            try
            {
                foreach (string old in Directory.GetFiles(dir, "*.txt").OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal).Skip(_keep))
                {
                    System.IO.File.Delete(old);
                }
            }
            catch (Exception)
            {
                // Pruning is best-effort.
            }
        }

        private static string SafeFileName(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name)
            {
                sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            }
            return sb.Length > 60 ? sb.ToString(0, 60) : sb.ToString();
        }

        /// <summary>Restricts the system backup folder to SYSTEM + Administrators (full) and Users (read).</summary>
        private static void TrySecureMachineDirectory(string dir)
        {
            try
            {
                var security = new DirectorySecurity();
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
                security.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
                Directory.SetAccessControl(dir, security);
            }
            catch (Exception)
            {
                // Not elevated (or not permitted): leave the inherited ACL in place.
            }
        }
    }
}
