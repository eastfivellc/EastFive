using EastFive.Extensions;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public abstract class InvokeApplication : IInvokeApplication
    {
        public virtual string[] ApiRoutes => new string[] { ApiRouteName };

        public virtual string[] MvcRoutes => new string[] { };

        public Uri ServerLocation { get; private set; }

        public string ApiRouteName { get; private set; }

        public IDictionary<string, string> Headers { get; private set; }

        public string AuthorizationHeader
        {
            get
            {
                if (this.Headers.IsDefaultOrNull())
                    return null;
                if (!this.Headers.ContainsKey("Authorization"))
                    return null;
                return this.Headers["Authorization"];
            }
            set
            {
                if (this.Headers.IsDefaultOrNull())
                    this.Headers = new Dictionary<string, string>();
                if (this.Headers.ContainsKey("Authorization"))
                {
                    this.Headers["Authorization"] = value;
                    return;
                }
                this.Headers.Add("Authorization", value);
            }
        }

        public InvokeApplication(Uri serverUrl, string apiRouteName)
        {
            this.ServerLocation = serverUrl;
            this.ApiRouteName = apiRouteName;
            this.Headers = new Dictionary<string, string>();
        }

        public abstract IApplication Application { get; }

        public virtual IHttpRequest GetHttpRequest()
        {
            var httpRequest = new HttpRequest(this.ServerLocation);

            foreach (var headerKVP in this.Headers)
                httpRequest.UpdateHeader(headerKVP.Key, x => headerKVP.Value.AsArray());

            return httpRequest;
        }

        public virtual RequestMessage<TResource> GetRequest<TResource>()
        {
            return BuildRequest<TResource>(this.Application);
        }

        protected virtual RequestMessage<TResource> BuildRequest<TResource>(IApplication application)
        {
            return new RequestMessage<TResource>(this);
        }

        public abstract Task<IHttpResponse> SendAsync(IHttpRequest httpRequest);

    }

    
}
