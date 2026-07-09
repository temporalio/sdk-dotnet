using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Google.Protobuf.Reflection;

var currFile = new StackTrace(true).GetFrame(0)?.GetFileName();
var projectDir = Path.GetFullPath(Path.Join(currFile, "../../../"));
var generatorDir = Path.Join(projectDir, "src/Temporalio.SystemNexus.Generator");
var protoDir = Path.Join(projectDir, "src/Temporalio/Bridge/sdk-core/crates/protos/protos");
var apiProtoDir = Path.Join(protoDir, "api_upstream");
var nexusWitDir = Path.Join(apiProtoDir, "nexus");
var descriptorPath = Path.Join(generatorDir, "obj/SystemNexus/temporal_api.bin");
var stagingOutputDir = Path.Join(generatorDir, "obj/SystemNexus/Generated");
var workflowsGeneratedDir = Path.Join(projectDir, "src/Temporalio/Workflows/Generated");
var workerGeneratedDir = Path.Join(projectDir, "src/Temporalio/Worker/Generated");
var obsoleteOutputDir = Path.Join(projectDir, "src/Temporalio/SystemNexus/Generated");
var operationsPath = Path.Join(workflowsGeneratedDir, "Operations.cs");

EnsureNexGen();
BuildDescriptor();
GenerateNexusApi();
PostProcessGeneratedNexusApi();
GeneratePayloadVisitor(operationsPath, descriptorPath, workerGeneratedDir);
return 0;

void EnsureNexGen()
{
    var nexGenCommand = NexGenCommand();
    var helpArgs = new[] { "help" };
    if (RunProcess(nexGenCommand, helpArgs, ignoreExitCode: true) == 0)
    {
        return;
    }

    throw new InvalidOperationException($"Unable to run nex-gen command {nexGenCommand}");
}

void BuildDescriptor()
{
    Directory.CreateDirectory(Path.GetDirectoryName(descriptorPath)!);
    RunProcess(
        "protoc",
        new[]
        {
            "-I=" + apiProtoDir,
            "-I=" + protoDir,
            "--include_imports",
            "--descriptor_set_out=" + descriptorPath,
            Path.Join(apiProtoDir, "temporal/api/workflowservice/v1/request_response.proto"),
        });
}

void GenerateNexusApi()
{
    if (Directory.Exists(stagingOutputDir))
    {
        new DirectoryInfo(stagingOutputDir).Delete(true);
    }

    Directory.CreateDirectory(stagingOutputDir);
    RunProcess(
        NexGenCommand(),
        new[]
        {
            "generate",
            "--lang",
            "dotnet",
            "--input",
            Path.Join(nexusWitDir, "workflow-service.wit"),
            "--input",
            Path.Join(nexusWitDir, "deps"),
            "--support-file",
            Path.Join(generatorDir, "wit/deps/nexus-temporal-types/dotnet/TemporalSupport.cs"),
            "--descriptors",
            descriptorPath,
            "--output",
            stagingOutputDir,
        });
}

void PostProcessGeneratedNexusApi()
{
    RecreateDirectory(workflowsGeneratedDir);
    RecreateDirectory(workerGeneratedDir);

    if (Directory.Exists(obsoleteOutputDir))
    {
        new DirectoryInfo(obsoleteOutputDir).Delete(true);
    }

    WriteWorkflowGeneratedFile("Models.cs");
    WriteWorkflowGeneratedFile("Operations.cs");
    WriteWorkflowGeneratedFile("Service.cs");
    WriteWorkflowGeneratedFile(Path.Join("Support", "TemporalSupport.cs"), "TemporalSupport.cs");
}

