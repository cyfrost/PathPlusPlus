using System;
using System.Collections.Generic;
using System.Security;
using Microsoft.Win32;

namespace PathPlusPlus.Core
{
    /// <summary>A raw registry value: the unexpanded text plus its registry type.</summary>
    public sealed class RawValue
    {
        public RawValue(string value, RegistryValueKind kind)
        {
            Value = value ?? string.Empty;
            Kind = kind;
        }

        public string Value { get; }

        /// <summary>REG_SZ (String) or REG_EXPAND_SZ (ExpandString).</summary>
        public RegistryValueKind Kind { get; }
    }

    /// <summary>Persistent storage for environment variables (the registry, or a fake in tests).</summary>
    public interface IEnvironmentStore
    {
        /// <summary>Returns the raw, unexpanded value, or null when the variable does not exist.</summary>
        RawValue Read(EnvScope scope, string name);

        void Write(EnvScope scope, string name, string value, RegistryValueKind kind);

        void Delete(EnvScope scope, string name);

        IList<string> GetNames(EnvScope scope);
    }

    /// <summary>
    /// Reads and writes the real environment registry keys.
    /// Values are always read with <see cref="RegistryValueOptions.DoNotExpandEnvironmentNames"/> and
    /// written with an explicit <see cref="RegistryValueKind"/>, so %VARIABLES% are never flattened.
    /// </summary>
    public sealed class RegistryEnvironmentStore : IEnvironmentStore
    {
        internal const string MachineKeyPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
        internal const string UserKeyPath = "Environment";

        public RawValue Read(EnvScope scope, string name)
        {
            return Guard(scope, () =>
            {
                using (var key = Open(scope, writable: false))
                {
                    if (key == null) return null;
                    object value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (value == null) return null;
                    RegistryValueKind kind = key.GetValueKind(name);
                    string text = value as string;
                    if (text == null)
                    {
                        text = value is string[] multi ? string.Join(";", multi) : Convert.ToString(value);
                    }
                    return new RawValue(text, kind);
                }
            });
        }

        public void Write(EnvScope scope, string name, string value, RegistryValueKind kind)
        {
            if (kind != RegistryValueKind.String && kind != RegistryValueKind.ExpandString)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Environment variables must be REG_SZ or REG_EXPAND_SZ.");
            }

            Guard<object>(scope, () =>
            {
                using (var key = Open(scope, writable: true))
                {
                    key.SetValue(name, value, kind);
                }
                return null;
            });
        }

        public void Delete(EnvScope scope, string name)
        {
            Guard<object>(scope, () =>
            {
                using (var key = Open(scope, writable: true))
                {
                    key.DeleteValue(name, throwOnMissingValue: false);
                }
                return null;
            });
        }

        public IList<string> GetNames(EnvScope scope)
        {
            return Guard(scope, () =>
            {
                using (var key = Open(scope, writable: false))
                {
                    return key == null ? (IList<string>)new string[0] : key.GetValueNames();
                }
            });
        }

        private static RegistryKey Open(EnvScope scope, bool writable)
        {
            RegistryHive hive = scope == EnvScope.Machine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
            string path = scope == EnvScope.Machine ? MachineKeyPath : UserKeyPath;
            using (RegistryKey root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
            {
                RegistryKey key = root.OpenSubKey(path, writable);
                if (key == null && writable && scope == EnvScope.User)
                {
                    key = root.CreateSubKey(path);
                }
                if (key == null && writable)
                {
                    throw new EnvOperationException(EnvError.Failed, "Registry key not found: " + scope.RegistryLocation());
                }
                return key;
            }
        }

        private static T Guard<T>(EnvScope scope, Func<T> action)
        {
            try
            {
                return action();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is SecurityException)
            {
                throw new EnvOperationException(
                    EnvError.NotPermitted,
                    scope == EnvScope.Machine
                        ? "Administrator rights are required to change system environment variables."
                        : "Access to the user environment was denied.",
                    ex);
            }
        }
    }
}
