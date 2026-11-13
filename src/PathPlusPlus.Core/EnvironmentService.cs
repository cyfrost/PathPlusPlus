using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Win32;

namespace PathPlusPlus.Core
{
    /// <summary>
    /// All environment changes go through here. Every write:
    /// (1) backs up the previous value, (2) preserves REG_EXPAND_SZ so %VARIABLES% keep working,
    /// and (3) broadcasts WM_SETTINGCHANGE so new programs see the change immediately.
    /// </summary>
    public sealed class EnvironmentService
    {
        private readonly IEnvironmentStore _store;
        private readonly IBackupStore _backups;
        private readonly IChangeNotifier _notifier;

        public EnvironmentService(IEnvironmentStore store, IBackupStore backups, IChangeNotifier notifier)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _backups = backups ?? throw new ArgumentNullException(nameof(backups));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public IBackupStore Backups => _backups;

        // ---------------------------------------------------------------- reads

        public string GetRawPath(EnvScope scope) => _store.Read(scope, EnvValidation.PathVariableName)?.Value ?? string.Empty;

        public PathList GetPath(EnvScope scope) => new PathList(GetRawPath(scope));

        public RawValue GetVariable(EnvScope scope, string name) => _store.Read(scope, name);

        public IList<string> GetVariableNames(EnvScope scope) => _store.GetNames(scope);

        // ---------------------------------------------------------------- PATH

        public ChangeResult AddToPath(EnvScope scope, string entry, bool prepend)
        {
            string error = EnvValidation.ValidatePathEntry(entry);
            if (error != null) throw new EnvOperationException(EnvError.InvalidArgument, error);
            entry = PathNormalizer.Clean(entry);

            RawValue before = _store.Read(scope, EnvValidation.PathVariableName);
            var list = new PathList(before?.Value);
            if (!list.Add(entry, prepend))
            {
                return new ChangeResult(false, 0, "'" + entry + "' is already in the " + scope.DisplayName() + " PATH.");
            }

            string backup = WritePath(scope, before, list.ToString());
            return new ChangeResult(true, 1,
                "Added '" + entry + "' to the " + (prepend ? "start" : "end") + " of the " + scope.DisplayName() + " PATH.", backup);
        }

        public ChangeResult RemoveFromPath(EnvScope scope, string entry)
        {
            if (PathNormalizer.Key(entry) == null) throw new EnvOperationException(EnvError.InvalidArgument, "The folder is empty.");

            RawValue before = _store.Read(scope, EnvValidation.PathVariableName);
            var list = new PathList(before?.Value);
            int removed = list.Remove(entry);
            if (removed == 0)
            {
                return new ChangeResult(false, 0, "'" + PathNormalizer.Clean(entry) + "' is not in the " + scope.DisplayName() + " PATH.");
            }

            string backup = WritePath(scope, before, list.ToString());
            return new ChangeResult(true, removed,
                "Removed " + removed + " entr" + (removed == 1 ? "y" : "ies") + " from the " + scope.DisplayName() + " PATH.", backup);
        }

        public ChangeResult DedupePath(EnvScope scope)
        {
            RawValue before = _store.Read(scope, EnvValidation.PathVariableName);
            var list = new PathList(before?.Value);
            int removed = list.RemoveDuplicatesAndEmpty();
            if (removed == 0)
            {
                return new ChangeResult(false, 0, "The " + scope.DisplayName() + " PATH has no duplicate or empty entries.");
            }

            string backup = WritePath(scope, before, list.ToString());
            return new ChangeResult(true, removed,
                "Removed " + removed + " duplicate or empty entr" + (removed == 1 ? "y" : "ies") + " from the " + scope.DisplayName() + " PATH.", backup);
        }

        /// <summary>
        /// Replaces the whole PATH (used by the editor). When <paramref name="expectedCurrent"/> is given, the write
        /// is refused if PATH changed since it was loaded, so changes made by installers are never silently lost.
        /// </summary>
        public ChangeResult SetPath(EnvScope scope, string newValue, string expectedCurrent)
        {
            newValue = newValue ?? string.Empty;
            RawValue before = _store.Read(scope, EnvValidation.PathVariableName);
            string current = before?.Value ?? string.Empty;

            if (expectedCurrent != null && !string.Equals(current, expectedCurrent, StringComparison.Ordinal))
            {
                throw new EnvOperationException(EnvError.Conflict,
                    "The " + scope.DisplayName() + " PATH was changed by another program after Path++ loaded it. Reload and try again.");
            }
            if (string.Equals(current, newValue, StringComparison.Ordinal))
            {
                return new ChangeResult(false, 0, "The " + scope.DisplayName() + " PATH is unchanged.");
            }

            string backup = WritePath(scope, before, newValue);
            return new ChangeResult(true, 1, "Saved the " + scope.DisplayName() + " PATH.", backup);
        }

        // ---------------------------------------------------------------- other variables

