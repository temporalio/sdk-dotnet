using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Xunit.Abstractions;

using Temporal.Util;

namespace Temporal.TestUtil
{
    internal sealed class TemporalLiteExeTestServerController : ITemporalTestServerController, IDisposable
    {
        public enum ExeBinarySource
        {
            Unspecified = 0,
            PrecompiledFromToolsRepo = 1,
            ReleaseBinTemporalLiteRepo = 2
        }

        private static class Config
        {
            public const ExeBinarySource ExeBinarySource
                                = TemporalLiteExeTestServerController.ExeBinarySource.ReleaseBinTemporalLiteRepo;
        }

        private readonly ITestOutputHelper _tstout;
        private readonly bool _redirectServerOutToTstout;
        private const string DefaultTemporalLiteProcArgsTemplate = "start --ephemeral --namespace {0} --port {1}";

        private const string TlsTemporalLiteProcArgsTemplate =
            "--tls-certificate-file \"{0}\" --tls-key-file \"{1}\" --client-certificate-authority \"{2}\"";

        private ProcessManager _temporalLiteProc = null;

        public TemporalLiteExeTestServerController(ITestOutputHelper tstout, bool redirectServerOutToTstout)
        {
            Validate.NotNull(tstout);
            _tstout = tstout;
            _redirectServerOutToTstout = redirectServerOutToTstout;
        }

        public async Task StartAsync(TestTlsOptions tlsOptions, int port = 7233)
        {
#pragma warning disable CS0162 // Unreachable code detected: Using const bools for settings
            if (Config.ExeBinarySource == ExeBinarySource.PrecompiledFromToolsRepo)
            {
                EnsureRunningWindows();
            }
#pragma warning restore CS0162 // Unreachable code detected

            if (IsPortInUse(port))
            {
                TstoutWriteLine();
                TstoutWriteLine($"WARNING!   Something is already listening on local port {port}."
                              + $" We will not be able to start TemporalLite."
                              + Environment.NewLine
                              + TstoutPrefix("WARNING!   However, this is most likely some kind of Temporal server,"
                                           + " so we will not abort based on this.")
                              + Environment.NewLine
                              + TstoutPrefix("WARNING!   Take notice of this, as it may affect any test in an unpredictable manner.")
                              + Environment.NewLine
                              + TstoutPrefix("WARNING!   You may not be running with a test-dedicated TemporalLite instance!"));
                TstoutWriteLine();

                return;
            }

            string temporalLiteExePath = GetTemporalLiteExePath();
            if (!File.Exists(temporalLiteExePath))
            {
                await InstallTemporalLiteAsync(temporalLiteExePath);
            }

            Start(temporalLiteExePath, tlsOptions, port);
        }

#pragma warning disable CS0162 // Unreachable code detected: Using const bools for settings
        private Task InstallTemporalLiteAsync(string temporalLiteExePath)
        {
            switch (Config.ExeBinarySource)
            {
                case ExeBinarySource.PrecompiledFromToolsRepo:
                    InstallTemporalLite_PrecompiledFromToolsRepo(temporalLiteExePath);
                    return Task.CompletedTask;

                case ExeBinarySource.ReleaseBinTemporalLiteRepo:
                    return InstallTemporalLite_ReleaseBinTemporalLiteRepo(temporalLiteExePath);


                case ExeBinarySource.Unspecified:
                default:
                    throw new Exception($"Unexpected value of {nameof(Config)}.{nameof(Config.ExeBinarySource)}: {Config.ExeBinarySource}.");
            }
        }
#pragma warning restore CS0162 // Unreachable code detected

