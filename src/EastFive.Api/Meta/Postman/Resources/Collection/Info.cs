using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Postman.Resources.Collection
{
    public class Info
    {
        public Guid _postman_id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string schema { get; set; }
    }
}
