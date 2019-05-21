using FluentAssertions;
using System;
using System.Collections.Generic;
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

        public Functionality()
        {
            sut = new Memory(true);
        }

        [Fact]
        public void Test1()
        {
            sut.SizeOf<string>(null).Should().Be(8);

            var x = new Test { Test2 = new Test2 { Test3 = new Test3 { } } };

            sut.SizeOf(x).Should().Be(72);
        }

        [Fact]
        public void Object()
        {
            sut.SizeOf(new object()).Should().Be(24);
        }

        [Fact]
        public void Loops()
        {
            var x = new TestLoop();
            var y = new TestLoop { x = x };
            x.y = y;

            sut.SizeOf(x).Should().Be(64);
        }

        [Fact]
        public void Array()
        {
            var arr = new int[4];

            sut.SizeOf(arr).Should().Be(40);

            sut.SizeOf(new[] { "", null, null }).Should().Be(46);
        }

        [Fact]
        public void ArrayArray()
        {
            sut.SizeOf(new[] { new int[4], new int[4] }).Should().Be(96);

            sut.SizeOf(new[] { new int[4], new int[4], null }).Should().Be(104);
        }

        [Fact]
        public void Dictionary()
        {
            var x = new Dictionary<int, int>();
            for (int i = 0; i < 100; ++i)
            {
                x.Add(i, i);
            }

            sut.SizeOf(x).Should().Be(4068);
        }

        [Fact]
        public void Strings()
        {
            sut.SizeOf("").Should().Be(22);
            sut.SizeOf("abc").Should().Be(28);
            sut.SizeOf(new string(' ', 100)).Should().Be(222);
        }

        [Fact]
        public void FinalizerShouldNotBeCalledExtraTimes()
        {
            sut.SizeOf(new TestFinalizer());

            GC.Collect();
            GC.WaitForPendingFinalizers();

            TestFinalizer.FinalizerWasCalled.Should().BeLessOrEqualTo(1);
        }

        private unsafe struct AP
        {
            public char* t;
        }

        [Fact]
        public void Pointers()
        {
            sut.SizeOf(new IntPtr()).Should().Be(IntPtr.Size);

            sut.SizeOf(new { a = new IntPtr() }).Should().Be(IntPtr.Size * 3);

            sut.SizeOf(new AP()).Should().Be(8);
        }

        [Fact]
        public void Tasks()
        {
            sut.SizeOf(Task.CompletedTask).Should().Be(64);

            sut.SizeOf(Task.Delay(1)).Should().Be(105);

            sut.SizeOf(Task.FromResult(4)).Should().Be(76);

            sut.SizeOf(Task.FromResult(4L)).Should().Be(80);
        }

        [Fact]
        public void ValueTasks()
        {
            sut.SizeOf(new ValueTask()).Should().Be(16);

            sut.SizeOf(new ValueTask<int>(4)).Should().Be(20);

            sut.SizeOf(new ValueTask(Task.Delay(1))).Should().Be(121);
        }

        private sealed class SealedC { }

        [Fact]
        public void Graph()
        {
            var o = new SealedC();
            sut.SizeOf(new { a = o, b = o, c = o }).Should().Be(64);
        }

        [Fact]
        public void Tree()
        {
            var sut2 = new Memory(false);
            var o = new SealedC();
            sut2.SizeOf(new { a = o, b = o, c = o }).Should().Be(112);
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
