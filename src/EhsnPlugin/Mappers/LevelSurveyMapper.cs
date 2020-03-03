using System;
using System.Collections.Generic;
using System.Linq;
using EhsnPlugin.DataModel;
using EhsnPlugin.Exceptions;
using EhsnPlugin.Helpers;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel.LevelSurveys;
using MoreLinq;

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

            var levelSurvey = (LevelSurvey) null;
            var levelSurveyTime = (DateTimeOffset?) null;

            foreach (var table in parsedSurvey.LevelCheckTables)
            {
                if (!table.upload.ToNullableBoolean() ?? false)
                    continue;

                var establishedRows = table.LevelChecksRow
                    .Where(row => row.establish.ToNullableDouble().HasValue)
                    .ToList();

                if (!establishedRows.Any())
                    continue;

                var originReferenceName = establishedRows.First().station;

                var measuredRows = establishedRows
                    .Where(row => row == establishedRows.First() || row.foresight.ToNullableDouble().HasValue)
                    .ToList();

                if (!measuredRows.Any())
                    continue;

                if (levelSurvey == null)
                {
                    levelSurvey = new LevelSurvey(originReferenceName)
                    {
                        Comments = parsedSurvey.LevelCheckComments,
                        Party = parsedSurvey.Party
                    };
                }
                else if (levelSurvey.OriginReferencePointName != originReferenceName)
                {
                    _logger.Error($"Can't change the {nameof(LevelSurvey)}.{nameof(levelSurvey.OriginReferencePointName)} from '{levelSurvey.OriginReferencePointName}' to '{originReferenceName}'. Retaining first origin.");
                }

                if (!levelSurveyTime.HasValue)
                {
                    levelSurveyTime = GetLevelSurveyTime(eHsn);
                }

                var distinctRows = measuredRows
                    .DistinctBy(r => new {r.station})
                    .ToList();

                var skippedRows = measuredRows
                    .Where(r => !distinctRows.Contains(r))
                    .ToList();

                if (skippedRows.Any())
                {
                    foreach (var skippedRow in skippedRows)
                    {
                        var keptRow = distinctRows.First(r => r.station == skippedRow.station);

                        _logger.Error($"'{keptRow.station}' using first circuit measurement of {keptRow.elevation}, and ignoring secondary measurement of {skippedRow.elevation}");
                    }
                }

                var measurements = distinctRows
                    .Select(row => new LevelSurveyMeasurement(row.station, levelSurveyTime.Value, row.elevation.ToNullableDouble() ?? 0) {Comments = row.comments})
                    .ToList();

                var secondaryMeasurements = measurements
                    .Where(m => IsReferencePointMeasured(levelSurvey, m.ReferencePointName))
                    .ToList();

                foreach (var secondaryMeasurement in secondaryMeasurements)
                {
                    var existingMeasurement = levelSurvey
                        .LevelSurveyMeasurements
                        .Single(m => m.ReferencePointName.Equals(secondaryMeasurement.ReferencePointName, StringComparison.InvariantCultureIgnoreCase));

                    if (!DoubleHelper.AreEqual(secondaryMeasurement.MeasuredElevation, existingMeasurement.MeasuredElevation))
                    {
                        _logger.Error($"'{existingMeasurement.ReferencePointName}' with first measured elevation of {existingMeasurement.MeasuredElevation}. Ignoring secondary measured elevation of {secondaryMeasurement.MeasuredElevation}");
                    }
                    else
                    {
                        _logger.Info($"'{existingMeasurement.ReferencePointName}' was remeasured a second time with the same elevation of {secondaryMeasurement.MeasuredElevation}");
                    }
                }

                var newMeasurements = measurements
                    .Where(m => !secondaryMeasurements.Contains(m))
                    .ToList();

                levelSurvey.LevelSurveyMeasurements.AddRange(newMeasurements);
            }

            var levelSurveys = new List<LevelSurvey>();

            if (levelSurvey != null)
            {
                levelSurveys.Add(levelSurvey);
            }

            return levelSurveys;
        }

        private bool IsReferencePointMeasured(LevelSurvey levelSurvey, string referencePointName)
        {
            return levelSurvey
                .LevelSurveyMeasurements
                .Any(m => m.ReferencePointName.Equals(referencePointName, StringComparison.InvariantCultureIgnoreCase));
        }

        private DateTimeOffset GetLevelSurveyTime(EHSN eHsn)
        {
            var aggregatedTimes = (eHsn.StageMeas?.StageMeasTable ?? new EHSNStageMeasStageMeasRow[0])
                .Select(row => TimeHelper.ParseTimeOrMinValue(row.time, _visitDateTime, _locationInfo.UtcOffset))
                .Where(time => time != DateTimeOffset.MinValue)
                .ToList();

            if (!aggregatedTimes.Any())
            {
                // return Discharge mean time if no time in stage table
                String meanDisTime = eHsn.DisMeas.mmtTimeVal;
                if (meanDisTime == null)
                {
                    throw new EHsnPluginException("Can't create average mean gage height time for level survey");
                }
                else
                {
                    String[] timeString = meanDisTime.Split(':');
                    int hour = Int32.Parse(timeString[0]);
                    int min = Int32.Parse(timeString[1]);

                    return new DateTimeOffset(_visitDateTime.Year, _visitDateTime.Month, _visitDateTime.Day, hour, min, 0, _locationInfo.UtcOffset);
                }
                
            }
            else
            {

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
}
