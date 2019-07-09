using System;
using System.Collections.Generic;
using System.Linq;
using EhsnPlugin.DataModel;
using EhsnPlugin.Helpers;
using EhsnPlugin.SystemCode;
using FieldDataPluginFramework;
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
        private readonly ILog _logger;

        public ReadingMapper(Config config, LocationInfo locationInfo, DateTime visitDate, EHSN eHsn, ILog logger)
        {
            Config = config;
            LocationInfo = locationInfo;
            VisitDate = visitDate;
            _eHsn = eHsn;
            _logger = logger;
        }

        public IEnumerable<Reading> Map()
        {
            var readings = new List<Reading>();

            AddSensorReadings(readings);
            AddEnvironmentalConditionReadings(readings);
            AddGageHeightReadings(readings);
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

            AddTankReading(readings, readingTime, _eHsn.EnvCond?.gasArrTime, SensorRefs.TankPressure, _eHsn.EnvCond?.gasSys.ToNullableDouble());
            AddTankReading(readings, readingTime, _eHsn.EnvCond?.gasDepTime, SensorRefs.TankPressure, _eHsn.EnvCond?.gasSysDepCtrl.ToNullableDouble());
            AddTankReading(readings, readingTime, _eHsn.EnvCond?.feedArrTime, SensorRefs.TankFeed, _eHsn.EnvCond?.feed.ToNullableDouble());
            AddTankReading(readings, readingTime, _eHsn.EnvCond?.feedDepTime, SensorRefs.TankFeed, _eHsn.EnvCond?.feedDepCtrl.ToNullableDouble());

            if ("bpm".Equals(_eHsn.EnvCond?.bpmRotChoice, StringComparison.InvariantCultureIgnoreCase))
            {
                AddTankReading(readings, readingTime, _eHsn.EnvCond?.bpmrotArrTime, SensorRefs.N2BubbleRate, _eHsn.EnvCond?.bpmRot.ToNullableDouble());
                AddTankReading(readings, readingTime, _eHsn.EnvCond?.bpmrotDepTime, SensorRefs.N2BubbleRate, _eHsn.EnvCond?.bpmrotDepCtrl.ToNullableDouble());
            }
        }

        private void AddTankReading(List<Reading> readings, DateTimeOffset? readingTime, string time, string sensorRefName, double? value)
        {
            if (!value.HasValue) return;

            var dateTimeOffset = TimeHelper.ParseTimeOrMinValue(time, VisitDate, LocationInfo.UtcOffset);

            if (dateTimeOffset != DateTimeOffset.MinValue)
                // Use the inferred reading time when a specific time is missing
                readingTime = dateTimeOffset;

            AddEnvironmentalConditionReading(readings, readingTime, sensorRefName, value.ToString());
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

        private void AddGageHeightReadings(List<Reading> readings)
        {
            var stageMeasRows = _eHsn.StageMeas?.StageMeasTable ?? new EHSNStageMeasStageMeasRow[0];

            foreach (var row in stageMeasRows)
            {
                AddGageHeightReading(readings, row);
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

            AddSensorReading(readings, time, sensor, observedValue, ReadingType.Routine);
            AddSensorReading(readings, time, sensor, sensorValue, ReadingType.Reference);
        }

        private void AddSensorReading(List<Reading> readings, DateTimeOffset dateTimeOffset, Config.Sensor sensor, string value, ReadingType readingType)
        {
            var reading = AddReading(readings, dateTimeOffset, sensor.ParameterId, sensor.UnitId, value);

            if (reading != null)
            {
                reading.ReadingType = readingType;
            }
        }

        private Reading AddReading(List<Reading> readings, DateTimeOffset? dateTimeOffset, string parameterId, string unitId, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var number = value.ToNullableDouble();

            if (!number.HasValue)
                throw new ArgumentException($"Can't parse '{value}' as a number for parameterId='{parameterId}' unitId='{unitId}'");

            var reading = new Reading(parameterId, new Measurement(number.Value, unitId))
            {
                DateTimeOffset = dateTimeOffset,
                Publish = true,
                ReadingType = ReadingType.Routine
            };

            readings.Add(reading);

            return reading;
        }

        private void AddEnvironmentalConditionReading(List<Reading> readings, DateTimeOffset? dateTimeOffset, string sensorRefName, string value)
        {
            if (!Config.KnownSensors.TryGetValue(sensorRefName, out var sensorRef)) return;

            AddReading(readings, dateTimeOffset, sensorRef.ParameterId, sensorRef.UnitId, value);
        }

        private void AddGageHeightReading(List<Reading> readings, EHSNStageMeasStageMeasRow stageMeasurement)
        {
            var hg1 = stageMeasurement.HG1.ToNullableDouble();
            var wl1 = stageMeasurement.WL1.ToNullableDouble();
            var hg2 = stageMeasurement.HG2.ToNullableDouble();
            var wl2 = stageMeasurement.WL2.ToNullableDouble();
            var src = stageMeasurement.SRC.ToNullableDouble();
            var srcAction = stageMeasurement.SRCApp;

            AddLoggerReading(readings, stageMeasurement.time, "HG", hg1, wl1, src, srcAction);
            AddLoggerReading(readings, stageMeasurement.time, "HG2", hg2, wl2, src, srcAction);
        }

        private void AddLoggerReading(List<Reading> readings, string timeText, string hgLabel, double? hg, double? wl, double? src, string srcAction)
        {
            var time = TimeHelper.ParseTimeOrMinValue(timeText, VisitDate, LocationInfo.UtcOffset);

            if (time == DateTimeOffset.MinValue)
                return;

            var referencePointName = wl.HasValue
                ? GetReferencePointName(timeText)
                : null;

            if (!hg.HasValue)
            {
                if (wl.HasValue)
                    _logger.Error($"Water level ({referencePointName}) has value {wl} but no logger value @{timeText}");

                return;
            }

            var isWaterLevel = wl.HasValue;
            var value = wl ?? hg;

            var reading = AddReading(readings, time, Parameters.StageHg, Units.DistanceUnitId, value.ToString());

            if (isWaterLevel)
            {
                reading.ReferencePointName = referencePointName;
                reading.Publish = true;
                reading.ReadingType = "Reset (RS)".Equals(srcAction, StringComparison.InvariantCultureIgnoreCase)
                    ? ReadingType.ResetAfter
                    : string.IsNullOrWhiteSpace(srcAction)
                        ? ReadingType.Routine
                        : ReadingType.Unknown;
            }
            else
            {
                reading.Publish = false;
                reading.ReadingType = ReadingType.Routine;
            }

            var comments = new []
                {
                    wl.HasValue ? $"{hgLabel}: {hg:F3}" : null,
                    !DoubleHelper.AreSame(src, 0.0) ? $"Sensor Reset Correction of {src:F3}" : null,
                    srcAction,
                }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (comments.Any())
            {
                reading.Comments = string.Join(", ", comments);
            }
        }

        private string GetReferencePointName(string time)
        {
            var levelChecksSummaryRows = _eHsn.LevelNotes?.LevelChecks?.LevelChecksSummaryTable ?? new EHSNLevelNotesLevelChecksSummaryTableRow[0];

            var levelCheck = levelChecksSummaryRows
                .FirstOrDefault(row => row.time == time);

            if (levelCheck == null)
                return null;

            return ParsedEhsnLevelSurvey.SanitizeBenchmarkName(levelCheck.reference);
        }
    }
}
