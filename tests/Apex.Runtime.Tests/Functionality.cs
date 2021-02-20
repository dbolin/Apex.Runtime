using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;

namespace Apex.Runtime.Tests
{
    public class Functionality
    {
        private readonly Memory sut;

        private struct Test3
        {
            public DateTime? test;
            public string asd;
        }
        private struct Test2
        {
            public int x;
            public int y;
            public Guid g;
            public Test3 Test3;
        }
        private class Test
        {
            public Test2 Test2;
            public int X;
        }

        private class TestLoop
        {
            public TestLoop x;
            public TestLoop y;
        }

        public class CustomEquality : IEquatable<CustomEquality>
        {
            public int A;

            public override bool Equals(object obj)
            {
                return Equals(obj as CustomEquality);
            }

            public bool Equals(CustomEquality other)
            {
                return other != null &&
                       A == other.A;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(A);
            }
        }

        public Functionality()
        {
            sut = new Memory(Memory.Mode.Graph);
        }

        private void ExactSize<T>(Func<T> func, int adjustment = 0)
        {
            GC.Collect();

            var s = GC.GetAllocatedBytesForCurrentThread();

            var t = func();

            GC.Collect();
            var e = GC.GetAllocatedBytesForCurrentThread();

            var actual = sut.SizeOf(t);

            t = func();

            actual.Should().Be(e - s + adjustment);
        }

        [Fact]
        public void TestNested()
        {
            sut.SizeOf<string>(null).Should().Be(4);

            var x = new Test { Test2 = new Test2 { Test3 = new Test3 { } } };

            ExactSize(() => new Test { Test2 = new Test2 { Test3 = new Test3 { } } });
        }

        [Fact]
        public void Object()
        {
            ExactSize(() => new object());
        }

        [Fact]
        public void Loops()
        {
            ExactSize(() =>
            {
                var x = new TestLoop();
                var y = new TestLoop { x = x };
                x.y = y;
                return x;
            });
        }

        [Fact]
        public void MultipleStrings()
        {
            ExactSize(() =>
            {
                var a = "1234567890";
                var x = new[] { a, a, a, a, a };
                return x;
            });
        }

        [Fact]
        public void MultipleNewStrings()
        {
            ExactSize(() =>
            {
                var a = new string(' ', 10);
                var b = new string(' ', 10);
                var x = new[] { a, b };
                return x;
            });
        }

        [Fact]
        public void Array()
        {
            ExactSize(() => new int[4]);

            ExactSize(() => new string[0]);

            ExactSize(() => new[] { new string(' ', 1), null, null });
        }

        [Fact]
        public void BoxedValues()
        {
            var x = 4;
            var obj = (object)x;
            ExactSize(() => obj, 4);
            ExactSize(() =>
            {
                var y = new object[] { obj };
                return y;
            }, 4);
        }

        public class Test1
        {
            int A;
        }

        public class Test1B : Test1
        {
            int B;
        }

        [Fact]
        public void ObjectArray()
        {
            ExactSize(() =>
            {
                var x = new Test1B();
                var y = new object[] { x, x, x, x };
                return y;
            });
        }

        [Fact]
        public void ArrayArray()
        {
            ExactSize(() => new[] { new int[4], new int[4] });

            ExactSize(() => new[] { new int[4], new int[4], null });
        }

        [Fact]
        public void Dictionary()
        {
            ExactSize(() => new Dictionary<int, int>(100));
        }

        [Fact]
        public void Strings()
        {
            for (int i = 0; i <= 100; ++i)
            {
                ExactSize(() => new string(' ', i));
            }
        }

        [Fact]
        public void FinalizerShouldNotBeCalledExtraTimes()
        {
            sut.SizeOf(new TestFinalizer());

            GC.Collect();
            GC.WaitForPendingFinalizers();

            TestFinalizer.FinalizerWasCalled.Should().BeLessOrEqualTo(1);
        }

        private unsafe class AP
        {
            public char* t;
        }

        [Fact]
        public void Pointers()
        {
            sut.SizeOf(new IntPtr()).Should().Be(IntPtr.Size);

            ExactSize(() => new { a = new IntPtr() });

            ExactSize(() => new AP[1] { new AP() });
        }

        [Fact]
        public void Tasks()
        {
            sut.SizeOf(Task.CompletedTask).Should().Be(41);

            sut.SizeOf(Task.Delay(1)).Should().Be(40);

            sut.SizeOf(Task.FromResult(4)).Should().Be(44);

            sut.SizeOf(Task.FromResult(4L)).Should().Be(52);
        }

        [Fact]
        public void ValueTasks()
        {
            sut.SizeOf(new ValueTask()).Should().Be(8);

            sut.SizeOf(new ValueTask<int>(4)).Should().Be(16);

            sut.SizeOf(new ValueTask(Task.Delay(1))).Should().Be(48);
        }

        private sealed class SealedC { }

        [Fact]
        public void Graph()
        {
            ExactSize(() =>
            {
                var o = new SealedC();
                return new { a = o, b = o, c = o };
            });
        }

        [Fact]
        public void Tree()
        {
            var sut2 = new Memory(Memory.Mode.Tree);
            var o = new SealedC();
            sut2.SizeOf(new { a = o }).Should().Be(24);
            sut2.SizeOf(new { a = o, b = o }).Should().Be(40);
            sut2.SizeOf(new { a = o, b = o, c = o }).Should().Be(56);
        }

        [Fact]
        public void CustomEqualityComparer()
        {
            ExactSize(() =>
            {
                var x = new CustomEquality();
                var y = new CustomEquality();
                return new[] { x, y };
            });
        }
    }

    internal class TestFinalizer
    {
        public static int FinalizerWasCalled;

        ~TestFinalizer()
        {
            FinalizerWasCalled++;
        }
    }
}
