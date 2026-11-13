using System;
using System.Security;

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
}