        private async Task InstallTemporalLite_ReleaseBinTemporalLiteRepo(string temporalLiteExePath)
        {
            const string DownloadedArchiveFileName = "Temporalite.Exe.Distro.zip";

            TstoutWriteLine();
            TstoutWriteLine($"TemporalLite executable not found at \"{temporalLiteExePath}\".");
            TstoutWriteLine($"Trying to install ({nameof(Config.ExeBinarySource)}=`{Config.ExeBinarySource}`)...");
            TstoutWriteLine();

            string temporalLiteDirPath = Path.GetDirectoryName(temporalLiteExePath);
            if (Directory.Exists(temporalLiteDirPath))
            {
                TstoutWriteLine($"Destination dir exists ({temporalLiteDirPath}).");
            }
            else
            {
                TstoutWriteLine($"Destination dir does not exist ({temporalLiteDirPath}). Creating...");
                DirectoryInfo destDir = Directory.CreateDirectory(temporalLiteDirPath);

                if (destDir.Exists)
                {
                    TstoutWriteLine($"Destination dir successfully created (${temporalLiteDirPath}).");
                }
                else
                {
                    TstoutWriteLine($"Cound not create destination dir (${temporalLiteDirPath}).");
                    throw new Exception($"Cound not create destination dir for TemporalLite (${temporalLiteDirPath}).");
                }
            }

            string releaseBinArchiveUrl = GetReleaseBinArchiveUrl();
            string downloadedArchiveFilePath = Path.Combine(temporalLiteDirPath, DownloadedArchiveFileName);
            TstoutWriteLine($"RuntimeEnvironmentInfo: {RuntimeEnvironmentInfo.SingletonInstance}");
            TstoutWriteLine($"Downloading TemporalLite from \"{releaseBinArchiveUrl}\"...");

            long dowloadedFileSize = 0;
            using (FileStream downloadOutStream = new(downloadedArchiveFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
                using (HttpClient client = new())
                {
                    using (HttpResponseMessage response = await client.GetAsync(releaseBinArchiveUrl))
                    using (Stream downloadInStream = await response.Content.ReadAsStreamAsync())
                    {
                        await downloadInStream.CopyToAsync(downloadOutStream);
                        dowloadedFileSize = downloadOutStream.Length;
                    }
                }
            }

            if (dowloadedFileSize < 1024)
            {
                TstoutWriteLine($"Finished downloading TemporalLite distribution archive, but only `{dowloadedFileSize}` bytes were received.");
                TstoutWriteLine($"Most likely the remote file at the specified URL does not exist, or there was a network issue.");
                TstoutWriteLine($"Download URL: \"{releaseBinArchiveUrl}\".");
                TstoutWriteLine($"Downloaded data: \"{downloadedArchiveFilePath}\".");
                TstoutWriteLine($"Giving up.");
                throw new Exception($"Cannot get TemporalLite distribution:"
                                  + $" Only `{dowloadedFileSize}` bytes downloaded from"
                                  + $" \"{releaseBinArchiveUrl}\" to \"{downloadedArchiveFilePath}\".");
            }

            TstoutWriteLine($"Finished downloading TemporalLite to \"{downloadedArchiveFilePath}\". Unpacking...");

            using (FileStream unpackInFileStream = new(downloadedArchiveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive archive = new(unpackInFileStream, ZipArchiveMode.Read, leaveOpen: true))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string entryTargetFilePath = Path.Combine(temporalLiteDirPath, entry.FullName);
                    TstoutWriteLine($"  - \"{entryTargetFilePath}\".");
                    entry.ExtractToFile(temporalLiteExePath, overwrite: true);
                }
            }

            TstoutWriteLine($"Unpacking completed.");

            if (File.Exists(temporalLiteExePath))
            {
                TstoutWriteLine($"The expected TemporalLite executable is among the"
                            + $" unpacked files ({temporalLiteExePath}).");
            }
            else
            {
                TstoutWriteLine($"The expected TemporalLite executable is NOT among the unpacked"
                            + $" files ({temporalLiteExePath}). Giving up.");
                throw new Exception($"TemporalLite distributable downloaded and unpacked,"
                                  + $" but the expected TemporalLite executable is NOT among"
                                  + $" the unpacked files ({temporalLiteExePath}).");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string temporalLiteExeName = Path.GetFileName(temporalLiteExePath);
                TstoutWriteLine($"Setting eXecutable mode for the TemporalLite binary (\"{temporalLiteExeName}\")...");

                string escapedTemporalLiteExePath = temporalLiteExePath.Replace("\"", "\\\"");
                ProcessManager chmod = ProcessManager.Start(exePath: "/bin/bash",
                                                            args: $"-c \"chmod -v +x {escapedTemporalLiteExePath}\"",
                                                            waitForInitOptions: null,
                                                            redirectToTstout: true,
                                                            tstoutProcNameMoniker: "bash",
                                                            _tstout);
                chmod.WaitForExit(timeout: 2000);

                TstoutWriteLine($"TemporalLite binary set to be eXecutable.");
            }

            TstoutWriteLine();
            TstoutWriteLine($"TemporalLite has been installed.");
            TstoutWriteLine();
        }

