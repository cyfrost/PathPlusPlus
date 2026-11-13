using System;
using System.Collections.Generic;
using System.IO;
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

    internal sealed class FakeNotifier : IChangeNotifier
    {
        public int Count { get; private set; }

        public void EnvironmentChanged() => Count++;
    }

    internal sealed class FakeSession : ISessionInfo
    {
        public bool IsElevated { get; set; }
        public bool IsDifferentUserThanDesktop { get; set; }
        public string UserName { get; set; } = "tester";
    }

    internal sealed class FakeElevator : IElevator
    {
        public List<EnvRequest> Requests { get; } = new List<EnvRequest>();
        public EnvResponse Response { get; set; } = new EnvResponse { Success = true, Changed = true, Message = "elevated ok" };

        public EnvResponse RunElevated(EnvRequest request)
        {
            Requests.Add(request);
            return Response;
        }
    }

    /// <summary>A service wired to fakes plus a temporary backup folder.</summary>
    internal sealed class TestEnv : IDisposable
    {
        public TestEnv()
        {
            Root = Path.Combine(Path.GetTempPath(), "PathPlusPlusTests-" + Guid.NewGuid().ToString("N"));
            Backups = new FileBackupStore(Path.Combine(Root, "user"), Path.Combine(Root, "machine"));
            Service = new EnvironmentService(Store, Backups, Notifier);
        }

        public string Root { get; }
        public FakeStore Store { get; } = new FakeStore();
        public FakeNotifier Notifier { get; } = new FakeNotifier();
        public FileBackupStore Backups { get; }
        public EnvironmentService Service { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
