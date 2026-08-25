using System;
using System.Security.Claims;

namespace EastFive.Web.Services
{
    public interface ITimeService
    {
        DateTime Utc { get; }
    }
}