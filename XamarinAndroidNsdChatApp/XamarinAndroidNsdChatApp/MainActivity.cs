using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Util;
using Android.Net.Nsd;
using Java.Interop;

namespace com.testy.chat.app
{
    [Activity(Label = "XamarinAndroidNsdChatApp", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        private NsdHelper mNsdHelper;

        private TextView mStatusView;
        private ChatHandler mUpdateHandler;

        public const String TAG = "NsdChat";

        ChatConnection mConnection;

        /** Called when the activity is first created. */
        protected override void OnCreate(Bundle savedInstanceState) 
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.Main);

            mStatusView = FindViewById<TextView>(Resource.Id.status);

            mUpdateHandler = new ChatHandler(this);
            mConnection = new ChatConnection(mUpdateHandler);

            mNsdHelper = new NsdHelper(this);
            mNsdHelper.InitializeNsd();
        }

        [Export]
        public void ClickAdvertise(View v) 
        {
            // Register service
            if(mConnection.GetLocalPort() > -1) {
                mNsdHelper.RegisterService(mConnection.GetLocalPort());
            } else {
                Log.Debug(TAG, "ServerSocket isn't bound.");
            }
        }

        [Export]
        public void ClickDiscover(View v) 
        {
            mNsdHelper.DiscoverServices();
        }

        [Export]
        public void ClickConnect(View v)
        {
            NsdServiceInfo service = mNsdHelper.GetChosenServiceInfo();
            if (service != null) 
            {
                Log.Debug(TAG, "Connecting.");
                mConnection.ConnectToServer(service.Host,
                        service.Port);
            } else 
            {
                Log.Debug(TAG, "No service to connect to!");
            }
        }

        [Export]
        public void ClickSend(View v)
        {
            EditText messageView = this.FindViewById<EditText>(Resource.Id.chatInput);
            if (messageView != null)
            {
                string messageString = messageView.Text.ToString();
                if (!string.IsNullOrEmpty(messageString)) 
                {
                    mConnection.SendMessage(messageString);
                }
                messageView.Text = "";
            }
        }

        public void AddChatLine(string line) {
            mStatusView.Append("\n" + line);
        }

        protected override void OnPause() {
            if (mNsdHelper != null) {
                mNsdHelper.StopDiscovery();
            }
            base.OnPause();
        }
    
        protected override void OnResume() {
            base.OnResume();
            if (mNsdHelper != null) {
                mNsdHelper.DiscoverServices();
            }
        }
    
        protected override void OnDestroy() {
            mNsdHelper.TearDown();
            mConnection.TearDown();
            base.OnDestroy();
        }
    }

    public class ChatHandler : Handler 
    {
        private MainActivity mMainActivity;
        public ChatHandler(MainActivity activity) : base()
        {
            this.mMainActivity = activity;
        }
        
         public override void HandleMessage(Message msg) {
            string chatLine = msg.Data.GetString("msg");
            mMainActivity.AddChatLine(chatLine);
        }
    }
}

