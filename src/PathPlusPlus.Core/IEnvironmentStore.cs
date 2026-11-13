using System.Collections.Generic;
using Microsoft.Win32;

namespace PathPlusPlus.Core
{
    /// <summary>Persistent storage for environment variables (the registry, or a fake in tests).</summary>
    public interface IEnvironmentStore
    {
        /// <summary>Returns the raw, unexpanded value, or null when the variable does not exist.</summary>
        RawValue Read(EnvScope scope, string name);

        void Write(EnvScope scope, string name, string value, RegistryValueKind kind);

        void Delete(EnvScope scope, string name);

        IList<string> GetNames(EnvScope scope);
    }
}
