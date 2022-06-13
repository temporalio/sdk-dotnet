using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Temporal.Util;
using Xunit.Abstractions;

namespace Temporal.TestUtil
{
    internal sealed class ProcessManager : IDisposable
    {
        private readonly Process _process;
        private readonly bool _redirectToTstout;
        private readonly ITestOutputHelper _tstout;
        private readonly string _tstoutProcNameMoniker;

        private ManualResetEventSlim _errorSignal = null;
        private ManualResetEventSlim _outputSignal = null;

        private volatile Action<string> _onOutputInspector = null;

        public record WaitForInitOptions(string InitCompletedMsg, int TimeoutMillis);

        public static ProcessManager Start(string exePath,
                                           string args,
                                           WaitForInitOptions waitForInitOptions,
                                           bool redirectToTstout,
                                           string tstoutProcNameMoniker,
                                           ITestOutputHelper tstout)
        {
            Validate.NotNull(exePath);

            if (waitForInitOptions != null)
            {
                Validate.NotNullOrWhitespace(waitForInitOptions.InitCompletedMsg);
            }

            int startMillis = Environment.TickCount;
            Process proc = new();

            proc.StartInfo.FileName = exePath;

            if (!String.IsNullOrWhiteSpace(args))
            {
                proc.StartInfo.Arguments = args;
            }

            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.RedirectStandardInput = true;

            tstout?.WriteLine($"[ProcMan] Starting proc."
                            + $" (RedirectToTstout={redirectToTstout};"
                            + $" File=\"{proc.StartInfo.FileName}\";"
                            + $" Args={Format.QuoteOrNull(proc.StartInfo.Arguments)})");

            ProcessManager procMan = new(proc, redirectToTstout, tstoutProcNameMoniker, tstout);

            ManualResetEventSlim initSignal = null;
            if (waitForInitOptions != null)
            {
                initSignal = new();
                procMan._onOutputInspector = (s) =>
                {
                    if (s != null && s.IndexOf(waitForInitOptions.InitCompletedMsg) >= 0)
                    {
                        procMan._onOutputInspector = null;
                        initSignal.Set();
                    }
                };
            }

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            tstout?.WriteLine($"[ProcMan] Proc `{proc.Id}` started (TS: {Format.AsReadablePreciseLocal(DateTimeOffset.Now)})."
                            + $" Startup took {(Environment.TickCount - startMillis)} msec so far.");

            if (initSignal != null)
            {
                tstout?.WriteLine($"[ProcMan] Waiting until init-completed-marker is encountered in the proc output"
                                + $" (Timeout={waitForInitOptions.TimeoutMillis}ms; Marker=\"{waitForInitOptions.InitCompletedMsg}\").");

                bool isInitSignalSet = initSignal.Wait(waitForInitOptions.TimeoutMillis);
                //bool isInitSignalSet = initSignal.Wait(Timeout.Infinite);  // for debug
                if (isInitSignalSet)
                {
                    tstout?.WriteLine($"[ProcMan] InitCompletedMsg was encountered.");
                }
                else
                {
                    tstout?.WriteLine($"[ProcMan] InitCompletedMsg was NOT encountered, but timeout was reached.");

                    throw new TimeoutException($"ProcessManager started the target process, but the process did not initialize within the timeout."
                                             + $" File=\"{proc.StartInfo.FileName}\";"
                                             + $" Args={Format.QuoteOrNull(proc.StartInfo.Arguments)};"
                                             + $" Timeout={waitForInitOptions.TimeoutMillis}ms;"
                                             + $" InitCompletedMsg=\"{waitForInitOptions.InitCompletedMsg}\".");
                }
            }

            int elapsedMillis = Environment.TickCount - startMillis;
            tstout?.WriteLine($"[ProcMan] Startup & initialization of proc `{proc.Id}` took {elapsedMillis} msec.");

            return procMan;
        }

        private ProcessManager(Process process, bool redirectToTstout, string tstoutProcNameMoniker, ITestOutputHelper tstout)
        {
            Validate.NotNull(process);

            _process = process;
            _redirectToTstout = redirectToTstout;

            if (redirectToTstout)
            {
                Validate.NotNull(tstoutProcNameMoniker);
                Validate.NotNull(tstout);
            }

            _tstoutProcNameMoniker = tstoutProcNameMoniker;
            _tstout = tstout;

            _process.OutputDataReceived += OnOutputDataReceived;
            _process.ErrorDataReceived += OnErrorDataReceived;
        }

