using EastFive.Analytics;
using EastFive.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Linq.Async
{
    public interface ISelectionResult<TSelect>
    {
        bool HasValue { get; }
        TSelect Value { get; }
    }

    public static partial class EnumerableAsync
    {
        public static IEnumerableAsync<TSelect> SelectOptional<TItem, TSelect>(this IEnumerableAsync<TItem> enumerable,
            Func<TItem, Func<TSelect, ISelectionResult<TSelect>>, Func<ISelectionResult<TSelect>>, ISelectionResult<TSelect>> selection)
        {
            return enumerable.SelectOptional<TItem, TSelect, int, IEnumerableAsync<TSelect>>(1,
                (item, carryUpdated, next, skip) => selection(item,
                    (selected) => next(selected, 1),
                    () => skip(1)),
                (aggregation, carryUpdated) => aggregation);
        }

        public static TResult SelectOptional<TItem, TSelect, TCarry1, TCarry2, TResult>(this IEnumerableAsync<TItem> enumerable,
            TCarry1 carry1, TCarry2 carry2,
            Func<TItem, TCarry1, TCarry2,
                Func<TSelect, TCarry1, TCarry2, ISelectionResult<TSelect>>,
                Func<TCarry1, TCarry2, ISelectionResult<TSelect>>,
                ISelectionResult<TSelect>> selection,
            Func<IEnumerableAsync<TSelect>, TCarry1, TCarry2, TResult> aggregation)
        {
            return enumerable.SelectOptional(carry1.PairWithValue(carry2),
                (item, carryUpdated, next, skip) => selection(item, carryUpdated.Key, carryUpdated.Value,
                    (selected, carry1Updated, carry2Updated) => next(selected, carry1Updated.PairWithValue(carry2Updated)),
                    (carry1Updated, carry2Updated) => skip(carry1Updated.PairWithValue(carry2Updated))),
                (IEnumerableAsync<TSelect> selectedItems, KeyValuePair<TCarry1, TCarry2> carryFinal) =>
                    aggregation(selectedItems, carryFinal.Key, carryFinal.Value));
        }

        public static TResult SelectOptionalIndexed<TItem, TSelect, TCarry, TResult>(this IEnumerableAsync<TItem> enumerable,
            TCarry carry,
            Func<TItem, TCarry, int, Func<TSelect, TCarry, ISelectionResult<TSelect>>, Func<TCarry, ISelectionResult<TSelect>>, ISelectionResult<TSelect>> selection,
            Func<IEnumerableAsync<TSelect>, TCarry, TResult> aggregation)
        {
            int index = 0;
            return enumerable.SelectOptional(carry,
                (item, carryUpdated, next, skip) => selection(item, carryUpdated, index++, next, skip),
                aggregation);
        }

        public static TResult SelectOptional<TItem, TSelect, TCarry, TResult>(this IEnumerableAsync<TItem> enumerable,
            TCarry carry,
            Func<TItem, TCarry, Func<TSelect, TCarry, ISelectionResult<TSelect>>, Func<TCarry, ISelectionResult<TSelect>>, ISelectionResult<TSelect>> selection,
            Func<IEnumerableAsync<TSelect>, TCarry, TResult> aggregation)
        {
            var aggregatedItems = enumerable
                .Select(
                    item => selection(item, carry,
                        (selected, carryUpdated) =>
                        {
                            carry = carryUpdated;
                            return new Selection<TSelect>(selected);
                        },
                        (carryUpdated) =>
                        {
                            carry = carryUpdated;
                            return new SelectionSkipped<TSelect>();
                        }))
                .Where(item => item.HasValue)
                .Select(item => item.Value);
            return aggregation(aggregatedItems, carry);
        }

        public static IEnumerableAsync<TSelect> SelectAsyncOptional<TItem, TSelect>(this IEnumerableAsync<TItem> enumerable,
            Func<TItem, Func<TSelect, ISelectionResult<TSelect>>, Func<ISelectionResult<TSelect>>, Task<ISelectionResult<TSelect>>> selection)
        {
            var enumerator = enumerable.GetEnumerator();
            var selections = EnumerableAsync.Yield<TSelect>(
                async (yieldAsync, yieldBreak) =>
                {
                    while (true)
                    {
                        if (!await enumerator.MoveNextAsync())
                            return yieldBreak;

                        var item = enumerator.Current;
                        var selectionResult = await selection(item,
                            (selected) =>
                            {
                                return new Selection<TSelect>(selected);
                            },
                            () =>
                            {
                                return new SelectionSkipped<TSelect>();
                            });
                        if (selectionResult.HasValue)
                            return yieldAsync(selectionResult.Value);
                    }
                });
            return selections;
        }

        public static IEnumerableAsync<TSelect> SelectOptionalAsync<TItem, TSelect, TResult>(this IEnumerableAsync<TItem> enumerable,
            Func<TItem,
                Func<TSelect, ISelectionResult<TSelect>>,
                Func<ISelectionResult<TSelect>>,
                Task<ISelectionResult<TSelect>>> selectionAsync)
        {
            return enumerable
                .Select(
                    (item) => selectionAsync(item,
                        (selected) => new Selection<TSelect>(selected),
                        () => new SelectionSkipped<TSelect>()))
                .Await()
                .Where(select => select.HasValue)
                .Select(select => select.Value);
        }

        public static Task<TResult> SelectOptionalAsync<TItem, TSelect, TCarry1, TCarry2, TResult>(this IEnumerableAsync<TItem> enumerable,
            TCarry1 carry1, TCarry2 carry2,
            Func<TItem, TCarry1, TCarry2, 
                Func<TSelect, TCarry1, TCarry2, ISelectionResult<TSelect>>, 
                Func<TCarry1, TCarry2, ISelectionResult<TSelect>>, 
                Task<ISelectionResult<TSelect>>> selectionAsync,
            Func<TSelect[], TCarry1, TCarry2, TResult> aggregation)
        {
            return enumerable.SelectOptionalAsync(carry1.PairWithValue(carry2),
                (item, carryUpdated, next, skip) => selectionAsync(item, carryUpdated.Key, carryUpdated.Value,
                    (selected, carry1Updated, carry2Updated) => next(selected, carry1Updated.PairWithValue(carry2Updated)),
                    (carry1Updated, carry2Updated) => skip(carry1Updated.PairWithValue(carry2Updated))),
                (TSelect[] selectedItems, KeyValuePair<TCarry1, TCarry2> carryFinal) =>
                    aggregation(selectedItems, carryFinal.Key, carryFinal.Value));
        }

        public static async Task<TResult> SelectOptionalAsync<TItem, TSelect, TCarry, TResult>(this IEnumerableAsync<TItem> enumerable,
            TCarry carry,
            Func<TItem, TCarry, Func<TSelect, TCarry, ISelectionResult<TSelect>>, Func<TCarry, ISelectionResult<TSelect>>, Task<ISelectionResult<TSelect>>> selectionAsync,
            Func<TSelect[], TCarry, TResult> aggregation)
        {
            var enumerator = enumerable.GetEnumerator();
            var selecteds = default(IEnumerableAsync<TSelect>);
            // var completedEvent = new System.Threading.ManualResetEvent(false);
            selecteds = EnumerableAsync.Yield<TSelect>(
                async (yieldReturn, yieldBreak) =>
                {
                    while (true)
                    {
                        if (!await enumerator.MoveNextAsync())
                            return yieldBreak;

                        var yieldResult = await selectionAsync(enumerator.Current, carry,
                            (selected, carryUpdated) =>
                            {
                                carry = carryUpdated;
                                return new Selection<TSelect>(selected);
                            },
                            (carryUpdated) =>
                            {
                                carry = carryUpdated;
                                return new SelectionSkipped<TSelect>();
                            });
                        if (yieldResult.HasValue)
                            return yieldReturn(yieldResult.Value);
                    }
                });
            var selectedResults = (await selecteds.Async()).ToArray();
            var aggregated = aggregation(selectedResults, carry);
            return aggregated;
        }

        public static IEnumerableAsync<TResult> SelectAsyncOptional<TItem, TResult>(this IEnumerable<TItem> items,
            Func<TItem, Func<TResult, ISelected<TResult>>, Func<ISelected<TResult>>, Task<ISelected<TResult>>> callback,
            ILogger logger = default)
        {
            var enumerator = items.GetEnumerator();
            logger.Trace("Got Enumerator");
            return Yield<TResult>(
                async (yieldReturn, yieldBreak) =>
                {
                    logger.Trace("Calling move next");
                    while (enumerator.MoveNext())
                    {
                        logger.Trace("Getting current");
                        var item = enumerator.Current;

                        var nextValue = await callback(item,
                            (nextItem) => new SelectedValue<TResult>(nextItem),
                            () => new SelectedValue<TResult>(false));
                        logger.Trace($"Next value HasValue = {nextValue.HasValue}");
                        if (nextValue.HasValue)
                            return yieldReturn(nextValue.Value);
                    }
                    logger.Trace("Completion");
                    return yieldBreak;
                });
        }

        public static TResult SelectOptional<TItem, TSelect, TCarry, TResult>(this IEnumerableAsync<TItem> enumerable,
            TCarry carry,
            Func<TItem, TCarry, Func<TSelect, TCarry, TResult>, Func<TCarry, TResult>, TResult> selection,
            Func<IEnumerableAsync<TSelect>, TCarry, TResult> aggregation)
        {
            var enumerator = enumerable.GetEnumerator();
            var selecteds = default(IEnumerableAsync<TSelect>);
            TResult aggregated = default(TResult);
            var completedEvent = new System.Threading.AutoResetEvent(false);
            selecteds = EnumerableAsync.Yield<TSelect>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (!await enumerator.MoveNextAsync())
                        return yieldBreak;

                    var yieldResult = default(IYieldResult<TSelect>);
                    var discard = selection(enumerator.Current, carry,
                        (selected, carryUpdated) =>
                        {
                            yieldResult = yieldReturn(selected);
                            completedEvent.WaitOne();
                            return aggregated;
                        },
                        (carryUpdated) =>
                        {
                            completedEvent.WaitOne();
                            return aggregated;
                        });
                    return yieldResult;
                });
            aggregated = aggregation(selecteds, carry);
            completedEvent.Set();
            return aggregated;
        }

        private static async Task<TResult> SelectOptionalAsync<TItem, TSelect, TCarry, TResult>(IEnumeratorAsync<TItem> enumerable,
            TCarry carry, IEnumerableAsync<TSelect> selecteds, Func<TSelect, Task> selectSelection,
            Func<TItem, TCarry, Func<TSelect, TCarry, Task<TResult>>, Func<TCarry, Task<TResult>>, Task<TResult>> selection,
            Func<IEnumerableAsync<TSelect>, TCarry, TResult> aggregation)
        {
            if (!await enumerable.MoveNextAsync())
                return aggregation(selecteds, carry);

            return await selection(enumerable.Current, carry,
                async (selected, carryUpdated) =>
                {
                    await selectSelection(selected);
                    return await SelectOptionalAsync(enumerable, carryUpdated, selecteds, selectSelection,
                        selection,
                        aggregation);
                },
                async (carryUpdated) =>
                {
                    return await SelectOptionalAsync(enumerable, carryUpdated, selecteds, selectSelection,
                        selection,
                        aggregation);
                });
        }

        private struct Selection<TSelect> : ISelectionResult<TSelect>
        {
            public Selection(TSelect selected)
            {
                this.Value = selected;
            }

            public bool HasValue => true;

            public TSelect Value { get; private set; }
        }

        private struct SelectionSkipped<TSelect> : ISelectionResult<TSelect>
        {
            public bool HasValue => false;

            public TSelect Value => throw new NotImplementedException();
        }
    }
}
