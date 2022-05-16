using System;
using Temporal.Common.Payloads;
using Xunit;

namespace Temporal.Sdk.Common.Tests
{
    public abstract class AbstractUnnamedTest
    {
        private readonly PayloadContainers.IUnnamed _instance;
        private readonly int _length;

        protected AbstractUnnamedTest(PayloadContainers.IUnnamed instance, int length)
        {
            _instance = instance;
            _length = length;
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_IUnnamed_GetValue_Negative_Index()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _instance.GetValue<object>(-1));
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_IUnnamed_GetValue_Index_Out_Of_Bounds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _instance.GetValue<object>(_instance.Count + 1));
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_IUnnamed_TryGetValue_Negative_Index()
        {
            Assert.False(_instance.TryGetValue<object>(-1, out _));
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_IUnnamed_TryGetValue_Index_Out_Of_Bounds()
        {
            Assert.False(_instance.TryGetValue<object>(_instance.Count + 1, out _));
        }

        [Fact]
        [Trait("Category", "Common")]
        public void Test_IUnnamed_Length_Is_Expected_Value()
        {
            Assert.Equal(_length, _instance.Count);
        }
    }
}