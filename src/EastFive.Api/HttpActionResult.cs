//using Microsoft.AspNetCore.Mvc;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net.Http;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Web.Http;

//namespace BlackBarLabs.Api
//{
//    public delegate Task<HttpResponseMessage> HttpActionDelegate();

//    public class HttpActionResult : Microsoft.AspNetCore.Mvc.IActionResult
//    {
//        private HttpActionDelegate callback;
        
//        public HttpActionResult(HttpActionDelegate callback)
//        {
//            this.callback = () => callback();
//        }

//        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
//        {
//            return callback();
//        }

//        public Task ExecuteResultAsync(ActionContext context)
//        {
//            return callback();
//        }
//    }

//    public static class HttpActionResultExtensions
//    {
        
//        public static HttpActionResult ActionResult(this object discard, HttpActionDelegate callback)
//        {
//            return new HttpActionResult(callback);
//        }
//    }
//}
