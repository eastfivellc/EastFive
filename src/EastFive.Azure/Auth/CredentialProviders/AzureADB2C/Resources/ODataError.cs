using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.AzureADB2C.Resources
{
    public class ODataError
    {
        [JsonProperty("odata.error")]
        public Error OdataError { get; set; }

        public class Error
        {
            [JsonProperty("code")]
            public string Code { get; set; }


            public class MessageType
            {
                [JsonProperty("lang")]
                public string Lang { get; set; }

                [JsonProperty("value")]
                public string Value { get; set; }
            }

            [JsonProperty("message")]
            public MessageType Message { get; set; }

            public class ValueType
            {
                [JsonProperty("item")]
                public string Item { get; set; }

                [JsonProperty("value")]
                public string Value { get; set; }

            }

            [JsonProperty("values")]
            public ValueType[] Values { get; set; }
        }
    }
}

//{"odata.error":
//{
//"code":"Request_BadRequest",
//"message":{"lang":"en","value":"Another object with the same value for property signInNames already exists."},
//"date":"2017-01-25T15:55:47",
//"requestId":"04c0661a-fc05-484c-be19-ac48a7741761",
//"values":
//[{"item":"PropertyName","value":"signInNames"},{"item":"PropertyErrorCode","value":"ObjectConflict"}]}}