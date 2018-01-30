using System;
using System.IO;
using System.Xml.Serialization;

namespace EhsnPlugin
{
    public class Parser
    {
        public EHSN LoadFromStream(Stream stream)
        {
            try
            {
                var serializaer = new XmlSerializer(typeof(EHSN));

                var eHsn = serializaer.Deserialize(stream) as EHSN;

                return eHsn;
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}
