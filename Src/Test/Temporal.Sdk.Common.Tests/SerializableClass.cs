namespace Temporal.Common.Payloads
{
    internal class SerializableClass
    {
        public static SerializableClass Default
        {
            get
            {
                return new SerializableClass { Name = "Test", Value = 1 };
            }
        }

        public string Name { get; set; }

        public int Value { get; set; }
    }
}