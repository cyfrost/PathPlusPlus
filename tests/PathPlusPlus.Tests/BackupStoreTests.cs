using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using PathPlusPlus.Core;
using Xunit;

namespace PathPlusPlus.Tests
{
    public class BackupStoreTests
    {
        [Fact]
        public void Values_with_newlines_and_separators_survive()
        {
            using (var env = new TestEnv())
            {
                const string value = "line1\n---\nline3;%X%\r\n";
                string file = env.Backups.Save(EnvScope.User, "ODD", new RawValue(value, RegistryValueKind.ExpandString));
                BackupInfo b = env.Backups.Load(file);
                Assert.Equal(value, b.Value);
                Assert.Equal("ODD", b.Name);
                Assert.True(b.Existed);
                Assert.Equal(RegistryValueKind.ExpandString, b.Kind);
                Assert.Equal(EnvScope.User, b.Scope);
            }
        }

        [Fact]
        public void Old_backups_are_pruned()
        {
            using (var env = new TestEnv())
            {
                var store = new FileBackupStore(Path.Combine(env.Root, "u"), Path.Combine(env.Root, "m"), keep: 3);
                for (int i = 0; i < 6; i++) store.Save(EnvScope.User, "X", new RawValue(i.ToString(CultureInfo.InvariantCulture), RegistryValueKind.String));
                var list = store.List();
                Assert.Equal(3, list.Count);
                Assert.Equal(new[] { "5", "4", "3" }, list.Select(b => b.Value).ToArray());
            }
        }

        [Fact]
        public void Foreign_files_are_ignored()
        {
            using (var env = new TestEnv())
            {
                string dir = env.Backups.GetDirectory(EnvScope.User);
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "notes.txt"), "hello");
                Assert.Empty(env.Backups.List());
            }
        }
    }
}
