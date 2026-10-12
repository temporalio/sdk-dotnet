#:property PublishAot=false
#:package System.CommandLine

using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

var root = Path.GetFullPath(Path.Combine(ScriptDirectory(), ".."));
var cli = new RootCommand("Prepare and verify SDK releases.");
var versionArgument = new Argument<string>("version") { Description = "SemVer release version (no build metadata)." };
var dateOption = new Option<string>("--date")
{
    Description = "Changelog date in YYYY-MM-DD format.",
    DefaultValueFactory = _ => DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
};
var assemblyOption = new Option<string?>("--assembly-version")
{
    Description = "Matching major.minor.patch[.positive-revision]; required for prereleases.",
};
var prepare = new Command("prepare", "Create and push a release branch and open its pull request. Requires a clean checkout.")
{
    versionArgument,
    dateOption,
    assemblyOption,
};
prepare.SetAction(async result =>
{
    var version = result.GetValue(versionArgument)!;
    var date = result.GetValue(dateOption)!;
    ValidateVersion(version);
    ValidateDate(date);
    await PrepareAsync(version, date, result.GetValue(assemblyOption)).ConfigureAwait(false);
});
cli.Add(prepare);
var verifyVersion = new Command("verify-version", "Print the validated repository version.");
verifyVersion.SetAction(_ => Console.WriteLine(ValidateProperties(XDocument.Load(Path.Combine(root, "Directory.Build.props")))));
cli.Add(verifyVersion);
var artifactsArgument = new Argument<string>("artifact-directory") { Description = "Directory containing packages and symbols." };
var packageVersionArgument = new Argument<string>("version") { Description = "Expected SemVer package version." };
var verifyPackages = new Command("verify-packages", "Validate packages and print their IDs.")
{
    artifactsArgument,
    packageVersionArgument,
};
verifyPackages.SetAction(async result =>
{
    var version = result.GetValue(packageVersionArgument)!;
    ValidateVersion(version);
    var ids = await DiscoverPackageIdsAsync(Path.Combine(root, "src")).ConfigureAwait(false);
    await ValidatePackagesAsync(result.GetValue(artifactsArgument)!, version, ids).ConfigureAwait(false);
    foreach (var id in ids.Order(StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine(id);
    }
});
cli.Add(verifyPackages);
var unpublishedVersionArgument = new Argument<string>("version") { Description = "SemVer version that must not exist on any source." };
var sourceOption = new Option<string[]>("--source")
{
    Description = "Override service indexes (repeat for multiple sources).",
    AllowMultipleArgumentsPerToken = false,
};
var unpublished = new Command("check-unpublished", "Reject existing versions on integration and production NuGet.")
{
    unpublishedVersionArgument,
    sourceOption,
};
unpublished.SetAction(async result =>
{
    var version = result.GetValue(unpublishedVersionArgument)!;
    ValidateVersion(version);
    var ids = await DiscoverPackageIdsAsync(Path.Combine(root, "src")).ConfigureAwait(false);
    var sources = result.GetValue(sourceOption)!;
    using var handler = new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true };
    using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    await CheckUnpublishedAsync(
        client,
        ids,
        version,
        sources.Length == 0 ? OfficialSources() : sources).ConfigureAwait(false);
});
cli.Add(unpublished);
var tests = new Command("test", "Run local tests without Git mutations, publication, or network access.");
tests.SetAction(async _ =>
{
    Test();
    await SelfTestAsync().ConfigureAwait(false);
    await TestUnpublishedAsync().ConfigureAwait(false);
});
cli.Add(tests);
var parsed = cli.Parse(args);
try
{
    return await parsed.InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = false }).ConfigureAwait(false);
}
catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException or
    System.ComponentModel.Win32Exception or OverflowException or HttpRequestException or
    TaskCanceledException or JsonException or KeyNotFoundException or System.Xml.XmlException)
{
    await Console.Error.WriteLineAsync($"Release command failed: {error.Message}").ConfigureAwait(false);
    if (parsed.CommandResult.Command == prepare)
    {
        await Console.Error.WriteLineAsync("No automatic rollback is performed. Inspect the checkout before retrying.").ConfigureAwait(false);
    }

    return 1;
}

static void ValidateVersion(string version)
{
    const string number = "(0|[1-9][0-9]*)";
    const string identifier = "(?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)";
    if (!Regex.IsMatch(version, $@"\A{number}\.{number}\.{number}(?:-{identifier}(?:\.{identifier})*)?\z"))
    {
        throw new ArgumentException("Version must be major.minor.patch with an optional SemVer prerelease (no build metadata).");
    }
}

