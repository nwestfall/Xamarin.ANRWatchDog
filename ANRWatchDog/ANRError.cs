using System;
using System.Collections.Generic;

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
    public class ANRError : Error
    {
        [Serializable]
        private class _ThreadTrace
        {
            public static string Name { get; private set; }
            public static StackTraceElement[] StackTrace { get; private set; }

            internal class _Thread : Throwable
            {
                internal _Thread(_Thread other) : base(Name, other) { }

                public override Throwable FillInStackTrace()
                {
                    SetStackTrace(_ThreadTrace.StackTrace);
                    return this;
                }
            }

            internal _ThreadTrace(string name, StackTraceElement[] stackTrace)
            {
                Name = name;
                StackTrace = stackTrace;
            }
        }

        private const long SERIAL_VERSION_UID = 1L;

        private ANRError(_ThreadTrace._Thread st) : base("Application Not Responding", st) { }

        public override Throwable FillInStackTrace()
        {
            SetStackTrace(new StackTraceElement[] { });
            return this;
        }

        public static ANRError New(string prefix, bool logThreadsWithoutStackTrace)
        {
            var mainThread = Looper.MainLooper.Thread;

            var threadComparer = new _StackTraceComparer(mainThread);

            var stackTraces = new Dictionary<Thread, StackTraceElement[]>(threadComparer);

            foreach(var entry in Thread.AllStackTraces)
            {
                if (entry.Key == mainThread || (entry.Key.Name.StartsWith(prefix) && (logThreadsWithoutStackTrace || entry.Value.Length > 0)))
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

            return new ANRError(tst);
        }

        public static ANRError NewMainOnly()
        {
            var mainThread = Looper.MainLooper.Thread;
            var mainStackTrace = mainThread.GetStackTrace();

            var tt = new _ThreadTrace(GetThreadTitle(mainThread), mainStackTrace);
            var tst = new _ThreadTrace._Thread(null);

            return new ANRError(tst);
        }

        private static string GetThreadTitle(Thread thread) => $"{thread.Name} (state = {thread.GetState()})";

        private class _StackTraceComparer : IEqualityComparer<Thread>
        {
            private readonly Thread _mainThread;

            public _StackTraceComparer(Thread mainThread) => _mainThread = mainThread;

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

            public int GetHashCode(Thread obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}