        private string GetReleaseBinArchiveUrl()
        {
            const string ReleaseBinBaseUrl = @"https://github.com/macrogreg/temporalite/releases/download/v0.0.3/";
            const string ReleaseBinArchiveName_Win_x86x64 = "temporalite_0.0.3_Windows_x86_64.zip";

#if NETFRAMEWORK
            return ReleaseBinBaseUrl + ReleaseBinArchiveName_Win_x86x64;
#else
            const string ReleaseBinArchiveName_Linux_x86x64 = "temporalite_0.0.3_Linux_x86_64.zip";
            const string ReleaseBinArchiveName_Linux_Arm64 = "temporalite_0.0.3_Linux_arm64.zip";
            const string ReleaseBinArchiveName_MacOS_x86x64 = "temporalite_0.0.3_Darwin_x86_64.zip";
            const string ReleaseBinArchiveName_MacOS_Arm64 = "temporalite_0.0.3_Darwin_arm64.zip";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    && (RuntimeInformation.OSArchitecture == Architecture.X86
                        || RuntimeInformation.OSArchitecture == Architecture.X64))
            {
                return ReleaseBinBaseUrl + ReleaseBinArchiveName_Win_x86x64;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    && (RuntimeInformation.OSArchitecture == Architecture.X86
                        || RuntimeInformation.OSArchitecture == Architecture.X64))
            {
                return ReleaseBinBaseUrl + ReleaseBinArchiveName_Linux_x86x64;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    && (RuntimeInformation.OSArchitecture == Architecture.Arm64))
            {
                return ReleaseBinBaseUrl + ReleaseBinArchiveName_Linux_Arm64;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    && (RuntimeInformation.OSArchitecture == Architecture.X86
                        || RuntimeInformation.OSArchitecture == Architecture.X64))
            {
                return ReleaseBinBaseUrl + ReleaseBinArchiveName_MacOS_x86x64;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    && (RuntimeInformation.OSArchitecture == Architecture.Arm64))
            {
                return ReleaseBinBaseUrl + ReleaseBinArchiveName_MacOS_Arm64;
            }

            TstoutWriteLine($"Unexpected OS/Architecture: {RuntimeInformation.OSDescription} / {RuntimeInformation.OSArchitecture}.");
            TstoutWriteLine("Giving up.");
            throw new Exception($"Unexpected OS/Architecture: {RuntimeInformation.OSDescription} / {RuntimeInformation.OSArchitecture}.");
#endif
        }

