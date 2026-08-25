using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;
using EastFive.Collections.Generic;
using EastFive.Reflection;
using EastFive.Linq.Async;
using EastFive.Linq;

namespace EastFive
{
    public static class RegexExtensions
    {
        public static IEnumerable<Match> AsMatches(this MatchCollection collection)
        {
            foreach (Match match in collection)
                yield return match;
        }

        public static IEnumerable<Group> AsGroups(this GroupCollection collection)
        {
            foreach (Group group in collection)
                yield return group;
        }

        public static IEnumerable<Capture> AsCaptures(this CaptureCollection collection)
        {
            foreach (Group capture in collection)
                yield return capture;
        }

        public static IEnumerable<(string name, string value)[]> AsSuccessfulMatchValues(this MatchCollection collection)
        {
            return collection
                .AsMatches()
                .Where(match => match.Success)
                .Select(
                    (match) =>
                    {
                        var matchingGroups = match.Groups
                            .AsGroups()
                            .Skip(1)
                            .Where(
                                groupMaybe =>
                                {
                                    var isGroup = groupMaybe is System.Text.RegularExpressions.Group;
                                    return isGroup;
                                })
                            .Select(
                                groupCast =>
                                {
                                    var group = groupCast as System.Text.RegularExpressions.Group;
                                    var groupName = group.Name;
                                    return (name: group.Name, value: group.Value);
                                })
                            .ToArray();

                        return matchingGroups;
                    });
        }

        /// <summary>
        /// Populates an object of type <typeparamref name="T"/> for each matching expression. The objects' properties are specified in <paramref name="expressions"/>
        /// </summary>
        /// <typeparam name="T">Must have empty contructor for Activator.CreateInstance<T>()</typeparam>
        /// <param name="input">String to match against.</param>
        /// <param name="regularExpression"></param>
        /// <param name="expressions">Which properties of the object to populate.</param>
        /// <returns></returns>
        public static T[] MatchRegex<T>(this string input, string regularExpression,
            params Expression<Func<T, object>>[] expressions)
        {
            var expressionLookup = expressions
                .Select(
                    exp => exp.MemberName(
                        (name) =>
                        {
                            var members = typeof(T).GetProperties()
                                .Cast<MemberInfo>()
                                .Concat(typeof(T).GetFields()).ToArray();
                            var propInfo = members.First(
                                prop => prop.Name == name);
                            return name.PairWithValue(propInfo);
                        },
                        () =>
                        {
                            // TODO: Warn here?
                            //throw new ArgumentException($"The expression '{exp.Body}' is invalid. You must supply an expression that references a property.");
                            return default(KeyValuePair<string, MemberInfo>?);
                        }))
                .SelectWhereHasValue()
                .ToDictionary();

            var regex = new Regex(regularExpression);
            var results = regex
                .Matches(input)
                .AsSuccessfulMatchValues() // .AsMatches()
                //.Where(match => match.Success && match.Groups.Count == (expressions.Length + 1))
                .Where(match => match.Length == (expressions.Length + 1))
                .Select(
                    (matchItems) =>
                    {
                        // var groups = match.Groups.AsGroups().Skip(1).ToArray();

                        var assignmentCollection = expressionLookup
                            .Select(
                                expKvp =>
                                {
                                    return matchItems
                                        .Where(
                                            matchItem =>
                                            {
                                                return matchItem.name == expKvp.Key;
                                            })
                                        .First(
                                            (matchItem, next) =>
                                            {
                                                return expKvp.Value.PairWithValue(matchItem.value);
                                            },
                                            () =>
                                            {
                                                return default(KeyValuePair<MemberInfo, string>?);
                                            });

                                    //var matchingGroups = groups
                                    //    .Where(
                                    //        group =>
                                    //        {
                                    //            var groupName = group.Name;
                                    //            return groupName == expKvp.Key;
                                    //        });
                                    //if (!matchingGroups.Any())
                                    //    return default(KeyValuePair<MemberInfo, string>?);

                                    //return expKvp.Value.PairWithValue(matchingGroups.First().Value);
                                })
                            .SelectWhereHasValue()
                            .ToArray();
                        
                        return assignmentCollection;
                    })
                .Where(assignmentCollection => assignmentCollection.Length == expressionLookup.Count)
                .Select(
                    (assignmentCollection) =>
                    {
                        return assignmentCollection.Aggregate(Activator.CreateInstance<T>(),
                            (r, assignment) =>
                            {
                                assignment.Key.SetValue(ref r, assignment.Value);
                                return r;
                            });
                    })
                .ToArray();

            return results;
        }

