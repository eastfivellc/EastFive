//using BlackBarLabs.Extensions;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace EastFive.Linq.Async
//{
//    public static class IEnumerableAsyncZ
//    {
//        public delegate Task<bool> IEnumerableDelegateAsync<TItem>(
//            Action<TItem, IEnumerableDelegateAsync<TItem>> callback);

//        public static async Task<IEnumerableDelegateAsync<TItem2>> SelectAsync<TItem1, TItem2>(
//            this IEnumerableDelegateAsync<TItem1> items,
//            Func<TItem1, TItem2> callback)
//        {
//            TItem2 item2 = default(TItem2);
//            IEnumerableDelegateAsync<TItem1> itemNext = default(IEnumerableDelegateAsync<TItem1>);
//            var hasNext = await items(
//                (item1, itemNextInner) =>
//                {
//                    itemNext = itemNextInner;
//                    item2 = callback(item1);
//                });
//            if (!hasNext)
//                return (a) => false.ToTask();

//            return (
//                async callbackInner =>
//                {
//                    IEnumerableDelegateAsync<TItem2> nextAgain = await SelectAsync(itemNext, callback);
//                    callbackInner(item2, nextAgain);
//                    return true;
//                });
//        }

//        public static async Task<TResult> AggregateAsync<TItem, TResult>(
//            this IEnumerableDelegateAsync<TItem> items,
//            TResult aggr,
//            Func<TItem, TResult, TResult> callback)
//        {
//            TResult aggrNext = default(TResult);
//            IEnumerableDelegateAsync<TItem> remainingItems = default(IEnumerableDelegateAsync<TItem>);
//            var hasNext = await items(
//                (item, nextAsyc) =>
//                {
//                    aggrNext = callback(item, aggr);
//                    remainingItems = nextAsyc;
//                });
//            if (!hasNext)
//                return aggr;
//            return await remainingItems.AggregateAsync(aggrNext, callback);
//        }

//        public static async Task<IEnumerableDelegateAsync<TItem>> UntilAsync<TItem, TAggregate>(
//            this IEnumerableDelegateAsync<TItem> items,
//            TAggregate initial,
//            Func<TItem, TAggregate, Func<TAggregate, bool>, Func<bool>, bool> predicate)
//        {
//            var itemNext = default(TItem);
//            var remainingItems = default(IEnumerableDelegateAsync<TItem>);
//            var hasNext = await items(
//                (itemNextInner, nextAsycInner) =>
//                {
//                    remainingItems = nextAsycInner;
//                    itemNext = itemNextInner;
//                });
//            if (!hasNext)
//                return (a) => false.ToTask();

//            var aggrNext = default(TAggregate);
//            var moveNext = predicate(
//                itemNext, initial,
//                (aggrNextNext) => { aggrNextNext = aggrNext; return true; },
//                () => false);

//            if(!moveNext)
//                return (a) => false.ToTask();

//            return await remainingItems.UntilAsync(aggrNext, predicate);
//        }

//        //public static async Task<TResult> ReduceAsync<TItem, TResult>(
//        //    this IEnumerableDelegateAsync<TItem> items,
//        //    TResult initialAggregate,
//        //    Func<TResult, TItem, TResult> callback)
//        //{
//        //    var aggrNextTask = default(Task<TResult>);
//        //    var hasNext = await items(
//        //        (item1, nextAsyc) =>
//        //        {
//        //            nextAsyc(
//        //                (item2, nextNextAsync) =>
//        //                {
//        //                    aggrNextTask = nextNextAsync.ReduceAsync(initialAggregate, callback);
//        //                })
//        //            aggrNext = callback(initialAggregate, item, aggr);
//        //            remainingItems = nextAsyc;
//        //        });
//        //    if (!hasNext)
//        //        return aggr;
//        //    return await remainingItems.AggregateAsync(aggrNext, callback);
//        //}

//        //public static async Task<TResult> ReduceInternalAsync<TItem, TResult>(
//        //    this IEnumerableDelegateAsync<TItem> items,
//        //    TResult initialAggregate,
//        //    Func<TResult, TItem, TResult> callback)
//        //{
//        //    var aggrNextTask = default(Task<TResult>);
//        //    var hasNext = await items(
//        //        (item1, nextAsyc) =>
//        //        {
//        //            nextAsyc(
//        //                (item2, nextNextAsync) =>
//        //                {
//        //                    aggrNextTask = nextNextAsync.ReduceAsync(initialAggregate, callback);
//        //                })
//        //            aggrNext = callback(initialAggregate, item, aggr);
//        //            remainingItems = nextAsyc;
//        //        });
//        //    if (!hasNext)
//        //        return aggr;
//        //    return await remainingItems.AggregateAsync(aggrNext, callback);
//        //}
//    }
//}
