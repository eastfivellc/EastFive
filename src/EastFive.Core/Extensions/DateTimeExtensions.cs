using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive
{
    public static class DateTimeExtensions
    {
        #region Equality

        #region Seconds

        #region EqualToSecond

        public static bool EqualToSecond(this DateTime time1, DateTime time2)
        {
            return
                time1.EqualToMinute(time2) &&
                time1.Second == time2.Second;
        }

        public static bool EqualToSecond(this DateTime? time1, DateTime? time2)
        {
            if (time1.HasValue != time2.HasValue)
                return false;
            if (!time1.HasValue)
                return false;
            return time1.Value.EqualToSecond(time2);
        }

        public static bool EqualToSecond(this DateTime time1, DateTime? time2)
        {
            if (!time2.HasValue)
                return false;
            return time1.EqualToSecond(time2.Value);
        }

        public static bool EqualToSecond(this DateTime? time1, DateTime time2)
        {
            return time2.EqualToSecond(time1);
        }

        #endregion

        #region IsEqualToOrPriorToSecond

        public static bool IsEqualToOrPriorToSecond(this DateTime time1, DateTime time2)
        {
            if (time1.EqualToSecond(time2))
                return true;
            return time1.IsPriorToSecond(time2);
        }

        #endregion

        #region IsPriorToSecond

        public static bool IsPriorToSecond(this DateTime time1, DateTime time2)
        {
            var delta = time2 - time1;
            return (delta.TotalSeconds >= 1.0);
        }

        #endregion

        #endregion

        #region EqualToMinute

        public static bool EqualToMinute(this DateTime time1, DateTime time2)
        {
            return
                time1.EqualToDay(time2) &&
                time1.Hour == time2.Hour &&
                time1.Minute == time2.Minute;
        }

        public static bool EqualToMinute(this DateTime? time1, DateTime? time2)
        {
            if (time1.HasValue != time2.HasValue)
                return false;
            if (!time1.HasValue)
                return false;
            return time1.Value.EqualToMinute(time2);
        }

        public static bool EqualToMinute(this DateTime time1, DateTime? time2)
        {
            if (!time2.HasValue)
                return false;
            return time1.EqualToMinute(time2.Value);
        }

        public static bool EqualToMinute(this DateTime? time1, DateTime time2)
        {
            return time2.EqualToMinute(time1);
        }

        #endregion

        #region EqualToDay

        public static bool EqualToDay(this DateTime time1, DateTime time2)
        {
            return
                time1.Year == time2.Year &&
                time1.DayOfYear == time2.DayOfYear;
        }

        public static bool EqualToDay(this DateTime? time1, DateTime? time2)
        {
            if (time1.HasValue != time2.HasValue)
                return false;
            if (!time1.HasValue)
                return false;
            return time1.Value.EqualToDay(time2);
        }

        public static bool EqualToDay(this DateTime time1, DateTime? time2)
        {
            if (!time2.HasValue)
                return false;
            return time1.EqualToDay(time2.Value);
        }

        public static bool EqualToDay(this DateTime? time1, DateTime time2)
        {
            return time2.EqualToDay(time1);
        }

        /// <summary>
        /// Check if <paramref name="time1"/> is the day proir to <paramref name="time2"/> or earlier
        /// </summary>
        /// <param name="time1"></param>
        /// <param name="time2"></param>
        /// <returns></returns>
        public static bool IsEqualOrProirToDay(this DateTime time1, DateTime time2)
        {
            if (time1.EqualToDay(time2))
                return true;
            return time1 < time2;
        }

        public static bool IsProirToDay(this DateTime time1, DateTime time2)
        {
            if (time1.EqualToDay(time2))
                return false;
            return time1 < time2;
        }

        public static bool IsLaterDay(this DateTime time1, DateTime time2)
        {
            if (time1.EqualToDay(time2))
                return false;
            return time1 > time2;
        }

        #endregion

        #region EqualToMonth

        public static bool EqualToMonth(this DateTime time1, DateTime time2)
        {
            return
                time1.Year == time2.Year &&
                time1.Month == time2.Month;
        }

        public static bool EqualToMonth(this DateTime? time1, DateTime? time2)
        {
            if (time1.HasValue != time2.HasValue)
                return false;
            if (!time1.HasValue)
                return false;
            return time1.Value.EqualToMonth(time2);
        }

        public static bool EqualToMonth(this DateTime time1, DateTime? time2)
        {
            if (!time2.HasValue)
                return false;
            return time1.EqualToMonth(time2.Value);
        }

        public static bool EqualToMonth(this DateTime? time1, DateTime time2)
        {
            return time2.EqualToMonth(time1);
        }

        #endregion

        #endregion

        #region Misc

        public static int BirthDateToAge(this DateTime dateOfBirth, DateTime? asOfMaybe = default)
        {
            var asOf = asOfMaybe.HasValue ? asOfMaybe.Value : DateTime.Now;
            int years = asOf.Year - dateOfBirth.Year;
            if ((dateOfBirth.Month > asOf.Month) || (dateOfBirth.Month == asOf.Month && dateOfBirth.Day > asOf.Day))
                years--;
            return years;
        }

        public static string BirthDateUnder2Formatted(this DateTime dateOfBirth)
        {
            // under two:  1m23do  (1 month 23 days old)
            var age = dateOfBirth.BirthDateToAge();
            if (age >= 2)
                return $"{age}yo";

            var asOf = DateTime.Today;
            var months = 0;
            while (asOf >= dateOfBirth.AddMonths(months + 1))
                months++;

            var days = 0;
            while (asOf >= dateOfBirth.AddMonths(months).AddDays(days + 1))
                days++;

            return $"{months}m{days}do";
        }

        public static DateTime AsKindUnspecified(this DateTime date)
        {
            // Printing Kind=Unspecified for ISO formatting looks like this (without any Z or -0:00 formatting):
            // 2019-12-18T00:00:00.0000000
            // A javascript client will assume this time is in whatever local time it has and not attempt any conversion
            // which is especially helpful for a date-only DateTime where we want what is sent to be displayed the same.
            return new DateTime(date.Ticks, DateTimeKind.Unspecified);
        }

        public static DateTime GetDayOfWeek(this DateTime date, DayOfWeek day)
        {
            // Sunday = 0
            var sunday = date.Date.AddDays(-(int)date.DayOfWeek);
            return sunday.AddDays((int)day);
        }

        public static DateTime GetFirstOfMonth(this DateTime date)
        {
            return new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        public static DateTime GetLastOfMonth(this DateTime date)
        {
            return date.AddMonths(1).GetFirstOfMonth().AddDays(-1);
        }

        public static DateTime GetFirstOfYear(this DateTime date)
        {
            return new DateTime(date.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        public static DateTime GetLastDayOfYear(this DateTime date)
        {
            return new DateTime(date.Year, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        }

        public static DateTime GetBusinessDay(this DateTime date)
        {
            DayOfWeek dayName = date.DayOfWeek;
            if (dayName == DayOfWeek.Saturday)
                return date.AddDays(2).GetBusinessDay();

            if (dayName == DayOfWeek.Sunday)
                return date.AddDays(1).GetBusinessDay();

            if (date.IsFederalHoliday())
                return date.AddDays(1).GetBusinessDay();

            return date;
        }

        // Taken from  https://www.codeproject.com/Tips/1168428/US-Federal-Holidays-Csharp
        public static bool IsFederalHoliday(this DateTime date)
        {
            // No holidays in March, April, June, August
            if (date.Month == 3 || date.Month == 4 || date.Month == 6 || date.Month == 8)
                return false;

            int nthWeekDay = (int)(Math.Ceiling((double)date.Day / 7.0d));
            DayOfWeek dayName = date.DayOfWeek;
            bool isThursday = dayName == DayOfWeek.Thursday;
            bool isFriday = dayName == DayOfWeek.Friday;
            bool isMonday = dayName == DayOfWeek.Monday;
            bool isWeekend = dayName == DayOfWeek.Saturday || dayName == DayOfWeek.Sunday;

            // New Years Day (Jan 1, or preceding Friday/following Monday if weekend)
            if ((date.Month == 12 && date.Day == 31 && isFriday) ||
                (date.Month == 1 && date.Day == 1 && !isWeekend) ||
                (date.Month == 1 && date.Day == 2 && isMonday)) return true;

            // MLK day (3rd monday in January)
            if (date.Month == 1 && isMonday && nthWeekDay == 3) return true;

            // President’s Day (3rd Monday in February)
            if (date.Month == 2 && isMonday && nthWeekDay == 3) return true;

            // Memorial Day (Last Monday in May)
            if (date.Month == 5 && isMonday && date.AddDays(7).Month == 6) return true;

            // Independence Day (July 4, or preceding Friday/following Monday if weekend)
            if ((date.Month == 7 && date.Day == 3 && isFriday) ||
                (date.Month == 7 && date.Day == 4 && !isWeekend) ||
                (date.Month == 7 && date.Day == 5 && isMonday)) return true;

            // Labor Day (1st Monday in September)
            if (date.Month == 9 && isMonday && nthWeekDay == 1) return true;

            // Columbus Day (2nd Monday in October)
            if (date.Month == 10 && isMonday && nthWeekDay == 2) return true;

            // Veteran’s Day (November 11, or preceding Friday/following Monday if weekend))
            if ((date.Month == 11 && date.Day == 10 && isFriday) ||
                (date.Month == 11 && date.Day == 11 && !isWeekend) ||
                (date.Month == 11 && date.Day == 12 && isMonday)) return true;

            // Thanksgiving Day (4th Thursday in November)
            if (date.Month == 11 && isThursday && nthWeekDay == 4) return true;

            // Christmas Day (December 25, or preceding Friday/following Monday if weekend))
            if ((date.Month == 12 && date.Day == 24 && isFriday) ||
                (date.Month == 12 && date.Day == 25 && !isWeekend) ||
                (date.Month == 12 && date.Day == 26 && isMonday)) return true;

            return false;
        }

        public static bool GetDisappearingOffset(string timeAndOffset, out TimeSpan duration) // ex: "02:00:00 -05:00", if no offset, .net assumes UTC
        {
            duration = TimeSpan.Zero;
            if (!DateTimeOffset.TryParse(timeAndOffset, out DateTimeOffset parsed))  // .net adds the date part for us when blank
                return false;

            var diff = parsed - DateTime.UtcNow;

            // represents duration until the next given time
            duration = diff >= TimeSpan.Zero
                ? diff
                : TimeSpan.FromHours(24) + diff;
            return true;
        }

    #endregion Misc

    #region Hash

    public static int HashToDay(this DateTime date)
            => (date.Year * 1000) + date.DayOfYear;

        #endregion

        #region Comparison

        public static DateTime Min(this DateTime time1, DateTime time2)
        {
            if (time1 < time2)
                return time1;
            return time2;
        }

        public static DateTime Max(this DateTime time1, DateTime time2)
        {
            if (time1 > time2)
                return time1;
            return time2;
        }

        public static bool IsInWindow(this DateTime when, DateTime start, TimeSpan duration,
            TimeSpanUnits units = TimeSpanUnits.continuous) => when.IsInWindow(start, start + duration, units: units);

        public static bool IsInWindow(this DateTime when, DateTime start, DateTime end,
            TimeSpanUnits units = TimeSpanUnits.continuous)
        {
            if (units == TimeSpanUnits.days)
            {
                if (when.EqualToDay(start))
                    return true;
                if (when.EqualToDay(end))
                    return true;
            }
            else if (units == TimeSpanUnits.months)
            {
                if (when.EqualToMonth(start))
                    return true;
                if (when.EqualToMonth(end))
                    return true;
            }
            else
            {
                if(units != TimeSpanUnits.continuous)
                    throw new ArgumentException($"IsInWindow does not support {nameof(TimeSpanUnits)} {nameof(units)} = `{units}`");
                if (start > end)
                    throw new ArgumentException($"Start time is greater than end time. {start} > {end}");
            }

            if (when < start)
                return false;

            if (when > end)
                return false;

            return true;
        }

        #endregion
    }
}
