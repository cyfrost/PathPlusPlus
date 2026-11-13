using System;
using System.IO;
using System.Text;

namespace PathPlusPlus.Core
{
    /// <summary>The elevated side of <see cref="ProcessElevator"/>. Only performs system-scope requests.</summary>
    public static class ElevatedWorker
    {
        public static bool IsWorkerInvocation(string[] args) =>
            args.Length >= 2 && (args[0] == ProcessElevator.RequestSwitch || args[0] == ProcessElevator.RequestFileSwitch);

        /// <returns>Process exit code: 0 on success.</returns>
        public static int Run(string[] args, EnvironmentService service, ISessionInfo session)
        {
            string resultFile = args.Length >= 3 ? args[2] : null;
            EnvResponse response;
            try
            {
                EnvRequest request = args[0] == ProcessElevator.RequestFileSwitch
                    ? EnvRequest.Deserialize(File.ReadAllText(args[1], Encoding.UTF8))
                    : EnvRequest.FromToken(args[1]);

                if (request.Scope != EnvScope.Machine)
                    response = EnvResponse.Fail(EnvError.NotPermitted, "The elevated helper only makes system-wide changes.");
                else if (!session.IsElevated)
                    response = EnvResponse.Fail(EnvError.NotPermitted, "Administrator rights were not granted.");
                else
                    response = RequestExecutor.Execute(service, request);
            }
            catch (Exception ex)
            {
                response = EnvResponse.Fail(EnvError.Failed, "Invalid request: " + ex.Message);
            }

            if (resultFile != null)
            {
                try
                {
                    File.WriteAllText(resultFile, response.Serialize(), new UTF8Encoding(false));
                }
                catch (Exception)
                {
                    // The caller falls back to the exit code.
                }
            }
            return response.Success ? 0 : (int)response.Error;
        }
    }
}
