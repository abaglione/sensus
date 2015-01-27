#region copyright
// Copyright 2014 The Rector & Visitors of the University of Virginia
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.OS;
using Android.Provider;
using Android.Speech;
using Android.Support.V4.Content;
using Android.Widget;
using SensusService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xamarin;
using Xamarin.Geolocation;

namespace Sensus.Android
{
    public class AndroidSensusServiceHelper : SensusServiceHelper
    {
        public const string INTENT_EXTRA_NAME_PING = "PING-SENSUS";

        private AndroidSensusService _service;
        private ConnectivityManager _connectivityManager;
        private readonly string _deviceId;
        private AndroidMainActivity _mainActivity;
        private ManualResetEvent _mainActivityWait;
        private readonly object _getMainActivityLocker = new object();
        private AndroidTextToSpeech _textToSpeech;

        public AndroidSensusService Service
        {
            get { return _service; }
        }

        public override string DeviceId
        {
            get { return _deviceId; }
        }

        public override bool WiFiConnected
        {
            get { return _connectivityManager.GetNetworkInfo(ConnectivityType.Wifi).IsConnected; }
        }

        public override bool IsCharging
        {
            get
            {
                IntentFilter filter = new IntentFilter(Intent.ActionBatteryChanged);
                BatteryStatus status = (BatteryStatus)_service.RegisterReceiver(null, filter).GetIntExtra(BatteryManager.ExtraStatus, -1);
                return status == BatteryStatus.Charging || status == BatteryStatus.Full;
            }
        }

        public override bool DeviceHasMicrophone
        {
            get { return PackageManager.FeatureMicrophone == "android.hardware.microphone"; }
        }

        public AndroidSensusServiceHelper(AndroidSensusService service)
            : base(new Geolocator(service))
        {
            _service = service;
            _connectivityManager = _service.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
            _deviceId = Settings.Secure.GetString(_service.ContentResolver, Settings.Secure.AndroidId);
            _textToSpeech = new AndroidTextToSpeech(_service);
            _mainActivityWait = new ManualResetEvent(false);
        }

        public Task<AndroidMainActivity> GetMainActivityAsync(bool foreground)
        {
            // this is asynchronous because it blocks while waiting for the main activity to start. but the new main activity runs on the UI 
            // thread, so if this method would be called from the main thread you'll get a deadlock. none of that will happen, though, since
            // this is all done asynchronously.
            return Task.Run(() =>
                {
                    lock (_getMainActivityLocker)
                    {
                        if (_mainActivity == null || (foreground && !_mainActivity.IsForegrounded))
                        {
                            Logger.Log("Main activity is not started or is not in the foreground. Starting it.", LoggingLevel.Normal);

                            // start the activity and wait for it to bind itself to the service
                            Intent intent = new Intent(_service, typeof(AndroidMainActivity));
                            intent.AddFlags(ActivityFlags.NewTask);

                            _mainActivityWait.Reset();
                            _service.StartActivity(intent);
                            _mainActivityWait.WaitOne();
                        }

                        return _mainActivity;
                    }
                });
        }

        public void SetMainActivity(AndroidMainActivity value)
        {
            _mainActivity = value;

            if (_mainActivity == null)
                Logger.Log("Main activity has been unset.", LoggingLevel.Normal);
            else
            {
                Logger.Log("Main activity has been set.", LoggingLevel.Normal);
                _mainActivityWait.Set();
            }
        }

        protected override void InitializeXamarinInsights()
        {
            Insights.Initialize(XAMARIN_INSIGHTS_APP_KEY, Application.Context);  // can't reference _service here since this method is called from the base class constructor.
        }

        public override Task<string> PromptForAndReadTextFileAsync(string promptTitle)
        {
            return Task.Run<string>(async () =>
                {
                    try
                    {
                        Intent intent = new Intent(Intent.ActionGetContent);
                        intent.SetType("*/*");
                        intent.AddCategory(Intent.CategoryOpenable);

                        Tuple<Result, Intent> result = await(await GetMainActivityAsync(true)).GetActivityResultAsync(intent, AndroidActivityResultRequestCode.PromptForFile, 60000 * 5);

                        if (result != null && result.Item1 == Result.Ok)
                            try
                            {
                                using (StreamReader file = new StreamReader(_service.ContentResolver.OpenInputStream(result.Item2.Data)))
                                    return file.ReadToEnd();
                            }
                            catch (Exception ex) { Toast.MakeText(_service, "Error reading text file:  " + ex.Message, ToastLength.Long); }
                    }
                    catch (ActivityNotFoundException) { Toast.MakeText(_service, "Please install a file manager from the Apps store.", ToastLength.Long); }
                    catch (Exception ex) { Toast.MakeText(_service, "Something went wrong while prompting you for a file to read:  " + ex.Message, ToastLength.Long); }

                    return null;
                });
        }

