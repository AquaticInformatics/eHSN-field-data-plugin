using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using EhsnPlugin.Helpers;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel;
using FieldDataPluginFramework.DataModel.ControlConditions;
using FieldDataPluginFramework.DataModel.DischargeActivities;
using FieldDataPluginFramework.DataModel.LevelSurveys;
using FieldDataPluginFramework.DataModel.PickLists;
using FieldDataPluginFramework.DataModel.Readings;

namespace EhsnPlugin.Mappers
{
    public class FieldVisitMapper
    {
        private Config Config { get; }
        private readonly EHSN _eHsn;
        private readonly LocationInfo _locationInfo;
        private readonly DateTime _visitDate;
        private readonly ILog _logger;

        public FieldVisitMapper(Config config, EHSN eHsn, LocationInfo locationInfo, ILog logger)
        {
            Config = config ?? throw new ArgumentException(nameof(config));
            _eHsn = eHsn ?? throw new ArgumentNullException(nameof(eHsn));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _locationInfo = locationInfo ?? throw new ArgumentNullException(nameof(locationInfo));

            _visitDate = GetVisitDate(eHsn.GenInfo);
        }

        public FieldVisitDetails MapFieldVisitDetails( )
        {
            var visitPeriod = GetVisitTimePeriod();

            return new FieldVisitDetails(visitPeriod)
            {
                Party = _eHsn.PartyInfo?.party,
                Weather = MapWeather(),
                Comments = MapComments()
            };
        }

        private static DateTime GetVisitDate(EHSNGenInfo eHsnGenInfo)
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

            if (!allTimes.Any())
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
            var channels = _eHsn.MidsecMeas?.DischargeMeasurement?.Channels ?? Array.Empty<EHSNMidsecMeasDischargeMeasurementChannel>();
            var stageMeasRows = _eHsn.StageMeas?.StageMeasTable ?? Array.Empty<EHSNStageMeasStageMeasRow>();
            var measResultTimes = _eHsn.MeasResults?.Times ?? Array.Empty<EHSNMeasResultsTime>();
            var levelChecksSummaryRows = _eHsn.LevelNotes?.LevelChecks?.LevelChecksSummaryTable ?? Array.Empty<EHSNLevelNotesLevelChecksSummaryTableRow>();
            var movingBoatTransectRows = _eHsn.MovingBoatMeas?.ADCPMeasTable ?? Array.Empty<EHSNMovingBoatMeasADCPMeasRow>();
            var levelChecksTable = _eHsn.LevelNotes?.LevelChecks?.LevelChecksTable[0]?.LevelChecksRow ?? Array.Empty<EHSNLevelNotesLevelChecksLevelChecksTableLevelChecksRow>();

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
                .Concat(levelChecksTable
                    .Select(row => ExtractTime(row.time)))
                .Concat(movingBoatTransectRows
                    .Select(row => ExtractJustTime(row.startTime)))
                .Append(ExtractTime(_eHsn.MovingBoatMeas?.ADCPMeasResults?.mmntStartTime))
                .Append(ExtractTime(_eHsn.MovingBoatMeas?.ADCPMeasResults?.mmntEndTime))
                .Append(ExtractTime(_eHsn.MovingBoatMeas?.ADCPMeasResults?.mmntMeanTime))
                .Append(ExtractTime(_eHsn.EnvCond?.gasArrTime))
                .Append(ExtractTime(_eHsn.EnvCond?.gasDepTime))
                .Append(ExtractTime(_eHsn.EnvCond?.feedArrTime))
                .Append(ExtractTime(_eHsn.EnvCond?.feedDepTime))
                .Append(ExtractTime(_eHsn.EnvCond?.bpmrotArrTime))
                .Append(ExtractTime(_eHsn.EnvCond?.bpmrotDepTime))
                .Append(ExtractTime(_eHsn.MeasResults?.loggerTimeTable?.Time7))
                .Append(ExtractTime(_eHsn.MeasResults?.loggerTimeTable?.Time8))
                .Append(ExtractTime(_eHsn.MeasResults?.loggerTimeTable?.Time9))
                .Append(ExtractTime(_eHsn.MeasResults?.loggerTimeTable?.Time10))
                .Where(d => d != DateTimeOffset.MinValue)
                .OrderBy(dateTimeOffset => dateTimeOffset)
                .ToList();
        }

