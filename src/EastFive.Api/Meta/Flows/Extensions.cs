using System;
using System.Collections.Generic;
using System.Linq;

using EastFive.Api.Resources;

namespace EastFive.Api.Meta.Flows
{
	public static class Extensions
	{
		public static string[] IfMatchesResponse(this IEnumerable<string> lines, Response response)
		{
			var ifCheckStart = $"if(pm.response.headers.members.some(function(element) {{ return element.key == \"{Core.Middleware.HeaderStatusName}\" && element.value == \"{response.ParamInfo.Name}\" }})) {{";
			var ifCheckEnd = "}\r";

			var wrappedLined = lines
				.Select(line => $"\t{line}")
				.Prepend(ifCheckStart)
				.Append(ifCheckEnd)
				.ToArray();

			return wrappedLined;
		}
	}
}