static void ValidateDate(string date)
{
    if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
    {
        throw new ArgumentException("Date must be a valid calendar date in YYYY-MM-DD format.");
    }
}

static string UpdateProperties(string text, string version, string? explicitAssemblyVersion)
{
    ValidateVersion(version);
    var numericVersion = version.Split('-')[0];
    var currentAssembly = GetProperty(text, "AssemblyVersion");
    var currentRevision = Regex.Match(currentAssembly, $@"\A{Regex.Escape(numericVersion)}\.([0-9]+)\z");
    var assembly = explicitAssemblyVersion;
    if (assembly is null)
    {
        if (version.Contains('-'))
        {
            throw new ArgumentException("Prereleases require --assembly-version with an explicit positive revision.");
        }

        assembly = currentRevision.Success
            ? $"{numericVersion}.{checked(int.Parse(currentRevision.Groups[1].Value, CultureInfo.InvariantCulture) + 1)}"
            : numericVersion;
    }

    ValidateAssemblyVersion(version, assembly);
    var parts = assembly.Split('.');
    if (currentRevision.Success &&
        (parts.Length != 4 || int.Parse(parts[3], CultureInfo.InvariantCulture) <=
            int.Parse(currentRevision.Groups[1].Value, CultureInfo.InvariantCulture)))
    {
        throw new ArgumentException("AssemblyVersion revision must increase after a prerelease, including for the final release.");
    }

    foreach (var (name, value) in new[] { ("Version", version), ("AssemblyVersion", assembly) })
    {
        _ = GetProperty(text, name);
        text = Regex.Replace(text, $@"<{name}>[^<]*</{name}>", _ => $"<{name}>{value}</{name}>");
    }

    return text;
}

