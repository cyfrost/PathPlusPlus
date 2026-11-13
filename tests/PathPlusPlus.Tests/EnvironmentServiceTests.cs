using System.IO;
using Microsoft.Win32;
using PathPlusPlus.Core;
using Xunit;

namespace PathPlusPlus.Tests
{
    public class EnvironmentServiceTests
    {
        private const string WindowsPath = @"%SystemRoot%\system32;%SystemRoot%;%SYSTEMROOT%\System32\Wbem";

        [Fact]
        public void AddToPath_preserves_variables_and_writes_expand_string()
        {
            using (var env = new TestEnv())
            {
                env.Store.Set(EnvScope.Machine, "Path", WindowsPath);
                ChangeResult r = env.Service.AddToPath(EnvScope.Machine, @"C:\Tools", prepend: false);

                Assert.True(r.Changed);
                RawValue after = env.Store.Read(EnvScope.Machine, "Path");
                Assert.Equal(WindowsPath + @";C:\Tools", after.Value);
                Assert.Equal(RegistryValueKind.ExpandString, after.Kind);
                Assert.Equal(1, env.Notifier.Count);
                Assert.True(File.Exists(r.BackupFile));
            }
        }

        [Fact]
        public void AddToPath_repairs_a_path_flattened_to_REG_SZ()
        {
            using (var env = new TestEnv())
            {
                env.Store.Set(EnvScope.User, "Path", @"C:\a", RegistryValueKind.String);
                env.Service.AddToPath(EnvScope.User, @"C:\b", prepend: false);
                Assert.Equal(RegistryValueKind.ExpandString, env.Store.Read(EnvScope.User, "Path").Kind);
            }
        }

        [Fact]
        public void AddToPath_creates_path_when_missing()
        {
            using (var env = new TestEnv())
            {
                env.Service.AddToPath(EnvScope.User, @"C:\b", prepend: false);
                Assert.Equal(@"C:\b", env.Store.Read(EnvScope.User, "Path").Value);
            }
        }

