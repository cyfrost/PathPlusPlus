using System.IO;
using PathPlusPlus.App;
using PathPlusPlus.Cli;
using PathPlusPlus.Core;
using Xunit;

namespace PathPlusPlus.Tests
{
    public class EnvRequestTests
    {
        [Fact]
        public void Request_round_trips_through_token()
        {
            var request = EnvRequest.SetPath(EnvScope.Machine, "C:\\a;\"quoted\";ünïcødé;\nnewline", "expected value");
            EnvRequest copy = EnvRequest.FromToken(request.ToToken());

            Assert.Equal(request.Operation, copy.Operation);
            Assert.Equal(request.Scope, copy.Scope);
            Assert.Equal(request.Value, copy.Value);
            Assert.Equal(request.Expected, copy.Expected);
            Assert.Null(copy.Entry);
        }

        [Fact]
        public void Token_is_command_line_safe()
        {
            string token = EnvRequest.AddToPath(EnvScope.Machine, @"C:\Program Files\My Tool", true).ToToken();
            Assert.DoesNotContain(" ", token);
            Assert.DoesNotContain("\"", token);
        }

        [Fact]
        public void Response_round_trip()
        {
            var response = EnvResponse.Fail(EnvError.Conflict, "changed: a=b\nsecond line");
            EnvResponse copy = EnvResponse.Deserialize(response.Serialize());
            Assert.False(copy.Success);
            Assert.Equal(EnvError.Conflict, copy.Error);
            Assert.Equal(response.Message, copy.Message);
        }
    }

    public class OperationRunnerTests
    {
        [Fact]
        public void Machine_changes_go_through_the_elevator_when_not_elevated()
        {
            using (var env = new TestEnv())
            {
                var elevator = new FakeElevator();
                var runner = new OperationRunner(env.Service, elevator, new FakeSession { IsElevated = false });

                EnvResponse r = runner.Run(EnvRequest.AddToPath(EnvScope.Machine, @"C:\x", false));

                Assert.True(r.Success);
                Assert.Single(elevator.Requests);
                Assert.Equal(0, env.Store.Writes);
            }
        }

        [Fact]
        public void Machine_changes_run_in_process_when_elevated()
        {
            using (var env = new TestEnv())
            {
                var elevator = new FakeElevator();
                var runner = new OperationRunner(env.Service, elevator, new FakeSession { IsElevated = true });
                runner.Run(EnvRequest.AddToPath(EnvScope.Machine, @"C:\x", false));
                Assert.Empty(elevator.Requests);
                Assert.Equal(1, env.Store.Writes);
            }
        }

        [Fact]
        public void User_changes_refused_when_running_as_another_account()
        {
            using (var env = new TestEnv())
            {
                var runner = new OperationRunner(env.Service, new FakeElevator(), new FakeSession { IsElevated = true, IsDifferentUserThanDesktop = true });
                EnvResponse r = runner.Run(EnvRequest.AddToPath(EnvScope.User, @"C:\x", false));
                Assert.False(r.Success);
                Assert.Equal(EnvError.NotPermitted, r.Error);
                Assert.Equal(0, env.Store.Writes);
            }
        }

        [Fact]
        public void Elevated_worker_refuses_user_scope_requests()
        {
            using (var env = new TestEnv())
            {
                string token = EnvRequest.AddToPath(EnvScope.User, @"C:\x", false).ToToken();
                string result = Path.Combine(env.Root, "r.txt");
                Directory.CreateDirectory(env.Root);

                int code = ElevatedWorker.Run(new[] { ProcessElevator.RequestSwitch, token, result }, env.Service, new FakeSession { IsElevated = true });

                Assert.Equal((int)EnvError.NotPermitted, code);
                Assert.Equal(EnvError.NotPermitted, EnvResponse.Deserialize(File.ReadAllText(result)).Error);
                Assert.Equal(0, env.Store.Writes);
            }
        }

        [Fact]
        public void Elevated_worker_performs_machine_requests_and_reports()
        {
            using (var env = new TestEnv())
            {
                env.Store.Set(EnvScope.Machine, "Path", @"C:\a");
                string token = EnvRequest.AddToPath(EnvScope.Machine, @"C:\b", false).ToToken();
                string result = Path.Combine(env.Root, "r.txt");
                Directory.CreateDirectory(env.Root);

                int code = ElevatedWorker.Run(new[] { ProcessElevator.RequestSwitch, token, result }, env.Service, new FakeSession { IsElevated = true });

                Assert.Equal(0, code);
                Assert.True(EnvResponse.Deserialize(File.ReadAllText(result)).Changed);
                Assert.Equal(@"C:\a;C:\b", env.Store.Read(EnvScope.Machine, "Path").Value);
            }
        }
    }

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

    public class AppTests
    {
        // The argv values below are exactly what Windows hands the app for each Explorer verb
        // (note: "%V" on a drive root arrives as  C:"  because \" is parsed as an escaped quote).
        [Theory]
        [InlineData(new[] { "--add-to-path", "C:\"" }, @"C:\")]
        [InlineData(new[] { "--add-to-path", @"C:\Tools\bin\" }, @"C:\Tools\bin")]
        [InlineData(new[] { "--add-to-path", "--parent", @"C:\Windows\notepad.exe" }, @"C:\Windows")]
        [InlineData(new[] { "--remove-from-path", "--parent", @"C:\file.txt" }, @"C:\")]
        [InlineData(new[] { "--add-to-path", @"C:\Tools\app.exe" }, @"C:\Tools")]           // a file without --parent
        [InlineData(new[] { "--new-variable", @"C:\Tools\app.exe" }, @"C:\Tools\app.exe")] // variables keep the file path
        [InlineData(new[] { "--new-variable", "--parent", @"C:\Program Files\Git\bin\git.exe" }, @"C:\Program Files\Git\bin")]
        [InlineData(new[] { "--ADD-TO-PATH", @"\\server\share\tools" }, @"\\server\share\tools")]
        public void Explorer_invocations_resolve_to_the_right_path(string[] args, string expected)
        {
            string error = PathPlusPlus.App.Program.ResolveTarget(args, p => p.EndsWith(".exe") || p.EndsWith(".txt"), out string target);
            Assert.Null(error);
            Assert.Equal(expected, target);
        }

        [Theory]
        [InlineData("--add-to-path")]
        [InlineData("--add-to-path|a|b")]
        [InlineData("--frob|C:\\x")]
        [InlineData("--add-to-path|\"\"")]
        public void Bad_invocations_are_rejected(string joinedArgs)
        {
            Assert.NotNull(PathPlusPlus.App.Program.ResolveTarget(joinedArgs.Split('|'), p => false, out _));
        }

        [Theory]
        [InlineData(@"C:\Tools\jdk-21", "JDK_21")]
        [InlineData(@"C:\Tools\go\", "GO")]
        [InlineData(@"C:\7zip", "_7ZIP")]
        [InlineData(@"C:\", "")]
        public void Suggested_variable_names(string path, string expected)
        {
            Assert.Equal(expected, NewVariableDialog.SuggestName(path));
        }
    }
}
