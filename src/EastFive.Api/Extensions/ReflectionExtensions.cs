using System;
using System.Collections.Generic;
using System.Linq;

using EastFive.Reflection;

namespace EastFive.Api.Extensions
{
	public static class ReflectionExtensions
	{
        /// <summary>
        /// Optional application-instance step for an attribute-interface scan
        /// (see <see cref="AttributeInterfaceScope"/>). Yields the attribute
        /// interfaces declared on the running application's type. A caller
        /// <c>Concat</c>s this in only when an <see cref="IApplication"/> is
        /// available and it is worth consulting the app — otherwise the same
        /// attributes can live at <c>[assembly:]</c> scope and be found by the
        /// assembly/domain steps. Lazy; safe on a null application.
        /// </summary>
        public static IEnumerable<T> AttributeInterfacesInApplication<T>(this IApplication application,
            bool inherit = true, bool multiple = false)
        {
            var appType = application?.GetType();
            return appType.AttributeInterfacesInType<T>(inherit: inherit, multiple: multiple);
        }

        public static bool TryGetAttributeInterfaceFromChain<T>(this System.Reflection.ParameterInfo parameterInfo,
            IApplication application,
            out T attributeInterface,
            bool inherit = false)
        {
            if (!typeof(T).IsInterface)
                throw new ArgumentException($"{typeof(T).FullName} is not an interface.");

            var attributes = parameterInfo.GetAttributesInterface<T>(inherit)
                .Select(attr => (T)attr)
                .ToArray();
            if (attributes.Any())
            {
                attributeInterface = attributes.First();
                return true;
            }

            if (application.GetType().TryGetAttributeInterface(out attributeInterface, inherit: inherit))
                return true;

            if (parameterInfo.ParameterType.TryGetAttributeInterface(out attributeInterface, inherit: inherit))
                return true;

            //attributeInterface = default;
            return false;
        }
    }
}

