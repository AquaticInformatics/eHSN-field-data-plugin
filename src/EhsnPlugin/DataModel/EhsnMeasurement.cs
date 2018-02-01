using System.Collections.Generic;

namespace EhsnPlugin.DataModel
{
    public class EhsnMeasurement
    {
        public List<MeasurementRecord> StageMeasurements { get; set; }
        public List<MeasurementRecord> DischargeMeasurements { get; set; }
        public List<MeasurementRecord> EnvironmentConditionMeasurements { get; set; }
        public List<MeasurementRecord> SensorStageMeasurements { get; set; }

        public EhsnMeasurement()
        {
            StageMeasurements = new List<MeasurementRecord>();
            DischargeMeasurements = new List<MeasurementRecord>();
            EnvironmentConditionMeasurements = new List<MeasurementRecord>();
            SensorStageMeasurements = new List<MeasurementRecord>();
        }
    }
}