static async Task PrepareAsync(string version, string date, string? assemblyVersion)
{
    var root = Path.GetFullPath(Path.Combine(ScriptDirectory(), ".."));
    Task<string> RunAsync(string command, params string[] arguments) => RunCommandAsync(root, command, arguments);
    async Task RequireCleanAsync()
    {
        if ((await RunAsync("git", "status", "--porcelain", "--untracked-files=all", "--ignore-submodules=none").ConfigureAwait(false)).Length != 0)
        {
            throw new InvalidOperationException("A clean worktree is required, including untracked files and submodules.");
        }
    }

    await RequireCleanAsync().ConfigureAwait(false);
    var origin = (await RunAsync("git", "remote", "get-url", "origin").ConfigureAwait(false)).Trim();
    var repository = Regex.Match(origin, @"\Agit@github\.com:(?<repo>[^/]+/[^/]+?)(?:\.git)?\z");
    if (!repository.Success)
    {
        repository = Regex.Match(origin, @"\Ahttps://github\.com/(?<repo>[^/]+/[^/]+?)(?:\.git)?/?\z");
    }

    if (!repository.Success)
    {
        throw new InvalidOperationException("Origin must be a GitHub HTTPS or git@github.com remote.");
    }

    var githubRepo = repository.Groups["repo"].Value;
    await RunAsync("gh", "auth", "status", "--hostname", "github.com").ConfigureAwait(false);
    await RunAsync("mise", "--version").ConfigureAwait(false);
    var branch = $"chore/release-{version}";
    if ((await RunAsync("git", "branch", "--list", branch).ConfigureAwait(false)).Length != 0 ||
        (await RunAsync("git", "ls-remote", "--heads", "origin", $"refs/heads/{branch}").ConfigureAwait(false)).Length != 0)
    {
        throw new InvalidOperationException($"Branch '{branch}' already exists locally or on origin.");
    }

    var propsPath = Path.Combine(root, "Directory.Build.props");
    _ = UpdateProperties(await File.ReadAllTextAsync(propsPath).ConfigureAwait(false), version, assemblyVersion);
    await RunAsync("git", "fetch", "origin", "main:refs/remotes/origin/main").ConfigureAwait(false);
    _ = UpdateProperties(await RunAsync("git", "show", "origin/main:Directory.Build.props").ConfigureAwait(false), version, assemblyVersion);
    await RunAsync("git", "switch", "--create", branch, "origin/main").ConfigureAwait(false);
    await RunAsync("git", "submodule", "update", "--init", "--recursive").ConfigureAwait(false);
    await RequireCleanAsync().ConfigureAwait(false);
    var props = UpdateProperties(await File.ReadAllTextAsync(propsPath).ConfigureAwait(false), version, assemblyVersion);
    await File.WriteAllTextAsync(propsPath, props, new UTF8Encoding(false)).ConfigureAwait(false);
    await RunAsync("mise", "run", "changelog:prepare", "--", "--version", version, "--date", date).ConfigureAwait(false);

    var changes = (await RunAsync("git", "-c", "core.quotePath=false", "diff", "--name-status", "--no-renames").ConfigureAwait(false))
        .Split('\n', StringSplitOptions.RemoveEmptyEntries);
    var paths = new List<string>();
    foreach (var change in changes)
    {
        var fields = change.TrimEnd('\r').Split('\t', 2);
        if (fields.Length != 2 || !(
            (fields[0] == "M" && fields[1] is "Directory.Build.props" or "CHANGELOG.md") ||
            (fields[0] == "D" && Regex.IsMatch(fields[1], @"\Achangelog/(added|breaking-changes|changed|deprecated|fixed|security|stabilized)/[^/]+\.md\z"))))
        {
            throw new InvalidOperationException($"Unexpected release change: {change}. Nothing has been committed.");
        }

        paths.Add(fields[1]);
    }

    if ((await RunAsync("git", "ls-files", "--others", "--exclude-standard").ConfigureAwait(false)).Length != 0 ||
        (await RunAsync("git", "diff", "--cached", "--name-only").ConfigureAwait(false)).Length != 0)
    {
        throw new InvalidOperationException("Unexpected untracked or staged changes. Nothing has been committed.");
    }

    if (!paths.Contains("Directory.Build.props") || !paths.Contains("CHANGELOG.md"))
    {
        throw new InvalidOperationException("Expected version and changelog updates. Nothing has been committed.");
    }

    paths.Insert(0, "--");
    paths.Insert(0, "add");
    await RunAsync("git", paths.ToArray()).ConfigureAwait(false);
    await RunAsync("git", "-c", "core.editor=true", "commit", "-m", $"Prepare release {version}").ConfigureAwait(false);
    await RequireCleanAsync().ConfigureAwait(false);
    var committedChanges = (await RunAsync(
        "git",
        "-c",
        "core.quotePath=false",
        "diff",
        "--name-status",
        "--no-renames",
        "HEAD^",
        "HEAD").ConfigureAwait(false)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
    if (!changes.Order(StringComparer.Ordinal).SequenceEqual(committedChanges.Order(StringComparer.Ordinal)))
    {
        throw new InvalidOperationException("Commit contents differ from expected release paths. Review manually before publishing.");
    }

    await RunAsync("git", "push", "--set-upstream", "origin", branch).ConfigureAwait(false);
    await RunAsync(
        "gh",
        "pr",
        "create",
        "--repo",
        githubRepo,
        "--base",
        "main",
        "--head",
        branch,
        "--title",
        $"Prepare release {version}",
        "--body",
        $"Prepare release {version} dated {date}. Preserve the published API compatibility baseline.",
        "--label",
        "skip-changelog").ConfigureAwait(false);
}

static void Test()
{
    const string props = "<Project>\r\n  <Version>1.20.0</Version>\r\n  <AssemblyVersion>1.20.0</AssemblyVersion>\r\n  <PackageValidationBaselineVersion>1.20.0</PackageValidationBaselineVersion>\r\n</Project>\r\n";
    var checks = 0;
    void Equal(string expected, string actual)
    {
        checks++;
        if (expected != actual)
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    void Reject(Action action)
    {
        checks++;
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException("Expected validation failure.");
    }

    foreach (var version in new[] { "1.21.0", "0.0.0", "1.21.0-alpha.1", "1.21.0-rc1" })
    {
        ValidateVersion(version);
        checks++;
    }

    foreach (var version in new[] { string.Empty, "1.21", "01.21.0", "1.21.0-01", "1.21.0-", "1.21.0+a", "1.21.0\n" })
    {
        Reject(() => ValidateVersion(version));
    }

    ValidateDate("2024-02-29");
    foreach (var date in new[] { "2025-02-29", "2026-2-03", "2026-13-01", "2026-01-01\n" })
    {
        Reject(() => ValidateDate(date));
    }

    Equal(
        props.Replace("1.20.0", "1.21.0").Replace(
            "<PackageValidationBaselineVersion>1.21.0",
            "<PackageValidationBaselineVersion>1.20.0"),
        UpdateProperties(props, "1.21.0", null));
    var prerelease = UpdateProperties(props, "1.21.0-alpha.1", "1.21.0.1");
    Equal("1.21.0-alpha.1", GetProperty(prerelease, "Version"));
    Equal("1.21.0.1", GetProperty(prerelease, "AssemblyVersion"));
    Equal("1.21.0.2", GetProperty(UpdateProperties(prerelease, "1.21.0", null), "AssemblyVersion"));
    Equal("1.21.0.3", GetProperty(UpdateProperties(prerelease, "1.21.0-beta", "1.21.0.3"), "AssemblyVersion"));
    Reject(() => UpdateProperties(props, "1.21.0-alpha", null));
    Reject(() => UpdateProperties(props, "1.21.0-alpha", "1.21.0"));
    Reject(() => UpdateProperties(props, "1.21.0", "1.22.0"));
    Reject(() => UpdateProperties(props, "1.21.0-alpha", "1.21.0.0"));
    Reject(() => UpdateProperties(props, "65535.0.0", null));
    Reject(() => UpdateProperties(props, "1.21.0", "1.21.0.65535"));
    Reject(() => UpdateProperties(prerelease, "1.21.0", "1.21.0.1"));
    Reject(() => UpdateProperties(props.Replace("<Version>", "<Missing>"), "1.21.0", null));
    Reject(() => UpdateProperties(props + "<Version>1.20.0</Version>", "1.21.0", null));
    Reject(() => UpdateProperties(props + "<AssemblyVersion>1.20.0</AssemblyVersion>", "1.21.0", null));
    Console.WriteLine($"Passed {checks} release helper checks. No Git or GitHub commands were run.");
}

static string GetProperty(string text, string name)
{
    var matches = Regex.Matches(text, $@"<{name}>([^<]*)</{name}>");
    if (matches.Count != 1)
    {
        throw new ArgumentException($"Expected exactly one {name} property.");
    }

    return matches[0].Groups[1].Value;
}

static string ScriptDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

static async Task<string> RunCommandAsync(string root, string command, string[] arguments)
{
    var start = new ProcessStartInfo(command)
    {
        WorkingDirectory = root,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    // Fail rather than hanging on credential/editor prompts in an unattended invocation.
    start.Environment["GIT_TERMINAL_PROMPT"] = "0";
    start.Environment["GIT_EDITOR"] = "true";
    start.Environment["GH_PROMPT_DISABLED"] = "1";
    foreach (var argument in arguments)
    {
        start.ArgumentList.Add(argument);
    }

    Console.WriteLine($"> {command} {string.Join(" ", arguments)}");
    using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {command}.");
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync().ConfigureAwait(false);
    var stdout = await output.ConfigureAwait(false);
    var stderr = await error.ConfigureAwait(false);
    Console.Write(stdout);
    await Console.Error.WriteAsync(stderr).ConfigureAwait(false);
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"{command} exited with code {process.ExitCode}.");
    }

    return stdout;
}

static void ValidateAssemblyVersion(string version, string assembly)
{
    ValidateVersion(version);
    var parts = assembly.Split('.');
    if (!Regex.IsMatch(assembly, @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:\.([1-9][0-9]*))?\z") ||
        string.Join(".", parts.Take(3)) != version.Split('-')[0] ||
        parts.Any(part => !int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value > 65534) ||
        (version.Contains('-') && parts.Length != 4))
    {
        throw new ArgumentException("Version and AssemblyVersion must have matching major.minor.patch versions, components 0..65534, and a positive revision for prereleases.");
    }
}

static string ValidateProperties(XDocument props)
{
    var version = props.Descendants("Version").Single().Value;
    try
    {
        ValidateAssemblyVersion(version, props.Descendants("AssemblyVersion").Single().Value);
    }
    catch (ArgumentException error)
    {
        throw new InvalidOperationException("Version and AssemblyVersion must have matching major.minor.patch versions.", error);
    }

    return version;
}

static async Task<HashSet<string>> DiscoverPackageIdsAsync(string sourceDirectory)
{
    var expectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var directory in Directory.GetDirectories(sourceDirectory))
    {
        foreach (var project in Directory.GetFiles(directory, "*.csproj"))
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("msbuild");
            startInfo.ArgumentList.Add(project);
            startInfo.ArgumentList.Add("-getProperty:IsPackable,PackageId");
            using var process = Process.Start(startInfo)!;
            var streams = await Task.WhenAll(
                process.StandardOutput.ReadToEndAsync(),
                process.StandardError.ReadToEndAsync()).ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            var output = streams[0];
            var error = streams[1];
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Cannot evaluate {project}:\n{output}\n{error}");
            }

            using var properties = JsonDocument.Parse(output);
            var values = properties.RootElement.GetProperty("Properties");
            if (values.GetProperty("IsPackable").GetString() == "true")
            {
                var id = values.GetProperty("PackageId").GetString();
                if (string.IsNullOrWhiteSpace(id) || !expectedIds.Add(id))
                {
                    throw new InvalidOperationException($"Empty or duplicate PackageId in {project}.");
                }
            }
        }
    }

    if (expectedIds.Count == 0)
    {
        throw new InvalidOperationException("No packable projects found.");
    }

    return expectedIds;
}

