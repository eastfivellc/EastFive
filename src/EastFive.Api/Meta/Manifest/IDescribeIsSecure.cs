using System;

namespace EastFive.Api;

public interface IDescribeIsSecure
{
    bool IsSecurityAttribute(Attribute attr);

    bool IsSecurityParameter(System.Reflection.ParameterInfo parameter);
}