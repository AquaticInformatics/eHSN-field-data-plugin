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
            LevelCheckTables = levelChecksTables == null
                ? new List<EHSNLevelNotesLevelChecksLevelChecksTable>()
                : new List<EHSNLevelNotesLevelChecksLevelChecksTable>(levelChecksTables);

            LevelSummaryRows = summaryTableRows == null
                ? new List<EHSNLevelNotesLevelChecksSummaryTableRow>()
                : new List<EHSNLevelNotesLevelChecksSummaryTableRow>(summaryTableRows);
        }
    }
}