void WriteWorkflowGeneratedFile(string stagingRelativePath, string? outputFileName = null)
{
    var sourcePath = Path.Join(stagingOutputDir, stagingRelativePath);
    var destinationPath = Path.Join(
        workflowsGeneratedDir,
        outputFileName ?? Path.GetFileName(stagingRelativePath));
    var contents = File.ReadAllText(sourcePath);
    contents = Regex.Replace(
        contents,
        @"^using (NexGen\.Support|Temporalio\.Workflows);\r?\n",
        string.Empty,
        RegexOptions.Multiline);
    contents = contents.Replace("namespace NexGen.Support", "namespace Temporalio.Workflows");
    contents = contents.Replace("NexGen.Support.", string.Empty);
    File.WriteAllText(destinationPath, contents);
}

static void RecreateDirectory(string path)
{
    if (Directory.Exists(path))
    {
        new DirectoryInfo(path).Delete(true);
    }

    Directory.CreateDirectory(path);
}

static void GeneratePayloadVisitor(
    string operationsPath,
    string descriptorPath,
    string outputDir)
{
    var generatedDir = Path.GetDirectoryName(operationsPath) ??
        throw new InvalidOperationException($"No directory for {operationsPath}");
    var servicePath = Path.Combine(generatedDir, "Service.cs");
    var source = File.ReadAllText(servicePath);
    var operations = ParseOperations(source);
    if (operations.Count == 0)
    {
        throw new InvalidOperationException($"No generated operations found in {servicePath}");
    }

    var messages = LoadMessages(descriptorPath);
    var messagesByCsharpType = messages.Values.ToDictionary(
        message => NormalizeTypeName(message.CsharpType),
        message => message);
    var operationMessages = operations.Select(operation =>
        new OperationMessages(
            GetMessage(messagesByCsharpType, operation.InputType),
            GetMessage(messagesByCsharpType, operation.OutputType))).ToList();
    var containsPayloadMemo = new Dictionary<string, bool>();
    var emittedVisitors = new HashSet<string>();
    var emittedMethods = new HashSet<string>();
    var builder = new StringBuilder();

    builder.AppendLine("// <auto-generated />");
    builder.AppendLine("// Generated by Temporalio.SystemNexus.Generator. DO NOT EDIT!");
    builder.AppendLine("#nullable enable");
    builder.AppendLine();
    builder.AppendLine("using System;");
    builder.AppendLine("using System.CodeDom.Compiler;");
    builder.AppendLine("using System.Collections.Generic;");
    builder.AppendLine("using System.Threading.Tasks;");
    builder.AppendLine("using Temporalio.Api.Common.V1;");
    builder.AppendLine();
    builder.AppendLine("namespace Temporalio.Worker");
    builder.AppendLine("{");
    builder.AppendLine("    [GeneratedCode(\"Temporalio.SystemNexus.Generator\", null)]");
    builder.AppendLine("    internal static partial class SystemNexusPayloadVisitor");
    builder.AppendLine("    {");
    builder.AppendLine("        private const string TemporalSystemEndpoint = \"__temporal_system\";");
    builder.AppendLine();
    builder.AppendLine("        private static readonly IReadOnlyDictionary<string, Func<Payload, PayloadVisitor, PayloadsVisitor, EnvelopeVisitor?, Task>> EnvelopeVisitors =");
    builder.AppendLine("            new Dictionary<string, Func<Payload, PayloadVisitor, PayloadsVisitor, EnvelopeVisitor?, Task>>");
    builder.AppendLine("            {");

    foreach (var operation in operationMessages)
    {
        EmitEnvelopeVisitor(builder, operation.Input, emittedVisitors);
        EmitEnvelopeVisitor(builder, operation.Output, emittedVisitors);
    }

    builder.AppendLine("            };");
    builder.AppendLine();
    builder.AppendLine("        private static async Task<bool> TryVisitAsync(");
    builder.AppendLine("            string? endpoint,");
    builder.AppendLine("            Payload payload,");
    builder.AppendLine("            PayloadVisitor visitPayload,");
    builder.AppendLine("            PayloadsVisitor visitPayloads,");
    builder.AppendLine("            EnvelopeVisitor? visitEnvelope)");
    builder.AppendLine("        {");
    builder.AppendLine("            if (!IsSystemNexusEndpoint(endpoint))");
    builder.AppendLine("            {");
    builder.AppendLine("                return false;");
    builder.AppendLine("            }");
    builder.AppendLine();
    builder.AppendLine("            if (!payload.Metadata.TryGetValue(\"messageType\", out var messageType) ||");
    builder.AppendLine("                !EnvelopeVisitors.TryGetValue(messageType.ToStringUtf8(), out var visit))");
    builder.AppendLine("            {");
    builder.AppendLine("                return false;");
    builder.AppendLine("            }");
    builder.AppendLine();
    builder.AppendLine("            await visit(payload, visitPayload, visitPayloads, visitEnvelope).ConfigureAwait(false);");
    builder.AppendLine("            return true;");
    builder.AppendLine("        }");
    builder.AppendLine();
    builder.AppendLine("        private static bool IsSystemNexusEndpoint(string? endpoint) => endpoint == TemporalSystemEndpoint;");
    builder.AppendLine();

    foreach (var operation in operationMessages)
    {
        EmitVisitMethod(builder, operation.Input, messages, containsPayloadMemo, emittedMethods);
        EmitVisitMethod(builder, operation.Output, messages, containsPayloadMemo, emittedMethods);
    }

    builder.AppendLine("    }");
    builder.AppendLine("}");

    File.WriteAllText(Path.Combine(outputDir, "SystemNexusPayloadVisitor.cs"), builder.ToString());
}

