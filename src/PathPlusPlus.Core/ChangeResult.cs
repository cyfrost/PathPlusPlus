namespace PathPlusPlus.Core
{
    /// <summary>The outcome of a change.</summary>
    public sealed class ChangeResult
    {
        public ChangeResult(bool changed, int count, string message, string backupFile = null)
        {
            Changed = changed;
            Count = count;
            Message = message;
            BackupFile = backupFile;
        }

        /// <summary>False when nothing needed to change (e.g. folder already in PATH).</summary>
        public bool Changed { get; }

        /// <summary>Number of entries affected (e.g. instances removed).</summary>
        public int Count { get; }

        public string Message { get; }

        public string BackupFile { get; }
    }
}
