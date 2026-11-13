using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;

namespace PathPlusPlus.Core
{
    /// <summary>Runs one request against an <see cref="EnvironmentService"/> in this process.</summary>
    public static class RequestExecutor
    {
        public static EnvResponse Execute(EnvironmentService service, EnvRequest request)
        {
            try
            {
                return EnvResponse.FromResult(Dispatch(service, request));
            }
            catch (EnvOperationException ex)
            {
                return EnvResponse.Fail(ex.Error, ex.Message);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is SecurityException)
            {
                return EnvResponse.Fail(EnvError.NotPermitted, ex.Message);
            }
            catch (Exception ex)
            {
                return EnvResponse.Fail(EnvError.Failed, ex.Message);
            }
        }

        private static ChangeResult Dispatch(EnvironmentService s, EnvRequest r)
        {
            switch (r.Operation)
            {
                case EnvOperation.AddToPath: return s.AddToPath(r.Scope, r.Entry, r.Prepend);
                case EnvOperation.RemoveFromPath: return s.RemoveFromPath(r.Scope, r.Entry);
                case EnvOperation.DedupePath: return s.DedupePath(r.Scope);
                case EnvOperation.SetPath: return s.SetPath(r.Scope, r.Value, r.Expected);
                case EnvOperation.SetVariable: return s.SetVariable(r.Scope, r.Name, r.Value, r.Force);
                case EnvOperation.DeleteVariable: return s.DeleteVariable(r.Scope, r.Name, r.Force);
                case EnvOperation.Restore:
                    BackupInfo info = s.Backups.Load(r.BackupFile);
                    if (info.Scope != r.Scope)
                    {
                        throw new EnvOperationException(EnvError.InvalidArgument, "That backup belongs to the " + info.Scope.DisplayName() + " environment.");
                    }
                    return s.Restore(r.BackupFile);
                default:
                    throw new EnvOperationException(EnvError.InvalidArgument, "Unknown operation " + r.Operation + ".");
            }
        }
    }

    /// <summary>Runs a request in an elevated helper process.</summary>
    public interface IElevator
    {
        EnvResponse RunElevated(EnvRequest request);
    }

    /// <summary>
    /// Decides where a request runs:
    /// user scope → in this process (refused if this process isn't the desktop user);
    /// system scope → in this process if already elevated, otherwise in an elevated helper (one UAC prompt).
    /// </summary>
    public sealed class OperationRunner
    {
        private readonly EnvironmentService _service;
        private readonly IElevator _elevator;
        private readonly ISessionInfo _session;

        public OperationRunner(EnvironmentService service, IElevator elevator, ISessionInfo session)
        {
            _service = service;
            _elevator = elevator;
            _session = session;
        }

        public EnvironmentService Service => _service;

        public ISessionInfo Session => _session;

        public bool NeedsElevation(EnvScope scope) => scope == EnvScope.Machine && !_session.IsElevated;

        public EnvResponse Run(EnvRequest request)
        {
            if (request.Scope == EnvScope.Machine)
            {
                return _session.IsElevated ? RequestExecutor.Execute(_service, request) : _elevator.RunElevated(request);
            }

            if (_session.IsDifferentUserThanDesktop)
            {
                return EnvResponse.Fail(EnvError.NotPermitted,
                    "Path++ is running as '" + _session.UserName + "', not as the person signed in to this desktop. " +
                    "A user-scope change would go to the wrong account. Close Path++ and start it normally (not 'Run as administrator').");
            }
            return RequestExecutor.Execute(_service, request);
        }
    }

    /// <summary>
    /// Starts PathPlusPlus.exe with "runas" and passes the request as a Base64 token (or a file for very long values).
    /// The helper writes its <see cref="EnvResponse"/> to a result file in this user's temp folder.
    /// </summary>
    public sealed class ProcessElevator : IElevator
    {
        public const string RequestSwitch = "--elevated-request";
        public const string RequestFileSwitch = "--elevated-request-file";
        public const string HelperExeName = "PathPlusPlus.exe";
        private const int MaxInlineTokenLength = 24000;

        private readonly string _helperPath;

        public ProcessElevator(string helperPath = null)
        {
            _helperPath = helperPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HelperExeName);
        }

        public EnvResponse RunElevated(EnvRequest request)
        {
            if (!File.Exists(_helperPath))
            {
                return EnvResponse.Fail(EnvError.Failed, "Can't find " + _helperPath + " to make a system-wide change.");
            }

            string resultFile = Path.Combine(Path.GetTempPath(), "PathPlusPlus-" + Guid.NewGuid().ToString("N") + ".result");
            string requestFile = null;
            try
            {
                File.WriteAllText(resultFile, string.Empty);
                string token = request.ToToken();
                string arguments;
                if (token.Length <= MaxInlineTokenLength)
                {
                    arguments = RequestSwitch + " " + token + " " + Quote(resultFile);
                }
                else
                {
                    requestFile = Path.Combine(Path.GetTempPath(), "PathPlusPlus-" + Guid.NewGuid().ToString("N") + ".request");
                    File.WriteAllText(requestFile, request.Serialize(), new UTF8Encoding(false));
                    arguments = RequestFileSwitch + " " + Quote(requestFile) + " " + Quote(resultFile);
                }

                var info = new ProcessStartInfo(_helperPath, arguments)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                Process process;
                try
                {
                    process = Process.Start(info);
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
                {
                    return EnvResponse.Fail(EnvError.Cancelled, "Cancelled — administrator approval was not given.");
                }

                using (process)
                {
                    if (process == null) return EnvResponse.Fail(EnvError.Failed, "Couldn't start the elevated helper.");
                    if (!process.WaitForExit(120000))
                    {
                        return EnvResponse.Fail(EnvError.Failed, "The elevated helper did not finish in time.");
                    }

                    string text = File.Exists(resultFile) ? File.ReadAllText(resultFile, Encoding.UTF8) : string.Empty;
                    if (text.Length > 0) return EnvResponse.Deserialize(text);

                    return process.ExitCode == 0
                        ? new EnvResponse { Success = true, Changed = true, Message = "Done." }
                        : EnvResponse.Fail(EnvError.Failed, "The elevated helper failed (exit code " + process.ExitCode + ").");
                }
            }
            catch (Exception ex)
            {
                return EnvResponse.Fail(EnvError.Failed, ex.Message);
            }
            finally
            {
                TryDelete(resultFile);
                if (requestFile != null) TryDelete(requestFile);
            }
        }

        private static string Quote(string path) => "\"" + path + "\"";

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
                // Temp file; ignore.
            }
        }
    }

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

    /// <summary>Wires up the real Windows implementations.</summary>
    public static class EnvironmentServices
    {
        public static EnvironmentService CreateService() =>
            new EnvironmentService(new RegistryEnvironmentStore(), FileBackupStore.CreateDefault(), new SettingChangeBroadcaster());

        public static OperationRunner CreateRunner() =>
            new OperationRunner(CreateService(), new ProcessElevator(), new WindowsSessionInfo());
    }
}
