using System;
using System.Threading;

using Android.Annotation;
using Android.App;
using Android.Widget;
using Android.OS;
using Android.Util;

namespace ANRTestApp
{
	[Activity(Label = "ANR Test App", MainLauncher = true)]
	public class MainActivity : Activity
	{
		static readonly object _mutex = new object();

		static void Sleep()
		{
			try
			{
				Thread.Sleep(8 * 1000);
			}
			catch(ThreadInterruptedException e)
			{
			}
		}

		static void InfiniteLoop()
		{
			int i = 0;
			while (true)
				i++;
		}

		class LockerThread : Java.Lang.Thread
		{
			public LockerThread()
			{
				Name = "APP: Locker";
			}

			public override void Run()
			{
				lock(_mutex)
				{
					while (true)
						MainActivity.Sleep();
				}
			}
		}

		void DeadLock()
		{
			new LockerThread().Start();

			new Handler().PostDelayed(() =>
			{
				lock (_mutex)
					Log.Error("ANR-Failed", "There should be a dead lock before this message");
			}, 1000);
		}

		int mode = 0;
		bool crash = true;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

			ANRWatchdogTestApplication application = (ANRWatchdogTestApplication)Application;

			var minAnrDurationButton = FindViewById<Button>(Resource.Id.minAnrDuration);
			minAnrDurationButton.Text = $"{ANRWatchdogTestApplication.duration} seconds";
			minAnrDurationButton.Click += (sender, e) =>
			{
				ANRWatchdogTestApplication.duration = ANRWatchdogTestApplication.duration % 5 + 2;
				minAnrDurationButton.Text = $"{ANRWatchdogTestApplication.duration} seconds";
			};

			var reportModeButton = FindViewById<Button>(Resource.Id.reportMode);
			reportModeButton.Text = "All threads";
			reportModeButton.Click += (sender, e) =>
			{
				mode = (mode + 1) % 3;
				switch(mode)
				{
					case 0:
						reportModeButton.Text = "All threads";
						application._anrWatchDog.SetReportAllThreads();
						break;
					case 1:
						reportModeButton.Text = "Main thread only";
						application._anrWatchDog.SetReportMainThreadOnly();
						break;
					case 2:
						reportModeButton.Text = "Filtered";
						application._anrWatchDog.SetReportThreadNamePrefix("APP:");
						break;
				}
			};

			var behaviorButton = FindViewById<Button>(Resource.Id.behaviour);
			behaviorButton.Text = "Crash";
			behaviorButton.Click += (sender, e) =>
			{
				crash = !crash;
				if(crash)
				{
					behaviorButton.Text = "Crash";
					application._anrWatchDog.SetANRListener(null);
				}
				else
				{
					behaviorButton.Text = "Silent";
					application._anrWatchDog.SetANRListener(application._silentListener);
				}
			};

			FindViewById(Resource.Id.threadSleep).Click += (sender, e) =>
			{
				Sleep();
			};

			FindViewById(Resource.Id.infiniteLoop).Click += (sender, e) =>
			{
				InfiniteLoop();
			};

			FindViewById(Resource.Id.deadlock).Click += (sender, e) =>
			{
				DeadLock();
			};
		}
	}
}