        public Process Process
        {
            get { return _process; }
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (_redirectToTstout)
            {
                _tstout.WriteLine($"[{_tstoutProcNameMoniker}:ERR] {e.Data}");
            }

            ManualResetEventSlim signal = _errorSignal;
            if (signal != null)
            {
                signal.Set();
            }
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (_redirectToTstout)
            {
                _tstout.WriteLine($"[{_tstoutProcNameMoniker}:STD] {e.Data}");
            }

            Action<string> onOutputInspector = _onOutputInspector;
            if (onOutputInspector != null)
            {
                onOutputInspector(e.Data);
            }

            ManualResetEventSlim signal = _outputSignal;
            if (signal != null)
            {
                signal.Set();
            }
        }

        public void SendCtrlC()
        {
            // @ToDo: In the long-term we need to either make the Ctrl-C "signal" work on all
            // OSes, or get rid of it altogether. However, at best this does tests a little
            // neater, so we can get to it after we have collected some more maturity of running
            // integration tests on different versions of Temporal and under different OSes.
#if NETFRAMEWORK
            bool isSucc = GenerateConsoleCtrlEvent(CtrlEvent.CtrlC, _process.Id);
            _process.StandardInput.Flush();
#else
            bool isSucc = false;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                isSucc = GenerateConsoleCtrlEvent(CtrlEvent.CtrlC, _process.Id);
                _process.StandardInput.Flush();
            }
#endif

            _process.CloseMainWindow();
            _process.StandardInput.Write("\x3");
            _process.StandardInput.Flush();
            _process.StandardInput.Close();

            _tstout?.WriteLine($"[ProcMan] Sent Ctrl-C to proc `{_process.Id}` (isSucc={isSucc}).");
        }

        public bool SendCtrlCAndWaitForExit(int timeout = Timeout.Infinite)
        {
            SendCtrlC();

            int startMillis = (timeout == Timeout.Infinite) ? 0 : Environment.TickCount;

            _process.WaitForExit(timeout);

            if (timeout != Timeout.Infinite)
            {
                int elapsedMillis = Environment.TickCount - startMillis;
                timeout = Math.Max(1, timeout - elapsedMillis);
            }

            DrainOutput(timeout);
            return _process.HasExited;
        }

        public bool KillAndWaitForExit(int timeout = Timeout.Infinite)
        {
            try
            {
                _process.Kill();
            }
            catch (InvalidOperationException)
            {
            }

            int startMillis = (timeout == Timeout.Infinite) ? 0 : Environment.TickCount;

            _process.WaitForExit(timeout);

            if (timeout != Timeout.Infinite)
            {
                int elapsedMillis = Environment.TickCount - startMillis;
                timeout = Math.Max(1, timeout - elapsedMillis);
            }

            DrainOutput(timeout);
            return _process.HasExited;
        }

        public bool WaitForExit(int timeout = Timeout.Infinite)
        {
            int startMillis = (timeout == Timeout.Infinite) ? 0 : Environment.TickCount;

            _process.WaitForExit(timeout);

            if (timeout != Timeout.Infinite)
            {
                int elapsedMillis = Environment.TickCount - startMillis;
                timeout = Math.Max(1, timeout - elapsedMillis);
            }

            DrainOutput(timeout);
            return _process.HasExited;
        }

        public bool DrainOutput(int timeout = Timeout.Infinite)
        {
            _errorSignal = new ManualResetEventSlim();
            _outputSignal = new ManualResetEventSlim();

            try
            {
                return _outputSignal.Wait(timeout) && _errorSignal.Wait(timeout);
            }
            catch (ArgumentOutOfRangeException aorEx)
            {
                throw new ArgumentOutOfRangeException($"{nameof(timeout)}={timeout}", aorEx);
            }
        }

        public void Dispose()
        {
            _process.OutputDataReceived -= OnOutputDataReceived;
            _process.ErrorDataReceived -= OnErrorDataReceived;

            ManualResetEventSlim errorMutex = Interlocked.Exchange(ref _errorSignal, null);
            if (errorMutex != null)
            {
                errorMutex.Dispose();
            }

            ManualResetEventSlim outputMutex = Interlocked.Exchange(ref _outputSignal, null);
            if (outputMutex != null)
            {
                outputMutex.Dispose();
            }

            _process.Dispose();
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GenerateConsoleCtrlEvent(CtrlEvent dwCtrlEvent, int dwProcessGroupId);

        private enum CtrlEvent
        {
            CtrlC = 0,
            CtrlBreak = 1
        }
    }
}
