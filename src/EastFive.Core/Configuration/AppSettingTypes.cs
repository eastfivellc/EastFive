using System;
namespace EastFive.Configuration
{
    public interface IProvideConfigurationKey
    {
        string Key { get; }
    }

    public class ConfigurationKey : IProvideConfigurationKey
    {
        public string Key
        {
            get;
            set;
        }

        public static implicit operator ConfigurationKey(string value)
        {
            return new ConfigurationKey()
            {
                Key = value,
            };
        }
    }

    public class ConnectionString : IProvideConfigurationKey
    {
        public ConnectionString(string value)
        {
            this.Key = value;
        }

        public string Key
        {
            get;
            set;
        }
    }
}

