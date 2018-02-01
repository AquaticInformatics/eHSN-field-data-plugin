using System;

namespace EhsnPlugin.DataModel
{
    public class MeasurementRecord
    {
        public MeasurementRecord()
        {
        }

        public MeasurementRecord(DateTime measurementTime, string parameterId, string unitId, double value)
            : this()
        {
            Time = measurementTime;
            ParameterId = parameterId;
            UnitId = unitId;
            Value = value;
        }

        public DateTime Time { get; set; }
        public string ParameterId { get; set; }
        public double Value { get; set; }
        public string UnitId { get; set; }
        public string Remark { get; set; }
    }
}