        public ChangeResult SetVariable(EnvScope scope, string name, string value, bool force)
        {
            string error = EnvValidation.ValidateName(name) ?? EnvValidation.ValidateValue(value);
            if (error != null) throw new EnvOperationException(EnvError.InvalidArgument, error);

            bool isPath = EnvValidation.IsPathName(name);
            if (isPath && !force)
            {
                throw new EnvOperationException(EnvError.InvalidArgument,
                    "Setting 'Path' directly would replace every entry in it. Use add/remove instead (or --force to really replace it).");
            }
            if (isPath && value.Length > EnvValidation.MaxValueLength)
            {
                throw new EnvOperationException(EnvError.InvalidArgument, "PATH would exceed " + EnvValidation.MaxValueLength + " characters.");
            }

            RawValue before = _store.Read(scope, name);
            RegistryValueKind kind = ChooseKind(name, value, before);
            if (before != null && before.Value == value && before.Kind == kind)
            {
                return new ChangeResult(false, 0, name + " already has that value in the " + scope.DisplayName() + " environment.");
            }

            string backup = _backups.Save(scope, name, before);
            _store.Write(scope, name, value, kind);
            _notifier.EnvironmentChanged();
            return new ChangeResult(true, 1,
                (before == null ? "Created " : "Updated ") + scope.DisplayName() + " variable " + name + ".", backup);
        }

        public ChangeResult DeleteVariable(EnvScope scope, string name, bool force)
        {
            string error = EnvValidation.ValidateName(name);
            if (error != null) throw new EnvOperationException(EnvError.InvalidArgument, error);
            if (EnvValidation.IsPathName(name) && !force)
            {
                throw new EnvOperationException(EnvError.InvalidArgument, "Refusing to delete 'Path' (use --force if you really mean it).");
            }

            RawValue before = _store.Read(scope, name);
            if (before == null)
            {
                return new ChangeResult(false, 0, name + " is not defined in the " + scope.DisplayName() + " environment.");
            }

            string backup = _backups.Save(scope, name, before);
            _store.Delete(scope, name);
            _notifier.EnvironmentChanged();
            return new ChangeResult(true, 1, "Deleted " + scope.DisplayName() + " variable " + name + ".", backup);
        }

        // ---------------------------------------------------------------- backups

        public ChangeResult Restore(string backupFile)
        {
            BackupInfo info;
            try
            {
                info = _backups.Load(backupFile);
            }
            catch (Exception ex) when (!(ex is EnvOperationException))
            {
                throw new EnvOperationException(EnvError.InvalidArgument, "Can't read backup: " + ex.Message, ex);
            }

            if (info.Scope == EnvScope.Machine && !_backups.IsInStore(info.File, EnvScope.Machine))
            {
                throw new EnvOperationException(EnvError.NotPermitted,
                    "System backups can only be restored from " + _backups.GetDirectory(EnvScope.Machine));
            }
            string error = EnvValidation.ValidateName(info.Name);
            if (error != null) throw new EnvOperationException(EnvError.InvalidArgument, error);

            RawValue current = _store.Read(info.Scope, info.Name);
            bool same = info.Existed
                ? current != null && current.Value == info.Value && current.Kind == info.Kind
                : current == null;
            if (same)
            {
                return new ChangeResult(false, 0, info.Name + " already matches that backup.");
            }

            string backup = _backups.Save(info.Scope, info.Name, current);
            if (info.Existed)
            {
                RegistryValueKind kind = info.Kind == RegistryValueKind.ExpandString ? RegistryValueKind.ExpandString : RegistryValueKind.String;
                _store.Write(info.Scope, info.Name, info.Value, kind);
            }
            else
            {
                _store.Delete(info.Scope, info.Name);
            }
            _notifier.EnvironmentChanged();
            return new ChangeResult(true, 1,
                "Restored " + info.Scope.DisplayName() + " variable " + info.Name + " to its state at " + info.TimestampUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) + ".", backup);
        }

        // ---------------------------------------------------------------- helpers

        private string WritePath(EnvScope scope, RawValue before, string newValue)
        {
            if (newValue.Length > EnvValidation.MaxValueLength)
            {
                throw new EnvOperationException(EnvError.InvalidArgument,
                    "The " + scope.DisplayName() + " PATH would be " + newValue.Length + " characters, above the Windows limit of " + EnvValidation.MaxValueLength + ".");
            }

            // Keep the original value name casing ("Path" vs "PATH") — registry names are case-insensitive.
            string backup = _backups.Save(scope, EnvValidation.PathVariableName, before);
            // PATH is always REG_EXPAND_SZ, matching Windows itself. This also repairs a PATH that an older tool flattened to REG_SZ.
            _store.Write(scope, EnvValidation.PathVariableName, newValue, RegistryValueKind.ExpandString);
            _notifier.EnvironmentChanged();
            return backup;
        }

        private static RegistryValueKind ChooseKind(string name, string value, RawValue before)
        {
            if (EnvValidation.IsPathName(name) || value.IndexOf('%') >= 0) return RegistryValueKind.ExpandString;
            if (before != null && before.Kind == RegistryValueKind.ExpandString) return RegistryValueKind.ExpandString;
            return RegistryValueKind.String;
        }
    }
}
