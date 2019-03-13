using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security;
using Android.OS;

using Java.Lang;

namespace Xamarin.ANRWatchDog
{
	/// <summary>
	/// Error thrown by <see cref="ANRWatchDog"/> when an ANR is detected.
	/// Contains the stack trace of the frozen UI thread.
	/// 
	/// It is important to notice that, in an ANRError, all the "Caused by" are not really the cause
	/// of the exception.  Each "Caused by" is the stack trace of the running thread.  Note that the main
	/// thread always comes first.
	/// </summary>
	[Serializable]
	public class ANRError : Error, ISerializable
    {
		[Serializable]
        private class _ThreadTrace : ISerializable
        {
			/// <summary>
			/// Gets the name.
			/// </summary>
			/// <value>The name.</value>
            public static string Name { get; private set; }
			/// <summary>
			/// Gets the stack trace.
			/// </summary>
			/// <value>The stack trace.</value>
            public static StackTraceElement[] StackTrace { get; private set; }

			[Serializable]
            internal class _Thread : Throwable, ISerializable
            {
				[SecuritySafeCritical]
				protected _Thread(SerializationInfo info, StreamingContext context)
					: base(info.GetString("Name"))
				{
				}

				internal _Thread(_Thread other) : base(Name, other) { }

                public override Throwable FillInStackTrace()
                {
                    SetStackTrace(_ThreadTrace.StackTrace);
                    return this;
                }

				public override void GetObjectData(SerializationInfo info, StreamingContext context)
				{
					base.GetObjectData(info, context);

					info.AddValue("Name", Name);
				}
			}

			[SecuritySafeCritical]
			protected _ThreadTrace(SerializationInfo info, StreamingContext context)
			{
				Name = info.GetString("Name");
				StackTrace = (StackTraceElement[])info.GetValue("StackTrace", typeof(StackTraceElement[]));
			}

			internal _ThreadTrace(string name, StackTraceElement[] stackTrace)
            {
                Name = name;
                StackTrace = stackTrace;
            }

			public void GetObjectData(SerializationInfo info, StreamingContext context)
			{
				info.AddValue("Name", Name);
				info.AddValue("StackTrace", StackTrace);
			}
		}

		private const long SERIAL_VERSION_UID = 1L;
        
		public readonly long Duration;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Xamarin.ANRWatchDog.ANRError"/> class.
		/// Used for Serialization
		/// </summary>
		/// <param name="info">Info.</param>
		/// <param name="context">Context.</param>
		[SecuritySafeCritical]
		protected ANRError(SerializationInfo info, StreamingContext context)
		{
			Duration = info.GetInt64("Duration");
			base.InitCause((Throwable)info.GetValue("Cause", typeof(Throwable)));
		}

		private ANRError(_ThreadTrace._Thread st, long duration) : 
        	base("Application Not Responding", st)
		{
			Duration = duration;
		}

		/// <summary>
		/// Fills the in stack trace.
		/// </summary>
		/// <returns>The in stack trace.</returns>
		public override Throwable FillInStackTrace()
        {
            SetStackTrace(new StackTraceElement[] { });
            return this;
        }

		/// <summary>
		/// New the specified prefix and logThreadsWithoutStackTrace.
		/// </summary>
		/// <returns>The new.</returns>
		/// <param name="prefix">Prefix.</param>
		/// <param name="logThreadsWithoutStackTrace">If set to <c>true</c> log threads without stack trace.</param>
        public static ANRError New(long duration, string prefix, bool logThreadsWithoutStackTrace)
        {
            var mainThread = Looper.MainLooper.Thread;

            var threadComparer = new _StackTraceComparer(mainThread);

            var stackTraces = new Dictionary<Thread, StackTraceElement[]>(threadComparer);

            foreach(var entry in Thread.AllStackTraces)
            {
                if (entry.Key == mainThread || (entry.Key.Name.StartsWith(prefix, StringComparison.Ordinal) && (logThreadsWithoutStackTrace || entry.Value.Length > 0)))
                    stackTraces.Add(entry.Key, entry.Value);
            }

            // Sometimes main is not returned in Thread.AllStackTraces - ensure that we list it
            if (!stackTraces.ContainsKey(mainThread))
                stackTraces.Add(mainThread, mainThread.GetStackTrace());

            _ThreadTrace._Thread tst = null;
            foreach (var entry in stackTraces)
            {
                var tt = new _ThreadTrace(GetThreadTitle(entry.Key), entry.Value);
                tst = new _ThreadTrace._Thread(tst);
            }

            return new ANRError(tst, duration);
        }

		/// <summary>
		/// News the main only.
		/// </summary>
		/// <returns>The main only.</returns>
        public static ANRError NewMainOnly(long duration)
        {
            var mainThread = Looper.MainLooper.Thread;
            var mainStackTrace = mainThread.GetStackTrace();

            var tt = new _ThreadTrace(GetThreadTitle(mainThread), mainStackTrace);
            var tst = new _ThreadTrace._Thread(null);

            return new ANRError(tst, duration);
        }

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);

			info.AddValue("Duration", Duration);
			info.AddValue("Cause", Cause);
		}

		private static string GetThreadTitle(Thread thread) => $"{thread.Name} (state = {thread.GetState()})";

        private class _StackTraceComparer : IEqualityComparer<Thread>
        {
            private readonly Thread _mainThread;

			/// <summary>
			/// Initializes a new instance of the <see cref="T:Xamarin.ANRWatchDog.ANRError._StackTraceComparer"/> class.
			/// </summary>
			/// <param name="mainThread">Main thread.</param>
            public _StackTraceComparer(Thread mainThread) => _mainThread = mainThread;

			/// <summary>
			/// Equals the specified lhs and rhs.
			/// </summary>
			/// <returns>The equals.</returns>
			/// <param name="lhs">Lhs.</param>
			/// <param name="rhs">Rhs.</param>
            public bool Equals(Thread lhs, Thread rhs)
            {
                if (lhs == rhs)
                    return true;
                if (lhs == _mainThread)
                    return false;
                if (rhs == _mainThread)
                    return false;
                return rhs.Name.Equals(lhs.Name);
            }

			/// <summary>
			/// Gets the hash code.
			/// </summary>
			/// <returns>The hash code.</returns>
			/// <param name="obj">Object.</param>
            public int GetHashCode(Thread obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}
