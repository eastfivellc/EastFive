using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive.Extensions;
using EastFive.Security;

namespace EastFive
{
    #region IRef(Obj)

    public interface IReferenceable
    {
        Guid id { get; }
    }

    public interface IRef<TType> : IReferenceable // TODO: , IRef
        where TType : IReferenceable
    {
        // Guid id { get; }
    }

    public interface IImplementRef<TType> : IReferenceable // TODO: , IRef
        where TType : IReferenceable
    {
        Type type { get; }
    }

    #endregion

    #region IRef(Obj)Optional

    public interface IReferenceableOptional
    {
        Guid? id { get; }

        bool HasValue { get; }
    }

    public interface IRefOptional<TType> : IReferenceableOptional
        where TType : IReferenceable
    {
        IRef<TType> Ref { get; }
    }

    #endregion

    #region IRef plural

    public interface IReferences
    {
        Guid[] ids { get; }
    }

    public interface IRefs<TType> : IReferences, IEnumerable<IRef<TType>>
        where TType : IReferenceable
    {
        IRef<TType>[] refs { get; }
    }

    #endregion

    public struct Ref<TType> : IRef<TType>
        where TType : IReferenceable
    {
        public Ref(Guid id) : this()
        {
            this.id = id;
        }

        public Guid id { get; private set; }

        public static implicit operator Ref<TType>(Guid value)
        {
            return new Ref<TType>()
            {
                id = value,
            };
        }

        public static implicit operator Ref<TType>(TType value)
        {
            return value.IsDefault() ?
                default(Ref<TType>)
                :
                new Ref<TType>(value.id);
        }

        public static IRef<TType> NewRef()
        {
            return Guid.NewGuid().AsRef<TType>();
        }

        public static IRef<TType> NewRef(string guidString)
        {
            return new Guid(guidString).AsRef<TType>();
        }

        public static bool TryParse(string input, out IRef<TType> result)
        {
            if (Guid.TryParse(input, out Guid guidResult))
            {
                result = guidResult.AsRef<TType>();
                return true;
            }
            result = default;
            return false;
        }

        public static IRef<TType> Parse(string input)
        {
            var guidResult = Guid.Parse(input);
            return guidResult.AsRef<TType>();
        }

        public static IRef<TType> SecureRef()
        {
            return SecureGuid.Generate().AsRef<TType>();
        }

        public override string ToString()
        {
            var typeName = typeof(TType).FullName;
            return $"{this.id}<{typeName}>";
        }

    }

    public struct ImplementRef<TInterface, TType> : IImplementRef<TInterface>
        where TInterface : IReferenceable
        where TType : TInterface
    {
        public Guid id { get; private set; }

        public Type type => typeof(TType);

        public ImplementRef(Guid id) : this()
        {
            this.id = id;
        }

        public static implicit operator ImplementRef<TInterface, TType>(Guid value)
        {
            return new ImplementRef<TInterface, TType>()
            {
                id = value,
            };
        }

        public static implicit operator ImplementRef<TInterface, TType>(TInterface value)
        {
            return value.IsDefault() ?
                default(ImplementRef<TInterface, TType>)
                :
                new ImplementRef<TInterface, TType>(value.id);
        }

        public static IImplementRef<TInterface> NewRef()
        {
            return Guid.NewGuid().AsImplementRef<TInterface, TType>();
        }

        public static IImplementRef<TInterface> NewRef(string guidString)
        {
            return new Guid(guidString).AsImplementRef<TInterface, TType>();
        }

        public static bool TryParse(string input, out IImplementRef<TInterface> result)
        {
            if (Guid.TryParse(input, out Guid guidResult))
            {
                result = guidResult.AsImplementRef<TInterface, TType>();
                return true;
            }
            result = default;
            return false;
        }

        public static IImplementRef<TInterface> Parse(string input)
        {
            var guidResult = Guid.Parse(input);
            return guidResult.AsImplementRef<TInterface, TType>();
        }

        public override string ToString()
        {
            var typeName = typeof(TType).FullName;
            var interfaceName = typeof(TInterface).FullName;
            return $"{this.id}<{interfaceName}:{typeName}>";
        }
    }

    public static class RefOptionalHelper
    {
        public static object CreateEmpty(Type type)
        {
            var emptyValue = typeof(RefOptional<>)
                .MakeGenericType(type)
                .GetMethod(nameof(RefOptional<IReferenceable>.Empty), BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, new object[] { });
            return emptyValue;
        }

