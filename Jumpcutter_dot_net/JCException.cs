using System;
using System.Runtime.Serialization;

namespace Jumpcutter_dot_net
{
    [Serializable]
    public class JCException : Exception
    {
        public JCException()
        {
        }

        public JCException(string message) : base(message)
        {
        }

        public JCException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected JCException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}