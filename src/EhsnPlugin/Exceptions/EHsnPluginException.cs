using System;
using System.Runtime.Serialization;

namespace EhsnPlugin.Exceptions
{
    public class EHsnPluginException : Exception
    {
        public EHsnPluginException()
        {
        }

        public EHsnPluginException(string message) : base(message)
        {
        }

        public EHsnPluginException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected EHsnPluginException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
