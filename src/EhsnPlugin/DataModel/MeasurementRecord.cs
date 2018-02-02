using System;

namespace EhsnPlugin.DataModel
{
    public class MeasurementRecord
    {
        public MeasurementRecord()
        {
        }

        public MeasurementRecord(DateTime start, DateTime end, string parameterId, string unitId, double value)
            : this()
        {
            StartTime = start;
            EndTime = end;
            ParameterId = parameterId;
            UnitId = unitId;
            Value = value;
        }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string ParameterId { get; set; }
        public double Value { get; set; }
        public string UnitId { get; set; }
        public string Remark { get; set; }
    }
}
