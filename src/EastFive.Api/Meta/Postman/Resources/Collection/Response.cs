using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Postman.Resources.Collection
{
    public class Response
    {
        public string name { get; set; }
        public Originalrequest originalRequest { get; set; }
        public string status { get; set; }
        public int code { get; set; }
        public string _postman_previewlanguage { get; set; }
        public Header[] header { get; set; }
        public object[] cookie { get; set; }
        public string body { get; set; }
    }

    public class Originalrequest
    {
        public string method { get; set; }
        public object[] header { get; set; }
        public Body body { get; set; }
        public Url url { get; set; }
    }
}
