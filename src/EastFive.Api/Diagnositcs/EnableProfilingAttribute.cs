using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Api.Core;
using EastFive.Diagnostics;
using System.Diagnostics;

namespace EastFive.Api.Diagnositcs
{
    public class EnableProfilingAttribute : System.Attribute, IInstigate
    {
        public const string ResponseProperty = "e5_diagnostics_profileresult";
        public bool CanInstigate(ParameterInfo parameterInfo)
        {
            return parameterInfo.ParameterType.IsAssignableFrom(typeof(IProfile));
        }

        public Task<IHttpResponse> Instigate(
            IApplication httpApp, IHttpRequest request,
            ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            if (parameterInfo.ParameterType.IsAssignableFrom(typeof(IProfile)))
            {
                var profiler = new Profiler(onSuccess);
                return profiler.InitializeAsync();
            }
            throw new Exception();
        }

        private class Profiler : IProfile
        {
            private Stopwatch stopWatch;
            private Func<object, Task<IHttpResponse>> getResponse;
            private Guid profileId;

            public IDictionary<TimeSpan, string> Events { get; private set; }

            public Profiler(Func<object, Task<IHttpResponse>> getResponse)
            {
                this.profileId = Guid.NewGuid();
                this.Events = new Dictionary<TimeSpan, string>();
                stopWatch = new Stopwatch();
                this.getResponse = getResponse;
            }

            public async Task<IHttpResponse> InitializeAsync()
            {
                stopWatch.Start();
                var response = await getResponse(this);
                response.Request.Properties.Add(ResponseProperty, this);
                return response;
            }

            public void MarkInternal(string message = null)
            {
                Events.Add(stopWatch.Elapsed, message);
            }

            public IMeasure StartInternal(string message = null)
            {
                var startedAt = stopWatch.Elapsed;
                return new Measurer(this, startedAt, message);
            }

            private class Measurer : IMeasure
            {
                private Profiler profiler;
                private TimeSpan startedAt;
                private string message;

                public Measurer(Profiler profiler, TimeSpan startedAt, string message)
                {
                    this.profiler = profiler;
                    this.startedAt = startedAt;
                    this.message = message;
                }

                public void EndInternal()
                {
                    var endedAt = this.profiler.stopWatch.Elapsed;
                    var duration = endedAt - this.startedAt;
                    profiler.Events.Add(startedAt, $"BEGIN[{message}] duration={duration}");
                    profiler.Events.Add(endedAt, $"END[{message}] duration={duration}");
                }
            }
        }

    }
}
