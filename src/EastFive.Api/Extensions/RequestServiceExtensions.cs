using System;
using System.Net.Http;
using EastFive.Web.Services;

namespace EastFive.Api
{
    public static class RequestServiceExtensions
    {
        public static Func<ISendMessageService> GetMailService(this HttpRequestMessage request)
        {
            return ServiceConfiguration.SendMessageService;
        }

        public static Func<ITimeService> GetDateTimeService(this HttpRequestMessage request)
        {
            return ServiceConfiguration.TimeService;
        }
    }
}