        public override async void ShareFileAsync(string path, string subject)
        {
            try
            {
                Intent intent = new Intent(Intent.ActionSend);
                intent.SetType("text/plain");
                intent.AddFlags(ActivityFlags.GrantReadUriPermission);

                if (!string.IsNullOrWhiteSpace(subject))
                    intent.PutExtra(Intent.ExtraSubject, subject);

                Java.IO.File file = new Java.IO.File(path);
                global::Android.Net.Uri uri = FileProvider.GetUriForFile(_service, "edu.virginia.sie.ptl.sensus.fileprovider", file);
                intent.PutExtra(Intent.ExtraStream, uri);

                // run from main activity to get a smoother transition back to sensus
                (await GetMainActivityAsync(true)).StartActivity(intent);
            }
            catch (Exception ex) { Logger.Log("Failed to start intent to share file \"" + path + "\":  " + ex.Message, LoggingLevel.Normal); }
        }

        protected override void StartSensusPings(int ms)
        {
            SetSensusMonitoringAlarm(ms);
        }

        protected override void StopSensusPings()
        {
            SetSensusMonitoringAlarm(-1);
        }

        public override Task TextToSpeechAsync(string text)
        {
            return _textToSpeech.SpeakAsync(text);
        }

        public override Task<string> PromptForInputAsync(string prompt, bool startVoiceRecognizer)
        {
            return Task.Run<string>(async () =>
                {
                    string input = null;
                    ManualResetEvent inputWait = new ManualResetEvent(false);

                    AndroidMainActivity mainActivity = await GetMainActivityAsync(true);

                    mainActivity.RunOnUiThread(async () =>
                        {
                            EditText responseEdit = new EditText(mainActivity);

                            AlertDialog dialog = new AlertDialog.Builder(mainActivity, global::Android.Resource.Style.ThemeTranslucentNoTitleBar)
                                                 .SetTitle("Sensus is Requesting Input")
                                                 .SetMessage(prompt)
                                                 .SetView(responseEdit)
                                                 .SetPositiveButton("OK", (o, e) =>
                                                     {
                                                         input = responseEdit.Text;
                                                         inputWait.Set();
                                                     })
                                                 .SetNegativeButton("Cancel", (o, e) =>
                                                     {
                                                         input = null;
                                                         inputWait.Set();
                                                     })
                                                 .Create();

                            // dismiss the keyguard
                            dialog.Window.AddFlags(global::Android.Views.WindowManagerFlags.DismissKeyguard);
                            dialog.Window.AddFlags(global::Android.Views.WindowManagerFlags.ShowWhenLocked);
                            dialog.Window.AddFlags(global::Android.Views.WindowManagerFlags.TurnScreenOn);

                            // dim whatever is behind the dialog
                            dialog.Window.AddFlags(global::Android.Views.WindowManagerFlags.DimBehind);
                            dialog.Window.Attributes.DimAmount = 0.75f;

                            dialog.Show();

                            if (startVoiceRecognizer)
                            {
                                Intent intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
                                intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
                                intent.PutExtra(RecognizerIntent.ExtraSpeechInputCompleteSilenceLengthMillis, 1500);
                                intent.PutExtra(RecognizerIntent.ExtraSpeechInputPossiblyCompleteSilenceLengthMillis, 1500);
                                intent.PutExtra(RecognizerIntent.ExtraSpeechInputMinimumLengthMillis, 15000);
                                intent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);
                                intent.PutExtra(RecognizerIntent.ExtraLanguage, Java.Util.Locale.Default);

                                Tuple<Result, Intent> result = await mainActivity.GetActivityResultAsync(intent, AndroidActivityResultRequestCode.RecognizeSpeech, 60000);

                                if (result != null && result.Item1 == Result.Ok)
                                {
                                    IList<string> matches = result.Item2.GetStringArrayListExtra(RecognizerIntent.ExtraResults);
                                    if (matches != null && matches.Count > 0)
                                        responseEdit.Text = matches[0];
                                }
                            }
                        });

                    inputWait.WaitOne();

                    return input;
                });
        }

        public override Task FlashNotificationAsync(string message)
        {
            return Task.Run(async () =>
                {
                    AndroidMainActivity mainActivity = await GetMainActivityAsync(false);
                    mainActivity.RunOnUiThread(() => Toast.MakeText(mainActivity, message, ToastLength.Long).Show());
                });
        }

        private void SetSensusMonitoringAlarm(int ms)
        {
            AlarmManager alarmManager = _service.GetSystemService(Context.AlarmService) as AlarmManager;
            Intent serviceIntent = new Intent(_service, typeof(AndroidSensusService));
            serviceIntent.PutExtra(INTENT_EXTRA_NAME_PING, true);
            PendingIntent pendingServiceIntent = PendingIntent.GetService(_service, 0, serviceIntent, PendingIntentFlags.UpdateCurrent);

            if (ms > 0)
                alarmManager.SetRepeating(AlarmType.RtcWakeup, Java.Lang.JavaSystem.CurrentTimeMillis() + ms, ms, pendingServiceIntent);
            else
                alarmManager.Cancel(pendingServiceIntent);
        }

        public override void Destroy()
        {
            base.Destroy();

            _textToSpeech.Dispose();
        }
    }
}