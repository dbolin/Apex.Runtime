using Apex.Runtime.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Apex.Runtime
{
    public sealed partial class Memory
    {
        internal static class Sizes<T>
        {
            public static Func<T, Memory, long> Method;
        }

        internal static class DynamicCode
        {
            private static readonly ConcurrentDictionary<Type, Delegate> _virtualMethods = new ConcurrentDictionary<Type, Delegate>();

            internal static Delegate GenerateMethod(Type type, bool isVirtual)
            {
                if (!isVirtual)
                {
                    return GenerateMethodImpl(type, isVirtual);
                }

                return _virtualMethods.GetOrAdd(type, t => GenerateMethodImpl(t, isVirtual));
            }

            private static Delegate GenerateMethodImpl(Type type, bool isVirtual)
            {
                var source = Expression.Parameter(isVirtual ? typeof(object) : type, "obj");
                var memory = Expression.Parameter(typeof(Memory), "memory");

                var statements = new List<Expression>();
                var localVariables = new List<ParameterExpression>();

                var castedSource = source;

                if (isVirtual)
                {
                    castedSource = Expression.Variable(type);
                    localVariables.Add(castedSource);
                    statements.Add(Expression.Assign(castedSource, Expression.Convert(source, type)));
                }

                var result = Expression.Variable(typeof(long), "result");
                localVariables.Add(result);

                statements.Add(Expression.Assign(result, Expression.Constant(0L)));

                var specificSizeExpression = GetSpecificSizeExpression(type, memory, castedSource);
                if(specificSizeExpression != null)
                {
                    statements.Add(Expression.AddAssign(result, specificSizeExpression));
                }
                else
                {
                    if (type.IsArray)
                    {
                        var elementType = type.GetElementType();
                        var dimensions = type.GetArrayRank();
                        var lengths = new List<ParameterExpression>();
                        for (int i = 0; i < dimensions; ++i)
                        {
                            lengths.Add(Expression.Variable(typeof(int)));
                        }

                        var loopExpressions = new List<Expression>();
                        loopExpressions.AddRange(lengths.Select((x, i) =>
                            Expression.Assign(x, Expression.Call(castedSource, "GetLength", Array.Empty<Type>(), Expression.Constant(i)))));

                        if (HasNoReferences(elementType))
                        {
                            loopExpressions.Add(
                                Expression.AddAssign(result,
                                    lengths.Select(x => Expression.Convert(x, typeof(long))).Aggregate((Expression)Expression.Constant(GetSizeOfType(elementType)), (x, y) => Expression.Multiply(x, y))));
                            statements.Add(Expression.Block(lengths, loopExpressions));
                            statements.Add(Expression.AddAssign(result, Expression.Constant((long)IntPtr.Size * 3)));
                        }
                        else
                        {
                            loopExpressions.Add(Expression.AddAssign(result,
                                    lengths.Select(x => Expression.Convert(x, typeof(long))).Aggregate((Expression)Expression.Constant((long)IntPtr.Size), (x, y) => Expression.Multiply(x, y))));

                            var indices = new List<ParameterExpression>();
                            var breakLabels = new List<LabelTarget>();
                            var continueLabels = new List<LabelTarget>();

                            for (int i = 0; i < dimensions; ++i)
                            {
                                indices.Add(Expression.Variable(typeof(int)));
                                breakLabels.Add(Expression.Label());
                                continueLabels.Add(Expression.Label());
                            }

                            var accessExpression = dimensions > 1
                                ? (Expression)Expression.ArrayIndex(castedSource, indices)
                                : Expression.ArrayIndex(castedSource, indices[0]);

                            Expression getSize = Expression.AddAssign(result, GetSizeExpression(elementType, memory, accessExpression));

                            var loop = getSize;

                            for (int i = 0; i < dimensions; ++i)
                            {
                                loop =
                                    Expression.Block(
                                        Expression.Assign(indices[i], Expression.Constant(0)),
                                        Expression.Loop(Expression.IfThenElse(
                                            Expression.GreaterThanOrEqual(indices[i], lengths[i]),
                                            Expression.Break(breakLabels[i]),
                                            Expression.Block(loop, Expression.Label(continueLabels[i]), Expression.Assign(indices[i], Expression.Increment(indices[i])))
                                        ), breakLabels[i])
                                    );
                            }

                            loopExpressions.Add(Expression.Block(indices, loop));

                            statements.Add(Expression.Block(lengths, loopExpressions));
                        }
                    }
                    else
                    {
                        statements.Add(Expression.AddAssign(result, Expression.Constant(GetSizeOfType(type))));
                        var fields = TypeFields.GetFields(type);
                        statements.AddRange(GetReferenceSizes(fields, castedSource, memory).Select(x => Expression.AddAssign(result, x)));
                    }

                    statements.Add(result);
                }

                var lambda = Expression.Lambda(Expression.Block(localVariables, statements), $"Apex.Runtime.Memory_SizeOf_{type.FullName}", new[] { source, memory }).Compile();

                return lambda;
            }

            private static bool HasNoReferences(Type elementType)
            {
                if (elementType == typeof(string))
                {
                    return false;
                }

                if(elementType.IsArray)
                {
                    return false;
                }

                var fields = TypeFields.GetFields(elementType);
                return fields.All(x => x.FieldType.IsPrimitive || x.FieldType.IsValueType && HasNoReferences(x.FieldType));
            }

            private static IEnumerable<Expression> GetReferenceSizes(List<FieldInfo> fields,
                Expression source,
                Expression memory)
            {
                foreach (var field in fields)
                {
                    var fieldType = field.FieldType;
                    if (fieldType.IsPrimitive || fieldType.IsPointer)
                    {
                        continue;
                    }

                    var subSource = Expression.MakeMemberAccess(source, field);

                    if (typeof(Task).IsAssignableFrom(fieldType))
                    {
                        var resultProperty = fieldType.GetProperty("Result");

                        if (resultProperty == null)
                        {
                            yield return Expression.Constant(GetSizeOfType(fieldType));
                            continue;
                        }

                        var resultType = resultProperty.PropertyType;

                        yield return Expression.Add(GetSizeExpression(resultType, memory, Expression.Property(subSource, resultProperty)), Expression.Constant(GetSizeOfType(fieldType)));
                        continue;
                    }

                    if (fieldType.IsValueType)
                    {
                        var fieldTypeFields = TypeFields.GetFields(fieldType);
                        var subSizes = GetReferenceSizes(fieldTypeFields, subSource, memory);
                        foreach (var subResult in subSizes)
                        {
                            yield return subResult;
                        }
                    }
                    else
                    {
                        yield return GetSizeExpression(fieldType, memory, subSource);
                    }
                }
            }

            private static Expression? GetSpecificSizeExpression(Type type, Expression memory, Expression access)
            {
                if (type == typeof(string))
                {
                    return Expression.Condition(
                           Expression.ReferenceEqual(access, Expression.Constant(null)),
                           Expression.Constant(0L),
                           Expression.Add(Expression.Constant((long)IntPtr.Size * 2 + 6), Expression.Convert(Expression.Multiply(Expression.Constant(2), Expression.Property(access, "Length")), typeof(long)))
                           );
                }

                if (typeof(Task).IsAssignableFrom(type) || type == typeof(ValueTask) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>)))
                {
                    var resultProperty = type.GetProperty("Result");

                    if (resultProperty == null)
                    {
                        return Expression.Constant(GetSizeOfType(type));
                    }
                    else
                    {
                        var resultType = resultProperty.PropertyType;

                        return Expression.Add(GetSizeExpression(resultType, memory, Expression.Property(access, resultProperty)), Expression.Constant(GetSizeOfType(type)));
                    }
                }

                if (type.IsPointer)
                {
                    return Expression.Constant((long)IntPtr.Size);
                }

                return null;
            }

            private static Expression GetSizeExpression(Type type, Expression memory, Expression access)
            {
                var specificSizeExpression = GetSpecificSizeExpression(type, memory, access);

                if(specificSizeExpression != null)
                {
                    return specificSizeExpression;
                }

                if (type.IsArray)
                {
                    return Expression.Call(memory, "GetSizeOfSealedInternal", new Type[] { type }, access);
                }

                if (type.IsSealed)
                {
                    if (type.IsValueType)
                    {
                        var fields = TypeFields.GetFields(type);
                        var all = GetReferenceSizes(fields, access, memory).Aggregate((Expression)Expression.Constant(0L), (x, y) => Expression.Add(x, y));

                        return Expression.Add(all, Expression.Constant(GetSizeOfType(type)));
                    }

                    if (HasNoReferences(type))
                    {
                        var fields = TypeFields.GetFields(type);
                        var all = GetReferenceSizes(fields, access, memory).Aggregate((Expression)Expression.Constant(0L), (x, y) => Expression.Add(x, y));

                        return Expression.Condition(
                           Expression.ReferenceEqual(access, Expression.Constant(null)),
                           Expression.Constant(0L),
                           Expression.Add(all, Expression.Constant(GetSizeOfType(type)))
                           );
                    }

                    return Expression.Call(memory, "GetSizeOfSealedInternal", new Type[] { type }, access);
                }
                else
                {
                    return Expression.Call(memory, "GetSizeOfInternal", Array.Empty<Type>(), access);
                }
            }
        }
    }
}
