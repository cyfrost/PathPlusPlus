using System;

namespace PathPlusPlus.Core
{
    /// <summary>An expected, user-explainable failure of an environment operation.</summary>
    [Serializable]
    public sealed class EnvOperationException : Exception
    {
        public EnvOperationException(EnvError error, string message) : base(message)
        {
            Error = error;
        }

        public EnvOperationException(EnvError error, string message, Exception inner) : base(message, inner)
        {
            Error = error;
        }

        public EnvError Error { get; }
    }
}
