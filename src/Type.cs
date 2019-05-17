using System;

namespace Apex.Runtime
{
    public static class Type<T>
    {
        public static bool IsSealed = typeof(T).IsSealed;
        public static bool IsValueType = typeof(T).IsValueType;
        public static bool IsNullable = typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>);
    }
}