        /// <summary>
        /// Populates an object of type <typeparamref name="T"/> for a single matching expression. The object's properties are specified in <paramref name="expressions"/>
        /// </summary>
        /// <typeparam name="T">Must have empty contructor for Activator.CreateInstance<T>()</typeparam>
        /// <param name="input">String to match against.</param>
        /// <param name="regularExpression"></param>
        /// <param name="expressions">Which properties of the object to populate.</param>
        public static TResult MatchRegex<T, TResult>(this string input, string regularExpression,
            Func<T, TResult> onMatched,
            Func<TResult> onNotMatched,
            params Expression<Func<T, object>>[] expressions)
        {
            var expressionLookup = expressions
                .Select(
                    exp => exp.MemberName(
                        (name) =>
                        {
                            var members = typeof(T).GetProperties()
                                .Cast<MemberInfo>()
                                .Concat(typeof(T).GetFields()).ToArray();
                            var propInfo = members.First(
                                prop => prop.Name == name);
                            return name.PairWithValue(propInfo);
                        },
                        () =>
                        {
                            // TODO: Warn here?
                            //throw new ArgumentException($"The expression '{exp.Body}' is invalid. You must supply an expression that references a property.");
                            return default(KeyValuePair<string, MemberInfo>?);
                        }))
                .SelectWhereHasValue()
                .ToDictionary();

            var regex = new Regex(regularExpression);
            var matches = regex
                .Matches(input)
                .AsMatches();
            var assignments = matches
                .Where(match => match.Success && match.Groups.Count == (expressions.Length + 1))
                .Select(
                    (match) =>
                    {
                        var groups = match.Groups.AsGroups().Skip(1).ToArray();

                        var assignmentCollection = expressionLookup
                            .Select(
                                expKvp =>
                                {
                                    var matchingGroups = groups
                                        .Where(
                                            groupMaybe =>
                                            {
                                                if (!(groupMaybe is System.Text.RegularExpressions.Group))
                                                    return false;
                                                var group = groupMaybe as System.Text.RegularExpressions.Group;
                                                var groupName = group.Name;
                                                return groupName == expKvp.Key;
                                            });
                                    if (!matchingGroups.Any())
                                        return default(KeyValuePair<MemberInfo, string>?);

                                    return expKvp.Value.PairWithValue(matchingGroups.First().Value);
                                })
                            .SelectWhereHasValue()
                            .ToArray();

                        return assignmentCollection;
                    })
                .ToArray();
            var results = assignments
                .Where(assignmentCollection => assignmentCollection.Length == expressionLookup.Count)
                .Select(
                    (assignmentCollection) =>
                    {
                        return assignmentCollection.Aggregate(Activator.CreateInstance<T>(),
                            (r, assignment) =>
                            {
                                assignment.Key.SetValue(ref r, assignment.Value);
                                return r;
                            });
                    })
                .ToArray();

            return results.Any()? onMatched(results.First()) : onNotMatched();
        }

        /// <summary>
        /// Invokes expression for each match of <paramref name="regularExpression"/> in <paramref name="input"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="input">String to match against.</param>
        /// <param name="regularExpression">
        /// To populate an expression parameter named `verb1` use "The fox (?<verb1>[a-zA-Z]+) the log."
        /// To ignore case: ".*(?i)IgNore CaSE oF THIS(?-i)
        /// </param>
        /// <param name="expression">
        /// Invoked once for each match. The argument must be named. For example, <code>(string verb1) =>...</code>"
        /// </param>
        /// <param name="onMatched">
        /// Method to handle the match results. An empty array is provided if <paramref name="input"/> did not match <paramref name="expression"/>.
        /// </param>
        /// <returns></returns>
        public static TResult MatchRegexInvoke<T, TResult>(this string input, string regularExpression,
            Expression<Func<string, T>> expression,
            Func<T[], TResult> onMatched)
        {
            var exec = expression.Compile();
            return input.MatchRegexInvokeDynamic(regularExpression,
                onMatched,
                expression.Parameters.ToArray(),
                invokeArgs => (T)exec.Invoke(invokeArgs[0]));
        }

