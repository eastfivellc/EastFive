using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Postman.Resources.Collection
{
    public class Item
    {
        public string name { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "event")]
        public Event[] _event { get; set; }

        public Request request { get; set; }

        public ProtocolProfileBehavior protocolProfileBehavior;

        public Response[] response { get; set; }

        public Item[] item { get; set; }
    }

    public class Event
    {
        public string listen { get; set; }
        public Script script { get; set; }
    }

    public class Script
    {
        public string[] exec { get; set; }
        public string type { get; set; }
    }

    public class ProtocolProfileBehavior
    {
        public bool followRedirects;
    }


}
