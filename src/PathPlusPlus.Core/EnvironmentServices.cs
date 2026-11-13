namespace PathPlusPlus.Core
{
    /// <summary>Wires up the real Windows implementations.</summary>
    public static class EnvironmentServices
    {
        public static EnvironmentService CreateService() =>
            new EnvironmentService(new RegistryEnvironmentStore(), FileBackupStore.CreateDefault(), new SettingChangeBroadcaster());

        public static OperationRunner CreateRunner() =>
            new OperationRunner(CreateService(), new ProcessElevator(), new WindowsSessionInfo());
    }
}
