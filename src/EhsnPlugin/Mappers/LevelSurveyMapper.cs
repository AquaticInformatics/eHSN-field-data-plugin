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

        private bool HasLevelSurvey(ParsedEhsnLevelSurvey parsedItem)
        {
            return parsedItem.LevelSummaryRows.Any() &&
                   parsedItem.LevelCheckTables.Any();
        }

        public LevelSurvey MapOrNull(EHSN eHsn)
        {
            if (eHsn == null) throw new ArgumentNullException(nameof(eHsn));

            var parsedSurvey = ParseLevelSurveyInfo(eHsn);

            if (!HasLevelSurvey(parsedSurvey))
            {
                return null;
            }

            Validate(parsedSurvey);

            return ToLevelSurvey(parsedSurvey);
        }

        private void Validate(ParsedEhsnLevelSurvey parsedEhsnLevelSurvey)
        {
            var levelSummaryRows = parsedEhsnLevelSurvey.LevelSummaryRows;

            foreach (var row in levelSummaryRows)
            {
                if (string.IsNullOrWhiteSpace(row.reference))
                {
                    throw new EHsnPluginException("Invalid SummaryTableRow: 'reference' cannot be null or empty.");
                }

                if (string.IsNullOrWhiteSpace(row.time) ||
                    TimeHelper.ParseTimeOrMinValue(row.time, _visitDateTime.Date) == DateTime.MinValue)
                {
                    throw new EHsnPluginException($"Invalid SummaryTableRow: time is invalid {row.time}.");
                }
            }
        }

        private LevelSurvey ToLevelSurvey(ParsedEhsnLevelSurvey parsedEhsnLevelSurvey)
        {
            var originReferenceName = GetFirstReferenceInSummaryTableAsOriginReferenceName(
                parsedEhsnLevelSurvey.LevelSummaryRows);

            var levelSurveyMeasurements = GetLevelSurveyMeasurements(parsedEhsnLevelSurvey).ToList();

            return new LevelSurvey(originReferenceName)
            {
                Comments = parsedEhsnLevelSurvey.LevelCheckComments,
                LevelSurveyMeasurements = levelSurveyMeasurements,
                Party = parsedEhsnLevelSurvey.Party
            };
        }

        private string GetFirstReferenceInSummaryTableAsOriginReferenceName(
            IReadOnlyList<EHSNLevelNotesLevelChecksSummaryTableRow> levelSummaryRows)
        {
            return levelSummaryRows.First().reference;
        }

        private IEnumerable<LevelSurveyMeasurement> GetLevelSurveyMeasurements(
            ParsedEhsnLevelSurvey parsedEhsnLevelSurvey)
        {
            var levelMeasurements = new List<LevelSurveyMeasurement>();

            var levelSummaryRows = parsedEhsnLevelSurvey.LevelSummaryRows;
            var checkTables = parsedEhsnLevelSurvey.LevelCheckTables;

            var referenceGroupedRows = levelSummaryRows.GroupBy(row => row.reference);

            foreach (var summaryRows in referenceGroupedRows)
            {
                var reference = summaryRows.Key;
                var summaryRowList = summaryRows.ToList();
                if (summaryRowList.Count > 1)
                {
                    _logger.Error($"{reference} has multiple summary rows. Only first one is used.");
                }

                var summaryRow = summaryRowList.First();

                var time = TimeHelper.ParseTimeOrMinValue(summaryRow.time, _visitDateTime);
                var timeOffset = new DateTimeOffset(time, _locationInfo.UtcOffset);

                var measuredElevation = GetAveragedMeasuredElevation(reference, checkTables);

                var measurement = new LevelSurveyMeasurement(reference, timeOffset, measuredElevation)
                {
                    Comments = GetCombinedRemarks(reference, checkTables)
                };

                levelMeasurements.Add(measurement);
            }

            return levelMeasurements;
        }

        private double GetAveragedMeasuredElevation(string reference,
            IReadOnlyList<EHSNLevelNotesLevelChecksLevelChecksTable> checkTables)
        {
            var allCheckRows = GetCheckRows(reference, checkTables).ToList();

            if (!allCheckRows.Any())
            {
                throw new EHsnPluginException("Not enough level check rows to determine the elevation " +
                                              $"for {reference}.");
            }

            var allElevationSum = allCheckRows.Select(row => row.elevation).Sum();

            return (double)allElevationSum / allCheckRows.Count;
        }

        private string GetCombinedRemarks(string reference,
            IReadOnlyList<EHSNLevelNotesLevelChecksLevelChecksTable> checkTables)
        {
            var allRemarks = GetCheckRows(reference, checkTables)
                .Select(row => row.comments)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToList();

            return allRemarks.Any()
                ? string.Join("\n", allRemarks)
                : string.Empty;
        }

        private IEnumerable<EHSNLevelNotesLevelChecksLevelChecksTableLevelChecksRow> GetCheckRows(
            string reference, IReadOnlyList<EHSNLevelNotesLevelChecksLevelChecksTable> checkTables)
        {
            return checkTables == null
                ? new List<EHSNLevelNotesLevelChecksLevelChecksTableLevelChecksRow>()
                : checkTables.Where(t => t.LevelChecksRow != null)
                                    .SelectMany(t => t.LevelChecksRow)
                                    .Where(row => string.Equals(row.station, reference, StringComparison.OrdinalIgnoreCase));
        }
    }
}