        /// <summary>
        /// Invokes expression for each match of <paramref name="regularExpression"/> in <paramref name="input"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="input">String to match against.</param>
        /// <param name="regularExpression">
        /// To populate expression parameters named `verb1` and `noun1` use "The fox (?<verb1>[a-zA-Z]+) the (?<noun1>[a-zA-Z]+)."
        /// To ignore case: ".*(?i)IgNore CaSE oF THIS(?-i)
        /// </param>
        /// <param name="expression">
        /// Invoked once for each match. The arguments must be named. For example, <code>(string verb1, string noun1) =>...</code>"
        /// </param>
        /// <param name="onMatched">
        /// Method to handle the match results. An empty array is provided if <paramref name="input"/> did not match <paramref name="expression"/>.
        /// </param>
        /// <returns></returns>
        public static TResult MatchRegexInvoke<T, TResult>(this string input, string regularExpression,
            Expression<Func<string, string, T>> expression,
            Func<T[], TResult> onMatched)
        {
            var exec = expression.Compile();
            return input.MatchRegexInvokeDynamic(regularExpression,
                onMatched,
                expression.Parameters.ToArray(),
                invokeArgs => (T)exec.Invoke(invokeArgs[0], invokeArgs[1]));
        }

        /// <summary>
        /// Invokes expression for each match of <paramref name="regularExpression"/> in <paramref name="input"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="input">String to match against.</param>
        /// <param name="regularExpression">
        /// To populate expression parameters named `verb1`, `noun1`, and `noun2` use "The (?<noun2>[a-zA-Z]+) (?<verb1>[a-zA-Z]+) the (?<noun1>[a-zA-Z]+)."
        /// To ignore case: ".*(?i)IgNore CaSE oF THIS(?-i)
        /// </param>
        /// <param name="expression">
        /// Invoked once for each match. The arguments must be named. For example, <code>(string verb1, string noun1, string noun2) =>...</code>"
        /// </param>
        /// <param name="onMatched">
        /// Method to handle the match results. An empty array is provided if <paramref name="input"/> did not match <paramref name="expression"/>.
        /// </param>
        /// <returns></returns>
        public static TResult MatchRegexInvoke<T, TResult>(this string input, string regularExpression,
            Expression<Func<string, string, string, T>> expression,
            Func<T[], TResult> onMatched)
        {
            var exec = expression.Compile();
            return input.MatchRegexInvokeDynamic(regularExpression,
                onMatched,
                expression.Parameters.ToArray(),
                invokeArgs => (T)exec.Invoke(invokeArgs[0], invokeArgs[1], invokeArgs[2]));
        }

        private static TResult MatchRegexInvokeDynamic<T, TResult>(this string input, string regularExpression,
            Func<T[], TResult> onMatched,
            ParameterExpression [] parameterSet,
            Func<string[], T> invoker)
        {
            if (input.IsNullOrWhiteSpace())
                return onMatched(new T[] { });
            var regex = new Regex(regularExpression);
            var matches = regex
                .Matches(input)
                .AsMatches();
            var parameterAndValuess = matches
                .Where(match => match.Success)
                .Select(
                    (match) =>
                    {
                        var groups = match.Groups.AsGroups().Skip(1).ToArray();

                        var assignmentCollection = parameterSet
                            .Select(
                                parameter =>
                                {
                                    var matchingGroups = groups
                                        .Where(
                                            groupMaybe =>
                                            {
                                                if (!(groupMaybe is System.Text.RegularExpressions.Group))
                                                    return false;
                                                var group = groupMaybe as System.Text.RegularExpressions.Group;
                                                var groupName = group.Name;
                                                return groupName == parameter.Name;
                                            });
                                    if (!matchingGroups.Any())
                                        return default(KeyValuePair<ParameterExpression, string>?);

                                    return parameter.PairWithValue(matchingGroups.First().Value);
                                })
                            .SelectWhereHasValue()
                            .ToArray();

                        return assignmentCollection;
                    })
                .Where(parameterAndValues => parameterAndValues.Length == parameterSet.Length)
                .ToArray();
            return parameterAndValuess
                .Aggregate(
                    new string[][] { },
                    (invokeArgss, parameterAndValues) =>
                    {
                        var nextArgs = parameterSet
                            .Aggregate(
                                new string[] { },
                                (invokeArgs, parameter) =>
                                {
                                    var valuesMaybe = parameterAndValues.Where(pAndV => pAndV.Key == parameter);
                                    if (!valuesMaybe.Any())
                                        throw new ArgumentException();
                                    var value = valuesMaybe.First();
                                    return invokeArgs.Append(value.Value).ToArray();
                                });
                        return invokeArgss.Append(nextArgs).ToArray();
                    },
                    (invokeArgss) =>
                    {
                        if (!invokeArgss.Any())
                            return onMatched(new T[] { });
                        
                        return onMatched(
                            invokeArgss
                                .Select(invoker)
                                .ToArray());
                    });
        }

