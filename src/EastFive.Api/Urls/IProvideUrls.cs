using System;

namespace EastFive.Api
{
    public interface IProvideUrl
    {
        Uri Link(string routeName, string controllerName, string action = default, string id = default);

        Uri Combine(string rightPart);
    }
}
