using System;
using System.Linq;
using EhsnPlugin.DataModel;
using EhsnPlugin.SystemCode;
using FieldDataPluginFramework.DataModel;
using FieldDataPluginFramework.DataModel.ChannelMeasurements;
using FieldDataPluginFramework.DataModel.DischargeActivities;

namespace EhsnPlugin.Mappers
{
    public class DischargeActivityMapper
    {
        private readonly EhsnMeasurement _ehsnMeasurement;
        private readonly EHSN _ehsn;

        public DischargeActivityMapper(EhsnMeasurement ehsnMeasurement, EHSN eHsn)
        {
            _ehsnMeasurement = ehsnMeasurement;
            _ehsn = eHsn;
        }

        public DischargeActivity Map()
        {
            var dischargeMeasurement = _ehsnMeasurement.DischargeMeasurements.First(m => m.ParameterId == Parameters.DischargeQr);

            var dischargeActivity = CreateDischargeActivityWithSummary(dischargeMeasurement);

            SetDischargeSection(dischargeActivity, dischargeMeasurement);

            return dischargeActivity;
        }

        private DischargeActivity CreateDischargeActivityWithSummary(MeasurementRecord dischargeMeasurement)
        {
            var factory = new DischargeActivityFactory(Units.MetricUnitSystem);

            //Discharge summary:
            var measurementPeriod = GetMeasurementPeriod();
            var dischargeActivity = factory.CreateDischargeActivity(measurementPeriod, dischargeMeasurement.Value);

            dischargeActivity.Comments = dischargeMeasurement.Remark;
            dischargeActivity.Party = _ehsn.PartyInfo.party;

            //Mean gage height:
            AddMeanGageHeight(dischargeActivity, _ehsnMeasurement.LocationUtcOffset);
            
            return dischargeActivity;
        }

        private DateTimeInterval GetMeasurementPeriod()
        {
            //All discharge measurements have the same start/end times:
            var measurement = _ehsnMeasurement.DischargeMeasurements.First();

            var start = new DateTimeOffset(measurement.StartTime, _ehsnMeasurement.LocationUtcOffset);
            var end = new DateTimeOffset(measurement.EndTime, _ehsnMeasurement.LocationUtcOffset);

            return new DateTimeInterval(start, end);
        }

        private void AddMeanGageHeight(DischargeActivity dischargeActivity, TimeSpan locationInfoUtcOffset)
        {
            var stageRecord = _ehsnMeasurement.DischargeMeasurements.First(m => m.ParameterId == Parameters.StageHg);

            var measurement = new GageHeightMeasurement(new Measurement(stageRecord.Value, stageRecord.UnitId), 
                new DateTimeOffset(stageRecord.StartTime, locationInfoUtcOffset)) ;

            dischargeActivity.GageHeightMeasurements.Add(measurement);
        }

        private void SetDischargeSection(DischargeActivity dischargeActivity, MeasurementRecord dischargeMeasurement)
        {
            var dischargeSection = CreateDischargeSectionWithDescription(dischargeActivity, dischargeMeasurement);

            SetChannelObservations(dischargeSection);

            dischargeActivity.ChannelMeasurements.Add(dischargeSection);
        }

        private ManualGaugingDischargeSection CreateDischargeSectionWithDescription(DischargeActivity dischargeActivity, 
            MeasurementRecord dischargeMeasurement)
        {
            var factory = new ManualGaugingDischargeSectionFactory(Units.MetricUnitSystem);
            var manualGaugingDischarge = factory.CreateManualGaugingDischargeSection(dischargeActivity.MeasurementPeriod, dischargeMeasurement.Value);

            //Party: 
            manualGaugingDischarge.Party = dischargeActivity.Party;

            //Discharge method default to mid-section:
            manualGaugingDischarge.DischargeMethod = DischargeMethodType.MidSection;

            return manualGaugingDischarge;
        }

        private void SetChannelObservations(ManualGaugingDischargeSection dischargeSection)
        {
            //River area:
            var areaMeasurement = _ehsnMeasurement.DischargeMeasurements.First(m => m.ParameterId == Parameters.RiverSectionArea);
            dischargeSection.AreaUnitId = areaMeasurement.UnitId;
            dischargeSection.AreaValue = areaMeasurement.Value;

            //Width:
            var widthMeasurement = _ehsnMeasurement.DischargeMeasurements.First(m => m.ParameterId == Parameters.RiverSectionWidth);
            dischargeSection.WidthValue = widthMeasurement.Value;

            //Velocity:
            var velocityMeasurement = _ehsnMeasurement.DischargeMeasurements.First(m => m.ParameterId == Parameters.WaterVelocityWv);
            dischargeSection.VelocityUnitId = velocityMeasurement.UnitId;
            dischargeSection.VelocityAverageValue = velocityMeasurement.Value;
        }
    }
}
