using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive.Collections.Generic;
using EastFive.Linq;
using EastFive.Reflection;
using Microsoft.AspNetCore.Http;
using EastFive.Extensions;

namespace EastFive.Api
{
    [RequestQuery]
    public class RequestQuery<TResource>
        : 
            EastFive.Linq.Queryable<
                TResource,
                RequestQuery<TResource>.RequestQueryProvideQuery>,
            IQueryable<TResource>,
            Linq.ISupplyQueryProvider<RequestQuery<TResource>>,
            IProvideServerLocation,
            IProvideHttpRequest,
            IProvideRequestExpression<TResource>
    {
        public RequestQuery(IProvideServerLocation invokeApplication)
            : base(new RequestQueryProvideQuery(invokeApplication))
        {
            this.InvokeApplication = invokeApplication;
        }

        private RequestQuery(IProvideServerLocation invokeApplication, Expression expr)
            : base(new RequestQueryProvideQuery(invokeApplication), expr)
        {
            this.InvokeApplication = invokeApplication;
        }

        public IProvideServerLocation InvokeApplication { get; private set; }

        public RequestQuery<TRelatedResource> Related<TRelatedResource>()
        {
            return new RequestQuery<TRelatedResource>(this.InvokeApplication);
        }

        public Uri ServerLocation
        {
            get
            {
                var requestMessage = this;
                var uriString = requestMessage.InvokeApplication.ServerLocation.AbsoluteUri
                    .TrimEnd('/'.AsArray());
                return new Uri(uriString);
            }
        }

        public IHttpRequest HttpRequest
        {
            get
            {
                var requestMessage = this;
                var requestProvider = (requestMessage.InvokeApplication as IProvideHttpRequest);
                return requestProvider.HttpRequest;
            }
        }

        public class RequestQueryProvideQuery :
            EastFive.Linq.QueryProvider<
                EastFive.Linq.Queryable<TResource,
                    EastFive.Api.RequestQuery<TResource>.RequestQueryProvideQuery>>
        {
            public RequestQueryProvideQuery(IProvideServerLocation invokeApplication)
                : base(
                    (queryProvider, type) => (queryProvider is RequestQuery<TResource>)?
                        (queryProvider as RequestQuery<TResource>).From()
                        :
                        new RequestQuery<TResource>(invokeApplication),
                    (queryProvider, expression, type) => (queryProvider is RequestQuery<TResource>) ?
                        (queryProvider as RequestQuery<TResource>).FromExpression(expression)
                        :
                        new RequestQuery<TResource>(invokeApplication, expression))
            {
            }

            public override object Execute(Expression expression)
            {
                return 1;
            }
        }

        public virtual RequestQuery<TResource> FromExpression(Expression condition)
        {
            return new RequestQuery<TResource>(
                  this.InvokeApplication,
                  condition);
        }

        internal virtual RequestQuery<TResource> From()
        {
            return new RequestQuery<TResource>(
                  this.InvokeApplication);
        }

        public RequestQuery<TResource> ActivateQueryable(QueryProvider<RequestQuery<TResource>> provider, Type type)
        {
            return From();
        }

        public RequestQuery<TResource> ActivateQueryableWithExpression(QueryProvider<RequestQuery<TResource>> queryProvider,
            Expression expression, Type elementType)
        {
            return FromExpression(expression);
        }

        IQueryable<TResource> IProvideRequestExpression<TResource>.FromExpression(Expression condition)
            => FromExpression(condition);
    }

    public class RequestQueryAttribute : Attribute, IInstigatableGeneric
    {
        public virtual Task<IHttpResponse> InstigatorDelegateGeneric(Type type,
                IApplication httpApp, IHttpRequest routeData, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            return type
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .First()
                .GetParameters()
                .Aggregate<ParameterInfo, Func<object [], Task<IHttpResponse>>>(
                    (invocationParameterValues) =>
                    {
                        var requestMessage = Activator.CreateInstance(type, invocationParameterValues);
                        return onSuccess(requestMessage);
                    },
                    (next, invocationParameterInfo) =>
                    {
                        return (previousParams) =>
                        {
                            return httpApp.Instigate(routeData, invocationParameterInfo,
                                (invocationParameterValue) =>
                                {
                                    return next(previousParams.Prepend(invocationParameterValue).ToArray());
                                });
                        };
                    })
                .Invoke(new object[] { });
        }
    }

    
}
