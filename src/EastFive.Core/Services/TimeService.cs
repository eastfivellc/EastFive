using System;

namespace EastFive.Web.Services
{
    public class TimeService : Web.Services.ITimeService
    {
        public DateTime Utc
        {
            get
            {
                return DateTime.UtcNow;
            }
        }
    }
}