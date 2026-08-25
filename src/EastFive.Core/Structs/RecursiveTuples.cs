using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive
{
    public struct RecursiveTuple<T1>
    {
        public T1 item1;
        public Func<RecursiveTuple<T1>> next;
    }

    public struct RecursiveTupleGroup<T1>
    {
        public T1 item;
        public RecursiveTupleGroup<T1>[] next;
    }

    public static class RecursiveTupleExtensions
    {
        public static IEnumerable<T> Bottoms<T>(this RecursiveTupleGroup<T> tupleGroup)
        {
            if (default(RecursiveTupleGroup<T>[]) == tupleGroup.next)
                return new T[] { tupleGroup.item };
            return tupleGroup.next
                .SelectMany(n => n.Bottoms());
        }
    }
}
