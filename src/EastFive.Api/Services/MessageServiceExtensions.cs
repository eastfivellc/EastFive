using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Services
{
    public static class MessageServiceExtensions
    {
        public static Dictionary<string, string> AddSubstitutionBlock(this Dictionary<string, string> dictionary,
            string replaceIfNullOrWhitespaceValue,
                Action<Action<string, string>> callback)
        {
            callback(
                (key, value) => dictionary.AddSubstitution(key, value, replaceIfNullOrWhitespaceValue));
            return dictionary;
        }

        public static Dictionary<string, string> AddSubstitution(this Dictionary<string, string> dictionary,
            string key, string value, string replaceIfNullOrWhitespaceValue)
        {
            var replacementValue = string.IsNullOrEmpty(value) ? replaceIfNullOrWhitespaceValue : value;
            dictionary.Add(key, replacementValue);
            return dictionary;
        }

        public static Dictionary<string, string> AddSubstitution(this Dictionary<string, string> dictionary,
            string key, Uri value, Uri replaceIfDefaultValue)
        {
            var replacementValue = default(Uri) != value ? value : replaceIfDefaultValue;
            var replacementValueString = default(Uri) != replacementValue ? replacementValue.AbsoluteUri : string.Empty;
            dictionary.Add(key, replacementValueString);
            return dictionary;
        }

        public static Dictionary<string, string> AddMoney(this Dictionary<string, string> dictionary,
            string key, int value)
        {
            if(value < 0)
                dictionary.Add(key, "-" + ((-value) / 100).ToString() + "." + ((-value) % 100).ToString());
            else
                dictionary.Add(key, (value / 100).ToString() + "." + (value % 100).ToString());
            return dictionary;
        }
    }
}
