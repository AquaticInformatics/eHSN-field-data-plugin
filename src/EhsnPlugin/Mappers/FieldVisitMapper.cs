using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EhsnPlugin.DataModel;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel;
using FieldDataPluginFramework.DataModel.DischargeActivities;

namespace EhsnPlugin.Mappers
{
    public class FieldVisitMapper
    {
        private readonly EHSN _eHsn;
        private readonly LocationInfo _locationInfo;
        private readonly EhsnMeasurement _ehsnMeasurement;
        private readonly DateTime _visitDate;
        
        public FieldVisitMapper(EHSN eHsn, LocationInfo locationInfo)
        {
            _eHsn = eHsn ?? throw new ArgumentNullException(nameof(eHsn));
            _locationInfo = locationInfo;

            _visitDate = GetVisitDate(eHsn.GenInfo);

            _ehsnMeasurement = new MeasurementParser(eHsn, _visitDate, _locationInfo.UtcOffset).Parse();
        }

        public FieldVisitDetails MapFieldVisitDetails( )
        {
            var visitPeriod = GetVisitTimePeriod(_locationInfo);

            return new FieldVisitDetails(visitPeriod)
            {
                Party = _eHsn.PartyInfo.party
            };
        }

        private DateTime GetVisitDate(EHSNGenInfo eHsnGenInfo)
        {
            if (string.IsNullOrWhiteSpace(eHsnGenInfo?.date?.Value))
            {
                throw new ArgumentNullException(nameof(eHsnGenInfo), "Missing visit date in the file.");
            }

            return DateTime.ParseExact(eHsnGenInfo.date.Value, "yyyy/MM/dd", CultureInfo.InvariantCulture);
        }

        private DateTimeInterval GetVisitTimePeriod(LocationInfo locationInfo)
        {
            var start = _visitDate;
            //Use end of day minus 1 minute as the visit end time:
            var end = _visitDate.AddDays(1).AddMinutes(-1);

            //Use the earliest time and latest time found in measurements if any as start/end times:
            var allMeasurementTimes =
                new List<DateTime>(_ehsnMeasurement.EnvironmentConditionMeasurements.Select(m => m.StartTime));
            allMeasurementTimes.AddRange(_ehsnMeasurement.DischargeMeasurements.Select(m => m.StartTime));
            allMeasurementTimes.AddRange(_ehsnMeasurement.SensorStageMeasurements.Select(m => m.StartTime));
            allMeasurementTimes.AddRange(_ehsnMeasurement.StageMeasurements.Select(m => m.StartTime));

            if(allMeasurementTimes.Any())
            {
                allMeasurementTimes.Sort();
                start = allMeasurementTimes.First();
                end = allMeasurementTimes.Last();
            }

            return new DateTimeInterval(new DateTimeOffset(start, locationInfo.UtcOffset),
                new DateTimeOffset(end, locationInfo.UtcOffset));
        }

        public DischargeActivity MapDischargeActivity()
        {
            return new DischargeActivityMapper(_ehsnMeasurement, _eHsn).Map();
        }
    }
}
