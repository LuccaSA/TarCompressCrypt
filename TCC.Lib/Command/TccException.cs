using System;
using System.Runtime.Serialization;

namespace TCC.Lib.Command
{
    [Serializable]
    public sealed class TccException : Exception
    {
        public TccException(string errors)
            : base(errors)
        {
        }
        
        public TccException(string errors, Exception exception)
            : base(errors, exception)
        {
        }

        private TccException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}