static List<OperationInfo> ParseOperations(string serviceSource)
{
    var operations = new List<OperationInfo>();
    var serviceMatches = Regex.Matches(
        serviceSource,
        @"\[NexusService\(""[^""]+""\)\]\s+internal\s+interface\s+\w+\s*\{(?<body>.*?)^\s*\}",
        RegexOptions.Multiline | RegexOptions.Singleline,
        TimeSpan.FromSeconds(5));
    foreach (Match serviceMatch in serviceMatches)
    {
        var operationMatches = Regex.Matches(
            serviceMatch.Groups["body"].Value,
            @"\[NexusOperation\(""[^""]+""\)\]\s+(?<output>[A-Za-z0-9_.:]+)\s+\w+\((?<input>[A-Za-z0-9_.:]+)\s+\w+\);",
            RegexOptions.Multiline,
            TimeSpan.FromSeconds(5));
        foreach (Match operationMatch in operationMatches)
        {
            operations.Add(new OperationInfo(
                NormalizeTypeName(operationMatch.Groups["input"].Value),
                NormalizeTypeName(operationMatch.Groups["output"].Value)));
        }
    }

    return operations;
}

static IReadOnlyDictionary<string, MessageInfo> LoadMessages(string descriptorPath)
{
    var descriptorSet = FileDescriptorSet.Parser.ParseFrom(File.ReadAllBytes(descriptorPath));
    var messages = new Dictionary<string, MessageInfo>();
    foreach (var file in descriptorSet.File)
    {
        var csharpNamespace = file.Options?.CsharpNamespace;
        if (string.IsNullOrEmpty(csharpNamespace))
        {
            continue;
        }

        foreach (var message in file.MessageType)
        {
            AddMessage(messages, file.Package, $"global::{csharpNamespace}", message);
        }
    }

    return messages;
}

static void AddMessage(
    Dictionary<string, MessageInfo> messages,
    string protoPrefix,
    string csharpPrefix,
    DescriptorProto descriptor)
{
    var fullName = string.IsNullOrEmpty(protoPrefix) ?
        descriptor.Name :
        $"{protoPrefix}.{descriptor.Name}";
    var csharpType = $"{csharpPrefix}.{descriptor.Name}";
    messages[fullName] = new MessageInfo(fullName, csharpType, descriptor);
    foreach (var nested in descriptor.NestedType)
    {
        AddMessage(messages, fullName, $"{csharpType}.Types", nested);
    }
}

static MessageInfo GetMessage(
    IReadOnlyDictionary<string, MessageInfo> messagesByCsharpType,
    string csharpType)
{
    if (!messagesByCsharpType.TryGetValue(NormalizeTypeName(csharpType), out var message))
    {
        throw new InvalidOperationException($"No protobuf message descriptor found for {csharpType}");
    }

    return message;
}

