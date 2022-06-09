using System.Collections.Generic;
using FieldDataPluginFramework.DataModel.ChannelMeasurements;

namespace EhsnPlugin
{
    public class Config
    {
        public string MinVersion { get; set; } = "v1.3";
        public string MaxVersion { get; set; } = "v2.3.1";
        public string DefaultChannelName { get; set; } = ChannelMeasurementBaseConstants.DefaultChannelName;
        public string UnknownMeterPlaceholder { get; set; } = "Unknown";
        public string StageWaterLevelMethodCode { get; set; }
        public string StageLoggerMethodCode { get; set; }
        public Dictionary<string, string> KnownControlConditions { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, Sensor> KnownSensors { get; set; } = new Dictionary<string, Sensor>();
        public Dictionary<string, string> KnownTopEstimateMethods { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> KnownBottomEstimateMethods { get; set; } = new Dictionary<string, string>();

        public class Sensor
        {
            public string ParameterId { get; set; }
            public string UnitId { get; set; }
            public string SensorMethodCode { get; set; }
        }
    }
}
