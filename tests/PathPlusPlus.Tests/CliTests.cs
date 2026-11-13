using System.IO;
using PathPlusPlus.Cli;
using PathPlusPlus.Core;
using Xunit;

namespace PathPlusPlus.Tests
{
    public class CliTests
    {
        private static (int code, string output, string error) Run(TestEnv env, FakeSession session, params string[] args)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var runner = new OperationRunner(env.Service, new FakeElevator(), session ?? new FakeSession());
            var app = new CliApp(runner, output, error, directoryExists: d => !d.Contains("missing"), getFullPath: p => @"C:\cwd\" + p);
            int code = app.Run(args);
            return (code, output.ToString(), error.ToString());
        }

        [Fact]
        public void Add_relative_folder_resolves_against_current_directory()
        {
            using (var env = new TestEnv())
            {
                var (code, _, _) = Run(env, null, "add", "tools");
                Assert.Equal(0, code);
                Assert.Equal(@"C:\cwd\tools", env.Store.Read(EnvScope.User, "Path").Value);
            }
        }

        [Fact]
        public void Add_missing_folder_needs_force()
        {
            using (var env = new TestEnv())
            {
                Assert.Equal((int)EnvError.InvalidArgument, Run(env, null, "add", @"C:\missing").code);
                Assert.Equal(0, Run(env, null, "add", @"C:\missing", "--force").code);
            }
        }

        [Fact]
        public void Add_twice_is_idempotent()
        {
            using (var env = new TestEnv())
            {
                Run(env, null, "add", @"C:\x");
                var (code, output, _) = Run(env, null, "add", @"C:\X\");
                Assert.Equal(0, code);
                Assert.Contains("already", output);
                Assert.Equal(@"C:\x", env.Store.Read(EnvScope.User, "Path").Value);
            }
        }

        [Fact]
        public void Unknown_scope_or_option_is_a_usage_error_and_writes_nothing()
        {
            using (var env = new TestEnv())
            {
                Assert.Equal(CliApp.ExitUsage, Run(env, null, "add", @"C:\x", "--bogus").code);
                Assert.Equal(CliApp.ExitUsage, Run(env, null, "add", @"C:\x", "--user", "--machine").code);
                Assert.Equal(CliApp.ExitUsage, Run(env, null, "frobnicate").code);
                Assert.Equal(0, env.Store.Writes);
            }
        }

        [Fact]
        public void No_arguments_shows_usage_with_nonzero_exit()
        {
            using (var env = new TestEnv())
            {
                var (code, output, _) = Run(env, null);
                Assert.Equal(CliApp.ExitUsage, code);
                Assert.Contains("Usage", output);
                Assert.Equal(0, Run(env, null, "help").code);
            }
        }

        [Fact]
        public void Set_path_is_refused_without_force()
        {
            using (var env = new TestEnv())
            {
                env.Store.Set(EnvScope.User, "Path", @"C:\a;C:\b");
                Assert.Equal((int)EnvError.InvalidArgument, Run(env, null, "set", "Path", @"C:\only").code);
                Assert.Equal(@"C:\a;C:\b", env.Store.Read(EnvScope.User, "Path").Value);
            }
        }

        [Fact]
        public void Get_missing_returns_not_found()
        {
            using (var env = new TestEnv())
            {
                Assert.Equal((int)EnvError.NotFound, Run(env, null, "get", "NOPE").code);
            }
        }

        [Fact]
        public void Machine_add_uses_elevation_and_system_alias_works()
        {
            using (var env = new TestEnv())
            {
                var output = new StringWriter();
                var elevator = new FakeElevator();
                var runner = new OperationRunner(env.Service, elevator, new FakeSession());
                int code = new CliApp(runner, output, new StringWriter(), d => true, p => p).Run(new[] { "add", @"C:\x", "--system" });
                Assert.Equal(0, code);
                Assert.Equal(EnvScope.Machine, Assert.Single(elevator.Requests).Scope);
            }
        }

        [Fact]
        public void Cancelled_elevation_returns_exit_code_4()
        {
            using (var env = new TestEnv())
            {
                var elevator = new FakeElevator { Response = EnvResponse.Fail(EnvError.Cancelled, "Cancelled") };
                var runner = new OperationRunner(env.Service, elevator, new FakeSession());
                int code = new CliApp(runner, new StringWriter(), new StringWriter(), d => true, p => p).Run(new[] { "add", @"C:\x", "--machine" });
                Assert.Equal(4, code);
            }
        }

        [Fact]
        public void Remove_all_and_list()
        {
            using (var env = new TestEnv())
            {
                env.Store.Set(EnvScope.User, "Path", @"C:\x;C:\y");
                var session = new FakeSession { IsElevated = true };
                Run(env, session, "remove", @"C:\x", "--all");
                Assert.Equal(@"C:\y", env.Store.Read(EnvScope.User, "Path").Value);

                var (code, output, _) = Run(env, session, "list", "--user", "--plain");
                Assert.Equal(0, code);
                Assert.Equal(@"C:\y", output.Trim());
            }
        }

        [Fact]
        public void Backups_and_restore_by_number()
        {
            using (var env = new TestEnv())
            {
                env.Store.Set(EnvScope.User, "Path", @"C:\a");
                Run(env, null, "add", @"C:\b");
                var (code, _, _) = Run(env, null, "restore", "1");
                Assert.Equal(0, code);
                Assert.Equal(@"C:\a", env.Store.Read(EnvScope.User, "Path").Value);
            }
        }
    }
}
