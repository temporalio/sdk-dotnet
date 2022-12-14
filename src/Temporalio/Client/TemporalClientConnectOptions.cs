namespace Temporalio.Client
{
    public class TemporalClientConnectOptions : TemporalConnectionOptions
    {
        public TemporalClientConnectOptions()
        {
            // TODO(cretz): DataConverter = Converters.DataConverter.Default;
        }

        public TemporalClientConnectOptions(string targetHost) : base(targetHost)
        {
            // TODO(cretz): DataConverter = Converters.DataConverter.Default;
        }

        public TemporalClientConnectOptions(TemporalClientConnectOptions other) : base(other)
        {
            Namespace = other.Namespace;
            // TODO(cretz): DataConverter = other.DataConverter;
        }

        public string Namespace { get; set; } = "default";

        // TODO(cretz): public Converters.DataConverter DataConverter { get; set; }

        public TemporalClientOptions AsClientOptions()
        {
            return new TemporalClientOptions()
            {
                Namespace = Namespace,
                // TODO(cretz): DataConverter = DataConverter
            };
        }
    }
}
