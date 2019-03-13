using Interlocked = System.Threading.Interlocked;

using Android.OS;
using Android.Util;

using Java.Lang;

namespace Xamarin.ANRWatchDog
{
    /// <summary>
    /// A watchdog timer thread that detects when the UI thread has frozen
    /// </summary>
    public class ANRWatchDog : Thread, IRunnable
    {
		/// <summary>
		/// ANR Listener.
		/// </summary>
        public interface IANRListener
        {
			/// <summary>
			/// Called when an ANR is dtected
			/// </summary>
			/// <param name="error">The Error describing the ANR.</param>
            void OnAppNotResponding(ANRError error);
        }

		public interface IANRInterceptor
		{
			/// <summary>
			/// Called when main thread has froze more time than defined by the timeout
			/// </summary>
			/// <returns>0 or negative if the ANR should be reported immmediately.  A positive number of ms to postpone the reporting.</returns>
			/// <param name="duration">The minimum time (in ms) the main thread has been froze (may be more).</param>
			long Intercept(long duration);
		}

		/// <summary>
		/// Interruption listener.
		/// </summary>
		public interface IInterruptionListener
        {
            void OnInterrupted(InterruptedException e);
        }

        private const int DEFAULT_ANR_TIMEOUT = 5000;

        private class DefaultANRListener : IANRListener
        {
            public void OnAppNotResponding(ANRError error) => throw error;
        }

		public class DefaultANRInterceptor : IANRInterceptor
		{
			public long Intercept(long duration) => 0L;
		}

		private class DefaultInterruptionListener : IInterruptionListener
        {
            public void OnInterrupted(InterruptedException e) => Log.Warn("ANRWatchdog", $"Interrupted: {e.Message}");
        }

        private IANRListener _anrListener = new DefaultANRListener();
		private IANRInterceptor _anrInterceptor = new DefaultANRInterceptor();
        private IInterruptionListener _interruptionListener = new DefaultInterruptionListener();

        private readonly Handler _uiHandler = new Handler(Looper.MainLooper);
		public readonly int TimeoutInterval;

		private string _namePrefix = "";
        private bool _logThreadsWithoutStackTrace = false;
        private bool _ignoreDebugger = false;
		private bool _isDisposed = false;

        private long _tick = 0L;
		private volatile bool _reported = false;

        /// <summary>
        /// Constructs a watchdog that checks the UI thread every <see cref="DEFAULT_ANR_TIMEOUT"/> milliseconds
        /// </summary>
        public ANRWatchDog() : this(DEFAULT_ANR_TIMEOUT) { }

        /// <summary>
        /// Constructs a watchdog that checks the ui thread every given interval
        /// </summary>
        /// <param name="timeoutInterval">The interval in milliseconds, between to checks of the UI thread.  It is therefore the maximum time the UI may freeze before being reported an ANR</param>
        public ANRWatchDog(int timeoutInterval) : base() => TimeoutInterval = timeoutInterval;

        /// <summary>
        /// Sets an interface for when an ANR is detected
        /// </summary>
        /// <param name="listener">If not set, the default behavior is to throw an error and crash the application</param>
        /// <returns>itself for chaining</returns>
        public ANRWatchDog SetANRListener(IANRListener listener)
        {
            _anrListener = listener ?? new DefaultANRListener();
            return this;
        }

		/// <summary>
		/// Sets an interface to intercept ANRs before they are reported
		/// If you set, you can define if, given the current duration of the detected ANR and external context, it is necessary to report the ANR.
		/// </summary>
		/// <returns>itself for changing.</returns>
		/// <param name="interceptor">The new interceptor or null.</param>
		public ANRWatchDog SetANRInterceptor(IANRInterceptor interceptor)
		{
			_anrInterceptor = interceptor ?? new DefaultANRInterceptor();
			return this;
		}

		/// <summary>
		/// Sets an interface for when the watchdog thread is interrupted.
		/// If not set, the default behavior is to just log the interruption message
		/// </summary>
		/// <param name="listener">The new listener or null</param>
		/// <returns>itself for chaining</returns>
		public ANRWatchDog SetInterruptionListener(IInterruptionListener listener)
        {
			_interruptionListener = listener ?? new DefaultInterruptionListener();
            return this;
        }

