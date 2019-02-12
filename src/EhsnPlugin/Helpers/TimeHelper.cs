﻿using System;
using System.Text.RegularExpressions;

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

        public static DateTime ParseTimeOrMinValue(string timeString, DateTime visitDate)
        {
            if (string.IsNullOrWhiteSpace(timeString) ||
                !Regex.IsMatch(timeString, @"^\d{2}:\d{2}(:\d{2}){0,1}$"))
            {
                return DateTime.MinValue;
            }

            var parts = timeString.Split(':');
            var hour = Int32.Parse(parts[0]);
            var minute = Int32.Parse(parts[1]);
            var second = parts.Length == 3 ? Int32.Parse(parts[2]) : 0;

            return new DateTime(visitDate.Year, visitDate.Month, visitDate.Day, hour, minute, second);
        }
    }
}