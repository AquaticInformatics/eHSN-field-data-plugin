using System;

namespace EhsnPlugin.Helpers
{
    public static class TimeHelper
    {
        public static DateTime GetMeanTimeTruncatedToMinute(DateTime start, DateTime end)
        {
            var exactMeanTime = new DateTime((start.Ticks + end.Ticks) / 2);

            var meanTime = new DateTime(
                exactMeanTime.Year,
                exactMeanTime.Month,
                exactMeanTime.Day,
                exactMeanTime.Hour,
                exactMeanTime.Minute,
                0);

            return meanTime < start ? start : meanTime;
        }
    }
}
