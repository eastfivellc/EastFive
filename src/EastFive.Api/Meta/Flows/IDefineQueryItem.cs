using EastFive.Api.Meta.Postman.Resources.Collection;
using EastFive.Api.Resources;
using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Flows
{
    public interface IDefineQueryItem
    {
        QueryItem[] GetQueryItem(Api.Resources.Method method, ParameterInfo parameter);
        QueryItem[] GetQueryItems(Api.Resources.Method method);
    }

    public class QueryItemRequiredAttribute : Attribute, IDefineQueryItem
    {
        private string queryKey;
        private string queryValue;

        public QueryItemRequiredAttribute(string queryKey, string queryValue)
        {
            this.queryKey = queryKey;
            this.queryValue = queryValue;
        }

        public QueryItem[] GetQueryItems(Api.Resources.Method method)
        {
            return new QueryItem()
            {
                key = queryKey,
                value = queryValue,
            }
            .AsArray();
        }

        public QueryItem[] GetQueryItem(Api.Resources.Method method, ParameterInfo parameter)
        {
            return new QueryItem()
            {
                key = queryKey,
                value = queryValue,
            }.AsArray();
        }
    }
}
