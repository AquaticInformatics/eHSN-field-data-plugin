using System;
using System.Collections.Generic;
using System.Linq;
using EhsnPlugin.DataModel;
using EhsnPlugin.Exceptions;
using EhsnPlugin.Helpers;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel.LevelSurveys;

namespace EhsnPlugin.Mappers
{
    public class LevelSurveyMapper
    {
        private readonly ILog _logger;

        private readonly LocationInfo _locationInfo;
        private readonly DateTime _visitDateTime;

        public LevelSurveyMapper(LocationInfo locationInfo, DateTime visitDate, ILog logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _locationInfo = locationInfo ?? throw new ArgumentNullException(nameof(locationInfo));

            _visitDateTime = visitDate;
        }

        private ParsedEhsnLevelSurvey ParseLevelSurveyInfo(EHSN eHsn)
        {
            return new ParsedEhsnLevelSurvey(eHsn.LevelNotes?.LevelChecks?.LevelChecksTable,
                           eHsn.LevelNotes?.LevelChecks?.LevelChecksSummaryTable)
            {
                LevelCheckComments = eHsn.LevelNotes?.LevelChecks?.comments,
                Party = eHsn.PartyInfo.party
            };
        }

        public IEnumerable<LevelSurvey> Map(EHSN eHsn)
        {
            if (eHsn == null) throw new ArgumentNullException(nameof(eHsn));

            var parsedSurvey = ParseLevelSurveyInfo(eHsn);

            var levelSurveyTime = GetLevelSurveyTime(eHsn);

            foreach (var table in parsedSurvey.LevelCheckTables)
            {
                var establishedRows = table.LevelChecksRow
                    .Where(row => row.establish.ToNullableDouble().HasValue)
                    .ToList();

                if (!establishedRows.Any())
                    continue;

                var originReferenceName = establishedRows.First().station;

                var measuredRows = establishedRows
                    .Where(row => row.foresight.ToNullableDouble().HasValue)
                    .ToList();

                if (!measuredRows.Any())
                    continue;

                yield return new LevelSurvey(originReferenceName)
                {
                    Comments = parsedSurvey.LevelCheckComments,
                    LevelSurveyMeasurements = measuredRows
                        .Select(row => new LevelSurveyMeasurement(row.station, levelSurveyTime, row.elevation.ToNullableDouble() ?? 0){Comments = row.comments})
                        .ToList(),
                    Party = parsedSurvey.Party,
                    // Method = "", // TODO: How to set "Differential Leveling"? Is it a picklist? Or a string?
                };
            }
        }

        private DateTimeOffset GetLevelSurveyTime(EHSN eHsn)
        {
            var aggregatedTimes = (eHsn.StageMeas?.StageMeasTable ?? new EHSNStageMeasStageMeasRow[0])
                .Where(row => row.MghCkbox.ToNullableBoolean() ?? false)
                .Select(row => TimeHelper.ParseTimeOrMinValue(row.time, _visitDateTime, _locationInfo.UtcOffset))
                .Where(time => time != DateTimeOffset.MinValue)
                .ToList();

            if (!aggregatedTimes.Any())
                throw new EHsnPluginException("Can't create average mean gage height time for level survey");

            var datetime = new DateTimeOffset(aggregatedTimes.Sum(time => time.Ticks) / aggregatedTimes.Count, _locationInfo.UtcOffset);

            // Truncate the seconds / fractional seconds
            return new DateTimeOffset(
                datetime.Year,
                datetime.Month,
                datetime.Day,
                datetime.Hour,
                datetime.Minute,
                0,
                _locationInfo.UtcOffset);
        }
    }
}
