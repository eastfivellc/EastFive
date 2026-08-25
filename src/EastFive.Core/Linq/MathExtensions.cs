using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EastFive.Extensions;


namespace EastFive.Linq
{
    public static class MathExtensions
    {
        public static IEnumerable<double> Normalize(this IEnumerable<double> items,
            double baseWeight = 1.0)
        {
            var total = items.Sum();
            var weight = baseWeight / total;
            foreach (var item in items)
            {
                yield return item * weight;
            }
        }

        public static double StdDev(this IEnumerable<float> list)
        {
            return list.StdDev(x => (double)x);
        }

        public static double StdDev(this IEnumerable<double> list)
        {
            return list.StdDev(x => x);
        }

        public static double StdDev<T>(this IEnumerable<T> list, Func<T, double> values)
        {
            // ref: https://stackoverflow.com/questions/2253874/linq-equivalent-for-standard-deviation
            // ref: http://warrenseen.com/blog/2006/03/13/how-to-calculate-standard-deviation/ 
            var mean = 0.0;
            var sum = 0.0;
            var stdDev = 0.0;
            var n = 0;
            foreach (var value in list.Select(values))
            {
                n++;
                var delta = value - mean;
                mean += delta / n;
                sum += delta * (value - mean);
            }
            if (1 < n)
                stdDev = Math.Sqrt(sum / (n - 1));

            return stdDev;

        }

    }
}