static void EmitEnvelopeVisitor(
    StringBuilder builder,
    MessageInfo message,
    HashSet<string> emittedVisitors)
{
    if (!emittedVisitors.Add(message.FullName))
    {
        return;
    }

    builder.AppendLine($"                [\"{message.FullName}\"] = (payload, visitPayload, visitPayloads, visitEnvelope) =>");
    builder.AppendLine($"                    VisitEnvelopeAsync<{message.CsharpType}>(");
    builder.AppendLine("                        payload,");
    builder.AppendLine($"                        {VisitMethodName(message)},");
    builder.AppendLine("                        visitPayload,");
    builder.AppendLine("                        visitPayloads,");
    builder.AppendLine("                        visitEnvelope),");
}

static void EmitVisitMethod(
    StringBuilder builder,
    MessageInfo message,
    IReadOnlyDictionary<string, MessageInfo> messages,
    Dictionary<string, bool> containsPayloadMemo,
    HashSet<string> emittedMethods)
{
    if (!emittedMethods.Add(message.FullName))
    {
        return;
    }

    foreach (var referenced in ReferencedMessagesWithPayloads(message, messages, containsPayloadMemo))
    {
        EmitVisitMethod(builder, referenced, messages, containsPayloadMemo, emittedMethods);
    }

    var fieldLines = VisitFieldLines(message, messages, containsPayloadMemo).ToList();
    var hasAwaits = fieldLines.Any(line => line.Contains("await ", StringComparison.Ordinal));
    builder.Append("        private static ");
    if (hasAwaits)
    {
        builder.Append("async ");
    }

    builder.AppendLine($"Task {VisitMethodName(message)}(");
    builder.AppendLine($"            {message.CsharpType} value,");
    builder.AppendLine("            PayloadVisitor visitPayload,");
    builder.AppendLine("            PayloadsVisitor visitPayloads)");
    builder.AppendLine("        {");
    foreach (var line in fieldLines)
    {
        builder.AppendLine($"            {line}");
    }

    if (!hasAwaits)
    {
        builder.AppendLine("            return Task.CompletedTask;");
    }

    builder.AppendLine("        }");
    builder.AppendLine();
}

static IEnumerable<MessageInfo> ReferencedMessagesWithPayloads(
    MessageInfo message,
    IReadOnlyDictionary<string, MessageInfo> messages,
    Dictionary<string, bool> containsPayloadMemo)
{
    foreach (var field in message.Descriptor.Field)
    {
        foreach (var referenced in FieldReferencedMessagesWithPayloads(field, messages, containsPayloadMemo))
        {
            yield return referenced;
        }
    }
}

static IEnumerable<MessageInfo> FieldReferencedMessagesWithPayloads(
    FieldDescriptorProto field,
    IReadOnlyDictionary<string, MessageInfo> messages,
    Dictionary<string, bool> containsPayloadMemo)
{
    if (!TryGetMessage(field.TypeName, messages, out var fieldMessage) ||
        IsPayload(fieldMessage) ||
        IsPayloads(fieldMessage) ||
        IsSearchAttributes(fieldMessage))
    {
        yield break;
    }

    if (fieldMessage.Descriptor.Options?.MapEntry == true)
    {
        var valueField = fieldMessage.Descriptor.Field.FirstOrDefault(field => field.Number == 2);
        if (valueField != null &&
            TryGetMessage(valueField.TypeName, messages, out var valueMessage) &&
            !IsPayload(valueMessage) &&
            !IsSearchAttributes(valueMessage) &&
            ContainsPayload(valueMessage, messages, containsPayloadMemo))
        {
            yield return valueMessage;
        }

        yield break;
    }

    if (ContainsPayload(fieldMessage, messages, containsPayloadMemo))
    {
        yield return fieldMessage;
    }
}

