using System;
using System.Collections.Generic;

namespace EhsnPlugin.DataModel
{
    public class ParsedEhsnLevelSurvey
    {
        public IReadOnlyList<EHSNLevelNotesLevelChecksLevelChecksTable> LevelCheckTables { get; }
        public string LevelCheckComments { get; set; }
        public string Party { get; set; }

        public ParsedEhsnLevelSurvey(EHSNLevelNotesLevelChecksLevelChecksTable[] levelChecksTables,
            EHSNLevelNotesLevelChecksSummaryTableRow[] summaryTableRows)
        {
            levelChecksTables ??= Array.Empty<EHSNLevelNotesLevelChecksLevelChecksTable>();

            foreach (var table in levelChecksTables)
            {
                table.LevelChecksRow ??= Array.Empty<EHSNLevelNotesLevelChecksLevelChecksTableLevelChecksRow>();

                foreach (var row in table.LevelChecksRow)
                {
                    if (row.station == null) { continue; }
                    row.station = SanitizeBenchmarkName(row.station);
                }
            }

            summaryTableRows ??= Array.Empty<EHSNLevelNotesLevelChecksSummaryTableRow>();

            foreach (var row in summaryTableRows)
            {
                row.reference = SanitizeBenchmarkName(row.reference);
            }

            LevelCheckTables = new List<EHSNLevelNotesLevelChecksLevelChecksTable>(levelChecksTables);
        }

        private const string PrimaryPrefix = "**";

        private static string SanitizeBenchmarkName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (value.StartsWith(PrimaryPrefix))
                return value.Substring(PrimaryPrefix.Length).Trim();

            return value;
        }
    }
}
