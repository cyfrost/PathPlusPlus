namespace PathPlusPlus.Core
{
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
}
