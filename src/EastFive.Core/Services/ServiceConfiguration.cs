using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Web.Services
{
    public static class ServiceConfiguration
    {
        public static  Func<ISendMessageService> SendMessageService;

        public static Func<ITimeService> TimeService;

        public static void Initialize(
            Func<ISendMessageService> sendMessageService,
            Func<ITimeService> timeService)
        {
            ServiceConfiguration.SendMessageService = sendMessageService;
            ServiceConfiguration.TimeService = timeService;
        }
    }
}
