using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Apex.Runtime.Internal
{
    internal static class TypeFields
    {
        private static DictionarySlim<Type, List<FieldInfo>> _cache = new DictionarySlim<Type, List<FieldInfo>>();

        private static object _cacheLock = new object();

        internal static List<FieldInfo> GetFields(Type type)
        {
            lock (_cacheLock)
            {
                ref var fields = ref _cache.GetOrAddValueRef(type);

                var originalType = type;
                var start = Enumerable.Empty<FieldInfo>();
                while (type != null)
                {
                    start = start.Concat(type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                        BindingFlags.NonPublic |
                                                        BindingFlags.DeclaredOnly));

                    type = type.BaseType;
                }

                fields = start.ToList();
                return fields;
            }
        }
    }
}
