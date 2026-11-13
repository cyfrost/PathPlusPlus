namespace PathPlusPlus.Core
{
    /// <summary>Runs a request in an elevated helper process.</summary>
    public interface IElevator
    {
        EnvResponse RunElevated(EnvRequest request);
    }
}
