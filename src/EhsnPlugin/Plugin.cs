using System;
using System.IO;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.Results;

namespace EhsnPlugin
{
    public class Plugin : IFieldDataPlugin
    {
        public ParseFileResult ParseFile(Stream fileStream, IFieldDataResultsAppender fieldDataResultsAppender, ILog logger)
        {
            try
            {
                var parser = new Parser(fieldDataResultsAppender, logger);
                var eHsn = parser.LoadFromStream(fileStream);

                if (eHsn == null)
                    return ParseFileResult.CannotParse();

                try
                {
                    parser.Parse(eHsn);

                    return ParseFileResult.SuccessfullyParsedAndDataValid();
                }
                catch(Exception exception)
                {
                    logger.Error($"File can be parsed but an error occurred: {exception.Message}\n{exception.StackTrace}");
                    return ParseFileResult.SuccessfullyParsedButDataInvalid(exception);
                }
            }
            catch(Exception exception)
            {
                logger.Error($"Something went wrong: {exception.Message}\n{exception.StackTrace}");
                return ParseFileResult.CannotParse(exception);
            }
        }

        public ParseFileResult ParseFile(Stream fileStream, LocationInfo targetLocation, IFieldDataResultsAppender fieldDataResultsAppender, ILog logger)
        {
            return ParseFile(fileStream, fieldDataResultsAppender, logger);
        }
    }
}