        public static object BindToRef(this Guid guidValue, Type typeReferenced)
        {
            var refType = typeof(EastFive.Ref<>).MakeGenericType(typeReferenced);
            var instance = Activator.CreateInstance(refType, new object[] { guidValue });
            return instance;
        }

        public static object BindToRefType(this Guid guidValue, Type refType)
        {
            var typeReferenced = refType.GenericTypeArguments.First();
            return guidValue.BindToRef(typeReferenced);
        }

        public static object BindToRefOptional(this Guid? guidValue, Type typeReferenced)
        {
            var refType = typeof(EastFive.RefOptional<>).MakeGenericType(typeReferenced);
            var instance = Activator.CreateInstance(refType, new object[] { guidValue });
            return instance;
        }

        public static object BindToRefOptionalType(this Guid? guidValue, Type refType)
        {
            var typeReferenced = refType.GenericTypeArguments.First();
            return guidValue.BindToRefOptional(typeReferenced);
        }
    }

    public struct RefOptional<TType> : IRefOptional<TType>
        where TType : IReferenceable
    {
        [Newtonsoft.Json.JsonIgnore]
        private IRef<TType> baseRef;

        public static IRefOptional<TType> Empty()
        {
            return new RefOptional<TType>
            {
                HasValue = false,
                baseRef = default(IRef<TType>),
            };
        }

        public RefOptional(Guid baseId)
        {
            this.HasValue = true;
            this.baseRef = new Ref<TType>(baseId);
        }

        public RefOptional(Guid? baseId)
        {
            if (baseId.HasValue)
            {
                this.HasValue = true;
                this.baseRef = new Ref<TType>(baseId.Value);
                return;
            }
            this.HasValue = false;
            this.baseRef = default(IRef<TType>);
        }

        public RefOptional(IRef<TType> baseRef)
        {
            this.HasValue = true;
            this.baseRef = baseRef;
        }

        public Guid? id
        {
            get
            {
                if (!this.HasValue)
                    return default(Guid?);
                if(this.baseRef == null)
                    return default(Guid?);
                return baseRef.id;
            }
        }

        public bool HasValue { get; private set; }

        [Newtonsoft.Json.JsonIgnore]
        public IRef<TType> Ref
        {
            get
            {
                if (!this.HasValue)
                    throw new InvalidOperationException("Attempt to de-option empty value");
                return baseRef;
            }
        }

        public static bool TryParse(string input, out IRefOptional<TType> result)
        {
            if (Guid.TryParse(input, out Guid guidResult))
            {
                result = guidResult.AsRefOptional<TType>();
                return true;
            }
            result = default;
            return false;
        }

        public override string ToString()
        {
            var typeName = typeof(TType).FullName;
            return this.HasValue ?
                $"{this.id.Value}?<{typeName}>"
                :
                $"null?<{typeName}>";
        }
    }

    public struct Refs<TType> : IRefs<TType>
        where TType : IReferenceable
    {
        public Refs(Guid[] ids) : this()
        {
            this.ids = ids;
        }

        public Guid[] ids { get; set; } 

        public IRef<TType>[] refs
        {
            get
            {
                var safeIds = ids ?? new Guid[] { };
                return safeIds.Select(id => id.AsRef<TType>()).ToArray();
            }
        }

        public static IRefs<TType> Empty()
        {
            return new Refs<TType>(new Guid[] { });
        }

        IEnumerator<IRef<TType>> IEnumerable<IRef<TType>>.GetEnumerator()
        {
            var safeIds = ids ?? new Guid[] { };
            return safeIds
                .Select(id => (IRef<TType>)new Ref<TType>(id))
                .GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            var safeIds = ids ?? new Guid[] { };
            return safeIds
                .Select(id => (IRef<TType>)new Ref<TType>(id))
                .GetEnumerator();
        }

        //IEnumerator<Guid> IEnumerable<Guid>.GetEnumerator()
        //{
        //    var safeIds = ids ?? new Guid[] { };
        //    return safeIds.AsEnumerable<Guid>().GetEnumerator();
        //}

        public static implicit operator Refs<TType>(Guid [] values)
        {
            return new Refs<TType>(values);
        }

        public static implicit operator Refs<TType>(TType[] values)
        {
            return values.IsDefault() ?
                new Refs<TType>(new Guid[] { })
                :
                new Refs<TType>(values.Select(v => v.id).ToArray());
        }
    }
}
