using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Postman.Resources.Collection
{
    public class Body
    {
        public string mode { get; set; }
        public string raw { get; set; }
        public Options options { get; set; }
        public FormData[] formdata { get; set; }
        public object file { get; set; }
    }

    public class Options
    {
        public Raw raw { get; set; }
    }

    public class Raw
    {
        public string language { get; set; }
    }

    public class FormData
    {
        public string key { get; set; }
        public string type { get; set; }
        public string src { get; set; }
        public string value { get; set; }
        public string description { get; set; }
        public bool disabled { get; set; }
    }
}
