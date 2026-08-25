using EastFive.Collections.Generic;
using EastFive.Threading;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EastFive.Analytics;
using EastFive.Extensions;

namespace EastFive.Linq.Async
{
    public static partial class EnumerableAsync
    {
        public static async Task<IEnumerable<T>> Async<T>(this IEnumerableAsync<T> enumerable,
            Analytics.ILogger logger = default(Analytics.ILogger))
        {
            var scopedLogger = logger.CreateScope("Async");
            var enumerator = enumerable.GetEnumerator();
            var firstStep = new Step<T>(default(T));
            var step = firstStep;
            scopedLogger.Trace("Moving to next step");
            while (await enumerator.MoveNextAsync())
            {
                scopedLogger.Trace("Moved to next step");
                var nextStep = new Step<T>(enumerator.Current);
                step.next = nextStep;
                step = nextStep;
            }
            scopedLogger.Trace("Last step");
            return new StepEnumerable<T>(firstStep);
        }

        private struct Step<T>
        {
            public Step(T current)
            {
                this.current = current;
                steps = new Step<T>?[1];
            }

            public T current;
            private Step<T>?[] steps;

            internal const int nextIndex = 0;
            public Step<T>? next
            {
                get
                {
                    return steps[nextIndex];
                }
                set
                {
                    steps[nextIndex] = value;
                }
            }
        }

        private class StepEnumerable<T> : IEnumerable<T>
        {
            internal class StepEnumerator : IEnumerator<T>
            {
                protected Step<T> current;
                protected Step<T> start;
                public StepEnumerator(Step<T> start)
                {
                    this.start = start;
                    this.current = start;
                }

                public T Current => current.current;

                object IEnumerator.Current => current.current;

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    if (!current.next.HasValue)
                        return false;
                    current = current.next.Value;
                    return true;
                }

                public void Reset()
                {
                    this.current = start;
                }
            }
            
            private Step<T> firstStep;
            public StepEnumerable(Step<T> firstStep)
            {
                this.firstStep = firstStep;
            }

            public IEnumerator<T> GetEnumerator()
            {
                return new StepEnumerator(firstStep);
            }

            IEnumerator IEnumerable.GetEnumerator() =>
                this.GetEnumerator();
        }
        
        
    }
}
