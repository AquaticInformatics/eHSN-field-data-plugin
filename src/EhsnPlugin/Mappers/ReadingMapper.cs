using System;
using System.Collections.Generic;
using System.Linq;
using EhsnPlugin.Helpers;
using EhsnPlugin.SystemCode;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel;
using FieldDataPluginFramework.DataModel.Readings;

namespace EhsnPlugin.Mappers
{
    public class ReadingMapper
    {
        private Config Config { get; }
        private LocationInfo LocationInfo { get; }
        private DateTime VisitDate { get; }
        private readonly EHSN _eHsn;

        public ReadingMapper(Config config, LocationInfo locationInfo, DateTime visitDate, EHSN eHsn)
        {
            Config = config;
            LocationInfo = locationInfo;
            VisitDate = visitDate;
            _eHsn = eHsn;
        }

        public IEnumerable<Reading> Map()
        {
            var readings = new List<Reading>();

            AddSensorReadings(readings);
            AddEnvironmentalConditionReadings(readings);
            AddNonAggregatedGageHeightReadings(readings);
            AddDischargeTemperatureReadings(readings);

            return readings;
        }

        private void AddSensorReadings(List<Reading> readings)
        {
            var sensorRefs = _eHsn.MeasResults?.SensorRefs ?? new EHSNMeasResultsSensorRef[0];

            foreach (var sensor in sensorRefs)
            {
                AddSensorReading(readings, sensor);
            }
        }

        private void AddEnvironmentalConditionReadings(List<Reading> readings)
        {
            var readingTime = InferEnvironmentalConditionReadingTime();

            AddEnvironmentalConditionReading(readings, readingTime, SensorRefs.WindSpeed, _eHsn.EnvCond?.windMagnitudeSpeed);
            AddEnvironmentalConditionReading(readings, readingTime, SensorRefs.BatteryVoltageUnderLoad, _eHsn.EnvCond?.batteryVolt);
        }

        private DateTimeOffset? InferEnvironmentalConditionReadingTime()
        {
            // Match the time inference logic from AquariusUploadManager.py:
            // Use the /DisMeas/mmtTimeVal if available
            // Else calculate the mean value of all /StageMeas/StageMeasTable/StageMeasRow[]/time

            var time = TimeHelper.ParseTimeOrMinValue(_eHsn.DisMeas?.mmtTimeVal, VisitDate, LocationInfo.UtcOffset);

            if (time != DateTimeOffset.MinValue)
                return time;

            var stageMeasRows = _eHsn.StageMeas?.StageMeasTable ?? new EHSNStageMeasStageMeasRow[0];

            var times = stageMeasRows
                .Select(row => TimeHelper.ParseTimeOrMinValue(row.time, VisitDate, LocationInfo.UtcOffset))
                .Where(d => d != DateTimeOffset.MinValue)
                .ToList();

            if (!times.Any()) return null;

            return TimeHelper.GetMeanTimeTruncatedToMinute(times.First(), times.Last());
        }

        private void AddNonAggregatedGageHeightReadings(List<Reading> readings)
        {
            var stageMeasRows = _eHsn.StageMeas?.StageMeasTable ?? new EHSNStageMeasStageMeasRow[0];

            foreach (var row in stageMeasRows)
            {
                AddNonAggregatedStageMeasurementReading(readings, row);
            }
        }

        private void AddDischargeTemperatureReadings(List<Reading> readings)
        {
            if (_eHsn.DisMeas == null) return;

            DateTimeOffset? time = TimeHelper.ParseTimeOrMinValue(_eHsn.DisMeas.mmtTimeVal, VisitDate, LocationInfo.UtcOffset);

            if (time == DateTimeOffset.MinValue)
            {
                var startTime = TimeHelper.ParseTimeOrMinValue(_eHsn.DisMeas.startTime, VisitDate, LocationInfo.UtcOffset);
                var endTime = TimeHelper.ParseTimeOrMinValue(_eHsn.DisMeas.endTime, VisitDate, LocationInfo.UtcOffset);

                time = TimeHelper.GetMeanTimeTruncatedToMinute(startTime, endTime);

                if (time == DateTimeOffset.MinValue)
                    time = null;
            }

            AddReading(readings, time, Parameters.WaterTemp, Units.TemperatureUnitId, _eHsn.DisMeas.waterTemp);
            AddReading(readings, time, Parameters.AirTemp, Units.TemperatureUnitId, _eHsn.DisMeas.airTemp);
        }

        private void AddSensorReading(List<Reading> readings, EHSNMeasResultsSensorRef sensorRef)
        {
            if (string.IsNullOrEmpty(sensorRef.Value) || !Config.KnownSensors.ContainsKey(sensorRef.Value)) return;

            var sensor = Config.KnownSensors[sensorRef.Value];

            var timeText = _eHsn.MeasResults.Times.SingleOrDefault(r => r.row == sensorRef.row)?.Value?.Trim();

            var time = TimeHelper.ParseTimeOrMinValue(timeText, VisitDate, LocationInfo.UtcOffset);

            if (time == DateTimeOffset.MinValue) return;

            var observedValue = _eHsn.MeasResults.ObservedVals.SingleOrDefault(r => r.row == sensorRef.row)?.Value;
            var sensorValue = _eHsn.MeasResults.SensorVals.SingleOrDefault(r => r.row == sensorRef.row)?.Value;

            AddSensorReading(readings, time, sensor, observedValue);
            AddSensorReading(readings, time, sensor, sensorValue);
        }

        private void AddSensorReading(List<Reading> readings, DateTimeOffset dateTimeOffset, Config.Sensor sensor, string value)
        {
            AddReading(readings, dateTimeOffset, sensor.ParameterId, sensor.UnitId, value);
        }

        private void AddReading(List<Reading> readings, DateTimeOffset? dateTimeOffset, string parameterId, string unitId, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            var number = value.ToNullableDouble();

            if (!number.HasValue)
                throw new ArgumentException($"Can't parse '{value}' as a number for parameterId='{parameterId}' unitId='{unitId}'");

            readings.Add(new Reading(parameterId, new Measurement(number.Value, unitId))
                {DateTimeOffset = dateTimeOffset});
        }

        private void AddEnvironmentalConditionReading(List<Reading> readings, DateTimeOffset? dateTimeOffset, string sensorRefName, string value)
        {
            if (!Config.KnownSensors.TryGetValue(sensorRefName, out var sensorRef)) return;

            AddReading(readings, dateTimeOffset, sensorRef.ParameterId, sensorRef.UnitId, value);
        }

        private void AddNonAggregatedStageMeasurementReading(List<Reading> readings, EHSNStageMeasStageMeasRow stageMeasurement)
        {
            // TODO: Add support for sensor reset corrections (add the corrected value to the reading comment)
            // TODO: When InstrumentDeployment/GeneralInfo/methodType == None, treat all stage readings as non-aggregated
            // TODO: If the measurement includes a water-level reference value, add that as a reference point?

            if (stageMeasurement.MghCkbox.ToBoolean()) return;

            var time = TimeHelper.ParseTimeOrMinValue(stageMeasurement.time, VisitDate, LocationInfo.UtcOffset);

            if (time == DateTimeOffset.MinValue) return;

            AddReading(readings, time, Parameters.StageHg, Units.DistanceUnitId, stageMeasurement.HG1);
            AddReading(readings, time, Parameters.StageHg, Units.DistanceUnitId, stageMeasurement.HG2);
        }
    }
}
