using System.Collections.Generic;

namespace EhsnPlugin.DataModel
{
    public class ParsedEhsnLevelSurvey
    {
        public IReadOnlyList<EHSNLevelNotesLevelChecksLevelChecksTable> LevelCheckTables { get; set; }
        public IReadOnlyList<EHSNLevelNotesLevelChecksSummaryTableRow> LevelSummaryRows { get; set; }
        public string LevelCheckComments { get; set; }
        public string Party { get; set; }

        public ParsedEhsnLevelSurvey(EHSNLevelNotesLevelChecksLevelChecksTable[] levelChecksTables, 
            EHSNLevelNotesLevelChecksSummaryTableRow[] summaryTableRows)
        {
            levelChecksTables = levelChecksTables ?? new EHSNLevelNotesLevelChecksLevelChecksTable[0];

            foreach (var table in levelChecksTables)
            {
                table.LevelChecksRow = table.LevelChecksRow ?? new EHSNLevelNotesLevelChecksLevelChecksTableLevelChecksRow[0];

                foreach (var row in table.LevelChecksRow)
                {
                    row.station = SanitizeBenchmarkName(row.station);
                }
            }

            summaryTableRows = summaryTableRows ?? new EHSNLevelNotesLevelChecksSummaryTableRow[0];

            foreach (var row in summaryTableRows)
            {
                row.reference = SanitizeBenchmarkName(row.reference);
            }

            LevelCheckTables = new List<EHSNLevelNotesLevelChecksLevelChecksTable>(levelChecksTables);
            LevelSummaryRows = new List<EHSNLevelNotesLevelChecksSummaryTableRow>(summaryTableRows);
        }

        private const string PrimaryPrefix = "**";

        private static string SanitizeBenchmarkName(string value)
        {
            if (value == null || !value.StartsWith(PrimaryPrefix))
                return value;

            return value.Substring(PrimaryPrefix.Length);
        }
    }
}
