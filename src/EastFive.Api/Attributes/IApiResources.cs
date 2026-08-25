using EastFive.Extensions;
using EastFive.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IApiResources
    {
        bool ShouldCheckAssembly(Assembly assembly);
    }

    public class ApiResourcesAttribute : Attribute, IApiResources
    {
        public string NameSpacePrefixes { get; set; }

        public bool ShouldCheckAssembly(Assembly assembly)
        {
            var nameSpacePrefixes = NameSpacePrefixes.Split(','.AsArray());
            return nameSpacePrefixes
                .First(
                    (nsPrefix, next) =>
                    {
                        if (assembly.FullName.StartsWith(nsPrefix))
                            return true;
                        return next();
                    },
                    () => false);
        }
    }
}
