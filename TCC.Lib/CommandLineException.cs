using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace TCC.Lib
{
    [Serializable]
    public sealed class CommandLineException : Exception
    {
        public CommandLineException(string errorMessage) 
            : base(errorMessage)
        { 
        }

        [ExcludeFromCodeCoverage]
        private CommandLineException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}