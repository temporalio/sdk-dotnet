#:property PublishAot=false

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

try
{
    if (args.Length == 1 && args[0] == "--self-test")
    {
        Test();
        return 0;
    }

    if (args.Length == 0 || (args.Length == 1 && (args[0] == "--help" || args[0] == "-h")))
    {
        Console.WriteLine(
            "Usage: dotnet run --file scripts/prepare-release.cs -- VERSION " +
            "[--date YYYY-MM-DD] [--assembly-version MAJOR.MINOR.PATCH.REVISION]\n" +
            "Publishes a release branch and opens a GitHub pull request. Requires a clean checkout.\n" +
            "Use --self-test to test transformations without accessing Git or GitHub.");
        return args.Length == 0 ? 1 : 0;
    }

    var version = args[0];
    var date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    string? assemblyVersion = null;
    var seen = new HashSet<string>(StringComparer.Ordinal);
    for (var i = 1; i < args.Length; i += 2)
    {
        if (i + 1 >= args.Length || !seen.Add(args[i]))
        {
            throw new ArgumentException("Options require a value and may only be supplied once.");
        }

        switch (args[i])
        {
            case "--date":
                date = args[i + 1];
                break;
            case "--assembly-version":
                assemblyVersion = args[i + 1];
                break;
            default:
                throw new ArgumentException($"Unknown option '{args[i]}'. Use --help.");
        }
    }

    ValidateVersion(version);
    ValidateDate(date);
    await PrepareAsync(version, date, assemblyVersion).ConfigureAwait(false);
    return 0;
}
catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException or System.ComponentModel.Win32Exception or OverflowException)
{
    await Console.Error.WriteLineAsync($"Release preparation failed: {error.Message}").ConfigureAwait(false);
    await Console.Error.WriteLineAsync("No automatic rollback is performed. Inspect the checkout before retrying.").ConfigureAwait(false);
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

    if (!Regex.IsMatch(assembly, @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:\.([1-9][0-9]*))?\z"))
    {
        throw new ArgumentException("AssemblyVersion must be major.minor.patch[.positive-revision].");
    }

    var parts = assembly.Split('.');
    if (string.Join(".", parts.Take(3)) != numericVersion ||
        parts.Any(part => !int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value > 65534))
    {
        throw new ArgumentException("AssemblyVersion must match release major.minor.patch; components must be 0..65534.");
    }

    if (version.Contains('-') && parts.Length != 4)
    {
        throw new ArgumentException("Prerelease AssemblyVersion must include a positive revision.");
    }

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

    // Pack must validate against a package already on NuGet, not this unpublished release.
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
