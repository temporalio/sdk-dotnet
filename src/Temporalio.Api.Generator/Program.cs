#pragma warning disable CA1852 // Don't care that Program.cs can be sealed

using System.Diagnostics;

var currFile = new StackTrace(true).GetFrame(0)?.GetFileName();
var projectDir = Path.GetFullPath(Path.Join(currFile, "../../../"));
var protoDir = Path.Join(projectDir, "src/Temporalio/Bridge/sdk-core/sdk-core-protos/protos");
var apiProtoDir = Path.Join(protoDir, "api_upstream");
var testSrvProtoDir = Path.Join(protoDir, "testsrv_upstream");
var bridgeProtoDir = Path.Join(protoDir, "local");

// Remove/recreate entire api dir
new DirectoryInfo(Path.Join(projectDir, "src/Temporalio/Api")).Delete(true);
new DirectoryInfo(Path.Join(projectDir, "src/Temporalio/Api/Dependencies/Google")).Create();
// Do not delete the .editorconfig from Bridge/Api
foreach (var fi in new DirectoryInfo(Path.Join(projectDir, "src/Temporalio/Bridge/Api")).GetFileSystemInfos())
{
    if (fi.Name != ".editorconfig")
    {
        if (fi is DirectoryInfo dir)
        {
            dir.Delete(true);
        }
        else
        {
            fi.Delete();
        }
    }
}

// Gen proto
foreach (var fi in new DirectoryInfo(apiProtoDir).GetFiles("*.proto", SearchOption.AllDirectories))
{
    if (fi.FullName.Contains("google/api"))
    {
        Protoc(
            fi.FullName,
            Path.Join(projectDir, "src/Temporalio/Api/Dependencies/Google"),
            string.Empty,
            apiProtoDir);
    }
    else if (!fi.FullName.Contains("google/protobuf"))
    {
        Protoc(fi.FullName, Path.Join(projectDir, "src/Temporalio"), "Temporalio", apiProtoDir);
    }
}
foreach (
    var fi in new DirectoryInfo(testSrvProtoDir).GetFiles("*.proto", SearchOption.AllDirectories))
{
    // Ignore gogo
    if (!fi.FullName.Contains("gogo"))
    {
        Protoc(
            fi.FullName,
            Path.Join(projectDir, "src/Temporalio"),
            "Temporalio",
            apiProtoDir,
            testSrvProtoDir);
    }
}
Protoc(
    Path.Join(projectDir, "src/Temporalio.Api.Generator/grpc_status.proto"),
    Path.Join(projectDir, "src/Temporalio"),
    "Temporalio",
    Path.Join(projectDir, "src/Temporalio.Api.Generator"));
foreach (
    var fi in new DirectoryInfo(bridgeProtoDir).GetFiles("*.proto", SearchOption.AllDirectories))
{
    Protoc(
        fi.FullName,
        Path.Join(projectDir, "src/Temporalio/Bridge/Api"),
        "Coresdk",
        apiProtoDir,
        bridgeProtoDir);
}
Protoc(
    Path.Join(projectDir, "src/Temporalio.Api.Generator/grpc_health.proto"),
    Path.Join(projectDir, "src/Temporalio/Bridge/Api"),
    "Coresdk",
    Path.Join(projectDir, "src/Temporalio.Api.Generator"));

// Gen RPC services
File.WriteAllText(
    Path.Join(projectDir, "src/Temporalio/Client/WorkflowService.Rpc.cs"),
    GenerateServiceRPCSource(
        Path.Join(apiProtoDir, "temporal/api/workflowservice/v1/service.proto"),
        "Temporalio.Client",
        "WorkflowService",
        "Temporalio.Api.WorkflowService.V1"));
File.WriteAllText(
    Path.Join(projectDir, "src/Temporalio/Client/OperatorService.Rpc.cs"),
    GenerateServiceRPCSource(
        Path.Join(apiProtoDir, "temporal/api/operatorservice/v1/service.proto"),
        "Temporalio.Client",
        "OperatorService",
        "Temporalio.Api.OperatorService.V1"));
File.WriteAllText(
    Path.Join(projectDir, "src/Temporalio/Client/TestService.Rpc.cs"),
    GenerateServiceRPCSource(
        Path.Join(testSrvProtoDir, "temporal/api/testservice/v1/service.proto"),
        "Temporalio.Client",
        "TestService",
        "Temporalio.Api.TestService.V1",
        new()
        {
            ["UnlockTimeSkippingWithSleep"] = "SleepRequest",
            ["GetCurrentTime"] = "Google.Protobuf.WellKnownTypes.Empty",
        },
        new()
        {
            ["SleepUntil"] = "SleepResponse",
            ["UnlockTimeSkippingWithSleep"] = "SleepResponse",
        }));