static IEnumerable<string> VisitFieldLines(
    MessageInfo message,
    IReadOnlyDictionary<string, MessageInfo> messages,
    Dictionary<string, bool> containsPayloadMemo)
{
    foreach (var field in message.Descriptor.Field)
    {
        if (!TryGetMessage(field.TypeName, messages, out var fieldMessage) ||
            IsSearchAttributes(fieldMessage))
        {
            continue;
        }

        var propertyName = PropertyName(field.Name, message.Descriptor.Name);
        if (fieldMessage.Descriptor.Options?.MapEntry == true)
        {
            var valueField = fieldMessage.Descriptor.Field.FirstOrDefault(field => field.Number == 2);
            if (valueField == null ||
                !TryGetMessage(valueField.TypeName, messages, out var valueMessage) ||
                IsSearchAttributes(valueMessage) ||
                !ContainsPayload(valueMessage, messages, containsPayloadMemo))
            {
                continue;
            }

            var valueName = UniqueLocalName(message, field, "item");
            yield return $"foreach (var {valueName} in value.{propertyName}.Values)";
            yield return "{";
            if (IsPayload(valueMessage))
            {
                yield return $"    if ({valueName} != null)";
                yield return "    {";
                yield return $"        await visitPayload({valueName}).ConfigureAwait(false);";
                yield return "    }";
            }
            else
            {
                yield return $"    if ({valueName} != null)";
                yield return "    {";
                yield return $"        await {VisitMethodName(valueMessage)}({valueName}, visitPayload, visitPayloads).ConfigureAwait(false);";
                yield return "    }";
            }

            yield return "}";
            continue;
        }

        if (!ContainsPayload(fieldMessage, messages, containsPayloadMemo))
        {
            continue;
        }

        if (IsPayload(fieldMessage))
        {
            if (field.Label == FieldDescriptorProto.Types.Label.Repeated)
            {
                var valueName = UniqueLocalName(message, field, "payload");
                yield return $"foreach (var {valueName} in value.{propertyName})";
                yield return "{";
                yield return $"    await visitPayload({valueName}).ConfigureAwait(false);";
                yield return "}";
            }
            else
            {
                yield return $"if (value.{propertyName} != null)";
                yield return "{";
                yield return $"    await visitPayload(value.{propertyName}).ConfigureAwait(false);";
                yield return "}";
            }
        }
        else if (IsPayloads(fieldMessage))
        {
            if (field.Label == FieldDescriptorProto.Types.Label.Repeated)
            {
                var valueName = UniqueLocalName(message, field, "payloads");
                yield return $"foreach (var {valueName} in value.{propertyName})";
                yield return "{";
                yield return $"    await visitPayloads({valueName}.Payloads_).ConfigureAwait(false);";
                yield return "}";
            }
            else
            {
                yield return $"if (value.{propertyName} != null)";
                yield return "{";
                yield return $"    await visitPayloads(value.{propertyName}.Payloads_).ConfigureAwait(false);";
                yield return "}";
            }
        }
        else if (field.Label == FieldDescriptorProto.Types.Label.Repeated)
        {
            var valueName = UniqueLocalName(message, field, "item");
            yield return $"foreach (var {valueName} in value.{propertyName})";
            yield return "{";
            yield return $"    await {VisitMethodName(fieldMessage)}({valueName}, visitPayload, visitPayloads).ConfigureAwait(false);";
            yield return "}";
        }
        else
        {
            yield return $"if (value.{propertyName} != null)";
            yield return "{";
            yield return $"    await {VisitMethodName(fieldMessage)}(value.{propertyName}, visitPayload, visitPayloads).ConfigureAwait(false);";
            yield return "}";
        }
    }
}

static bool ContainsPayload(
    MessageInfo message,
    IReadOnlyDictionary<string, MessageInfo> messages,
    Dictionary<string, bool> memo) =>
    ContainsPayloadCore(message, messages, memo, new HashSet<string>());

