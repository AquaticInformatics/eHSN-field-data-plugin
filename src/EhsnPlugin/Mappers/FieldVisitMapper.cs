using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EhsnPlugin.DataModel;
using EhsnPlugin.Helpers;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel;
using FieldDataPluginFramework.DataModel.DischargeActivities;
using FieldDataPluginFramework.DataModel.LevelSurveys;
using FieldDataPluginFramework.DataModel.Readings;

namespace EhsnPlugin.Mappers
{
    public class FieldVisitMapper
    {
        private readonly EHSN _eHsn;
        private readonly LocationInfo _locationInfo;
        private readonly EhsnMeasurement _ehsnMeasurement;
        private readonly DateTime _visitDate;
        private readonly ILog _logger;

        public FieldVisitMapper(EHSN eHsn, LocationInfo locationInfo, ILog logger)
        {
            _eHsn = eHsn ?? throw new ArgumentNullException(nameof(eHsn));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _locationInfo = locationInfo ?? throw new ArgumentNullException(nameof(locationInfo));

            _visitDate = GetVisitDate(eHsn.GenInfo);

            _ehsnMeasurement = new MeasurementParser(eHsn, _visitDate, _locationInfo.UtcOffset).Parse();
        }

        public FieldVisitDetails MapFieldVisitDetails( )
        {
            var visitPeriod = GetVisitTimePeriod();

            return new FieldVisitDetails(visitPeriod)
            {
                Party = _eHsn.PartyInfo.party,
                Weather = MapWeather(),
                Comments = MapComments()
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

        private DateTimeInterval GetVisitTimePeriod()
        {
            var allTimes = ExtractAllTimes();

            if (allTimes.Count < 2)
                throw new ArgumentException($"Can't infer the start and end time. Too few time values found in the file. Date={_visitDate:yyyy/MM/dd} Count={allTimes.Count} Times={string.Join(", ", allTimes.Select(d => d.ToString("O")))}");

            return new DateTimeInterval(
                allTimes.First(),
                allTimes.Last());
        }

        private List<DateTimeOffset> ExtractAllTimes()
        {
            // just date in /GenInfo/date
            // DateTimeUtc in: (ignore UTC indicator, it is really a location local time)
            //  /MidsecMeas/DischargeMeasurement/Channels/Channel[]/Panels/Panel[]@Date
            //  /MidsecMeas/DischargeMeasurement/Channels/Channel[]/Edges/Edge[]@Date
            // time in:
            //  /StageMeas/StageMeasTable/StageMeasRow[]/time
            //  /DisMeas/startTime
            //  /DisMeas/endTime
            //  /DisMeas/mmtTimeVal
            //  /MeasResults/Times/Time[]
            //  /LevelNotes/LevelChecks/LevelChecksSummaryTable/time
            //  /MovingBoatMeas/ADCPMeasTable/ADCPMeasRow[]/startTime (HH:MM:SS)
            //  /MovingBoatMeas/ADCPMeasResults/mmntStartTime
            //  /MovingBoatMeas/ADCPMeasResults/mmntEndTime
            //  /MovingBoatMeas/ADCPMeasResults/mmntMeanTime
            var channels = _eHsn.MidsecMeas?.DischargeMeasurement?.Channels ?? new EHSNMidsecMeasDischargeMeasurementChannel[0];
            var stageMeasRows = _eHsn.StageMeas?.StageMeasTable ?? new EHSNStageMeasStageMeasRow[0];
            var measResultTimes = _eHsn.MeasResults?.Times ?? new EHSNMeasResultsTime[0];
            var levelChecksSummaryRows = _eHsn.LevelNotes?.LevelChecks?.LevelChecksSummaryTable ?? new EHSNLevelNotesLevelChecksSummaryTableRow[0];
            var movingBoatTransectRows = _eHsn.MovingBoatMeas?.ADCPMeasTable ?? new EHSNMovingBoatMeasADCPMeasRow[0];

            return channels
                .SelectMany(ExtractAllTimes)
                .Concat(stageMeasRows
                    .Select(row => ExtractTime(row.time)))
                .Append(ExtractTime(_eHsn.DisMeas.startTime))
                .Append(ExtractTime(_eHsn.DisMeas.endTime))
                .Append(ExtractTime(_eHsn.DisMeas.mmtTimeVal))
                .Concat(measResultTimes
                    .Select(time => ExtractTime(time.Value)))
                .Concat(levelChecksSummaryRows
                    .Select(row => ExtractTime(row.time)))
                .Concat(movingBoatTransectRows
                    .Select(row => ExtractJustTime(row.startTime)))
                .Append(ExtractTime(_eHsn.MovingBoatMeas?.ADCPMeasResults?.mmntStartTime))
                .Append(ExtractTime(_eHsn.MovingBoatMeas?.ADCPMeasResults?.mmntEndTime))
                .Append(ExtractTime(_eHsn.MovingBoatMeas?.ADCPMeasResults?.mmntMeanTime))
                .Where(d => d != DateTimeOffset.MinValue)
                .OrderBy(dateTimeOffset => dateTimeOffset)
                .ToList();
        }

        private IEnumerable<DateTimeOffset> ExtractAllTimes(EHSNMidsecMeasDischargeMeasurementChannel channel)
        {
            var panels = channel.Panels ?? new EHSNMidsecMeasDischargeMeasurementChannelPanel[0];
            var edges = channel.Edges ?? new EHSNMidsecMeasDischargeMeasurementChannelEdge[0];

            return panels
                .Select(panel => panel.Date)
                .Concat(edges
                    .Select(edge => edge.Date))
                .Select(dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified), _locationInfo.UtcOffset));
        }

