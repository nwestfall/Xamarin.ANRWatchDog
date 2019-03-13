using System;

using Android.App;
using Android.Util;

using Xamarin.ANRWatchDog;

namespace ANRTestApp
{
	[Application(
#if DEBUG
		Debuggable = true
#endif
	)]
	public class ANRWatchdogTestApplication : Application
	{
		class SilentListener : ANRWatchDog.IANRListener
		{
			public void OnAppNotResponding(ANRError error) =>
				Log.Error("ANR-Watchdog-Demo", "", error);
		}

		class DefaultListener : ANRWatchDog.IANRListener
		{
			public void OnAppNotResponding(ANRError error)
			{
				Log.Error("ANR-Watchdog-Demo", "Detected Application Not Responding!");

				Log.Info("ANR-Watchdog-Demo", "Error was successfully serialized");

				throw error;
			}
		}

		class DefaultInterceptor : ANRWatchDog.IANRInterceptor
		{
			public long Intercept(long duration)
			{
				long ret = ANRWatchdogTestApplication.duration * 1000 - duration;
				if (ret > 0)
					Log.Warn("ANR-Watchdog-Demo", $"Intercepted ANR that is too short ({duration} ms), postponing for {ret} ms.");
				return ret;
			}
		}

		internal ANRWatchDog _anrWatchDog = new ANRWatchDog(2000);

		internal static int duration = 4;

		internal readonly ANRWatchDog.IANRListener _silentListener = new SilentListener();

		public override void OnCreate()
		{
			base.OnCreate();

			_anrWatchDog
				.SetANRListener(new DefaultListener())
				.SetANRInterceptor(new DefaultInterceptor())
				.Start();
		}
	}
}