        private void InstallTemporalLite_PrecompiledFromToolsRepo(string temporalLiteExePath)
        {
            const string BuildToolsRepoRootDirName = "temporal-dotnet-buildtools";
            const string TemporalLiteZipDirName = "TemporalLite";
            const string TemporalLiteZipFileName = "temporalite-win-1.16.2.exe.zip";

            const string TemporalLiteExeZipEntryName = "temporalite-1.16.2.exe";

            EnsureRunningWindows();

            TstoutWriteLine();
            TstoutWriteLine($"TemporalLite executable not found at \"{temporalLiteExePath}\".");
            TstoutWriteLine($"Trying to install ({nameof(Config.ExeBinarySource)}=`{Config.ExeBinarySource}`)...");
            TstoutWriteLine();

            string environmentRootDirPath = TestEnvironment.GetEnvironmentRootDirPath();
            TstoutWriteLine($"Root of the Test's Environment: \"{environmentRootDirPath}\".");

            TstoutWriteLine($"Checking for Build Tools Repo under \"{BuildToolsRepoRootDirName}\"...");
            string buildToolsRepoRootPath = Path.Combine(environmentRootDirPath, BuildToolsRepoRootDirName);
            if (!File.Exists(buildToolsRepoRootPath))
            {
                string msg = $"Build Tools Repo directory does not exist (\"{buildToolsRepoRootPath}\")."
                           + $" Did you clone `macrogreg/temporal-dotnet-buildtools`?";
                TstoutWriteLine(msg);
                throw new Exception(msg);
            }

            TstoutWriteLine($"Checking for TemporalLite executable archive...");

            string temporalLiteZipFilePath = Path.Combine(buildToolsRepoRootPath, TemporalLiteZipDirName, TemporalLiteZipFileName);
            if (!File.Exists(temporalLiteZipFilePath))
            {
                TstoutWriteLine($"TemporalLite executable archive cannot be found (\"{temporalLiteZipFilePath}\"). Giving up.");
                throw new Exception($"Build Tools Repo directory is present, but TemporalLite executable archive cannot"
                                  + $" be found under \"{temporalLiteZipFilePath}\".");
            }

            TstoutWriteLine($"Unpacking TemporalLite executable (\"{temporalLiteZipFilePath}\")...");

            string temporalLiteDirPath = Path.GetDirectoryName(temporalLiteExePath);
            if (Directory.Exists(temporalLiteDirPath))
            {
                TstoutWriteLine($"Destination dir exists ({temporalLiteDirPath}).");
            }
            else
            {
                TstoutWriteLine($"Destination dir does not exist ({temporalLiteDirPath}). Creating...");
                DirectoryInfo destDir = Directory.CreateDirectory(temporalLiteDirPath);

                if (destDir.Exists)
                {
                    TstoutWriteLine($"Destination dir successfully created (${temporalLiteDirPath}).");
                }
                else
                {
                    TstoutWriteLine($"Cound not create destination dir (${temporalLiteDirPath}).");
                    throw new Exception($"Cound not create destination dir for the TemporalLite Exe (${temporalLiteDirPath}).");
                }
            }

            using FileStream inFStr = new(temporalLiteZipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using ZipArchive archive = new(inFStr, ZipArchiveMode.Read);

            TstoutWriteLine($"Archive opened. Accessing entry \"{TemporalLiteExeZipEntryName}\".");
            ZipArchiveEntry entry = archive.GetEntry(TemporalLiteExeZipEntryName);

            TstoutWriteLine($"Extracting entry to \"{temporalLiteExePath}\"...");
            entry.ExtractToFile(temporalLiteExePath, overwrite: false);

            TstoutWriteLine($"Extracted.");
            TstoutWriteLine();
            TstoutWriteLine($"TemporalLite has been installed.");
            TstoutWriteLine();
        }

        private string CreateDefaultServerArgs(string @namespace, int port)
        {
            return String.Format(DefaultTemporalLiteProcArgsTemplate, @namespace, port);
        }

        private string CreateTlsServerArgs(bool useMtls)
        {
            string binaryRoot = Environment.CurrentDirectory;
            string certificate = Path.Combine(binaryRoot, TestEnvironment.ServerCertificatePath);
            string key = Path.Combine(binaryRoot, TestEnvironment.ServerKeyPath);
            string ca = Path.Combine(binaryRoot, TestEnvironment.CaCertificatePath);
            if (useMtls)
            {
                return $"{String.Format(TlsTemporalLiteProcArgsTemplate, certificate, key, ca)} --mtls";
            }

            return String.Format(TlsTemporalLiteProcArgsTemplate, certificate, key, ca);
        }

        private string CreateServerArgs(string @namespace, TestTlsOptions tlsOptions, int port)
        {
            switch (tlsOptions)
            {
                case TestTlsOptions.None:
                    return CreateDefaultServerArgs(@namespace, port);
                case TestTlsOptions.Server:
                    return $"{CreateDefaultServerArgs(@namespace, port)} {CreateTlsServerArgs(false)}";
                case TestTlsOptions.Mutual:
                    return $"{CreateDefaultServerArgs(@namespace, port)} {CreateTlsServerArgs(true)}";
                default:
                    throw new ArgumentOutOfRangeException(nameof(tlsOptions), "unexpected tls arguments received");
            }
        }

        private void Start(string temporalLiteExePath, TestTlsOptions tlsOptions, int port)
        {
            const string TemporalLiteNamespace = "default";

            const string TemporalLiteInitCompletedMsg = "worker service started";
            const int TemporalLiteInitTimeoutMillis = 15000;
            string args = CreateServerArgs(TemporalLiteNamespace, tlsOptions, port);
            string temporalLiteProcArgs = String.Format(args, TemporalLiteNamespace);

            try
            {
                _temporalLiteProc = ProcessManager.Start(temporalLiteExePath,
                                                         temporalLiteProcArgs,
                                                         new ProcessManager.WaitForInitOptions(TemporalLiteInitCompletedMsg,
                                                                                               TemporalLiteInitTimeoutMillis),
                                                         _redirectServerOutToTstout,
                                                         "TmprlLt",
                                                         _tstout);

                TstoutWriteLine($"TemporalLite started (ProcId={_temporalLiteProc.Process.Id}).");
            }
            catch (TimeoutException toEx)
            {
                throw new TimeoutException($"Starting/Initializing of TemporalLite timed out after {TemporalLiteInitTimeoutMillis}ms."
                                         + $" Check that TemporalLite is not already running.",
                                           toEx);
            }
        }

        public Task ShutdownAsync()
        {
            ProcessManager temporalLiteProc = Interlocked.Exchange(ref _temporalLiteProc, null);
            if (temporalLiteProc != null)
            {
                Shutdown(temporalLiteProc);
            }

            return Task.CompletedTask;
        }

        private void Shutdown(ProcessManager temporalLiteProc)
        {
            const int CtrlCTimeoutMillis = 50;
            const int KillTimeoutMillis = 100;

            int startMillis = Environment.TickCount;

            if (temporalLiteProc.Process.HasExited)
            {
                TstoutWriteLine($"TemporalLite was already shut down when Shutdown was actually requested."
                              + $" Draining output (timeout={KillTimeoutMillis} msec)...");

                temporalLiteProc.DrainOutput(KillTimeoutMillis);
            }
            else
            {
                TstoutWriteLine($"Shutting down TemporalLite (timeout={CtrlCTimeoutMillis} msec)...");

                if (temporalLiteProc.SendCtrlCAndWaitForExit(CtrlCTimeoutMillis))
                {
                    TstoutWriteLine($"Successfully shut down TemporalLite.");
                }
                else
                {
                    TstoutWriteLine($"Could not gracefully shut down TemporalLite within the timeout ({KillTimeoutMillis} msec)."
                                  + $" Will kill the process.");

                    temporalLiteProc.KillAndWaitForExit(KillTimeoutMillis);
                }
            }

            int elapsedMillis = Environment.TickCount - startMillis;
            TstoutWriteLine($"Shutdown took {elapsedMillis} msec.");
        }

        private static void EnsureRunningWindows()
        {
            if (!TestEnvironment.IsWindows)
            {
                throw new PlatformNotSupportedException($"With the currently specified {nameof(Config)} settings,"
                                                      + $" {nameof(TemporalLiteExeTestServerController)} currently only supports Windows.");
            }
        }

#pragma warning disable CS0162 // Unreachable code detected: Using const bools for settings
        private string GetTemporalLiteExePath()
        {
            const string TemporalLiteExeDirName = "TemporalLite";

            string temporalLiteExeFileName = null;

            if (Config.ExeBinarySource == ExeBinarySource.PrecompiledFromToolsRepo)
            {
                temporalLiteExeFileName = "temporalite-1.16.2.exe";
            }
            else if (Config.ExeBinarySource == ExeBinarySource.ReleaseBinTemporalLiteRepo)

            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    temporalLiteExeFileName = "temporalite.exe";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                        || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    temporalLiteExeFileName = "temporalite";
                }
            }