        private IEnumerable<DateTimeOffset> ExtractAllTimes(EHSNMidsecMeasDischargeMeasurementChannel channel)
        {
            var panels = channel.Panels ?? Array.Empty<EHSNMidsecMeasDischargeMeasurementChannelPanel>();
            var edges = channel.Edges ?? Array.Empty<EHSNMidsecMeasDischargeMeasurementChannelEdge>();

            return panels
                .Select(panel => panel.Date)
                .Concat(edges
                    .Select(edge => edge.Date))
                .Select(dateTime => TimeHelper.CoerceDateTimeIntoUtcOffset(dateTime, _locationInfo.UtcOffset));
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

            var stringBuilder = new StringBuilder();

            AddCommentLine(stringBuilder, "Cloud Cover", _eHsn.EnvCond.cloudCover);
            AddCommentLine(stringBuilder, "Precipitation", _eHsn.EnvCond.precipitation);
            AddCommentLine(stringBuilder, "Wind Conditions", _eHsn.EnvCond.windMagnitude);
            AddCommentLine(stringBuilder, "Wind Speed", _eHsn.EnvCond.windMagnitudeSpeed);
            AddCommentLine(stringBuilder, "Wind Direction", _eHsn.EnvCond.windDirection);

            return stringBuilder.ToString().Trim();
        }

        private static void AddCommentLine(StringBuilder stringBuilder, string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var trimmedValue = value.Trim();

            // Single-line values map to a single line in the comment
            if (trimmedValue.Count(v => v == '\n') == 0)
            {
                stringBuilder.AppendFormat("{0}: {1}\n", label, trimmedValue);
                return;
            }

            // Multi-line values are de-marked
            stringBuilder.AppendFormat("== {0}:\n", label);
            stringBuilder.AppendFormat("{0}\n", trimmedValue);
            stringBuilder.Append("==\n");
        }

        private string MapComments()
        {
            var stringBuilder = new StringBuilder();

            AddCommentLine(stringBuilder, "Station Health Remarks", _eHsn.EnvCond?.stationHealthRemark);

            if (_eHsn.EnvCond?.intakeFlushed.ToBoolean() ?? false)
                AddCommentLine(stringBuilder, "Intake Flushed", $"@{_eHsn.EnvCond?.intakeTime}");

            if (_eHsn.EnvCond?.orificePurged.ToBoolean() ?? false)
                AddCommentLine(stringBuilder, "Orifice Purged", $"@{_eHsn.EnvCond?.orificeTime}");

            if (_eHsn.EnvCond?.downloadedProgram.ToBoolean() ?? false)
                AddCommentLine(stringBuilder, "Downloaded Program", string.Empty);

            if (_eHsn.EnvCond?.downloadedData.ToBoolean() ?? false)
                AddCommentLine(stringBuilder, "Downloaded Data", $"From {_eHsn.EnvCond?.dataPeriodStart} To {_eHsn.EnvCond?.dataPeriodEnd}");

            AddCommentLine(stringBuilder, "Stage Activity Summary Remarks", _eHsn.StageMeas?.stageRemark);
            AddCommentLine(stringBuilder, "Field Review Site Notes", _eHsn.FieldReview?.siteNotes);
            AddCommentLine(stringBuilder, "Field Review Plan Notes", _eHsn.FieldReview?.planNotes);

            return stringBuilder.ToString().Trim();
        }

        public DischargeActivity MapDischargeActivityOrNull()
        {
            return new DischargeActivityMapper(Config, _locationInfo, _visitDate, _eHsn, _logger).Map();
        }

        public IEnumerable<Reading> MapReadings()
        {
            return new ReadingMapper(Config, _locationInfo, _visitDate, _eHsn, _logger).Map();
        }

        public IEnumerable<LevelSurvey> MapLevelSurveys()
        {
            return new LevelSurveyMapper(_locationInfo, _visitDate, _logger).Map(_eHsn);
        }

        public ControlCondition MapControlConditionOrNull()
        {
            var conditionTypeText = _eHsn.DisMeas?.condition?.Trim();
            var conditionRemarks = _eHsn.DisMeas?.controlConditionRemark?.Trim();

            if (string.IsNullOrWhiteSpace(conditionTypeText) && string.IsNullOrWhiteSpace(conditionRemarks))
                return null;

            var controlCondition = new ControlCondition
            {
                Comments = conditionRemarks,
                Party = _eHsn.PartyInfo?.party,
            };

            if (!string.IsNullOrWhiteSpace(conditionTypeText))
            {
                if (Config.KnownControlConditions.TryGetValue(conditionTypeText, out var controlConditionValue))
                {
                    controlCondition.ConditionType = new ControlConditionPickList(controlConditionValue);
                }
                else
                {
                    controlCondition.Comments = string.Join("\n", new[] {conditionTypeText, controlCondition.Comments}
                        .Select(s => !string.IsNullOrWhiteSpace(s)));
                }
            }

            return controlCondition;
        }
    }
}
