using Xunit;

namespace Temporal.TestUtil
{
    internal class SerializableClass
    {
        public static SerializableClass CreateDefault()
        {
            return new SerializableClass { Name = "Test", Value = 1 };
        }

        public static void AssertEqual(SerializableClass expected, SerializableClass actual)
        {
            if (expected == null)
            {
                Assert.Null(actual);
                return;
            }

            Assert.NotNull(actual);
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Value, actual.Value);
        }

        public string Name { get; set; }

        public int Value { get; set; }
    }
}