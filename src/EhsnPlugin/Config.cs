using System.Collections.Generic;

namespace EhsnPlugin
{
    public class Config
    {
        public string MinVersion { get; set; } = "v1.3";
        public string MaxVersion { get; set; } = "v1.3.2";
        public Dictionary<string, string> KnownControlConditions { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, Sensor> KnownSensors { get; set; } = new Dictionary<string, Sensor>();

        public class Sensor
        {
            public string ParameterId { get; set; }
            public string UnitId { get; set; }
        }
    }
}
