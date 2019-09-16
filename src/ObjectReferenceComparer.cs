using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Apex.Runtime
{
    internal sealed class ObjectReferenceComparer : IEqualityComparer<object>
    {
        new public bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}