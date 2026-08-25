using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EastFive
{
    public static class RefExtensions
    {
        public static IRef<T> AsRef<T>(this Guid id)
            where T : IReferenceable
        {
            return new Ref<T>(id);
        }

        public static IRefs<T> AsRefs<T>(this IRef<T> first)
            where T : IReferenceable
        {
            return new Refs<T>([first.id]);
        }

        public static IRefs<T> AsRefs<T>(this IEnumerable<Guid> ids)
            where T : IReferenceable
        {
            return new Refs<T>(ids.ToArray());
        }

        public static IRefs<T> Refs<T>(this IEnumerable<IRef<T>> refs)
            where T : IReferenceable
        {
            return refs.Select(r => r.id).AsRefs<T>();
        }

        public static IRefs<T> AsRefs<T>(this IEnumerable<T> refs)
            where T : IReferenceable
        {
            return refs.Select(r => r.id).AsRefs<T>();
        }

        public static async Task<IRefs<T>> AsRefsAsync<T>(this IEnumerableAsync<Guid> ids)
            where T : IReferenceable
        {
            var idsArray = await ids.ToArrayAsync();
            return new Refs<T>(idsArray);
        }

        public static Task<IRefs<T>> AsRefsAsync<T>(this IEnumerableAsync<IRef<T>> refs)
            where T : IReferenceable
        {
            return refs.Select(r => r.id).AsRefsAsync<T>();
        }

        public static IRefOptional<T> Optional<T>(this IRef<T> baseRef)
            where T : IReferenceable
        {
            if (baseRef is null)
                return RefOptional<T>.Empty();

            return new RefOptional<T>(baseRef);
        }

        public static IRefOptional<T> AsRefOptional<T>(this Guid? baseRef)
            where T : IReferenceable
        {
            if (!baseRef.HasValue)
                return new RefOptional<T>();

            return baseRef.Value.AsRef<T>().Optional();
        }

        public static IRefOptional<TType> AsRefOptional<TType>(this Guid guid)
            where TType : IReferenceable
        {
            return guid.AsOptional().AsRefOptional<TType>();
        }

        public static IRef<TCast> CastRef<TCast>(this IReferenceable sourceRef)
            where TCast : IReferenceable
        {
            if (sourceRef is null)
                return null;
            return sourceRef.id.AsRef<TCast>();
        }

        public static bool TryCastObjRef(this object refObj, Type type, out object parsedRefObj)
        {
            if (refObj.IsDefaultOrNull())
            {
                parsedRefObj = type.GetDefault();
                return false;
            }
            var objType = refObj.GetType();

            if (objType.IsAssignableTo(typeof(Guid)))
            {
                var refGuid = (Guid)refObj;
                return refGuid.TryCastRef(type, out parsedRefObj);
            }

            if (objType.IsAssignableTo(typeof(string)))
            {
                var refStr = (string)refObj;
                return refStr.TryParseRef(type, out parsedRefObj, out var didMatchType);
            }

            parsedRefObj = type.GetDefault();
            return false;
        }

        public static IRef<T> ParseRef<T>(this string guidString)
            where T : IReferenceable
        {
            return Ref<T>.NewRef(guidString);
        }

        public static bool TryParseRef(this string refString, Type type,
            out object parsedRefObj, out bool didMatch)
        {
            if (type.IsSubClassOfGeneric(typeof(IReferenceable)))
            {
                didMatch = true;
                if (type.IsSubClassOfGeneric(typeof(IRef<>)))
                {
                    var refType = typeof(Ref<>).MakeGenericType(type.GenericTypeArguments);
                    if (Guid.TryParse(refString, out Guid id))
                    {
                        parsedRefObj = Activator.CreateInstance(refType, id);
                        return true;
                    }
                    parsedRefObj = refType.GetDefault();
                    return false;
                }
            }

            if (type.IsSubClassOfGeneric(typeof(IReferenceableOptional)))
            {
                didMatch = true;
                if (type.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                {
                    var refType = typeof(RefOptional<>).MakeGenericType(type.GenericTypeArguments);
                    if (Guid.TryParse(refString, out Guid id))
                    {
                        parsedRefObj = Activator.CreateInstance(refType, id);
                        return true;
                    }
                    parsedRefObj = RefOptionalHelper.CreateEmpty(type.GenericTypeArguments.First());
                    return true;
                }
            }

            if (type.IsSubClassOfGeneric(typeof(IReferences)))
            {
                didMatch = true;
                if (type.IsSubClassOfGeneric(typeof(IRefs<>)))
                {
                    var ids = GetGuids();
                    var refType = typeof(Refs<>).MakeGenericType(type.GenericTypeArguments);
                    parsedRefObj = Activator.CreateInstance(refType, ids);
                    return true;
                }
            }

            didMatch = false;
            parsedRefObj = type.GetDefault();
            return false;

            Guid[] GetGuids()
            {
                if (refString.IsNullOrWhiteSpace())
                    return new Guid[] { };

                return refString
                    .Split(',')
                    .TrySelect(
                        (string guidStr, out Guid guid) =>
                        {
                            return Guid.TryParse(guidStr, out guid);
                        })
                    .ToArray();
            }
        }

        public static bool TryCastRef(this Guid refGuid, Type type, out object parsedRefObj)
        {
            if (type.IsSubClassOfGeneric(typeof(IReferenceable)))
            {
                if (type.IsSubClassOfGeneric(typeof(IRef<>)))
                {
                    var refType = typeof(Ref<>).MakeGenericType(type.GenericTypeArguments);
                    parsedRefObj = Activator.CreateInstance(refType, refGuid);
                    return true;
                }
            }

            if (type.IsSubClassOfGeneric(typeof(IReferenceableOptional)))
            {
                if (type.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                {
                    var refType = typeof(RefOptional<>).MakeGenericType(type.GenericTypeArguments);
                    parsedRefObj = Activator.CreateInstance(refType, refGuid);
                    return true;
                }
            }

            parsedRefObj = type.GetDefault();
            return false;

        }

        public static IImplementRef<TInterface> AsImplementRef<TInterface, TType>(this Guid id)
            where TInterface : IReferenceable
            where TType : TInterface
        {
            return new ImplementRef<TInterface, TType>(id);
        }

        public static IImplementRef<TInterface> AsImplementRef<TInterface, TType>(this IRef<TType> @ref)
            where TInterface : IReferenceable
            where TType : TInterface
        {
            return @ref.id.AsImplementRef<TInterface, TType>();
        }

        public static void AsImplementRef<TInterface, TType>(this IRef<TType> @ref, out IImplementRef<TInterface> implementRef)
            where TInterface : IReferenceable
            where TType : TInterface
        {
            if(@ref.IsDefaultOrNull())
            {
                implementRef = default;
                return;
            }
            implementRef = @ref.id.AsImplementRef<TInterface, TType>();
        }

        public static bool HasValueNotNull<T>(this IRefOptional<T> refOptional) 
            where T : IReferenceable
        {
            if (refOptional.IsDefaultOrNull())
                return false;
            return refOptional.HasValue && !refOptional.Ref.id.IsDefault();
        }

        public static TResult HasValueNotDefault<T, TResult>(this IRefOptional<T> refOptional,
            Func<IRef<T>, TResult> onValue,
            Func<TResult> onEmpty)
            where T : IReferenceable
        {
            if (!refOptional.IsDefaultOrNull())
                if (refOptional.HasValue)
                    if (!refOptional.Ref.id.IsDefault())
                        return onValue(refOptional.Ref);
            return onEmpty();
        }

        public static bool TryGetRefWithValue<T>(this IRefOptional<T> refOptional, out IRef<T> refValue)
            where T : IReferenceable
        {
            if (!refOptional.IsDefaultOrNull())
            {
                if (refOptional.HasValue)
                {
                    if (!refOptional.Ref.id.IsDefault())
                    {
                        refValue = refOptional.Ref;
                        return true;
                    }
                }
            }
            refValue = default;
            return false;
        }

        public static Guid? GetIdMaybeNullSafe<T>(this IRefOptional<T> refOptional)
            where T : IReferenceable
        {
            if (refOptional.IsDefaultOrNull())
                return default;
            return refOptional.id;
        }

        public static bool EqualsRef<T>(this IRef<T> refValue1, IRef<T> refValue2)
            where T : IReferenceable
        {
            if (refValue1.IsDefaultOrNull())
                return refValue2.IsDefaultOrNull();

            if (refValue2.IsDefaultOrNull())
                return false;

            return refValue1.id == refValue2.id;
        }

        public static bool EqualsRef<T>(this IRefOptional<T> refOptional, IRef<T> refValue)
            where T : IReferenceable => refValue.EqualsRef(refOptional);

        public static bool EqualsRef<T>(this IRef<T> refValue,  IRefOptional<T> refOptional)
            where T : IReferenceable
        {
            if (refOptional.IsDefaultOrNull())
                return false;
            if (!refOptional.HasValue)
                return false;
            return refValue.EqualsRef(refOptional.Ref);
        }

        public static bool EqualsRef<T>(this IRefOptional<T> refOptional1, IRefOptional<T> refOptional2)
            where T : IReferenceable
        {
            if (refOptional1.HasValueNotNull())
            {
                var refValue = refOptional1.Ref;
                return refValue.EqualsRef(refOptional2);
            }
            return !refOptional2.HasValueNotNull();
        }

        public static bool EqualsRef<T>(this IRefOptional<T> refOptional1, IRefOptional<T> refOptional2,
                out bool areBothNull)
            where T : IReferenceable
        {
            if (refOptional1.HasValueNotNull())
            {
                var refValue = refOptional1.Ref;
                areBothNull = false;
                return refValue.EqualsRef(refOptional2);
            }
            areBothNull = !refOptional2.HasValueNotNull();
            return areBothNull;
        }

        public static bool EqualsRefAndHasValue<T>(this IRefOptional<T> refOptional1, IRefOptional<T> refOptional2)
            where T : IReferenceable
        {
            var areEqual = refOptional1.EqualsRef(refOptional2, out var areBothNull);
            if (areBothNull)
                return false;
            return areEqual;
        }

        public static bool EqualsRefOrEitherIsNull<T>(this IRefOptional<T> refOptional1, IRefOptional<T> refOptional2)
            where T : IReferenceable
        {
            if (!refOptional1.HasValueNotNull())
                return true;
            if (!refOptional2.HasValueNotNull())
                return true;
            var areEqual = refOptional1.EqualsRef(refOptional2);
            return areEqual;
        }

        public static bool EqualsRefOrIsNull<T>(this IRefOptional<T> refOptional1, IRef<T> refOptional2)
            where T : IReferenceable
        {
            if (!refOptional1.HasValueNotNull())
                return true;
            var areEqual = refOptional1.EqualsRef(refOptional2);
            return areEqual;
        }

        public static bool EqualsRefOrIsNull<T>(this IRef<T> refOptional1, IRefOptional<T> refOptional2)
            where T : IReferenceable
        {
            if (!refOptional2.HasValueNotNull())
                return true;
            var areEqual = refOptional1.EqualsRef(refOptional2);
            return areEqual;
        }
    }
}
