namespace Temporalio.Tests;

/// <summary>
/// Reasons tests are excluded from Temporal Cloud test execution.
/// </summary>
public enum CloudTestExclusionReason
{
    /// <summary>
    /// The test requires a Temporal Cloud capability that the test harness cannot provision or a
    /// feature that is not yet available in Temporal Cloud.
    /// </summary>
    RequiresCloudProvisioning,

    /// <summary>
    /// The test needs setup or assertions adapted for the Temporal Cloud environment.
    /// </summary>
    NeedsCloudAdaptation,

    /// <summary>
    /// The test inherently requires one or more local Temporal Server instances. For example, it
    /// may start a dev or time-skipping server or customize server configuration.
    /// </summary>
    RequiresLocalServer,
}
