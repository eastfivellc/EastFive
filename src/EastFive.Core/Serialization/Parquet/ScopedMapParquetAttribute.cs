using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Linq.Async;

namespace EastFive.Serialization.Parquet
{
    public abstract class ScopedMapParquetAttribute : Attribute, IMapParquet
    {
        public string Scope { get; set; }

        public string Scopes { get; set; }

        public virtual bool DoesParse(string scope)
        {
            if (this.Scope.HasBlackSpace())
                if (String.Equals(Scope, scope, StringComparison.Ordinal))
                    return true;

            if (this.Scopes.HasBlackSpace())
                return this.Scopes
                    .Split(',')
                    .Select(scopeCandidate => scopeCandidate.Trim())
                    .Where(scopeCandidate => String.Equals(scopeCandidate, scope, StringComparison.Ordinal))
                    .Any();

            return this.Scope.IsNullOrWhiteSpace();
        }

        abstract public TResource[] Parse<TResource>(Stream parquetData,
            IFilterParquet[] textFilters,
            params Stream[] parquetDataJoins);
    }
}

