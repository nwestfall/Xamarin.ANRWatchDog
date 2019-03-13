using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Android.App;
using Android.Runtime;
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
				Log.Error("ANR-Watchdog-Demo", error, "ANR");
		}

		class DefaultListener : ANRWatchDog.IANRListener
		{
			public void OnAppNotResponding(ANRError error)
			{
				Log.Error("ANR-Watchdog-Demo", "Detected Application Not Responding!");

				// Test serialization
				try
				{
					IFormatter serializeFormatter = new BinaryFormatter();
					using (var stream = new MemoryStream())
					{
						// Serialize
						serializeFormatter.Serialize(stream, error);

						// Deserialize
						IFormatter deserializeFormatter = new BinaryFormatter();
						stream.Position = 0;
						var deserializedError = (ANRError)deserializeFormatter.Deserialize(stream);
						Log.Error("ANR-Watchdog-Demo", error, "Original ANR");
						Log.Error("ANR-Watchdog-Demo", deserializedError, "Deserialized ANR");
					}
				}
				catch(Exception ex)
				{
					throw ex;
				}

				Log.Info("ANR-Watchdog-Demo", "Error was successfully serialized");

				throw error;
			}
		}

		class DefaultInterceptor : ANRWatchDog.IANRInterceptor
		{
			public long Intercept(long duration)
			{
				long ret = (ANRWatchdogTestApplication.duration * 1000) - duration;
				if (ret > 0)
					Log.Warn("ANR-Watchdog-Demo", $"Intercepted ANR that is too short ({duration} ms), postponing for {ret} ms.");
				return ret;
			}
		}

		internal ANRWatchDog _anrWatchDog = new ANRWatchDog(2000);

		internal static int duration = 4;

		internal readonly ANRWatchDog.IANRListener _silentListener = new SilentListener();

		public ANRWatchdogTestApplication(IntPtr intPtr, JniHandleOwnership jniHandleOwnership)
			: base(intPtr, jniHandleOwnership) {  }

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
