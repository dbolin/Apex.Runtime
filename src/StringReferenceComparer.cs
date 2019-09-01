using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Apex.Runtime
{
    internal sealed class StringReferenceComparer : IEqualityComparer<string>
    {
        public bool Equals(string x, string y)
        {
            //return StringComparer.Ordinal.Equals(x, y);
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(string obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}