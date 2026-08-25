using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using Microsoft.AspNetCore.Http;

namespace EastFive.Api
{
    public interface IProvideBlobValue
    {
        TResult ProvideValue<TResult>(MultipartContentTokenParser valueToBind,
            Func<object, TResult> onBound,
            Func<string, TResult> onFailure);

        TResult ProvideValue<TResult>(IFormFile valueToBind,
            Func<object, TResult> onBound,
            Func<string, TResult> onFailure);
    }
}