        [Fact]
        public void AddToPath_is_a_no_op_when_already_present()
        {
            using (var env = new TestEnv())
            {
                env.Store.Set(EnvScope.User, "Path", @"C:\Tools\");
                ChangeResult r = env.Service.AddToPath(EnvScope.User, @"c:\tools", prepend: false);

                Assert.False(r.Changed);
                Assert.Equal(0, env.Store.Writes);
                Assert.Equal(0, env.Notifier.Count);
                Assert.Empty(env.Backups.List());
            }
        }

        [Fact]
        public void AddToPath_rejects_invalid_entries()
        {
            using (var env = new TestEnv())
            {
                var ex = Assert.Throws<EnvOperationException>(() => env.Service.AddToPath(EnvScope.User, @"C:\a;C:\b", false));
                Assert.Equal(EnvError.InvalidArgument, ex.Error);
                Assert.Equal(0, env.Store.Writes);
            }
        }

        [Fact]
        public void AddToPath_rejects_values_over_the_limit()
        {
            using (var env = new TestEnv())
            {
                env.Store.Set(EnvScope.User, "Path", "C:\\" + new string('x', 32760));
                var ex = Assert.Throws<EnvOperationException>(() => env.Service.AddToPath(EnvScope.User, @"C:\Tools", false));
                Assert.Equal(EnvError.InvalidArgument, ex.Error);
            }
        }

        [Fact]
        public void RemoveFromPath_counts_and_preserves_other_entries()
        {
            using (var env = new TestEnv())
            {
                env.Store.Set(EnvScope.User, "Path", @"%USERPROFILE%\bin;C:\Tools;C:\TOOLS\");
                ChangeResult r = env.Service.RemoveFromPath(EnvScope.User, @"C:\Tools");

                Assert.Equal(2, r.Count);
                Assert.Equal(@"%USERPROFILE%\bin", env.Store.Read(EnvScope.User, "Path").Value);
            }
        }

        [Fact]
        public void RemoveFromPath_not_found_changes_nothing()
        {
            using (var env = new TestEnv())
            {
                env.Store.Set(EnvScope.User, "Path", @"C:\a");
                Assert.False(env.Service.RemoveFromPath(EnvScope.User, @"C:\b").Changed);
                Assert.Equal(0, env.Store.Writes);
            }
        }

        [Fact]
        public void SetPath_refuses_when_changed_by_someone_else()
        {
            using (var env = new TestEnv())
            {
                env.Store.Set(EnvScope.User, "Path", @"C:\a;C:\installer-added");
                var ex = Assert.Throws<EnvOperationException>(() => env.Service.SetPath(EnvScope.User, @"C:\a;C:\b", expectedCurrent: @"C:\a"));
                Assert.Equal(EnvError.Conflict, ex.Error);
                Assert.Equal(0, env.Store.Writes);
            }
        }

        [Fact]
        public void SetPath_writes_when_unchanged_since_load()
        {
            using (var env = new TestEnv())
            {
                env.Store.Set(EnvScope.User, "Path", @"C:\a");
                Assert.True(env.Service.SetPath(EnvScope.User, @"C:\b;C:\a", expectedCurrent: @"C:\a").Changed);
                Assert.Equal(@"C:\b;C:\a", env.Store.Read(EnvScope.User, "Path").Value);
            }
        }

        [Fact]
        public void Dedupe()
        {
            using (var env = new TestEnv())
            {
                env.Store.Set(EnvScope.User, "Path", @"C:\a;;C:\A\;C:\b");
                Assert.Equal(2, env.Service.DedupePath(EnvScope.User).Count);
                Assert.Equal(@"C:\a;C:\b", env.Store.Read(EnvScope.User, "Path").Value);
            }
        }

        [Fact]
        public void SetVariable_refuses_Path_without_force()
        {
            using (var env = new TestEnv())
            {
                env.Store.Set(EnvScope.User, "Path", @"C:\a;C:\b");
                var ex = Assert.Throws<EnvOperationException>(() => env.Service.SetVariable(EnvScope.User, "PATH", @"C:\only", force: false));
                Assert.Equal(EnvError.InvalidArgument, ex.Error);
                Assert.Equal(@"C:\a;C:\b", env.Store.Read(EnvScope.User, "Path").Value);
            }
        }

        [Fact]
        public void SetVariable_chooses_kind()
        {
            using (var env = new TestEnv())
            {
                env.Service.SetVariable(EnvScope.User, "PLAIN", @"C:\x", false);
                env.Service.SetVariable(EnvScope.User, "EXPANDING", @"%USERPROFILE%\x", false);
                Assert.Equal(RegistryValueKind.String, env.Store.Read(EnvScope.User, "PLAIN").Kind);
                Assert.Equal(RegistryValueKind.ExpandString, env.Store.Read(EnvScope.User, "EXPANDING").Kind);
            }
        }

        [Fact]
        public void DeleteVariable_missing_is_a_no_op()
        {
            using (var env = new TestEnv())
            {
                Assert.False(env.Service.DeleteVariable(EnvScope.User, "NOPE", false).Changed);
                Assert.Equal(0, env.Store.Writes);
            }
        }

        [Fact]
        public void Restore_round_trip_including_kind()
        {
            using (var env = new TestEnv())
            {
                env.Store.Set(EnvScope.User, "Path", @"%USERPROFILE%\bin;C:\a");
                ChangeResult add = env.Service.AddToPath(EnvScope.User, @"C:\b", false);

                ChangeResult restore = env.Service.Restore(add.BackupFile);

                Assert.True(restore.Changed);
                RawValue now = env.Store.Read(EnvScope.User, "Path");
                Assert.Equal(@"%USERPROFILE%\bin;C:\a", now.Value);
                Assert.Equal(RegistryValueKind.ExpandString, now.Kind);
                Assert.Equal(2, env.Backups.List().Count); // restoring backs up first
            }
        }

        [Fact]
        public void Restore_of_a_variable_that_did_not_exist_deletes_it()
        {
            using (var env = new TestEnv())
            {
                ChangeResult set = env.Service.SetVariable(EnvScope.User, "NEWVAR", "value", false);
                env.Service.Restore(set.BackupFile);
                Assert.Null(env.Store.Read(EnvScope.User, "NEWVAR"));
            }
        }

        [Fact]
        public void Machine_restore_only_from_machine_backup_folder()
        {
            using (var env = new TestEnv())
            {
                env.Store.Set(EnvScope.Machine, "Path", @"C:\a");
                ChangeResult add = env.Service.AddToPath(EnvScope.Machine, @"C:\b", false);

                string copy = Path.Combine(env.Root, "elsewhere.txt");
                File.Copy(add.BackupFile, copy);
                var ex = Assert.Throws<EnvOperationException>(() => env.Service.Restore(copy));
                Assert.Equal(EnvError.NotPermitted, ex.Error);
            }
        }
    }
}
