using EastFive.Reflection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Resources
{
    public interface IDocumentProperty
    {
        Property GetProperty(MemberInfo property, HttpApplication httpApp);
    }

    public class Property
    {
        public Property()
        {
        }

        public Property(MemberInfo member, HttpApplication httpApp)
        {
            this.IsIdentfier = member.ContainsAttributeInterface<IIdentifyResource>();
            this.IsTitle = member.ContainsAttributeInterface<ITitleResource>();
            this.Name = member.GetCustomAttribute<JsonPropertyAttribute, string>(
                (attr) => attr.PropertyName.HasBlackSpace() ? attr.PropertyName : member.Name,
                () => member.Name);
            this.Description = member.GetCustomAttribute<System.ComponentModel.DescriptionAttribute, string>(
                (attr) => attr.Description,
                () => string.Empty);
            this.Options = new KeyValuePair<string, string>[] { };
            var type = member.GetPropertyOrFieldType();
            this.Type = Parameter.GetTypeName(type, httpApp);
        }

        public string Name { get; set; }

        public string Description { get; set; }

        public KeyValuePair<string, string>[] Options { get; set; }

        public string Type { get; set; }

        public bool IsTitle { get; set; }

        public bool IsIdentfier { get; set; }
    }
}
