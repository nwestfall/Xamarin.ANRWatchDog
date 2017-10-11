[![Build status](https://ci.appveyor.com/api/projects/status/na0iqlm9j0vt3ps3?svg=true)](https://ci.appveyor.com/project/nwestfall/xamarin-anrwatchdog)
[![NuGet version](https://badge.fury.io/nu/Xamarin.ANRWatchDog.svg)](https://badge.fury.io/nu/Xamarin.ANRWatchDog)

# Xamarin.ANRWatchDog
Xamarin port of [https://github.com/SalomonBrys/ANR-WatchDog](https://github.com/SalomonBrys/ANR-WatchDog)

A simple watchdog that detects Android ANRs (Application Not Responding).


Table of contents
-----------------

  * [ANR-WatchDog](#anr-watchdog)
    * [Table of contents](#table-of-contents)
    * [Why it exists](#why-it-exists)
    * [What it does](#what-it-does)
    * [Can it work with crash reporters?](#can-it-work-with-crash-reporters)
    * [How it works](#how-it-works)
  * [Usage](#usage)
    * [Install](#install)
      * [Nuget](#nuget)
    * [Reading the ANRError exception report](#reading-the-anrerror-exception-report)
    * [Configuration](#configuration)
      * [Timeout (minimum hanging time for an ANR)](#timeout-minimum-hanging-time-for-an-anr)
      * [Debugger](#debugger)
      * [On ANR callback](#on-anr-callback)
      * [Filtering reports](#filtering-reports)
      * [Watchdog thread](#watchdog-thread)

Why it exists
-------------

There is currently no way for an android application to catch and report ANR errors.  
If your application is not in the play store (either because you are still developing it or because you are distributing it differently), the only way to investigate an ANR is to pull the file /data/anr/traces.txt.  
Additionally, we found that using the Play Store was not as effective as being able to choose our own bug tracking service.

There is an [issue entry](https://code.google.com/p/android/issues/detail?id=35380) in the android bug tracker describing this lack, feel free to star it ;)


What it does
------------

It sets up a "watchdog" timer that will detect when the UI thread stops responding. When it does, it raises an error with all threads stack traces (main first).


Can it work with crash reporters?
---------------------------------

Yes! I'm glad you asked: That's the reason why it was developed in the first place!  
As this throws an error, a crash handler can intercept it and handle it the way it needs.

Known working crash reporters include:

 * [ACRA](https://github.com/ACRA/acra)
 * [Crashlytics](https://get.fabric.io/crashlytics)
 * [HockeyApp](http://hockeyapp.net/)

And there is no reason why it should not work with *[insert your favourite crash reporting system here]*.


How it works
------------

The watchdog is a simple thread that does the following in a loop:

1.  Schedules a runnable to be run on the UI thread as soon as possible.
2.  Wait for 5 seconds. (5 seconds is the default, but it can be configured).
3.  See if the runnable has been run. If it has, go back to 1.
4.  If the runnable has not been run, which means that the UI thread has been blocked for at least 5 seconds, it raises an error with all running threads stack traces.



Usage
=====


Install
-------

### Nuget
```
Install-Package Xamarin.ANRWatchDog
```


Reading the ANRError exception report
-------------------------------------

The `ANRError` stack trace is a bit particular, it has the stack traces of all the threads running in your application. So, in the report, **each `caused by` section is not the cause of the precedent exception**, but the stack trace of a different thread.

Here is a dead lock example:

    FATAL EXCEPTION: |ANR-WatchDog|
        Process: anrwatchdog.github.com.testapp, PID: 26737
        com.github.anrwatchdog.ANRError: Application Not Responding
        Caused by: com.github.anrwatchdog.ANRError$_$_Thread: main (state = WAITING)
            at testapp.MainActivity$1.run(MainActivity.java:46)
            at android.os.Handler.handleCallback(Handler.java:739)
            at android.os.Handler.dispatchMessage(Handler.java:95)
            at android.os.Looper.loop(Looper.java:135)
            at android.app.ActivityThread.main(ActivityThread.java:5221)
        Caused by: com.github.anrwatchdog.ANRError$_$_Thread: APP: Locker (state = TIMED_WAITING)
            at java.lang.Thread.sleep(Native Method)
            at java.lang.Thread.sleep(Thread.java:1031)
            at java.lang.Thread.sleep(Thread.java:985)
            at testapp.MainActivity.SleepAMinute(MainActivity.java:18)
            at testapp.MainActivity.access$100(MainActivity.java:12)
            at testapp.MainActivity$LockerThread.run(MainActivity.java:36)

From this report, we can see that the stack traces of two threads. The first (the "main" thread) is stuck at `MainActivity.java:46` while the second thread (named "App: Locker") is locked in a Sleep at `MainActivity.java:18`.  
From there, if we looked at those two lines, we would surely understand the cause of the dead lock!

Note that some crash reporting library (such as Crashlytics) report all thread stack traces at the time of an uncaught exception. In that case, having all threads in the same exception can be cumbersome. In such cases, simply use `setReportMainThreadOnly()`.


Configuration
-------------


### Timeout (minimum hanging time for an ANR)

* To set a different timeout (5000 millis is the default):

    ```C#
    if (BuildConfig.DEBUG == false) {
        new ANRWatchDog(10000 /*timeout*/).Start();
    }
    ```


### Debugger

* By default, the watchdog will ignore ANRs if the debugger is attached. This is because it detects execution pauses and breakpoints as ANRs.
To disable this and throw an `ANRError` even if the debugger is connected, you can add `setIgnoreDebugger(true)`:

    ```C#
    new ANRWatchDog().SetIgnoreDebugger(true).Start();
    ```


### On ANR callback

* If you would prefer not to crash the application when an ANR is detected, you can enable a callback instead:

    ```C#
    public class MyANRListener : ANRWatchDog.IANRListener
    {
      public void OnAppNotResponding(ANRError error)
      {
        //Handle the error.  For example, log it to HockeyApp:
        ExceptionHandler.SaveException(error, new CrashManager());
      }
    }
    
    new ANRWatchDog().SetANRListener(new MyANRListener()).Start();
    ```

    **This is very important when delivering your app in production.** When in the hand of the final user, it's *probably better* not to crash after 5 seconds, but simply report the ANR to whatever reporting system you use. Maybe, after some more seconds, the app will "de-freeze".

### Filtering reports

* If you would like to have only your own threads to be reported in the ANRError, and not all threads (including system threads such as the `FinalizerDaemon` thread), you can set a prefix: only the threads whose name starts with this prefix will be reported.

    ```C#
    new ANRWatchDog().SetReportThreadNamePrefix("APP:").Start();
    ```

    Then, when you start a thread, don't forget to set its name to something that starts with this prefix (if you want it to be reported):

    ```C#
    public class MyAmazingThread : Thread {
        public override void Run() {
            NAME = "APP: Amazing!";
            /* ... do amazing things ... */
        }
    }
    ```

* If you want to have only the main thread stack trace and not all the other threads (like in version 1.0), you can:

    ```C#
    new ANRWatchDog().SetReportMainThreadOnly().Start();
    ```


### Watchdog thread

* ANRWatchDog is a thread, so you can interrupt it at any time.

* If you are programming with Android's multi process capability (like starting an activity in a new process), remember that you will need an ANRWatchDog thread per process.
