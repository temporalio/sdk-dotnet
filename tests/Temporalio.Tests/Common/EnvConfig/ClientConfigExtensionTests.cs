using System.Text.Json;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Common.EnvConfig;
using Xunit;
using Xunit.Abstractions;

namespace Temporalio.Tests.Common.EnvConfig
{
    public class ClientConfigExtensionTests : TestBase
    {
        private const string TomlConfigExt = @"
[profile.default]
address = ""default-address""
[profile.default.ext.first]
value = ""first-value""
nested = { list = [1, 2], flag = true }
datetime = 1979-05-27T07:32:00Z
[profile.default.ext.second]
[profile.default.ext.second.deeper]
value = 5

[profile.other]
address = ""other-address""
";

        public ClientConfigExtensionTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Load_ProfileWithExt_ExposesRawTables(bool strict)
        {
            var profile = ClientEnvConfig.Profile.Load(new()
            {
                ConfigSource = DataSource.FromUTF8String(TomlConfigExt),
                ConfigFileStrict = strict,
                OverrideEnvVars = new Dictionary<string, string>(),
            });
            Assert.Equal("default-address", profile.Address);
            Assert.NotNull(profile.Ext);
            Assert.Equal(new[] { "first", "second" }, profile.Ext.Keys.OrderBy(k => k));
            var first = profile.Ext["first"];
            Assert.Equal("first-value", first.GetProperty("value").GetString());
            Assert.Equal(
                new[] { 1, 2 },
                first.GetProperty("nested").GetProperty("list").EnumerateArray().Select(v => v.GetInt32()));
            Assert.True(first.GetProperty("nested").GetProperty("flag").GetBoolean());
            Assert.Equal("1979-05-27T07:32:00Z", first.GetProperty("datetime").GetString());
            Assert.Equal(5, profile.Ext["second"].GetProperty("deeper").GetProperty("value").GetInt32());

            var config = ClientEnvConfig.Load(new()
            {
                ConfigSource = DataSource.FromUTF8String(TomlConfigExt),
                ConfigFileStrict = strict,
                OverrideEnvVars = new Dictionary<string, string>(),
            });
            Assert.Equal(2, config.Profiles["default"].Ext?.Count);
            Assert.Null(config.Profiles["other"].Ext);
        }

        [Fact]
        public void Load_StrictModeWithExt_StillRejectsUnknownKeys()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                ClientEnvConfig.Profile.Load(new()
                {
                    ConfigSource = DataSource.FromUTF8String(@"
[profile.default]
unrecognized_field = ""should-fail""
[profile.default.ext.first]
value = ""first-value""
"),
                    ConfigFileStrict = true,
                    OverrideEnvVars = new Dictionary<string, string>(),
                }));
            Assert.Contains("unrecognized_field", ex.Message);
        }

