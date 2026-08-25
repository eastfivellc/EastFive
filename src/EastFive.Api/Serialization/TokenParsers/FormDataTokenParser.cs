using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Serialization
{
    public class FormDataTokenParser : IParseToken
    {
        private string valueForKey;

        public FormDataTokenParser(string valueForKey)
        {
            this.valueForKey = valueForKey;
        }

        public IParseToken[] ReadArray()
        {
            throw new NotImplementedException();
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
            return this.valueForKey;
        }
    }
}