        public static bool TryMatchRegex(this string input, string regularExpression,
            out (string name, string value)[] matches,
            RegexOptions? optionsMaybe = default(RegexOptions?))
        {
            if (input.IsNullOrWhiteSpace())
            {
                matches = default;
                return false;
            }
            if (regularExpression.IsNullOrWhiteSpace())
            {
                matches = default;
                return false;
            }
            try
            {
                var regex = optionsMaybe.HasValue ?
                    new Regex(regularExpression, optionsMaybe.Value)
                    :
                    new Regex(regularExpression);
                var successfulMatches = regex
                    .Matches(input)
                    .AsSuccessfulMatchValues();

                if (successfulMatches.None())
                {
                    matches = new (string name, string value)[] { };
                    return false;
                }

                matches = successfulMatches.First();
                return true;
            }
            catch (RegexParseException)
            {
                matches = default;
                return false;
            }
        }

        public static bool TryMatchRegex(this string input, Regex regex,
            out (string name, string value)[] matches)
        {
            if (input.IsNullOrWhiteSpace())
            {
                matches = default;
                return false;
            }
            if (regex.IsDefaultOrNull())
            {
                matches = default;
                return false;
            }
            try
            {
                var successfulMatches = regex
                    .Matches(input)
                    .AsSuccessfulMatchValues();

                if(successfulMatches.None())
                {
                    matches = new (string name, string value)[] { };
                    return false;
                }

                matches = successfulMatches.First();
                return true;
            } catch (RegexParseException)
            {
                matches = default;
                return false;
            }
        }

        /// <summary>
        /// Invokes expression if there is a match for <paramref name="regularExpression"/> in <paramref name="input"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input">String to match against.</param>
        /// <param name="regularExpression">
        /// To populate expression parameter named `noun1` use "The (?<noun1>[a-zA-Z]+) jumped"
        /// To ignore case: ".*(?i)IgNore CaSE oF THIS(?-i)
        /// </param>
        /// <param name="expression">
        /// Invoked if there is a match. The arguments must be named. For example, <code>(string verb1, string noun1, string noun2) =>...</code>"
        /// </param>
        /// <returns>True if input matches regular expression, otherwise false.</returns>
        public static bool TryMatchRegex<T>(this string input, string regularExpression,
            Expression<Func<string, T>> expression,
            out T result)
        {
            var exec = expression.Compile();
            return input.TryParseRegexDynamic(regularExpression,
                expression.Parameters.ToArray(),
                invokeArgs => (T)exec.Invoke(
                    invokeArgs[0]),
                out result);
        }

        /// <summary>
        /// Invokes expression if there is a match for <paramref name="regularExpression"/> in <paramref name="input"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input">String to match against.</param>
        /// <param name="regularExpression">
        /// To populate expression parameters named `verb1`, and `noun1` use "The (?<noun1>[a-zA-Z]+) (?<verb1>[a-zA-Z]+)."
        /// To ignore case: ".*(?i)IgNore CaSE oF THIS(?-i)
        /// </param>
        /// <param name="expression">
        /// Invoked if there is a match. The arguments must be named. For example, <code>(string verb1, string noun1, string noun2) =>...</code>"
        /// </param>
        /// <returns>True if input matches regular expression, otherwise false.</returns>
        public static bool TryMatchRegex<T>(this string input, string regularExpression,
            Expression<Func<string, string, T>> expression,
            out T result)
        {
            var exec = expression.Compile();
            return input.TryParseRegexDynamic(regularExpression,
                expression.Parameters.ToArray(),
                invokeArgs => (T)exec.Invoke(
                    invokeArgs[0], invokeArgs[1]),
                out result);
        }

