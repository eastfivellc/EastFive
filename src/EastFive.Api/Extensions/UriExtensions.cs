using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Web;

using EastFive;
using EastFive.Collections.Generic;
using EastFive.Linq.Expressions;
using EastFive.Api.Resources;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Serialization;
using EastFive.Text;
using System.Security.Cryptography;
using System.Text;

namespace EastFive.Api
{
    public static class UriExtensions
    {
        public static string HashQueryParameters(this Uri uri, string ignoreKey = default)
        {
            #pragma warning disable SCS0006 // Weak hashing function
            var md5 = MD5.Create();
            var paramsHash = uri.ParseQuery()
                .Where(
                    kvp =>
                    {
                        if (ignoreKey.IsDefault())
                            return true;
                        return String.Compare(kvp.Key, ignoreKey, true) != 0;
                    })
                .OrderBy(k => k.Key)
                .SelectMany(kvp => UTF8Encoding.UTF8.GetBytes($"{kvp.Key}||{kvp.Value}"))
                .ToArray();
            return paramsHash.Md5Checksum();
            #pragma warning restore SCS0006 // Weak hashing function
        }

        public static Uri SetQueryParametersHash(this Uri uri, string hashKey)
        {
            var hashIdStr = uri.ParseQuery()[hashKey];
            if (!Guid.TryParse(hashIdStr, out Guid hashId))
                throw new ArgumentException($"Cannot parse param[{hashKey}]=`{hashIdStr}` as Guid.", "hashKey");

            var paramsHash = uri.HashQueryParameters(hashKey);
            var newUri = uri.SetQueryParam(hashKey, $"{hashId.ToString("N")}{paramsHash}");
            return newUri;
        }

        public static Uri SetQueryParametersHash(this Uri uri)
        {
            var hashIdStr = uri.GetFile();
            if (!Guid.TryParse(hashIdStr, out Guid hashId))
                throw new ArgumentException($"Cannot parse file = `{hashIdStr}` as Guid.", "uri");

            var paramsHash = uri.HashQueryParameters();
            var newUri = uri.SetFile($"{hashId.ToString("N")}{paramsHash}");
            return newUri;
        }

        public static TResult VerifyParametersHash<TResult>(this Uri uri,
            string hashKey = default,
            Func<Guid, string, TResult> onValid = default,
            Func<string, TResult> onInvalid = default)
        {
            return ParseParam(
                paramValue =>
                {
                    var guidLength = Guid.Empty.ToString("N").Length;

                    var guidStr = paramValue.Substring(0, guidLength);
                    if (!Guid.TryParse(guidStr, out Guid id))
                        return onInvalid($"Could not convert `{guidStr}` to UUID");

                    var hashProvided = paramValue.Substring(guidLength);
                    var paramsHash = uri.HashQueryParameters();
                    if (paramsHash != hashProvided)
                        return onInvalid($"`{hashProvided}` is invalid");

                    return onValid(id, paramsHash);
                });

            TResult ParseParam(Func<string, TResult> onParsed)
            {
                if (hashKey.IsDefault())
                    return onParsed(uri.GetFile());

                if (uri.TryGetQueryParam(hashKey, out string paramStr))
                    return onParsed(paramStr);

                return onInvalid($"Url {uri} does not contain parameter `{hashKey}`.");
            }
        }
    }
}
