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

        private TccException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}