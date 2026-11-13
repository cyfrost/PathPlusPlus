using System;
using System.IO;
using PathPlusPlus.Core;

namespace PathPlusPlus.Tests
{
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
