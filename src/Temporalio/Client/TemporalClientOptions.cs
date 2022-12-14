namespace Temporalio.Client
{
    public class TemporalClientOptions
    {
        public string Namespace { get; set; } = "default";

        // TODO(cretz): public Converters.DataConverter DataConverter { get; set; }

        public TemporalClientOptions()
        {
            // TODO(cretz): DataConverter = Converters.DataConverter.Default;
        }
    }
}
