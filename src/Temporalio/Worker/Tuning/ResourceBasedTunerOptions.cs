namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Options for target resource usage.
    /// </summary>
    /// <param name="TargetMemoryUsage">A value between 0 and 1 that represents the target
    /// (system) memory usage. It's not recommended to set this higher than 0.8, since how much
    /// memory a workflow may use is not predictable, and you don't want to encounter OOM
    /// errors.</param>
    /// <param name="TargetCpuUsage">A value between 0 and 1 that represents the target (system)
    /// CPU usage. This can be set to 1.0 if desired, but it's recommended to leave some
    /// headroom for other processes.</param>
    public sealed record ResourceBasedTunerOptions(
        double TargetMemoryUsage,
        double TargetCpuUsage);
}