        /// <summary>
        /// Set thre prefix that a thread's name must have for the thread to be reported
        /// Not that the main thread is always reported
        /// Default "".
        /// </summary>
        /// <param name="prefix">The thread name's prefix for a thread to be reported</param>
        /// <returns>itself for chaining</returns>
        public ANRWatchDog SetReportThreadNamePrefix(string prefix)
        {
            _namePrefix = prefix ?? string.Empty;
            return this;
        }

        /// <summary>
        /// Set that only the main thread will be reported
        /// </summary>
        /// <returns>itself for chaining</returns>
        public ANRWatchDog SetReportMainThreadOnly()
        {
            _namePrefix = null;
            return this;
        }

		/// <summary>
		/// Sets that all threads will be reported (default behavior).
		/// </summary>
		/// <returns>Itself for changing.</returns>
		public ANRWatchDog SetReportAllThreads()
		{
			_namePrefix = string.Empty;
			return this;
		}

		/// <summary>
		/// Set that all running threads will be reported,
		/// even those from which no stack trace could be extracted
		/// Default false
		/// </summary>
		/// <param name="logThreadsWithoutStackTrace">Whether or not all running threads should be reported</param>
		/// <returns>itself for chaining</returns>
		public ANRWatchDog SetLogThreadsWithoutStackTrace(bool logThreadsWithoutStackTrace)
        {
            _logThreadsWithoutStackTrace = logThreadsWithoutStackTrace;
            return this;
        }

        /// <summary>
        /// Set whether to ignore the debugger when detecting ANRs
        /// When ingoring the debugger, ANRWatchdog will detect ANRs even if the debugger is connected.
        /// By default, it does not, to avoid interpreting debugging pauses as ANRs.
        /// Default false.
        /// </summary>
        /// <param name="ignoreDebugger">Whenter to ignore the debugger</param>
        /// <returns>itself for chaining</returns>
        public ANRWatchDog SetIgnoreDebugger(bool ignoreDebugger)
        {
            _ignoreDebugger = ignoreDebugger;
            return this;
        }

		/// <summary>
		/// Run this instance.
		/// </summary>
        public override void Run()
        {
            Name = "|ANR-WatchDog|";

			long interval = TimeoutInterval;
			while (!_isDisposed && IsAlive && !IsInterrupted)
			{
				bool needPost = Interlocked.Read(ref _tick) == 0;
				Interlocked.Exchange(ref _tick, _tick + interval);
				if (needPost)
				{
					_uiHandler.Post(() =>
						{
							Interlocked.Exchange(ref _tick, 0);
							_reported = false;
						});
				}

				try
				{
					Sleep(interval);
				}
				catch (InterruptedException e)
				{
					_interruptionListener?.OnInterrupted(e);
				}

				//If the main thread has not handled _ticker, it is blocked. ANR
				if(Interlocked.Read(ref _tick) != 0 && !_reported)
				{
					//noinspection ConstantConditions
					if (!_ignoreDebugger &&
						(Debug.IsDebuggerConnected || Debug.WaitingForDebugger()) // Android Debug
						||
						(System.Diagnostics.Debugger.IsAttached)) // c# debug
					{
						Log.Warn("ANRWatchdog", "An ANR was detected but ignored because the debugger is connected (you can prevent this with setIgnoreDebugger(true))");
						_reported = true;
						continue;
					}

					interval = _anrInterceptor.Intercept(Interlocked.Read(ref _tick));
					if (interval > 0)
						continue;

					ANRError error = (_namePrefix != null) ? ANRError.New(Interlocked.Read(ref _tick), _namePrefix, _logThreadsWithoutStackTrace) : ANRError.NewMainOnly(Interlocked.Read(ref _tick));
					_anrListener?.OnAppNotResponding(error);
					interval = TimeoutInterval;
					_reported = true;
				}
			}
        }

		/// <summary>
		/// Dispose the specified disposing.
		/// </summary>
		/// <param name="disposing">If set to <c>true</c> disposing.</param>
		protected override void Dispose(bool disposing)
		{
			if(!_isDisposed && disposing)
			{
				_isDisposed = true;

				_anrListener = null;
				_interruptionListener = null;
			}

			base.Dispose(disposing);
		}
	}
}