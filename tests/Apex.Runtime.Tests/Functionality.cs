using FluentAssertions;
using System;
using System.Collections.Generic;
using Xunit;

namespace Apex.Runtime.Tests
{
    public class Functionality
    {
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

        [Fact]
        public void Test1()
        {
            var sut = new Memory(true);

            var x = new Test { Test2 = new Test2 { Test3 = new Test3 { } } };

            sut.SizeOf(x).Should().Be(72);
        }

        [Fact]
        public void Object()
        {
            var sut = new Memory(true);

            sut.SizeOf(new object()).Should().Be(24);
        }

        [Fact]
        public void Loops()
        {
            var sut = new Memory(true);

            var x = new TestLoop();
            var y = new TestLoop { x = x };
            x.y = y;

            sut.SizeOf(x).Should().Be(64);
        }

        [Fact]
        public void Array()
        {
            var sut = new Memory(true);

            var arr = new int[4];

            sut.SizeOf(arr).Should().Be(40);
        }

        [Fact]
        public void Dictionary()
        {
            var sut = new Memory(true);

            var x = new Dictionary<int, int>();
            for (int i = 0; i < 100; ++i)
            {
                x.Add(i, i);
            }

            sut.SizeOf(x).Should().Be(4060);
        }
    }
}