        private DateTimeOffset ExtractTime(string timeText)
        {
            return TimeHelper.ParseTimeOrMinValue(timeText, _visitDate, _locationInfo.UtcOffset);
        }

        private DateTimeOffset ExtractJustTime(DateTime dateTime)
        {
            return new DateTimeOffset(
                _visitDate.Year,
                _visitDate.Month,
                _visitDate.Day,
                dateTime.Hour,
                dateTime.Minute,
                dateTime.Second,
                _locationInfo.UtcOffset);
        }

        private string MapWeather()
        {
            if (_eHsn.EnvCond == null) return null;

            var lines = new List<string>();

            AddCondition(lines, "Cloud Cover: ", _eHsn.EnvCond.cloudCover);
            AddCondition(lines, "Precipitation: ", _eHsn.EnvCond.precipitation);
            AddCondition(lines, "Wind Conditions: ", _eHsn.EnvCond.windMagnitude);
            AddCondition(lines, "Wind Speed: ", _eHsn.EnvCond.windMagnitudeSpeed);
            AddCondition(lines, "Wind Direction: ", _eHsn.EnvCond.windDirection);

            return string.Join("\n", lines);
        }

        private static void AddCondition(List<string> lines, string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            lines.Add($"{label}{value.Trim()}");
        }

        private string MapComments()
        {
            var lines = new List<string>();
            
            AddCondition(lines, string.Empty, _eHsn.EnvCond?.stationHealthRemark);
            AddCondition(lines, string.Empty, _eHsn.FieldReview?.siteNotes);

            return string.Join("\n", lines);
        }

        public DischargeActivity MapDischargeActivity()
        {
            return new DischargeActivityMapper(_ehsnMeasurement, _eHsn).Map();
        }

        public IEnumerable<Reading> MapReadings()
        {
            return new ReadingMapper(_ehsnMeasurement).Map();
        }

        public LevelSurvey MapLevelSurveyOrNull()
        {
            return new LevelSurveyMapper(_locationInfo, _visitDate, _logger).MapOrNull(_eHsn);
        }
    }
}