static async Task ValidatePackagesAsync(string artifactDirectory, string version, HashSet<string> expectedIds)
{
    var packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var path in Directory.EnumerateFiles(artifactDirectory, "*", SearchOption.AllDirectories)
        .Where(path => path.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase)))
    {
        if (!path.EndsWith(".nupkg", StringComparison.Ordinal) && !path.EndsWith(".snupkg", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Package extensions must be lowercase for publishing: {path}.");
        }

        using var archive = await ZipFile.OpenReadAsync(path).ConfigureAwait(false);
        using var nuspec = await archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)).OpenAsync().ConfigureAwait(false);
        var document = await XDocument.LoadAsync(nuspec, LoadOptions.None, CancellationToken.None).ConfigureAwait(false);
        var metadata = document.Descendants().Single(element => element.Name.LocalName == "metadata");
        var id = metadata.Elements().Single(element => element.Name.LocalName == "id").Value;
        var packageVersion = metadata.Elements().Single(element => element.Name.LocalName == "version").Value;
        if (!expectedIds.Contains(id) || !packageVersion.Equals(version, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unexpected package {id} {packageVersion} in {path}.");
        }

        var ids = path.EndsWith(".snupkg", StringComparison.Ordinal) ? symbols : packages;
        if (!ids.Add(id))
        {
            throw new InvalidOperationException($"Duplicate package {id} in {path}.");
        }
    }

    if (!expectedIds.SetEquals(packages) || !expectedIds.SetEquals(symbols))
    {
        throw new InvalidOperationException(
            $"Expected a package and symbols for every packable project. " +
            $"Missing packages: {string.Join(", ", expectedIds.Where(id => !packages.Contains(id)))}. " +
            $"Missing symbols: {string.Join(", ", expectedIds.Where(id => !symbols.Contains(id)))}.");
    }
}

