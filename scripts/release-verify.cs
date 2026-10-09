#:property PublishAot=false

using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

try
{
    if (args.Length == 1 && args[0] == "version")
    {
        Console.WriteLine(ValidateVersion(XDocument.Load("Directory.Build.props")));
    }
    else if (args.Length == 3 && args[0] == "packages")
    {
        var expectedIds = await DiscoverPackageIdsAsync("src").ConfigureAwait(false);
        await ValidatePackagesAsync(args[1], args[2], expectedIds).ConfigureAwait(false);
        foreach (var id in expectedIds.Order(StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(id);
        }
    }
    else if (args.Length == 1 && args[0] == "--self-test")
    {
        await SelfTestAsync().ConfigureAwait(false);
    }
    else
    {
        throw new ArgumentException("Usage: dotnet run --file scripts/release-verify.cs -- version | packages <artifact-directory> <version> | --self-test");
    }
}
catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException or System.Xml.XmlException or JsonException)
{
    await Console.Error.WriteLineAsync(error.Message).ConfigureAwait(false);
    return 1;
}

return 0;

static string ValidateVersion(XDocument props)
{
    var version = props.Descendants("Version").Single().Value;
    var assembly = props.Descendants("AssemblyVersion").Single().Value;
    const string number = "(0|[1-9][0-9]*)";
    const string identifier = "(?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)";
    var match = Regex.Match(version, $@"\A{number}\.{number}\.{number}(?:-{identifier}(?:\.{identifier})*)?\z");
    if (!match.Success ||
        !Regex.IsMatch(assembly, $@"\A{number}\.{number}\.{number}(?:\.[1-9][0-9]*)?\z") ||
        !Version.TryParse(assembly, out var assemblyVersion) ||
        new[] { assemblyVersion.Major, assemblyVersion.Minor, assemblyVersion.Build, assemblyVersion.Revision }.Any(component => component > 65534) ||
        (version.Contains('-') && assemblyVersion.Revision < 1) ||
        assemblyVersion.Major.ToString() != match.Groups[1].Value ||
        assemblyVersion.Minor.ToString() != match.Groups[2].Value ||
        assemblyVersion.Build.ToString() != match.Groups[3].Value)
    {
        throw new InvalidOperationException("Version and AssemblyVersion must have matching major.minor.patch versions.");
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
                expectedIds.Add(values.GetProperty("PackageId").GetString()!);
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
    var packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var symbols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

        var paths = path.EndsWith(".snupkg", StringComparison.Ordinal) ? symbols : packages;
        if (!paths.TryAdd(id, path))
        {
            throw new InvalidOperationException($"Duplicate package {id} in {path}.");
        }
    }

    if (!expectedIds.SetEquals(packages.Keys) || !expectedIds.SetEquals(symbols.Keys))
    {
        throw new InvalidOperationException(
            $"Expected a package and symbols for every packable project. " +
            $"Missing packages: {string.Join(", ", expectedIds.Except(packages.Keys))}. " +
            $"Missing symbols: {string.Join(", ", expectedIds.Except(symbols.Keys))}.");
    }

    foreach (var id in expectedIds)
    {
        // NuGet push discovers symbols by adjacent filename, not nuspec metadata.
        if (Path.ChangeExtension(packages[id], ".snupkg") != symbols[id])
        {
            throw new InvalidOperationException($"Symbols for {id} must have the same basename and directory as its package.");
        }
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
            if (ValidateVersion(props) != version)
            {
                throw new InvalidOperationException($"Self-test failed: version {version}.");
            }
        }
        else
        {
            await ExpectFailureAsync(
                () => Task.FromResult(ValidateVersion(props)),
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

        foreach (var scenario in new[]
        {
            "duplicate package", "duplicate symbols", "wrong version", "wrong symbols version", "unexpected package",
            "missing package", "missing symbols", "empty artifacts", "missing nuspec",
            "duplicate nuspec", "invalid XML", "missing metadata", "missing id", "missing version",
            "duplicate id", "duplicate version", "misnamed symbols", "misplaced symbols", "uppercase extension",
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
            else if (scenario is "misnamed symbols" or "misplaced symbols" or "uppercase extension")
            {
                var destination = scenario == "misplaced symbols"
                    ? Path.Combine(Directory.CreateDirectory(Path.Combine(artifacts, "symbols")).FullName, "Package.First.snupkg")
                    : Path.Combine(artifacts, scenario == "misnamed symbols" ? "renamed.snupkg" : "uppercase.NUPKG");
                File.Move(
                    Path.Combine(artifacts, scenario == "uppercase extension" ? "Package.First.nupkg" : "Package.First.snupkg"),
                    destination);
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
                "misnamed symbols" or "misplaced symbols" => "same basename and directory",
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

    Console.WriteLine($"Release verification self-tests passed ({tests} cases).");
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
