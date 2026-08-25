using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Serialization
{
    public struct ParseToken : IParseToken
    {
        private string value;

        public ParseToken(string value)
        {
            this.value = value;
        }

        public bool IsString => true;

        public IParseToken[] ReadArray()
        {
            throw new NotImplementedException();
        }

        public byte[] ReadBytes()
        {
            return System.Text.Encoding.UTF8.GetBytes(value);
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
            var bytes = System.Text.Encoding.UTF8.GetBytes(value);
            return new MemoryStream(bytes);
        }

        public string ReadString()
        {
            return this.value;
        }
    }
}