        /// <summary>
        /// Invokes expression if there is a match for <paramref name="regularExpression"/> in <paramref name="input"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input">String to match against.</param>
        /// <param name="regularExpression">
        /// To populate expression parameters named `verb1`, `noun1`, and `noun2` use "The (?<noun2>[a-zA-Z]+) (?<verb1>[a-zA-Z]+) the (?<noun1>[a-zA-Z]+)."
        /// To ignore case: ".*(?i)IgNore CaSE oF THIS(?-i)
        /// </param>
        /// <param name="expression">
        /// Invoked if there is a match. The arguments must be named. For example, <code>(string verb1, string noun1, string noun2) =>...</code>"
        /// </param>
        /// <returns>True if input matches regular expression, otherwise false.</returns>
        public static bool TryMatchRegex<T>(this string input, string regularExpression,
            Expression<Func<string, string, string, T>> expression,
            out T result)
        {
            var exec = expression.Compile();
            return input.TryParseRegexDynamic(regularExpression,
                expression.Parameters.ToArray(),
                invokeArgs => (T)exec.Invoke(
                    invokeArgs[0], invokeArgs[1], invokeArgs[2]),
                out result);
        }

        public static bool TryMatchRegex<T>(this string input, string regularExpression,
            Expression<Func<string, string, string, string, T>> expression,
            out T result)
        {
            var exec = expression.Compile();
            return input.TryParseRegexDynamic(regularExpression,
                expression.Parameters.ToArray(),
                invokeArgs => (T)exec.Invoke(
                    invokeArgs[0], invokeArgs[1], invokeArgs[2],
                    invokeArgs[3]),
                out result);
        }

        /// <summary>
        /// Invokes expression if there is a match for <paramref name="regularExpression"/> in <paramref name="input"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input">String to match against.</param>
        /// <param name="regularExpression">
        /// To populate expression parameters named `verb1`, `noun1`, and `noun2` use "The (?<noun2>[a-zA-Z]+) (?<verb1>[a-zA-Z]+) the (?<noun1>[a-zA-Z]+)."
        /// To ignore case: ".*(?i)IgNore CaSE oF THIS(?-i)
        /// </param>
        /// <param name="expression">
        /// Invoked if there is a match. The arguments must be named. For example, <code>(string verb1, string noun1, string noun2) =>...</code>"
        /// </param>
        /// <returns>True if input matches regular expression, otherwise false.</returns>
        public static bool TryMatchRegex<T>(this string input, string regularExpression,
            Expression<Func<string, string, string, string, string, T>> expression,
            out T result)
        {
            var exec = expression.Compile();
            return input.TryParseRegexDynamic(regularExpression,
                expression.Parameters.ToArray(),
                invokeArgs => (T)exec.Invoke(
                    invokeArgs[0], invokeArgs[1], invokeArgs[2],
                    invokeArgs[3], invokeArgs[4]),
                out result);
        }

        private static bool TryParseRegexDynamic<T>(this string input, string regularExpression,
            ParameterExpression[] parameterSet,
            Func<string[], T> invoker,
            out T result)
        {
            var regex = new Regex(regularExpression);
            if(input is null)
            {
                result = default;
                return false;
            }
            var kvp = regex
                .Matches(input)
                .AsMatches()
                .Where(match => match.Success)
                .Select(
                    (match) =>
                    {
                        var groups = match.Groups.AsGroups().Skip(1).ToArray();

                        var assignmentCollection = parameterSet
                            .Select(
                                parameter =>
                                {
                                    var matchingGroups = groups
                                        .Where(
                                            groupMaybe =>
                                            {
                                                if (!(groupMaybe is System.Text.RegularExpressions.Group))
                                                    return false;
                                                var group = groupMaybe as System.Text.RegularExpressions.Group;
                                                var groupName = group.Name;
                                                return groupName == parameter.Name;
                                            });
                                    if (!matchingGroups.Any())
                                        return default(KeyValuePair<ParameterExpression, string>?);

                                    return parameter.PairWithValue(matchingGroups.First().Value);
                                })
                            .SelectWhereHasValue()
                            .ToArray();

                        return assignmentCollection;
                    })
                .Where(parameterAndValues => parameterAndValues.Length == parameterSet.Length)
                .First(
                    (parameterAndValues, next) =>
                    {
                        var nextArgs = parameterSet
                            .Aggregate(
                                new string[] { },
                                (invokeArgs, parameter) =>
                                {
                                    var valuesMaybe = parameterAndValues.Where(pAndV => pAndV.Key == parameter);
                                    if (!valuesMaybe.Any())
                                        throw new ArgumentException();
                                    var value = valuesMaybe.First();
                                    return invokeArgs.Append(value.Value).ToArray();
                                });
                        var result = invoker(nextArgs);
                        return result.PairWithKey(true);
                    },
                    () => default(T).PairWithKey(false));

            result = kvp.Value;
            return kvp.Key;
        }
    }
}
