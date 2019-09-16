using Apex.Runtime.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace Apex.Runtime
{
    public sealed partial class Memory
    {
        public enum Mode
        {
            Tree,
            Graph,
            Detailed
        }

        [DebuggerDisplay("Size = {TotalSize}, {Children?.Count ?? 0} Children")]
        public sealed class ObjectDetails
        {
            public object? Ref;
            public long TotalSize;
            public List<ObjectDetails>? Children;
        }

        private static ConcurrentDictionary<Type, int> _objectSizes = new ConcurrentDictionary<Type, int>();
        private readonly DictionarySlim<Type, Func<object, Memory, long>> _virtualMethods = new DictionarySlim<Type, Func<object, Memory, long>>();
        private Func<object, Memory, long>? _lastMethod;
        private Type? _lastType;

        private readonly HashSet<object>? _objectLookup;
        private readonly Stack<ObjectDetails>? _objectDetails;
        private ObjectDetails? _currentDetail;

        public Memory(Mode mode)
        {
            if(mode == Mode.Graph || mode == Mode.Detailed)
            {
                _objectLookup = new HashSet<object>(new ObjectReferenceComparer());
            }

            if(mode == Mode.Detailed)
            {
                _objectDetails = new Stack<ObjectDetails>();
            }
        }

        public long SizeOf<T>(T obj)
        {
            if(obj is null)
            {
                return IntPtr.Size;
            }

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
                _objectLookup?.Clear();
            }
        }

        public ObjectDetails DetailedSizeOf<T>(T obj)
        {
            if(_objectDetails == null)
            {
                throw new InvalidOperationException("DetailedSizeOf is not supported except for Mode = Detailed");
            }

            if (obj is null)
            {
                return new ObjectDetails { TotalSize = IntPtr.Size };
            }

            SizeOf(obj);

            try
            {
                return _currentDetail!;
            }
            finally
            {
                _currentDetail = null;
            }
        }

        internal long GetSizeOfInternal(object obj)
        {
            if (obj is null)
            {
                return 0;
            }

            if (_objectLookup != null)
            {
                if(!_objectLookup.Add(obj))
                {
                    return 0;
                }
            }

            if (_objectDetails == null)
            {
                return GetTotalSizeOf(obj);
            }

            DetailedPreamble(obj);

            var result = GetTotalSizeOf(obj);

            DetailedPostamble(result);

            return result;
        }

        private void DetailedPostamble(long result)
        {
            _currentDetail!.TotalSize = result;

            if (_objectDetails!.Count > 0)
            {
                _currentDetail = _objectDetails.Pop();
            }
        }

        private void DetailedPreamble(object obj)
        {
            if (_currentDetail?.Ref == null)
            {
                _currentDetail = new ObjectDetails { Ref = obj };
            }
            else
            {
                _objectDetails!.Push(_currentDetail);
                var newDetail = new ObjectDetails { Ref = obj };
                if (_currentDetail.Children == null)
                {
                    _currentDetail.Children = new List<ObjectDetails> { newDetail };
                }
                else
                {
                    _currentDetail.Children.Add(newDetail);
                }

                _currentDetail = newDetail;
            }
        }

        private long GetTotalSizeOf(object obj)
        {
            var type = obj.GetType();

            if (_lastType == type)
            {
                return _lastMethod!(obj, this);
            }

            ref var method = ref _virtualMethods.GetOrAddValueRef(type);

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

            if (!Type<T>.IsValueType && _objectLookup != null)
            {
                if(!_objectLookup.Add(obj))
                {
                    return 0;
                }
            }

            var method = Sizes<T>.Method;
            if (method == null)
            {
                method = (Func<T, Memory, long>)DynamicCode.GenerateMethod(obj.GetType(), false);
                Sizes<T>.Method = method;
            }

            if (_objectDetails == null)
            {
                return method(obj, this);
            }

            DetailedPreamble(obj);

            var result = method(obj, this);

            DetailedPostamble(result);

            return result;
        }

        internal static long GetSizeOfType(Type type)
        {
            return _objectSizes.GetOrAdd(type,
                t =>
                {
                    var (size, overhead) = ObjectLayoutInspector.TypeInspector.GetSize(t);
                    return size + overhead;
                });
        }
    }
}
