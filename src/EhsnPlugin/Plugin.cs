using System;
using System.IO;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel;
using FieldDataPluginFramework.Results;

namespace EhsnPlugin
{
    public class Plugin : IFieldDataPlugin
    {
        public ParseFileResult ParseFile(Stream fileStream, IFieldDataResultsAppender fieldDataResultsAppender, ILog logger)
        {
            var eHsn = new Parser()
                .LoadFromStream(fileStream);

            if (eHsn == null)
                return ParseFileResult.CannotParse();

            var targetLocation = fieldDataResultsAppender.GetLocationByIdentifier("MyDummyLocation");

            var now = DateTimeOffset.UtcNow;

            var visit = fieldDataResultsAppender.AddFieldVisit(targetLocation,
                new FieldVisitDetails(new DateTimeInterval(now.AddHours(-1), now)));

            return ParseFileResult.SuccessfullyParsedAndDataValid();
        }

        public ParseFileResult ParseFile(Stream fileStream, LocationInfo targetLocation, IFieldDataResultsAppender fieldDataResultsAppender, ILog logger)
        {
            return ParseFile(fileStream, fieldDataResultsAppender, logger);
        }
    }
}
