using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Postman.Resources.Collection
{
    public class Request
    {
        public string method { get; set; }
        public Header[] header { get; set; }
        public Url url { get; set; }
        public Body body { get; set; }
        public string description { get; set; }
    }

    public class Url
    {
        public const string VariableHostName = "{{HOST}}";

        public string raw { get; set; }
        public string[] host { get; set; }
        public string[] path { get; set; }
        public QueryItem[] query { get; set; }
    }

    public class Header
    {
        public string key { get; set; }
        public string value { get; set; }
        public string type { get; set; }
        public bool disabled;
        public string description;

        public static bool IsGeneratedHeader(string key)
        {
            if (IsCalculatedHeader(key))
                return true;

            if ("Cache-Control".Equals(key, StringComparison.OrdinalIgnoreCase))
                return true;
            if ("User-Agent".Equals(key, StringComparison.OrdinalIgnoreCase))
                return true;
            if ("Accept".Equals(key, StringComparison.OrdinalIgnoreCase))
                return true;
            if ("Accept-Encoding".Equals(key, StringComparison.OrdinalIgnoreCase))
                return true;
            if ("Connection".Equals(key, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        public static bool IsCalculatedHeader(string key)
        {
            if ("Content-Length".Equals(key, StringComparison.OrdinalIgnoreCase))
                return true;
            if ("Postman-Token".Equals(key, StringComparison.OrdinalIgnoreCase))
                return true;
            if ("Host".Equals(key, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
    }

    public struct QueryItem
    {
        public string key;
        public string value;
        public string description;
        public bool disabled;
    }

}
