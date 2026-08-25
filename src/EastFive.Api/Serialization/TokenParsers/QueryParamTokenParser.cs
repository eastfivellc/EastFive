using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Serialization
{
    public class QueryParamTokenParser : IParseToken
    {
        private string value;

        public QueryParamTokenParser(string value)
        {
            this.value = value;
        }

        public IParseToken[] ReadArray()
        {
            return value
                .Split(new char[] { ',' })
                .Select(
                    v => new QueryParamTokenParser(v))
                .ToArray();
        }

        public byte[] ReadBytes()
        {
            throw new NotImplementedException();
        }

        public IDictionary<string, IParseToken> ReadDictionary()
        {
            throw new NotImplementedException();
        }

        public T ReadObject<T>()
        {
            throw new NotImplementedException();
        }

        public object ReadObject()
        {
            throw new NotImplementedException();
        }

        public Stream ReadStream()
        {
            throw new NotImplementedException();
        }

        public bool IsString => true;
        public string ReadString()
        {
            return value;
        }
    }
}
