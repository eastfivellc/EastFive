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
    [RequestMessage]
    public class RequestMessage<TResource>
        : 
            EastFive.Linq.Queryable<
                TResource, 
                RequestMessage<TResource>.RequestMessageProvideQuery>,
            IQueryable<TResource>,
            Linq.ISupplyQueryProvider<RequestMessage<TResource>>,
            IProvideServerLocation,
            IProvideHttpRequest,
            IProvideRequestExpression<TResource>
    {
        public RequestMessage(IInvokeApplication invokeApplication)
            : base(new RequestMessageProvideQuery(invokeApplication))
        {
            this.InvokeApplication = invokeApplication;
        }

        private RequestMessage(IInvokeApplication invokeApplication, Expression expr)
            : base(new RequestMessageProvideQuery(invokeApplication), expr)
        {
            this.InvokeApplication = invokeApplication;
        }

        public IInvokeApplication InvokeApplication { get; private set; }

        public RequestMessage<TRelatedResource> Related<TRelatedResource>()
        {
            return new RequestMessage<TRelatedResource>(this.InvokeApplication);
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

        public class RequestMessageProvideQuery :
            EastFive.Linq.QueryProvider<
                EastFive.Linq.Queryable<TResource,
                    EastFive.Api.RequestMessage<TResource>.RequestMessageProvideQuery>>
        {
            public RequestMessageProvideQuery(IInvokeApplication invokeApplication)
                : base(
                    (queryProvider, type) => (queryProvider is RequestMessage<TResource>)?
                        (queryProvider as RequestMessage<TResource>).From()
                        :
                        new RequestMessage<TResource>(invokeApplication),
                    (queryProvider, expression, type) => (queryProvider is RequestMessage<TResource>) ?
                        (queryProvider as RequestMessage<TResource>).FromExpression(expression)
                        :
                        new RequestMessage<TResource>(invokeApplication, expression))
            {
            }

            public override object Execute(Expression expression)
            {
                return 1;
            }
        }

        public virtual RequestMessage<TResource> FromExpression(Expression condition)
        {
            return new RequestMessage<TResource>(
                  this.InvokeApplication,
                  condition);
        }

        internal virtual RequestMessage<TResource> From()
        {
            return new RequestMessage<TResource>(
                  this.InvokeApplication);
        }

        public RequestMessage<TResource> ActivateQueryable(QueryProvider<RequestMessage<TResource>> provider, Type type)
        {
            return From();
        }

        public RequestMessage<TResource> ActivateQueryableWithExpression(QueryProvider<RequestMessage<TResource>> queryProvider,
            Expression expression, Type elementType)
        {
            return FromExpression(expression);
        }

        IQueryable<TResource> IProvideRequestExpression<TResource>.FromExpression(Expression condition)
            => FromExpression(condition);
    }

    public class RequestMessageAttribute : Attribute, IInstigatableGeneric
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
