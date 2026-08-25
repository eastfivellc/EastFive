using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EastFive.Linq;

namespace EastFive.Extensions
{
    public static class ComputationExtensions
    {
        public static string ToMoneyString(this int cents)
        {
            return (cents / 100).ToString() + (cents % 100).ToString();
        }
        
        public static TResult FromMoneyString<TResult>(this string money,
            Func<int, TResult> parsed,
            Func<TResult> invalidOrUnrecognizedFormat)
        {
            if (String.IsNullOrWhiteSpace(money))
                return invalidOrUnrecognizedFormat();

            var regex = new Regex(@"(\-)?(\d+)?(\.(\d+))?");
            var matches = regex.Matches(money);
            if (matches.Count == 0 || matches[0].Groups.Count != 5)
                return invalidOrUnrecognizedFormat();

            var match = matches[0];
            var multiplier = match.Groups[1].Success ? -1 : 1;
            var dollars = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            var cents = match.Groups[3].Success ?
                    match.Groups[4].Value.Length == 2?
                        int.Parse(match.Groups[4].Value)
                        :
                        int.Parse(match.Groups[4].Value) * 10

                :
                    0;

            var value = multiplier * ((dollars * 100) + cents);
            return parsed(value);
        }

        private struct NumericalRange
        {
            public string preStr;
            public string postStr;
            public string comparisonStr;

            internal bool TryParse(out Func<int, bool> isInRange)
            {
                isInRange = default;

                if (!TryParseInt(this.preStr, out int? pre))
                    return false;
                
                if (!TryParseInt(this.postStr, out int? post))
                    return false;

                if (pre.HasValue && post.HasValue)
                {
                    isInRange = (int v) =>
                    {
                        if (v < pre.Value)
                            return false;
                        return v < post.Value;
                    };
                    return true;
                }

                bool success;
                var compStr = this.comparisonStr;
                (success, isInRange) = compStr
                    .NullToEmpty()
                    .Where(c => !c.Equals('='))
                    .Single(
                        onSingle: (comparitor) =>
                        {
                            var hasEquals = compStr.Contains('=');
                            if (!TryParseComparison(comparitor, out Func<int, int, bool> compareInner))
                                return (false, default(Func<int, bool>));

                            if (pre.HasValue)
                                return (true,
                                    (v) =>
                                    {
                                        if (hasEquals)
                                            if (pre.Value == v)
                                                return true;
                                        return compareInner(pre.Value, v);
                                    });

                            if (post.HasValue)
                                return (true,
                                    (v) =>
                                    {
                                        if (hasEquals)
                                            if (post.Value == v)
                                                return true;
                                        return compareInner(v, post.Value);
                                    });

                            return (false, default(Func<int, bool>));
                        },
                        onNoneOrMultiple: () =>
                        {
                            var isAllEquals = compStr
                                .NullToEmpty()
                                .All(c => c.Equals('='));
                            if(!isAllEquals)
                                return (false, default(Func<int, bool>));

                            if (pre.HasValue)
                                return (true,
                                    v =>
                                    {
                                        return v == pre.Value;
                                    });

                            if (post.HasValue)
                                return (true,
                                    v =>
                                    {
                                        return v == post.Value;
                                    }
                                );

                            return (false, default(Func<int, bool>));
                        });

                return success;


                bool TryParseInt(string input, out int? result)
                {
                    if (int.TryParse(input, out int inputInt))
                    {
                        result = inputInt;
                        return true;
                    }

                    result = default(int?);
                    return input.IsNullOrWhiteSpace();
                }

                bool TryParseComparison(char input, out Func<int, int, bool> compare)
                {
                    if (input == '>')
                    {
                        compare = (vInput, vCompareTo) => vInput > vCompareTo;
                        return true;
                    }
                    if (input == '<')
                    {
                        compare = (vInput, vCompareTo) => vInput < vCompareTo;
                        return true;
                    }
                    if (input == '≤')
                    {
                        compare = (vInput, vCompareTo) => vInput <= vCompareTo;
                        return true;
                    }
                    if (input == '�')
                    {
                        compare = (vInput, vCompareTo) => vInput >= vCompareTo;
                        return true;
                    }
                    if (input == '≥')
                    {
                        compare = (vInput, vCompareTo) => vInput >= vCompareTo;
                        return true;
                    }

                    compare = default;
                    return true;
                }
            }
        }

        public static bool TryParseNumericalRange(this string numericalRangeString, out Func<int, bool> isInRange)
        {
            NumericalRange range;
            if (!numericalRangeString.TryMatchRegex(
                "(?<pre>[0-9]*)\\s*(?<comparison>[≤≥�<=>-]+)\\s*(?<post>[0-9]*)",
                (pre, comparison, post) => new NumericalRange { preStr = pre, comparisonStr = comparison, postStr = post },
                out range))
            {
                isInRange = default;
                return false;
            }

            if (!range.TryParse(out isInRange))
                return false;

            return true;
        }

        //public static void Main(string[] args)
        //{
        //    var strInput = "-.3";     //should match

        //    var a = FromMoneyString(strInput,
        //                            (v) => v,
        //                            () => 0);
        //    if (a != -30)
        //        throw new Exception();


        //    strInput = "-.03";
        //    a = FromMoneyString(strInput,
        //                            (v) => v,
        //                            () => 0);
        //    if (a != -3)
        //        throw new Exception();

        //    strInput = ".03";
        //    a = FromMoneyString(strInput,
        //                            (v) => v,
        //                            () => 0);
        //    if (a != 3)
        //        throw new Exception();


        //    strInput = "-3.1";
        //    a = FromMoneyString(strInput,
        //                            (v) => v,
        //                            () => 0);
        //    if (a != -310)
        //        throw new Exception();

        //    strInput = "2.";
        //    a = FromMoneyString(strInput,
        //                            (v) => v,
        //                            () => 0);
        //    if (a != 200)
        //        throw new Exception();

        //    strInput = "230";
        //    a = FromMoneyString(strInput,
        //                            (v) => v,
        //                            () => 0);
        //    if (a != 23000)
        //        throw new Exception();


        //    strInput = "-3";
        //    a = FromMoneyString(strInput,
        //                            (v) => v,
        //                            () => 0);
        //    if (a != -300)
        //        throw new Exception();


        //    Console.WriteLine(a);
        //}

    }
}
