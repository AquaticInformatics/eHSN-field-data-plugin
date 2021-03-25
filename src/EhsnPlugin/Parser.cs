using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using EhsnPlugin.Mappers;
using EhsnPlugin.Validators;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.Results;
using ServiceStack;
using XmlSerializer = System.Xml.Serialization.XmlSerializer;

namespace EhsnPlugin
{
    public class Parser
    {
        private readonly IFieldDataResultsAppender _appender;
        private readonly ILog _logger;

        private Config Config { get; }
        private VersionValidator VersionValidator { get; }
 
        public Parser(IFieldDataResultsAppender appender, ILog logger)
        {
            _appender = appender;
            _logger = logger;

            Config = LoadConfig();
            VersionValidator = new VersionValidator(Config);
        }

        private Config LoadConfig()
        {
            var settings = _appender.GetPluginConfigurations();

            if (settings == null || !settings.TryGetValue(nameof(Config), out var jsonText) || string.IsNullOrWhiteSpace(jsonText))
            {
                jsonText = GetDefaultConfiguration();
            }

            return jsonText.FromJson<Config>();
        }

        private string GetDefaultConfiguration()
        {
            using (var stream = new MemoryStream(LoadEmbeddedResource("Config.json")))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static byte[] LoadEmbeddedResource(string path)
        {
            // ReSharper disable once PossibleNullReferenceException
            var resourceName = $"{MethodBase.GetCurrentMethod().DeclaringType.Namespace}.{path}";

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new ArgumentException($"Can't load '{resourceName}' as embedded resource.");

                return stream.ReadFully();
            }
        }

        public EHSN LoadFromStream(Stream stream)
        {
            var originalXmlText = ReadXmlText(stream);
            var cleanedUpXml = GetXmlWithEmptyElementsRemoved(originalXmlText);

            try
            {
                return DeserializeXml(cleanedUpXml);
            }
            catch (Exception exception)
            {
                _logger.Error($"XML parsing error: {exception.Message} {exception.InnerException?.Message}");
                return null;
            }
        }

        private string ReadXmlText(Stream stream)
        {
            using (var streamReader = new StreamReader(stream, Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true))
            {
                var originalXml = streamReader.ReadToEnd();
                stream.Position = 0;

                return originalXml;
            }
        }

        private string GetXmlWithEmptyElementsRemoved(string originalXml)
        {
            return Regex.Replace(originalXml, @"<[a-zA-Z]\w*\/>", string.Empty);
        }

        private EHSN DeserializeXml(string xmlText)
        {
            var serializer = new XmlSerializer(typeof(EHSN));
            var memoryStream = new MemoryStream((new UTF8Encoding()).GetBytes(xmlText));

            return serializer.Deserialize(memoryStream) as EHSN;
        }

        public void Parse(EHSN eHsn)
        {
            VersionValidator.ThrowIfUnsupportedVersion(eHsn.version);

            _logger.Info($"Parsing eHSN '{eHsn.version}' from location '{eHsn.GenInfo.station.number}' ({eHsn.GenInfo.station.Value}) collected on {eHsn.GenInfo.date.Value}");

            var locationIdentifier = eHsn.GenInfo.station.number;

            var locationInfo = _appender.GetLocationByIdentifier(locationIdentifier);

            var mapper = new FieldVisitMapper(Config, eHsn, locationInfo, _logger);

            var fieldVisitInfo = AppendMappedFieldVisitInfo(mapper, locationInfo);

            AppendMappedMeasurements(mapper, fieldVisitInfo);
        }

        private FieldVisitInfo AppendMappedFieldVisitInfo(FieldVisitMapper mapper, LocationInfo locationInfo)
        {
            var fieldVisitDetails = mapper.MapFieldVisitDetails();

            _logger.Info($"Successfully parsed one visit '{fieldVisitDetails.FieldVisitPeriod.Start:O}/{fieldVisitDetails.FieldVisitPeriod.End:O}' for location '{locationInfo.LocationIdentifier}'");

            return _appender.AddFieldVisit(locationInfo, fieldVisitDetails);
        }

        private void AppendMappedMeasurements(FieldVisitMapper mapper, FieldVisitInfo fieldVisitInfo)
        {
            var controlCondition = mapper.MapControlConditionOrNull();

            if (controlCondition != null)
            {
                _appender.AddControlCondition(fieldVisitInfo, controlCondition);
            }

            var dischargeActivity = mapper.MapDischargeActivityOrNull();

            if (dischargeActivity != null)
            {
                _appender.AddDischargeActivity(fieldVisitInfo, dischargeActivity);
            }

            foreach (var reading in mapper.MapReadings())
            {
                _appender.AddReading(fieldVisitInfo, reading);
            }

            foreach(var levelSurvey in mapper.MapLevelSurveys())
            {
                _appender.AddLevelSurvey(fieldVisitInfo, levelSurvey);
            }
        }
    }
}
