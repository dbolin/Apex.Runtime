using Apex.Runtime.Internal;
using System;
using System.Collections.Concurrent;

namespace Apex.Runtime
{
    public sealed partial class Memory
    {
        private static ConcurrentDictionary<Type, int> _objectSizes = new ConcurrentDictionary<Type, int>();
        private readonly DictionarySlim<Type, Func<object, Memory, long>> VirtualMethods = new DictionarySlim<Type, Func<object, Memory, long>>();
        private readonly DictionarySlim<object, int> _objectLookup = new DictionarySlim<object, int>();
        private Func<object, Memory, long> _lastMethod;
        private Type _lastType;

        public long SizeOfGraph<T>(T obj)
        {
            try
            {
                if (Type<T>.IsSealed)
                {
                    return GetSizeOfSealedInternal(obj);
                }

                return GetSizeOfInternal(obj);
            }
            finally
            {
                _objectLookup.Clear();
            }
        }

        internal long GetSizeOfInternal(object obj)
        {
            if(obj is null)
            {
                return 0;
            }

            ref int x = ref _objectLookup.GetOrAddValueRef(obj);
            if(x != 0)
            {
                return 0;
            }

            x = 1;

            var type = obj.GetType();

            if (_lastType == type)
            {
                return _lastMethod(obj, this);
            }

            ref var method = ref VirtualMethods.GetOrAddValueRef(type);

            if (method == null)
            {
                method = (Func<object, Memory, long>)DynamicCode.GenerateMethod(type, true);
            }

            _lastType = type;
            _lastMethod = method;

            return method(obj, this);
        }

        internal long GetSizeOfSealedInternal<T>(T obj)
        {
            if (obj is null)
            {
                return 0;
            }

            if (!Type<T>.IsValueType)
            {
                ref int x = ref _objectLookup.GetOrAddValueRef(obj);
                if (x != 0)
                {
                    return 0;
                }

                x = 1;
            }

            var method = Sizes<T>.Method;
            if (method == null)
            {
                method = (Func<T, Memory, long>)DynamicCode.GenerateMethod(obj.GetType(), false);
                Sizes<T>.Method = method;
            }

            return method(obj, this);
        }

        internal static long GetSizeOfType(Type type)
        {
            return _objectSizes.GetOrAdd(type,
                t =>
                {
                    var (size, overhead) = ObjectLayoutInspector.InspectorHelper.GetSize(t);
                    return size + overhead;
                });
        }
    }
}
