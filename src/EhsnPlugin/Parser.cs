using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using EhsnPlugin.Mappers;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.Results;

namespace EhsnPlugin
{
    public class Parser
    {
        private readonly IFieldDataResultsAppender _appender;
        private readonly ILog _logger;
 
        public Parser(IFieldDataResultsAppender appender, ILog logger)
        {
            _appender = appender;
            _logger = logger;
        }

        public EHSN LoadFromStream(Stream stream)
        {
            try
            {
                var cleanedUpXml = GetXmlWithEmptyElementsRemoved(stream);

                var serializaer = new XmlSerializer(typeof(EHSN));
                var memoryStream = new MemoryStream((new UTF8Encoding()).GetBytes(cleanedUpXml));              

                return serializaer.Deserialize(memoryStream) as EHSN;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private string GetXmlWithEmptyElementsRemoved(Stream stream)
        {
            using (var streamReader = new StreamReader(stream, Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true))
            {
                var originalXml = streamReader.ReadToEnd();
                stream.Position = 0;

                return Regex.Replace(originalXml, @"<[a-zA-Z][^<>]*\/>",string.Empty);
            }
        }

        public void Parse(EHSN eHsn)
        {
            var locationIdentifier = eHsn.GenInfo.station.number;

            var locationInfo = _appender.GetLocationByIdentifier(locationIdentifier);

            var mapper = new FieldVisitMapper(eHsn, locationInfo);

            var fieldVisitInfo = AppendMappedFieldVisitInfo(mapper, locationInfo);

            AppendMappedMeasurements(mapper, fieldVisitInfo);
        }

        private FieldVisitInfo AppendMappedFieldVisitInfo(FieldVisitMapper mapper, LocationInfo locationInfo)
        {
            var fieldVisitDetails = mapper.MapFieldVisitDetails();

            _logger.Info($"Successfully parsed one visit '{fieldVisitDetails.FieldVisitPeriod}' for location '{locationInfo.LocationIdentifier}'");

            return _appender.AddFieldVisit(locationInfo, fieldVisitDetails);
        }

        private void AppendMappedMeasurements(FieldVisitMapper mapper, FieldVisitInfo fieldVisitInfo)
        {
            _appender.AddDischargeActivity(fieldVisitInfo, mapper.MapDischargeActivity());
        }
    }
}
