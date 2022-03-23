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
            var readingtime = InferEnvironmentalConditionReadingTime();

            AddReading(readings, readingtime, Parameters.WaterTemp, Units.TemperatureUnitId, _eHsn.DisMeas.waterTemp);
            AddReading(readings, readingtime, Parameters.AirTemp, Units.TemperatureUnitId, _eHsn.DisMeas.airTemp);
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
            var sensorReading = AddSensorReading(readings, time, sensor, sensorValue, ReadingType.Reference);

            if (sensorReading != null && !string.IsNullOrEmpty(sensor.SensorMethodCode))
            {
                sensorReading.Method = sensor.SensorMethodCode;
            }
        }

        private Reading AddSensorReading(List<Reading> readings, DateTimeOffset dateTimeOffset, Config.Sensor sensor, string value, ReadingType readingType)
        {
            var reading = AddReading(readings, dateTimeOffset, sensor.ParameterId, sensor.UnitId, value);

            if (reading != null)
            {
                reading.ReadingType = readingType;
            }

            return reading;
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
                Publish = false,
                ReadingType = ReadingType.Unknown
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
            var timeText = stageMeasurement.time;

            var time = TimeHelper.ParseTimeOrMinValue(timeText, VisitDate, LocationInfo.UtcOffset);

            if (time == DateTimeOffset.MinValue)
                return;

            var hg1 = stageMeasurement.HG1.ToNullableDouble();
            var wl1 = stageMeasurement.WL1.ToNullableDouble();
            var hg2 = stageMeasurement.HG2.ToNullableDouble();
            var wl2 = stageMeasurement.WL2.ToNullableDouble();
            var surge = stageMeasurement.Surge.ToNullableDouble();
            var sensorResetCorrection = stageMeasurement.SRC.ToNullableDouble();
            var srcAction = stageMeasurement.SRCApp;
            var readingType = stageMeasurement.ReadingType;
            var wl1Header = SanitizeBenchmarkName(_eHsn?.StageMeas?.WL1Header);
            var wl2Header = SanitizeBenchmarkName(_eHsn?.StageMeas?.WL2Header);
            var gaugeCorrection1 = _eHsn?.StageMeas?.GCHG1.ToNullableDouble();
            var gaugeCorrection2 = _eHsn?.StageMeas?.GCHG2.ToNullableDouble();
            string hg1Header = "WL Source: HG";
            string hg2Header = "WL Source: HG2";

            AddLoggerReading(readings, time, hg1, gaugeCorrection1, hg1Header, sensorResetCorrection, srcAction, surge);
            AddLoggerReading(readings, time, hg2, gaugeCorrection2, hg2Header, sensorResetCorrection, srcAction, surge);
            AddWaterLevelReading(readings, time, wl1, gaugeCorrection1, wl1Header, hg1, hg2, sensorResetCorrection, srcAction, readingType, surge);
            AddWaterLevelReading(readings, time, wl2, gaugeCorrection2, wl2Header, hg1, hg2, sensorResetCorrection, srcAction, readingType, surge);
        }

        private void AddWaterLevelReading(
            List<Reading> readings,
            DateTimeOffset time,
            double? wl,
            double? gaugeCorrection,
            string wlHeader,
            double? hg1,
            double? hg2,
            double? sensorResetCorrection,
            string srcAction,
            string readingType,
            double? surge)
        {
            if (!wl.HasValue) return;

            var hgComments = GetHgComment(gaugeCorrection, hg1, hg2);

            var reading = AddReading(readings, time, Parameters.StageHg, Units.DistanceUnitId, wl.ToString());

            reading.Method = Config.StageWaterLevelMethodCode;
            reading.AdjustmentAmount = gaugeCorrection;
            reading.ReferencePointName = wlHeader;
            reading.Publish = true;
            reading.ReadingType = Enum.TryParse<ReadingType>(readingType, true, out var value) ? value : ReadingType.Unknown;
            reading.Uncertainty = surge;

            AddReadingComments(reading, sensorResetCorrection, srcAction, hgComments.ToArray());
        }

        private IEnumerable<string> GetHgComment(double? gc, double? hg1, double? hg2)
        {
            if (gc.HasValue)
                yield return $"GC: {gc:F3}";

            if (hg1.HasValue)
                yield return $"HG: {hg1:F3}";

            if (hg2.HasValue)
                yield return $"HG2: {hg2:F3}";
        }

        private void AddLoggerReading(
            List<Reading> readings,
            DateTimeOffset time,
            double? hg,
            double? gaugeCorrection,
            string hgHeader,
            double? sensorResetCorrection,
            string srcAction,
            double? surge)
        {
            if (!hg.HasValue) return;

            var reading = AddReading(readings, time, Parameters.StageHg, Units.DistanceUnitId, hg.ToString());

            reading.Method = Config.StageLoggerMethodCode;
            reading.Uncertainty = surge;

            var adjustmentAmount = sensorResetCorrection + gaugeCorrection;

            if (HasAdjustmentAmount(adjustmentAmount))
            {
                reading.AdjustmentAmount = adjustmentAmount;
            }

            AddReadingComments(reading, sensorResetCorrection, srcAction, hgHeader);
        }

        private void AddReadingComments(Reading reading, double? sensorResetCorrection, string srcAction, params string[] otherComments)
        {
            var comments = otherComments
                .Concat(new []
                {
                    HasAdjustmentAmount(sensorResetCorrection) ? $"SRC: {sensorResetCorrection:F3}" : null,
                    srcAction,
                })
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (comments.Any())
            {
                reading.Comments = string.Join("\r\n", comments);
            }
        }

        private static bool HasAdjustmentAmount(double? adjustmentAmount)
        {
            return adjustmentAmount.HasValue && !DoubleHelper.AreSame(adjustmentAmount, 0);
        }

        private const string PrimaryPrefix = "**";

        public static string SanitizeBenchmarkName(string value)
        {
            string[] invalidBM = { "RP1", "RP2", "RP3", "RP4", "RP5", "TP1", "TP2", "TP3", "TP4", "TP5" };
            if (value == null)
                return value;
            if (invalidBM.Contains(value))
                return null;
            if (value.StartsWith(PrimaryPrefix))
                return value.Substring(PrimaryPrefix.Length).Trim();
            return value;
        }
    }
}
