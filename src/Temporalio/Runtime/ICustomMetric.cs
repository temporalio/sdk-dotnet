namespace Temporalio.Runtime
{
    public interface ICustomMetric<T> where T : struct
    {
        public interface ICounter : ICustomMetric<T>
        {
            void Add(T value, object tags);
        }

        public interface IHistogram : ICustomMetric<T>
        {
            void Record(T value, object tags);
        }

        public interface IGauge : ICustomMetric<T>
        {
#pragma warning disable CA1716 // We are ok with using the "Set" name even though "set" is in lang
            void Set(T value, object tags);
#pragma warning restore CA1716
        }
    }
}