static async Task SelfTestAsync()
{
    var tests = 0;
    foreach (var (version, assembly, valid) in new[]
    {
        ("1.2.3", "1.2.3", true),
        ("1.2.3", "1.2.3.2", true),
        ("1.2.3-rc.1", "1.2.3.1", true),
        ("1.2.3-beta-2", "1.2.3.2", true),
        ("1.2.3", "1.2.3.0", false),
        ("1.2.3-rc.1", "1.2.3", false),
        ("1.2.3-rc.1", "1.2.3.0", false),
        ("1.2.3", "01.2.3", false),
        ("1.2.3-01", "1.2.3.1", false),
        ("65535.2.3", "65535.2.3", false),
        ("1.2.3", "1.2.3.65535", false),
        ("1.2.3", "2.2.3.0", false),
        ("1.2.3", "1.3.3.0", false),
        ("1.2.3", "1.2.4.0", false),
        ("1.2.3", "invalid", false),
        ("01.2.3", "1.2.3.0", false),
        ("1.2", "1.2.0.0", false),
        ("1.2.3-", "1.2.3.0", false),
        ("1.2.3+build", "1.2.3.0", false),
    })
    {
        var props = XDocument.Parse($"<Project><PropertyGroup><Version>{version}</Version><AssemblyVersion>{assembly}</AssemblyVersion></PropertyGroup></Project>");
        if (valid)
        {
            if (ValidateProperties(props) != version)
            {
                throw new InvalidOperationException($"Self-test failed: version {version}.");
            }
        }
        else
        {
            await ExpectFailureAsync(
                () => Task.FromResult(ValidateProperties(props)),
                $"version {version}, assembly {assembly}",
                "matching major.minor.patch").ConfigureAwait(false);
        }

        tests++;
    }

    var root = Directory.CreateTempSubdirectory("release-verify-").FullName;
    try
    {
        var source = Directory.CreateDirectory(Path.Combine(root, "src")).FullName;
        foreach (var (name, packable, id) in new[]
        {
            ("First", true, "Package.First"),
            ("Second", true, "Package.Second"),
            ("Tool", false, "Not.A.Package"),
        })
        {
            var directory = Directory.CreateDirectory(Path.Combine(source, name)).FullName;
            await File.WriteAllTextAsync(
                Path.Combine(directory, $"{name}.csproj"),
                $"<Project><PropertyGroup><IsPackable>{(packable ? "true" : "false")}</IsPackable><PackageId>{id}</PackageId></PropertyGroup></Project>").ConfigureAwait(false);
        }

        var expectedIds = await DiscoverPackageIdsAsync(source).ConfigureAwait(false);
        var wantedIds = new[] { "Package.First", "Package.Second" };
        if (!expectedIds.SetEquals(wantedIds))
        {
            throw new InvalidOperationException("Self-test failed: packable project discovery.");
        }

        tests++;
        var emptySource = Directory.CreateDirectory(Path.Combine(root, "empty-src")).FullName;
        await ExpectFailureAsync(() => DiscoverPackageIdsAsync(emptySource), "no packable projects", "No packable projects").ConfigureAwait(false);
        tests++;

        foreach (var version in new[] { "1.2.3", "1.2.3-rc.1" })
        {
            var artifacts = Directory.CreateDirectory(Path.Combine(root, version)).FullName;
            foreach (var id in expectedIds)
            {
                var nested = Directory.CreateDirectory(Path.Combine(artifacts, id)).FullName;
                await WritePackageAsync(Path.Combine(nested, $"{id}.nupkg"), id, version).ConfigureAwait(false);
                await WritePackageAsync(Path.Combine(nested, $"{id}.snupkg"), id.ToUpperInvariant(), version.ToUpperInvariant()).ConfigureAwait(false);
            }

            await File.WriteAllTextAsync(Path.Combine(artifacts, "ignored.txt"), "not a package").ConfigureAwait(false);
            await ValidatePackagesAsync(artifacts, version, expectedIds).ConfigureAwait(false);
            tests++;
        }

        foreach (var scenario in new[] { "renamed symbols", "separate symbols directory" })
        {
            var artifacts = Directory.CreateDirectory(Path.Combine(root, scenario)).FullName;
            const string version = "1.2.3";
            foreach (var id in expectedIds)
            {
                await WritePackageAsync(Path.Combine(artifacts, $"{id}.nupkg"), id, version).ConfigureAwait(false);
                var symbolsPath = scenario == "renamed symbols"
                    ? Path.Combine(artifacts, $"{id}-symbols.snupkg")
                    : Path.Combine(Directory.CreateDirectory(Path.Combine(artifacts, "symbols")).FullName, $"{id}.snupkg");
                await WritePackageAsync(symbolsPath, id, version).ConfigureAwait(false);
            }

            await ValidatePackagesAsync(artifacts, version, expectedIds).ConfigureAwait(false);
            tests++;
        }

        foreach (var scenario in new[]
        {
            "duplicate package", "duplicate symbols", "wrong version", "wrong symbols version", "unexpected package",
            "missing package", "missing symbols", "empty artifacts", "missing nuspec",
            "duplicate nuspec", "invalid XML", "missing metadata", "missing id", "missing version",
            "duplicate id", "duplicate version", "uppercase extension",
        })
        {
            var artifacts = Directory.CreateDirectory(Path.Combine(root, scenario)).FullName;
            const string version = "1.2.3";
            foreach (var id in expectedIds)
            {
                if (scenario != "empty artifacts" && !(scenario == "missing package" && id == "Package.First"))
                {
                    await WritePackageAsync(Path.Combine(artifacts, $"{id}.nupkg"), id, version).ConfigureAwait(false);
                }

                if (scenario != "empty artifacts" && !(scenario == "missing symbols" && id == "Package.First"))
                {
                    await WritePackageAsync(Path.Combine(artifacts, $"{id}.snupkg"), id, version).ConfigureAwait(false);
                }
            }

            if (scenario is "duplicate package" or "duplicate symbols")
            {
                await WritePackageAsync(
                    Path.Combine(artifacts, scenario == "duplicate package" ? "extra.nupkg" : "extra.snupkg"),
                    "package.first",
                    version).ConfigureAwait(false);
            }
            else if (scenario is "wrong version" or "wrong symbols version" or "unexpected package")
            {
                await WritePackageAsync(
                    Path.Combine(artifacts, scenario == "wrong symbols version" ? "extra.snupkg" : "extra.nupkg"),
                    scenario == "unexpected package" ? "Unexpected" : "Package.First",
                    scenario == "unexpected package" ? version : "1.2.4").ConfigureAwait(false);
            }
            else if (scenario == "uppercase extension")
            {
                File.Move(
                    Path.Combine(artifacts, "Package.First.nupkg"),
                    Path.Combine(artifacts, "uppercase.NUPKG"));
            }
            else if (scenario is not ("missing package" or "missing symbols" or "empty artifacts"))
            {
                using var archive = await ZipFile.OpenAsync(Path.Combine(artifacts, "malformed.nupkg"), ZipArchiveMode.Create).ConfigureAwait(false);
                var xml = scenario switch
                {
                    "invalid XML" => "<package>",
                    "missing metadata" => "<package/>",
                    "missing id" => "<package><metadata><version>1.2.3</version></metadata></package>",
                    "missing version" => "<package><metadata><id>Package.First</id></metadata></package>",
                    "duplicate id" => "<package><metadata><id>Package.First</id><id>Package.First</id><version>1.2.3</version></metadata></package>",
                    "duplicate version" => "<package><metadata><id>Package.First</id><version>1.2.3</version><version>1.2.3</version></metadata></package>",
                    _ => "<package><metadata><id>Package.First</id><version>1.2.3</version></metadata></package>",
                };
                foreach (var name in scenario == "duplicate nuspec" ? new[] { "first.nuspec", "second.nuspec" } :
                    new[] { scenario == "missing nuspec" ? "other.txt" : "package.nuspec" })
                {
                    using var writer = new StreamWriter(await archive.CreateEntry(name).OpenAsync().ConfigureAwait(false));
                    await writer.WriteAsync(xml).ConfigureAwait(false);
                }
            }

            var message = scenario switch
            {
                "duplicate package" or "duplicate symbols" => "Duplicate package",
                "wrong version" or "wrong symbols version" or "unexpected package" => "Unexpected package",
                "missing package" or "missing symbols" or "empty artifacts" => "Missing",
                "uppercase extension" => "lowercase",
                _ => null,
            };
            await ExpectFailureAsync(() => ValidatePackagesAsync(artifacts, version, expectedIds), scenario, message).ConfigureAwait(false);
            tests++;
        }
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }

    Console.WriteLine($"Release verification tests passed ({tests} cases).");
}

