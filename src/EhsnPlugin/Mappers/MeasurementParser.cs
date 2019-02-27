using System;
using System.Collections.Generic;
using System.Linq;
using EhsnPlugin.DataModel;
using EhsnPlugin.Helpers;
using EhsnPlugin.SystemCode;

namespace EhsnPlugin.Mappers
{
    public class MeasurementParser
    {
        private readonly EHSN _eHsn;
        private readonly TimeSpan _locationUtcOffset;
        private DateTime _visitDate;

        public MeasurementParser(EHSN eHsn, DateTime visitDate, TimeSpan locationUtcOffset)
        {
            _eHsn = eHsn ?? throw new ArgumentNullException(nameof(eHsn));

            _visitDate = visitDate;
            _locationUtcOffset = locationUtcOffset;
        }

        public EhsnMeasurement Parse()
        {
            var measurementMeanTime = GetMeasurementMeanTimeOrMinValue();

            return new EhsnMeasurement
            {
                LocationUtcOffset = _locationUtcOffset,
                StageMeasurements = ParseStageMeasurements(),
                DischargeMeasurements = ParseDischargeMeasurements(),
                EnvironmentConditionMeasurements = ParseEnvironmentConditionMeasurements(measurementMeanTime),
                SensorStageMeasurements = ParseMeasResults()
            };
        }

        private DateTime GetMeasurementMeanTimeOrMinValue()
        {
            if (_eHsn.DisMeas == null)
                return DateTime.MinValue;

            //Start/End times should both exist in eHSN files:
            var start = ParseTimeOrMinValue(_eHsn.DisMeas.startTime);
            var end = ParseTimeOrMinValue(_eHsn.DisMeas.endTime);

            if (start == DateTime.MinValue || end == DateTime.MinValue)
                return DateTime.MinValue;

            return TimeHelper.GetMeanTimeTruncatedToMinute(start, end);
        }

        private DateTime ParseTimeOrMinValue(string timeString)
        {
            return TimeHelper.ParseTimeOrMinValue(timeString, _visitDate, _locationUtcOffset).DateTime;
        }

        private List<MeasurementRecord> ParseStageMeasurements()
        {
            var measurements = new List<MeasurementRecord>();

            if (_eHsn.StageMeas?.StageMeasTable == null)
                return measurements;

            foreach (var row in _eHsn.StageMeas?.StageMeasTable)
            {
                var time = ParseTimeOrMinValue(row.time);
                if (time == DateTime.MinValue)
                    continue;

                var value = row.WL1 ?? row.HG1;

                if (value == null) continue;

                var measurement = new MeasurementRecord(time, time, Parameters.StageHg, 
                    Units.DistanceUnitId, double.Parse(value))
                {
                    Remark = GetStageRowRemark(row)
                };

                measurements.Add(measurement);
            }

            return measurements;
        }

        private string GetStageRowRemark(EHSNStageMeasStageMeasRow row)
        {
            return !string.IsNullOrWhiteSpace(row.SRC)
                ? $"@{row.time} {row.SRCApp}. Correction:{row.SRC}"
                : string.Empty;
        }

        private List<MeasurementRecord> ParseDischargeMeasurements()
        {
            var measurements = new List<MeasurementRecord>();

            if (_eHsn.DisMeas == null)
                return measurements;

            var start = ParseTimeOrMinValue(_eHsn.DisMeas.startTime);
            var end = ParseTimeOrMinValue(_eHsn.DisMeas.endTime);

            //Section width:
            measurements.Add(new MeasurementRecord(start, end, Parameters.RiverSectionWidth, Units.DistanceUnitId, (double)_eHsn.DisMeas.width));

            //Section area:
            measurements.Add(new MeasurementRecord(start, end, Parameters.RiverSectionArea, Units.AreaUnitId, (double)_eHsn.DisMeas.area));

            //Velocity:
            measurements.Add(new MeasurementRecord(start, end, Parameters.WaterVelocityWv, Units.VelocityUnitId, (double)_eHsn.DisMeas.meanVel));

            //Mean gauge height:
            measurements.Add(new MeasurementRecord(start, end, Parameters.StageHg, Units.DistanceUnitId, (double)_eHsn.DisMeas.mgh));

            //Discharge:
            measurements.Add(new MeasurementRecord(start, end, Parameters.DischargeQr, Units.DischargeUnitId, (double)_eHsn.DisMeas.discharge));

            return measurements;
        }

        private List<MeasurementRecord> ParseEnvironmentConditionMeasurements(DateTime measurementTime)
        {
            var measurements = new List<MeasurementRecord>();

            if (_eHsn.EnvCond?.batteryVolt == null)
                return measurements;

            measurements.Add(new MeasurementRecord(measurementTime, measurementTime, Parameters.Voltage, Units.VoltageUnitId,
                double.Parse(_eHsn.EnvCond.batteryVolt)));

            return measurements;
        }

        private List<MeasurementRecord> ParseMeasResults()
        {
            var measurements = new List<MeasurementRecord>();

            if (_eHsn.MeasResults?.SensorRefs == null ||
                !_eHsn.MeasResults.SensorRefs.Any())
            {
                return measurements;
            }

            //From the sample files, 4 sonsor refs are found:
            // "IQ Plus Discharge","IQ Plus Velocity","IQ Plus Temperature","Head Stage (m)"
            // Only "Head Stage" is known to map to parameter HD. 
            // Using it as an example here to map sensor values to readings.

            var measResults = _eHsn.MeasResults;

            for (int index = 0; index < measResults.SensorRefs.Length; ++index)
            {
                var sensorRef = measResults.SensorRefs[index].Value?.Trim();
                var timeString = measResults.Times[index].Value;

                switch (sensorRef)
                {
                    case "Head Stage (m)":
                        var time = ParseTimeOrMinValue(timeString);
                        if (time == DateTime.MinValue)
                            break;
                        var value = measResults.SensorVals[index].Value;

                        measurements.Add(new MeasurementRecord(time, time, Parameters.HeadStage, Units.DistanceUnitId, double.Parse(value)));
                        break;
                }
            }

            return measurements;
        }
    }
}