            if (temporalLiteExeFileName == null)
            {
                string errMsg = $"Could not decide on TemporalLite exe file name."
                              + $" {nameof(Config)}.{nameof(Config.ExeBinarySource)}: {Config.ExeBinarySource}."
                              + $" OSDescription: {RuntimeInformation.OSDescription}.";

                TstoutWriteLine(errMsg);
                TstoutWriteLine("Giving up.");
                throw new Exception(errMsg);
            }

            string binaryRootDirPath = TestEnvironment.GetBinaryRootDirPath();
            return Path.Combine(binaryRootDirPath, TemporalLiteExeDirName, temporalLiteExeFileName);
        }
#pragma warning restore CS0162 // Unreachable code detected

        public void Dispose()
        {
            ShutdownAsync().GetAwaiter().GetResult();
        }

        private static string TstoutPrefix(string text)
        {
            return (text == null) ? text : ("[TmprlLt Ctrl] " + text);
        }

        private void TstoutWriteLine(string text = null)
        {
            if (text == null)
            {
                _tstout.WriteLine(String.Empty);
            }
            else
            {
                _tstout.WriteLine(TstoutPrefix(text));
            }
        }

        private static bool IsPortInUse(int port)

        {
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] ipEndPoints = ipProperties.GetActiveTcpListeners();

            foreach (IPEndPoint endPoint in ipEndPoints)
            {
                if (endPoint.Port == port)
                {
                    return true;

                }
            }

            return false;
        }
    }
}
