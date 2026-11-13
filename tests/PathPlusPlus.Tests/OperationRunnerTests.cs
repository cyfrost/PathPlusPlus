using System.IO;
using PathPlusPlus.Core;
using Xunit;

namespace PathPlusPlus.Tests
{
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
}