static bool ContainsPayloadCore(
    MessageInfo message,
    IReadOnlyDictionary<string, MessageInfo> messages,
    Dictionary<string, bool> memo,
    HashSet<string> visiting)
{
    if (IsSearchAttributes(message))
    {
        return false;
    }

    if (IsPayload(message) || IsPayloads(message))
    {
        return true;
    }

    if (memo.TryGetValue(message.FullName, out var result))
    {
        return result;
    }

    if (!visiting.Add(message.FullName))
    {
        return false;
    }

    result = false;
    foreach (var field in message.Descriptor.Field)
    {
        if (!TryGetMessage(field.TypeName, messages, out var fieldMessage) ||
            IsSearchAttributes(fieldMessage))
        {
            continue;
        }

        if (fieldMessage.Descriptor.Options?.MapEntry == true)
        {
            var valueField = fieldMessage.Descriptor.Field.FirstOrDefault(field => field.Number == 2);
            if (valueField != null &&
                TryGetMessage(valueField.TypeName, messages, out var valueMessage) &&
                ContainsPayloadCore(valueMessage, messages, memo, visiting))
            {
                result = true;
                break;
            }
        }
        else if (ContainsPayloadCore(fieldMessage, messages, memo, visiting))
        {
            result = true;
            break;
        }
    }

    visiting.Remove(message.FullName);
    memo[message.FullName] = result;
    return result;
}

static bool TryGetMessage(
    string typeName,
    IReadOnlyDictionary<string, MessageInfo> messages,
    out MessageInfo message) =>
    messages.TryGetValue(typeName.TrimStart('.'), out message!);

static bool IsPayload(MessageInfo message) => message.FullName == "temporal.api.common.v1.Payload";

static bool IsPayloads(MessageInfo message) => message.FullName == "temporal.api.common.v1.Payloads";

static bool IsSearchAttributes(MessageInfo message) =>
    message.FullName == "temporal.api.common.v1.SearchAttributes";

static string VisitMethodName(MessageInfo message) =>
    "Visit_" + Regex.Replace(message.FullName, @"[^A-Za-z0-9_]", "_");

static string PropertyName(string protoName, string messageName)
{
    var builder = new StringBuilder();
    var capitalizeNext = true;
    foreach (var ch in protoName)
    {
        if (ch == '_')
        {
            capitalizeNext = true;
            continue;
        }

        builder.Append(capitalizeNext ? char.ToUpperInvariant(ch) : ch);
        capitalizeNext = false;
    }

    var propertyName = builder.ToString();
    return propertyName == messageName ? propertyName + "_" : propertyName;
}

static int RunProcess(string fileName, IEnumerable<string> arguments, bool ignoreExitCode = false)
{
    using var process = new Process
    {
        StartInfo =
        {
            FileName = fileName,
            RedirectStandardError = true,
            UseShellExecute = false,
        },
    };
    foreach (var argument in arguments)
    {
        process.StartInfo.ArgumentList.Add(argument);
    }

    Console.WriteLine("Running {0} {1}", fileName, string.Join(" ", arguments));
    process.Start();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (stderr.Length > 0)
    {
        Console.Error.Write(stderr);
    }

    if (!ignoreExitCode && process.ExitCode != 0)
    {
        throw new InvalidOperationException($"{fileName} failed with exit code {process.ExitCode}");
    }

    return process.ExitCode;
}

static string NexGenCommand() =>
    Environment.GetEnvironmentVariable("NEX_GEN_BIN") is { Length: > 0 } value ? value : "nex-gen";

static string UniqueLocalName(MessageInfo message, FieldDescriptorProto field, string prefix) =>
    $"{prefix}_{Regex.Replace(message.FullName, @"[^A-Za-z0-9_]", "_")}_{field.Number}";

static string NormalizeTypeName(string typeName) =>
    typeName.StartsWith("global::", StringComparison.Ordinal) ? typeName["global::".Length..] : typeName;

internal sealed record OperationInfo(string InputType, string OutputType);

internal sealed record OperationMessages(MessageInfo Input, MessageInfo Output);

internal sealed record MessageInfo(string FullName, string CsharpType, DescriptorProto Descriptor);
