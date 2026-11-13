using System;

namespace PathPlusPlus.Cli
{
    internal sealed class CliUsageException : Exception
    {
        public CliUsageException(string message) : base(message)
        {
        }
    }
}
