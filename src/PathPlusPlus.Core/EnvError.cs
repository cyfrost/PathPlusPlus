namespace PathPlusPlus.Core
{
    /// <summary>Categories of failure, mapped to CLI exit codes and UI messages.</summary>
    public enum EnvError
    {
        None = 0,
        Failed = 1,
        InvalidArgument = 2,
        NotFound = 3,
        Cancelled = 4,
        NotPermitted = 5,
        Conflict = 6,
    }
}
