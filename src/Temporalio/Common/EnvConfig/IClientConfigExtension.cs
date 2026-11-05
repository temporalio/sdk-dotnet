using System.Collections.Generic;
using Temporalio.Client;

namespace Temporalio.Common.EnvConfig
{
    /// <summary>
    /// Extension that adds its own config to client environment configuration and applies it to
    /// the client through a plugin. An extension's TOML config lives in the
    /// <c>profile.{profile}.ext.{name}</c> table, and any environment variables it reads are
    /// recommended to use a <c>TEMPORAL_EXT_{NAME}_</c> prefix.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Extensions are passed via <see cref="ClientEnvConfig.ProfileLoadOptions.Extensions" /> to
    /// <see cref="ClientEnvConfig.LoadClientConnectOptions" />.
    /// </para>
    /// <para>WARNING: This API is experimental and may change in the future.</para>
    /// </remarks>
    public interface IClientConfigExtension
    {
        /// <summary>
        /// Gets the extension's table key under <c>profile.{profile}.ext</c>. Must be unique among
        /// loaded extensions.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Read this extension's config from the profile and env, and create a plugin that applies
        /// it to the client.
        /// </summary>
        /// <param name="context">The loaded profile and env.</param>
        /// <returns>The plugin, or null if there's nothing to apply.</returns>
        ITemporalClientPlugin? CreateClientPlugin(ClientConfigExtensionContext context);
    }

    /// <summary>
    /// Input to <see cref="IClientConfigExtension.CreateClientPlugin" />.
    /// </summary>
    /// <param name="Profile">The loaded profile, with core environment variables already
    /// applied. The extension's config is in <see cref="ClientEnvConfig.Profile.Ext" />.</param>
    /// <param name="Env">Environment variables to read extension environment variables from, or
    /// null if environment loading is disabled. This is
    /// <see cref="ClientEnvConfig.ProfileLoadOptions.OverrideEnvVars" /> if set, otherwise the
    /// process environment.</param>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    public sealed record ClientConfigExtensionContext(
        ClientEnvConfig.Profile Profile,
        IReadOnlyDictionary<string, string>? Env);
}
