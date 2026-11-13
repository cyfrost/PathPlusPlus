using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PathPlusPlus.Core
{
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
}
