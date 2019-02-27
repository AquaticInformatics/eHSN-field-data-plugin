using System;
using System.Collections.Generic;
using System.Linq;
using EhsnPlugin.DataModel;
using EhsnPlugin.Helpers;
using FieldDataPluginFramework.DataModel;
using FieldDataPluginFramework.DataModel.Readings;

namespace EhsnPlugin.Mappers
{
    public class ReadingMapper
    {
        private readonly EhsnMeasurement _ehsnMeasurement;
        private readonly EHSN _eHsn;

        public ReadingMapper(EHSN eHsn, EhsnMeasurement ehsnMeasurement)
        {
            _eHsn = eHsn;
            _ehsnMeasurement = ehsnMeasurement;
        }

        public IEnumerable<Reading> Map()
        {
            var readings = new List<Reading>();

            readings.AddRange(_ehsnMeasurement.EnvironmentConditionMeasurements.Select(MapReading));
            readings.AddRange(_ehsnMeasurement.StageMeasurements.Select(MapReading));
            readings.AddRange(_ehsnMeasurement.SensorStageMeasurements.Select(MapReading));

            return readings;
        }

        private Reading MapReading(MeasurementRecord record)
        {
            var meanTime = TimeHelper.GetMeanTimeTruncatedToMinute(record.StartTime, record.EndTime);

            return new Reading(record.ParameterId, new Measurement(record.Value,record.UnitId))
            {
                Comments =record.Remark,
                DateTimeOffset = new DateTimeOffset(meanTime, _ehsnMeasurement.LocationUtcOffset)
            };
        }
    }
}