static async Task WritePackageAsync(string path, string id, string version)
{
    using var archive = await ZipFile.OpenAsync(path, ZipArchiveMode.Create).ConfigureAwait(false);
    using var writer = new StreamWriter(await archive.CreateEntry("package.nuspec").OpenAsync().ConfigureAwait(false));
    await writer.WriteAsync(
        $"<package xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\"><metadata><id>{id}</id><version>{version}</version></metadata></package>").ConfigureAwait(false);
}

static async Task ExpectFailureAsync(Func<Task> action, string scenario, string? message)
{
    try
    {
        await action().ConfigureAwait(false);
    }
    catch (Exception error) when (error is InvalidOperationException or System.Xml.XmlException)
    {
        if (message is null || error.Message.Contains(message, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException($"Self-test failed: {scenario}: unexpected error: {error.Message}", error);
    }

    throw new InvalidOperationException($"Self-test failed: {scenario}: expected rejection.");
}

static string[] OfficialSources() =>
[
    "https://apiint.nugettest.org/v3/index.json",
    "https://api.nuget.org/v3/index.json",
];

static async Task CheckUnpublishedAsync(HttpClient client, IEnumerable<string> ids, string version, IEnumerable<string> sources)
{
    ValidateVersion(version);
    // NuGet's flat-container protocol requires lowercase paths, unlike ordinary text comparisons.
#pragma warning disable CA1308
    var normalizedVersion = version.ToLowerInvariant();
#pragma warning restore CA1308
    foreach (var source in sources)
    {
        using var indexResponse = await client.GetAsync(new Uri(source, UriKind.Absolute)).ConfigureAwait(false);
        indexResponse.EnsureSuccessStatusCode();
        using var index = JsonDocument.Parse(await indexResponse.Content.ReadAsStringAsync().ConfigureAwait(false));
        var addresses = index.RootElement.GetProperty("resources").EnumerateArray()
            .Where(resource => resource.GetProperty("@type").GetString() == "PackageBaseAddress/3.0.0")
            .Select(resource => resource.GetProperty("@id").GetString())
            .ToArray();
        if (addresses.Length == 0)
        {
            throw new InvalidOperationException($"No PackageBaseAddress/3.0.0 resource in {source}.");
        }

        foreach (var address in addresses)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp) ||
                !baseUri.AbsolutePath.EndsWith('/') ||
                baseUri.Query.Length != 0 || baseUri.Fragment.Length != 0)
            {
                throw new InvalidOperationException($"Invalid PackageBaseAddress in {source}.");
            }

            foreach (var id in ids)
            {
#pragma warning disable CA1308
                var normalizedId = id.ToLowerInvariant();
#pragma warning restore CA1308
                // A concrete nuspec exists even for unlisted versions; search and version lists are insufficient.
                var uri = new Uri(baseUri, $"{Uri.EscapeDataString(normalizedId)}/{Uri.EscapeDataString(normalizedVersion)}/{Uri.EscapeDataString(normalizedId)}.nuspec");
                using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    continue;
                }

                response.EnsureSuccessStatusCode();
                throw new InvalidOperationException($"Package {id} {version} already exists at {uri}.");
            }
        }
    }
}