// Change Google namespace to Temporalio.Api.Dependencies.Google
foreach (
    var fi in new DirectoryInfo(Path.Join(projectDir, "src/Temporalio/Api")).GetFiles(
        "*.cs",
        SearchOption.AllDirectories))
{
    if (fi.Extension != ".cs")
    {
        continue;
    }
    string contents = File.ReadAllText(fi.FullName);
    if (fi.FullName.Contains("Google"))
    {
        // change the namespace field
        var newContents = contents.Replace(
            "namespace Google.Api ",
            "namespace Temporalio.Api.Dependencies.Google.Api ");
        newContents = newContents.Replace("Google.Api.", "Temporalio.Api.Dependencies.Google.Api.");
        File.WriteAllText(fi.FullName, newContents);
    }
    else
    {
        var newContents = contents.Replace("Google.Api.", "Temporalio.Api.Dependencies.Google.Api.");
        if (contents != newContents)
        {
            File.WriteAllText(fi.FullName, newContents);
        }
    }
}

// Change "Coresdk" namespace to "Temporalio.Bridge.Api"
foreach (
    var fi in new DirectoryInfo(Path.Join(projectDir, "src/Temporalio/Bridge/Api")).GetFiles(
        "*.cs",
        SearchOption.AllDirectories))
{
    File.WriteAllText(
        fi.FullName,
        File.ReadAllText(fi.FullName).Replace("Coresdk", "Temporalio.Bridge.Api"));
}

static void Protoc(string file, string outDir, string baseNamespace, params string[] includes)
{
    var protocArgs = new List<string> { "--csharp_out=" + outDir };
    if (!string.IsNullOrEmpty(baseNamespace))
    {
        var opt = "--csharp_opt=base_namespace=" + baseNamespace;
        // Mark core sdk as internal
        if (baseNamespace == "Coresdk")
        {
            opt += ",internal_access";
        }
        protocArgs.Add(opt);
    }
    foreach (var include in includes)
    {
        protocArgs.Add("-I=" + include);
    }
    protocArgs.Add(file);
    Console.WriteLine("Running protoc {0}", string.Join(" ", protocArgs));
    var process = Process.Start("protoc", protocArgs);
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException("protoc failed");
    }
}

static string GenerateServiceRPCSource(
    string protoFile,
    string serviceNamespace,
    string serviceClass,
    string protoNamespace,
    Dictionary<string, string>? requestOverrides = null,
    Dictionary<string, string>? responseOverrides = null)
{
    var code = "// <auto-generated>\n//     Generated. DO NOT EDIT!\n// </auto-generated>\n";
    code += "#pragma warning disable 8669\n";
    code += "using System.Threading.Tasks;\n";
    code += $"using {protoNamespace};\n\n";
    code += $"namespace {serviceNamespace}\n{{\n";
    code += $"    public abstract partial class {serviceClass}\n    {{";
    foreach (var call in ServiceCalls(protoFile))
    {
        var req = string.Empty;
        if (requestOverrides?.TryGetValue(call, out req) != true)
        {
            req = call + "Request";
        }
        var resp = string.Empty;
        if (responseOverrides?.TryGetValue(call, out resp) != true)
        {
            resp = call + "Response";
        }
        // TODO(cretz): Copy docs from gRPC service?
        code += "\n        /// <summary>";
        code += $"\n        /// Invoke {call}.";
        code += "\n        /// </summary>";
        code += "\n        /// <param name=\"req\">Request for the call.</param>";
        code += "\n        /// <param name=\"options\">Optional RPC options.</param>";
        code += "\n        /// <returns>RPC response</returns>";
        code +=
            $"\n        public async Task<{resp}> {call}Async({req} req, RpcOptions? options = null)\n";
        code += "        {\n";
        code +=
            $"            return await InvokeRpcAsync(\"{call}\", req, {resp}.Parser, options);\n";
        code += "        }\n";
    }
    code += "    }\n}";
    return code;
}

static IEnumerable<string> ServiceCalls(string protoFile)
{
    return File.ReadAllLines(protoFile)
        .Select(v => v.TrimStart())
        .Where(v => v.StartsWith("rpc"))
        .Select(v =>
        {
            v = v[(v.IndexOf("rpc") + 3)..].TrimStart();
#pragma warning disable CA1861 // We don't mind the perf cost here of new array each time
            return v[..v.IndexOfAny(new[] { ' ', '(' })];
#pragma warning restore CA1861
        })
        .OrderBy(v => v);
}
