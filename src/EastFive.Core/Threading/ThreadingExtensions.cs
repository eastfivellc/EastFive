using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace EastFive.Threading
{
    public static class ThreadingExtensions
    {
        public static void WaitAll(this IEnumerable<EventWaitHandle> mutexes)
        {
            var mutexArray = mutexes.ToArray(); // Ensure the enumerable has been enumerated
            foreach (var mutex in mutexArray)
                mutex.WaitOne();
        }

        public static void WaitAll(this IEnumerable<ManualResetEvent> mutexes)
            => mutexes.Cast<EventWaitHandle>().WaitAll();

        public static void WaitAll(this IEnumerable<AutoResetEvent> mutexes)
            => mutexes.Cast<EventWaitHandle>().WaitAll();

    }
}
