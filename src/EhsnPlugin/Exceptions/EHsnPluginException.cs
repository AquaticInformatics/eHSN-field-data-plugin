using System;
#if NETFRAMEWORK
using System.Runtime.Serialization;
#endif

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

#if NETFRAMEWORK
        // This method is only relevant for net472's AppDomain-based plugin loading, which is not used in net10.0.
        // net10.0. will warn that the constructor is obsolete
        protected EHsnPluginException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif
    }
}