static async Task TestUnpublishedAsync()
{
    var tests = 0;
    var sources = OfficialSources();
    var ids = new[] { "Package.First", "Package.Second" };
    foreach (var scenario in new[]
    {
        "absent", "existing integration", "unlisted production", "second package exists",
        "package 401", "package 403", "package 500", "package 503", "package redirect",
        "index 401", "index 403", "index 404", "index 500", "transport", "timeout", "package transport", "package timeout",
        "invalid JSON", "missing resources", "missing resource", "invalid address", "relative address",
    })
    {
        var requests = new List<string>();
        using var handler = new TestHttpHandler(request =>
        {
            var uri = request.RequestUri!.AbsoluteUri;
            requests.Add(uri);
            var isIndex = sources.Contains(uri, StringComparer.Ordinal);
            if (scenario == "transport")
            {
                throw new HttpRequestException("Simulated transport failure.");
            }

            if (scenario == "timeout")
            {
                throw new TaskCanceledException("Simulated timeout.");
            }

            var status = HttpStatusCode.OK;
            string content;
            if (isIndex)
            {
                if (scenario.StartsWith("index ", StringComparison.Ordinal))
                {
                    status = (HttpStatusCode)int.Parse(scenario.AsSpan(6), CultureInfo.InvariantCulture);
                }

                // Distinct advertised hosts ensure requests follow the index rather than a hardcoded endpoint.
                var address = uri == sources[0] ? "https://integration.example/flat/" : "https://production.example/flat/";
                content = scenario switch
                {
                    "invalid JSON" => "{",
                    "missing resources" => "{}",
                    "missing resource" => """{"resources":[]}""",
                    "invalid address" => """{"resources":[{"@type":"PackageBaseAddress/3.0.0","@id":"file:///flat/"}]}""",
                    "relative address" => """{"resources":[{"@type":"PackageBaseAddress/3.0.0","@id":"/flat/"}]}""",
                    _ => $$"""{"resources":[{"@type":"SearchQueryService/3.0.0","@id":"https://search.example/"},{"@type":"PackageBaseAddress/3.0.0","@id":"{{address}}"}]}""",
                };
            }
            else
            {
                if (scenario == "package transport")
                {
                    throw new HttpRequestException("Simulated package transport failure.");
                }

                if (scenario == "package timeout")
                {
                    throw new TaskCanceledException("Simulated package timeout.");
                }

                if (request.Method != HttpMethod.Get || (
                    !uri.EndsWith("/package.first/1.2.3-rc.1/package.first.nuspec", StringComparison.Ordinal) &&
                    !uri.EndsWith("/package.second/1.2.3-rc.1/package.second.nuspec", StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException($"Unexpected package request: {request.Method} {uri}.");
                }

                status = scenario switch
                {
                    "existing integration" when uri.Contains("integration.example", StringComparison.Ordinal) => HttpStatusCode.OK,
                    "unlisted production" when uri.Contains("production.example", StringComparison.Ordinal) => HttpStatusCode.OK,
                    "second package exists" when uri.Contains("package.second", StringComparison.Ordinal) => HttpStatusCode.OK,
                    "package redirect" => HttpStatusCode.Redirect,
                    _ when scenario.StartsWith("package ", StringComparison.Ordinal) =>
                        (HttpStatusCode)int.Parse(scenario.AsSpan(8), CultureInfo.InvariantCulture),
                    _ => HttpStatusCode.NotFound,
                };
                content = "<package/>";
            }

            return new HttpResponseMessage(status) { Content = new StringContent(content) };
        });
        using var client = new HttpClient(handler);
        var rejected = false;
        try
        {
            await CheckUnpublishedAsync(client, ids, "1.2.3-RC.1", sources).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            rejected = true;
            if (error is InvalidOperationException && !error.Message.Contains("already exists", StringComparison.Ordinal) &&
                !error.Message.Contains("PackageBaseAddress", StringComparison.Ordinal))
            {
                throw;
            }
        }

        if (rejected != (scenario != "absent") ||
            (scenario == "absent" && (requests.Count != 6 || !sources.All(requests.Contains))))
        {
            throw new InvalidOperationException($"HTTP test failed: {scenario}.");
        }

        tests++;
    }

    Console.WriteLine($"Unpublished-version tests passed ({tests} cases, no network access).");
}

// Keeping the simulated handler inline preserves the single-file tool.
#pragma warning disable SA1649
/// <summary>
/// Prevents release tests from making real network requests.
/// </summary>
/// <param name="respond">The simulated endpoint behavior.</param>
internal sealed class TestHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(respond(request));
}
#pragma warning restore SA1649
