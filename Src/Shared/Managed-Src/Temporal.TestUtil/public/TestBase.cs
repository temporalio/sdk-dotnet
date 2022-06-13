using System;
using System.Threading;
using System.Threading.Tasks;
using Temporal.Util;
using Xunit;
using Xunit.Abstractions;

namespace Temporal.TestUtil
{
    public class TestBase : IAsyncLifetime, IDisposable
    {
        private readonly ITestOutputHelper _tstout;
        private volatile int _isDisposed = 0;
        private string _tstoutWriteLineMoniker = null;

        /// <summary>Prevents subclasses from not passing the required parameters by making the default ctor private.</summary>
        private TestBase()
        {
        }

        protected TestBase(ITestOutputHelper tstout)
        {
            Validate.NotNull(tstout);
            _tstout = tstout;

            TstoutWriteLine($"{this.GetType().Name}: {RuntimeEnvironmentInfo.SingletonInstance}");
        }

        public virtual ITestOutputHelper Tstout
        {
            get { return _tstout; }
        }

        public string TstoutWriteLineMoniker
        {
            get { return _tstoutWriteLineMoniker; }
            set { _tstoutWriteLineMoniker = value; }
        }

        public virtual string TstoutPrefixLine(string text)
        {
            if (text == null)
            {
                return null;
            }

            string tstoutWriteLineMoniker = TstoutWriteLineMoniker;
            if (tstoutWriteLineMoniker == null)
            {
                return text;
            }

            return '[' + TstoutWriteLineMoniker + ']' + text;
        }

        public virtual void TstoutWriteLine(string text = null)
        {
            if (text == null)
            {
                _tstout.WriteLine(String.Empty);
            }
            else
            {
                _tstout.WriteLine(TstoutPrefixLine(text));
            }
        }

        public virtual Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public virtual Task DisposeAsync()
        {
            if (0 == Interlocked.Exchange(ref _isDisposed, 1))
            {
                Dispose(isDisposingSync: false, isDisposingAsync: true, isFinalizing: false);
            }

            return Task.CompletedTask;
        }

        protected virtual void Dispose(bool isDisposingSync, bool isDisposingAsync, bool isFinalizing)
        {
            if (isFinalizing)
            {
                Tstout.WriteLine($"{nameof(Dispose)}(..) method was called from the finalizer."
                               + $" This might indicate a problem with the test setup."
                               + $" Test class: \"{this.GetType().FullName}\".");
            }

            int c = 0;
            c += isDisposingSync ? 1 : 0;
            c += isDisposingAsync ? 1 : 0;
            c += isFinalizing ? 1 : 0;

            if (c != 1)
            {
                Tstout.WriteLine($"During {nameof(Dispose)}(..) exactly one of the invoker flags must be True. However, {c} such flags are True:"
                               + $" isDisposingSync={isDisposingSync}; isDisposingAsync={isDisposingAsync}; isFinalizing={isFinalizing}."
                               + $" This might indicate a problem with the test setup."
                               + $" Test class: \"{this.GetType().FullName}\".");
            }

            _isDisposed = 1;
        }

        ~TestBase()
        {
            if (0 == Interlocked.Exchange(ref _isDisposed, 1))
            {
                Dispose(isDisposingSync: false, isDisposingAsync: false, isFinalizing: true);
            }
        }

        public void Dispose()
        {
            if (0 == Interlocked.Exchange(ref _isDisposed, 1))
            {
                Dispose(isDisposingSync: true, isDisposingAsync: false, isFinalizing: false);
                GC.SuppressFinalize(this);
            }
        }
    }
}
