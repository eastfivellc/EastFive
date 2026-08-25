using System;
using System.Collections.Generic;

using EastFive.Extensions;

namespace EastFive.Diagnostics
{
	public interface IProfile
	{
		IDictionary<TimeSpan, string> Events { get; }
		void MarkInternal(string message = default);
		IMeasure StartInternal(string message = default);
	}

	public interface IMeasure
    {
		void EndInternal();
    }

	public static class ProfilingExtensions
    {
		public static void Mark(this IProfile profile, string message = default)
		{
			if (profile.IsDefaultOrNull())
				return;
			profile.MarkInternal(message);
		}

		public static IMeasure Start(this IProfile profile, string message = default)
		{
			if (profile.IsDefaultOrNull())
				return default;
			return profile.StartInternal(message);
		}

		public static void End(this IMeasure measure)
		{
			if (measure.IsDefaultOrNull())
				return;
			measure.EndInternal();
		}
	}
}

