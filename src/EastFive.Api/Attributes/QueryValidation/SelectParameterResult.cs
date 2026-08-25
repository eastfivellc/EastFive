using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public struct SelectParameterResult
    {
        public static SelectParameterResult FailureFile(string why, string key, ParameterInfo parameterInfo)
        {
            return new SelectParameterResult
            {
                valid = false,
                value = default(object),
                failure = why,
                parameterInfo = parameterInfo,
                fromQuery = false,
                fromBody = false,
                key = key,
            };
        }

        public static SelectParameterResult FailureQuery(string why, string key, ParameterInfo parameterInfo)
        {
            return new SelectParameterResult
            {
                valid = false,
                value = default(object),
                failure = why,
                parameterInfo = parameterInfo,
                fromQuery = true,
                fromBody = false,
                key = key,
            };
        }

        public static SelectParameterResult FailureBody(string why, string key, ParameterInfo parameterInfo)
        {
            return new SelectParameterResult
            {
                valid = false,
                value = default(object),
                failure = why,
                parameterInfo = parameterInfo,
                fromQuery = false,
                fromBody = true,
                key = key,
            };
        }

        public static SelectParameterResult FailureHeader(string why, string key, ParameterInfo parameterInfo)
        {
            return new SelectParameterResult
            {
                valid = false,
                value = default(object),
                failure = why,
                parameterInfo = parameterInfo,
                fromQuery = false,
                fromBody = false,
                key = key,
            };
        }

        public static SelectParameterResult Body(object v, string key, ParameterInfo parameterInfo)
        {
            return new SelectParameterResult
            {
                valid = true,
                value = v,
                failure = string.Empty,
                parameterInfo = parameterInfo,
                fromQuery = false,
                fromBody = true,
                key = key,
            };
        }

        public static SelectParameterResult Query(object v, string key, ParameterInfo parameterInfo)
        {
            return new SelectParameterResult
            {
                valid = true,
                value = v,
                failure = string.Empty,
                parameterInfo = parameterInfo,
                fromQuery = true,
                fromBody = false,
                fromFile = false,
                key = key,
            };
        }

        public static SelectParameterResult File(object v, string key, ParameterInfo parameterInfo)
        {
            return new SelectParameterResult
            {
                valid = true,
                value = v,
                failure = string.Empty,
                parameterInfo = parameterInfo,
                fromQuery = true,
                fromBody = false,
                fromFile = true,
                key = key,
            };
        }

        public static SelectParameterResult Header(object v, string key, ParameterInfo parameterInfo)
        {
            return new SelectParameterResult
            {
                valid = true,
                value = v,
                failure = string.Empty,
                parameterInfo = parameterInfo,
                fromQuery = false,
                fromBody = false,
                fromFile = false,
                key = key,
            };
        }

        public SelectParameterResult(object v, string key, ParameterInfo parameterInfo)
        {
            valid = true;
            value = v;
            failure = string.Empty;
            this.parameterInfo = parameterInfo;
            fromQuery = true;
            fromBody = false;
            fromFile = false;
            this.key = key;
        }

        public bool valid;
        public object value;
        public string failure;
        public ParameterInfo parameterInfo;
        public bool fromQuery;
        public bool fromBody;
        public bool fromFile;
        public string key;

        public string Location
        {
            get
            {
                if (fromFile)
                    return "FILE";
                if (fromQuery)
                    return "QUERY";
                if (fromBody)
                    return "BODY";
                return string.Empty;
            }
        }
    }
}
