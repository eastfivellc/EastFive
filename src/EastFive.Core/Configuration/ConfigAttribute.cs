using System;

namespace EastFive.Web
{
    /// <summary>Marks a static class of configuration key constants. Configuration metadata
    /// (label, description, scope, field kind) lives on typed configuration members
    /// (<see cref="EastFive.Configuration.ConfigurationMemberAttribute"/>).</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigAttribute : Attribute
    {
    }
}