        [Fact]
        public void LoadClientConnectOptions_Extensions_CalledInOrderAndPluginsSet()
        {
            var env = new Dictionary<string, string>
            {
                ["TEMPORAL_ADDRESS"] = "env-address",
                ["TEMPORAL_EXT_FIRST_VALUE"] = "env-value",
            };
            var calls = new List<string>();
            var first = new TestExtension("first", req =>
            {
                calls.Add("first");
                // Env vars have already been applied to the profile
                Assert.Equal("env-address", req.Profile.Address);
                Assert.Equal("file-value", req.Profile.Ext!["first"].GetProperty("value").GetString());
                Assert.Same(env, req.Env);
                return new SimplePlugin($"first-{req.Env!["TEMPORAL_EXT_FIRST_VALUE"]}");
            });
            var noop = new TestExtension("noop", req =>
            {
                calls.Add("noop");
                Assert.False(req.Profile.Ext!.ContainsKey("noop"));
                return null;
            });
            var second = new TestExtension("second", _ =>
            {
                calls.Add("second");
                return new SimplePlugin("second");
            });

            var options = ClientEnvConfig.LoadClientConnectOptions(new()
            {
                ConfigSource = DataSource.FromUTF8String(@"
[profile.default]
address = ""file-address""
[profile.default.ext.first]
value = ""file-value""
"),
                ConfigFileStrict = true,
                OverrideEnvVars = env,
                Extensions = new IClientConfigExtension[] { second, first, noop },
            });
            Assert.Equal(new[] { "second", "first", "noop" }, calls);
            Assert.Equal(new[] { "second", "first-env-value" }, options.Plugins?.Select(p => p.Name));
            Assert.Equal("env-address", options.TargetHost);
        }

        [Fact]
        public void LoadClientConnectOptions_NoPlugins_LeavesPluginsUnset()
        {
            var extensionSets = new IReadOnlyCollection<IClientConfigExtension>?[]
            {
                null,
                Array.Empty<IClientConfigExtension>(),
                new[] { new TestExtension("noop", _ => null) },
            };
            foreach (var extensions in extensionSets)
            {
                var options = ClientEnvConfig.LoadClientConnectOptions(new()
                {
                    ConfigSource = DataSource.FromUTF8String(TomlConfigExt),
                    OverrideEnvVars = new Dictionary<string, string>(),
                    Extensions = extensions,
                });
                Assert.Null(options.Plugins);
            }
        }

        [Fact]
        public void LoadClientConnectOptions_Extensions_ReceiveEnv()
        {
            var gotEnvs = new List<IReadOnlyDictionary<string, string>?>();
            var extensions = new[]
            {
                new TestExtension("ext", req =>
                {
                    gotEnvs.Add(req.Env);
                    return null;
                }),
            };
            var env = new Dictionary<string, string> { ["TEMPORAL_EXT_EXT_VALUE"] = "value" };
            var processVar = $"TEMPORAL_EXT_EXT_TEST_{Guid.NewGuid():N}";
            Environment.SetEnvironmentVariable(processVar, "process-value");
            try
            {
                // Defaults to process env
                ClientEnvConfig.LoadClientConnectOptions(new()
                {
                    ConfigSource = DataSource.FromUTF8String("[profile.default]"),
                    Extensions = extensions,
                });
                // Uses override env when given
                ClientEnvConfig.LoadClientConnectOptions(new()
                {
                    ConfigSource = DataSource.FromUTF8String("[profile.default]"),
                    OverrideEnvVars = env,
                    Extensions = extensions,
                });
                // Null when env is disabled
                ClientEnvConfig.LoadClientConnectOptions(new()
                {
                    ConfigSource = DataSource.FromUTF8String("[profile.default]"),
                    DisableEnv = true,
                    OverrideEnvVars = env,
                    Extensions = extensions,
                });
            }
            finally
            {
                Environment.SetEnvironmentVariable(processVar, null);
            }

            Assert.Equal(3, gotEnvs.Count);
            Assert.Equal("process-value", gotEnvs[0]![processVar]);
            Assert.Same(env, gotEnvs[1]);
            Assert.Null(gotEnvs[2]);
        }

        [Fact]
        public void LoadClientConnectOptions_InvalidExtensions_ThrowBeforeCallingAny()
        {
            TestExtension NewExt(string name) => new(name, _ => throw new InvalidOperationException("should not be called"));
            var cases = new (IClientConfigExtension[] Extensions, string Message)[]
            {
                (new[] { NewExt("a"), NewExt("b"), NewExt("a") }, "Duplicate client config extension name \"a\""),
                (new[] { NewExt("a"), NewExt(string.Empty) }, "Client config extension at index 1 has empty name"),
                (new IClientConfigExtension[] { null! }, "Client config extension at index 0 is null"),
            };
            foreach (var (extensions, message) in cases)
            {
                var ex = Assert.Throws<ArgumentException>(() =>
                    ClientEnvConfig.LoadClientConnectOptions(new()
                    {
                        ConfigSource = DataSource.FromUTF8String("[profile.default]"),
                        OverrideEnvVars = new Dictionary<string, string>(),
                        Extensions = extensions,
                    }));
                Assert.Equal(message, ex.Message);
            }
        }

        [Fact]
        public void LoadClientConnectOptions_ExtensionThrows_WrapsWithExtensionName()
        {
            var extErr = new FormatException("bad config");
            var ex = Assert.Throws<InvalidOperationException>(() =>
                ClientEnvConfig.LoadClientConnectOptions(new()
                {
                    ConfigSource = DataSource.FromUTF8String("[profile.default]"),
                    OverrideEnvVars = new Dictionary<string, string>(),
                    Extensions = new[] { new TestExtension("bad", _ => throw extErr) },
                }));
            Assert.Equal("Client config extension \"bad\": bad config", ex.Message);
            Assert.Same(extErr, ex.InnerException);
        }

        [Fact]
        public async Task ExampleExtension_ApiKeyFile_AppliesApiKey()
        {
            var keyPath = Path.GetTempFileName();
            await File.WriteAllTextAsync(keyPath, "my-api-key\n");
            try
            {
                var options = ClientEnvConfig.LoadClientConnectOptions(new()
                {
                    ConfigSource = DataSource.FromUTF8String($@"
[profile.default]
address = ""my-address""
[profile.default.ext.api_key_file]
path = {JsonSerializer.Serialize(keyPath)}
"),
                    ConfigFileStrict = true,
                    OverrideEnvVars = new Dictionary<string, string>(),
                    Extensions = new[] { new ApiKeyFileExtension() },
                });
                var plugin = Assert.Single(options.Plugins!);
                TemporalClientConnectOptions? connected = null;
                await plugin.ConnectAsync(options, opts =>
                {
                    connected = opts;
                    return Task.FromResult<TemporalConnection>(null!);
                });
                Assert.Equal("my-address", connected?.TargetHost);
                Assert.Equal("my-api-key", connected?.ApiKey);
                Assert.NotNull(connected?.Tls);

                // Nothing configured
                var unconfigured = ClientEnvConfig.LoadClientConnectOptions(new()
                {
                    ConfigSource = DataSource.FromUTF8String("[profile.default]"),
                    OverrideEnvVars = new Dictionary<string, string>(),
                    Extensions = new[] { new ApiKeyFileExtension() },
                });
                Assert.Null(unconfigured.Plugins);

                // Invalid config
                var ex = Assert.Throws<InvalidOperationException>(() =>
                    ClientEnvConfig.LoadClientConnectOptions(new()
                    {
                        ConfigSource = DataSource.FromUTF8String("[profile.default.ext.api_key_file]\npath = 5"),
                        OverrideEnvVars = new Dictionary<string, string>(),
                        Extensions = new[] { new ApiKeyFileExtension() },
                    }));
                Assert.Equal(
                    "Client config extension \"api_key_file\": path must be a string, got Number",
                    ex.Message);
            }
            finally
            {
                File.Delete(keyPath);
            }
        }

        private sealed class TestExtension : IClientConfigExtension
        {
            private readonly Func<ClientConfigExtensionRequest, ITemporalClientPlugin?> createClientPlugin;

            public TestExtension(
                string name, Func<ClientConfigExtensionRequest, ITemporalClientPlugin?> createClientPlugin)
            {
                Name = name;
                this.createClientPlugin = createClientPlugin;
            }

            public string Name { get; }

            public ITemporalClientPlugin? CreateClientPlugin(ClientConfigExtensionRequest request) =>
                createClientPlugin(request);
        }

        /// <summary>
        /// Example extension that reads an API key from a file, configured like:
        /// <code>
        /// [profile.default.ext.api_key_file]
        /// path = "/path/to/api-key"
        /// </code>
        /// or with the <c>TEMPORAL_EXT_API_KEY_FILE_PATH</c> env var, which takes precedence.
        /// </summary>
        private sealed class ApiKeyFileExtension : IClientConfigExtension
        {
            public string Name => "api_key_file";

            public ITemporalClientPlugin? CreateClientPlugin(ClientConfigExtensionRequest request)
            {
                string? keyPath = null;
                if (request.Profile.Ext?.TryGetValue(Name, out var config) == true &&
                    config.TryGetProperty("path", out var raw))
                {
                    if (raw.ValueKind != JsonValueKind.String)
                    {
                        throw new FormatException($"path must be a string, got {raw.ValueKind}");
                    }
                    keyPath = raw.GetString();
                }
                if (request.Env?.TryGetValue("TEMPORAL_EXT_API_KEY_FILE_PATH", out var envPath) == true)
                {
                    keyPath = envPath;
                }
                if (keyPath == null)
                {
                    return null;
                }
                return new Plugin(File.ReadAllText(keyPath).Trim());
            }

            private sealed class Plugin : ITemporalClientPlugin
            {
                private readonly string apiKey;

                public Plugin(string apiKey) => this.apiKey = apiKey;

                public string Name => "example.ApiKeyFileExtension";

                public void ConfigureClient(TemporalClientOptions options)
                {
                }

                public Task<TemporalConnection> ConnectAsync(
                    TemporalClientConnectOptions options,
                    Func<TemporalClientConnectOptions, Task<TemporalConnection>> continuation)
                {
                    options = (TemporalClientConnectOptions)options.Clone();
                    options.ApiKey = apiKey;
                    options.Tls ??= new TlsOptions();
                    return continuation(options);
                }
            }
        }
    }
}
