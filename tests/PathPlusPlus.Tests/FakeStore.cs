using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using PathPlusPlus.Core;

namespace PathPlusPlus.Tests
{
    internal sealed class FakeStore : IEnvironmentStore
    {
        private readonly Dictionary<EnvScope, Dictionary<string, RawValue>> _data = new Dictionary<EnvScope, Dictionary<string, RawValue>>
        {
            [EnvScope.User] = new Dictionary<string, RawValue>(StringComparer.OrdinalIgnoreCase),
            [EnvScope.Machine] = new Dictionary<string, RawValue>(StringComparer.OrdinalIgnoreCase),
        };

        public int Writes { get; private set; }

        public FakeStore Set(EnvScope scope, string name, string value, RegistryValueKind kind = RegistryValueKind.ExpandString)
        {
            _data[scope][name] = new RawValue(value, kind);
            return this;
        }

        public RawValue Read(EnvScope scope, string name) => _data[scope].TryGetValue(name, out RawValue v) ? v : null;

        public void Write(EnvScope scope, string name, string value, RegistryValueKind kind)
        {
            Writes++;
            _data[scope][name] = new RawValue(value, kind);
        }

        public void Delete(EnvScope scope, string name)
        {
            Writes++;
            _data[scope].Remove(name);
        }

        public IList<string> GetNames(EnvScope scope) => _data[scope].Keys.ToList();
    }
}
