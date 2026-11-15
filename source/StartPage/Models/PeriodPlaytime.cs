using System;
using System.Collections.Generic;
using LandingPage.Models.GameActivity;
namespace LandingPage.Models
{
    public sealed class PeriodPlaytime
    {
        public decimal TodaySeconds { get; private set; }
        public decimal WeekSeconds { get; private set; }
        public decimal MonthSeconds { get; private set; }
        public decimal YearSeconds { get; private set; }
        public static PeriodPlaytime Calculate(IEnumerable<Session> sessions,DateTime now,DayOfWeek weekStartsOn)
        {
            var day = now.Date;
            var week = day.AddDays(-((7+(int)day.DayOfWeek-(int)weekStartsOn)%7));
            var month = new DateTime(now.Year,now.Month,1,0,0,0,DateTimeKind.Local);
            var year = new DateTime(now.Year,1,1,0,0,0,DateTimeKind.Local);
            var end = now.ToUniversalTime();
            var result = new PeriodPlaytime();
            foreach(var session in sessions)
            {
                if(session == null || session.DateSession == DateTime.MinValue || session.ElapsedSeconds == 0) continue;
                // GameActivity writes UTC session timestamps. Older offset-less values are UTC too.
                var start = session.DateSession.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(session.DateSession,DateTimeKind.Utc) : session.DateSession.ToUniversalTime();
                if(start >= end) continue;
                var duration = Math.Min((double)session.ElapsedSeconds,(end-start).TotalSeconds);
                var finish = start.AddSeconds(duration);
                if(finish > end) finish = end;
                result.TodaySeconds += Overlap(start,finish,day.ToUniversalTime());
                result.WeekSeconds += Overlap(start,finish,week.ToUniversalTime());
                result.MonthSeconds += Overlap(start,finish,month.ToUniversalTime());
                result.YearSeconds += Overlap(start,finish,year.ToUniversalTime());
            }
            return result;
        }
        private static decimal Overlap(DateTime start,DateTime finish,DateTime lower)
        { if(start < lower) start = lower; return finish > start ? (decimal)(finish-start).TotalSeconds : 0; }
        public static string Hours(decimal seconds) => (seconds/3600m).ToString("N1")+" h";
    }
}
