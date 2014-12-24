using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Net.Nsd;
using Android.Util;

namespace com.testy.chat.app
{
    public class NsdHelper
    {
        private Context mContext;

        public NsdManager NsdManager { get; set; }
        public NsdManager.IResolveListener ResolveListener { get; set; }
        public NsdManager.IDiscoveryListener DiscoveryListener { get; set; }
        public NsdManager.IRegistrationListener RegistrationListener { get; set; }
        public NsdServiceInfo Service { get; set; }

        public const string SERVICE_TYPE = "_http._tcp.";
        public const string TAG = "NsdHelper";
        public string ServiceName { get; set; }

        public NsdHelper(Context context)
        {
            this.mContext = context;
            this.NsdManager = (NsdManager)context.GetSystemService(Context.NsdService);
            this.ServiceName = "NsdChat";
        }

        public void InitializeNsd()
        {
            InitializeResolveListener();
            InitializeDiscoveryListener();
            InitializeRegistrationListener();

            //mNsdManager.init(mContext.getMainLooper(), this);

        }

        public void InitializeDiscoveryListener()
        {
            this.DiscoveryListener = new DiscoveryListener(this);
        }

        public void InitializeResolveListener()
        {
            this.ResolveListener = new ResolveListener(this);
        }

        public void InitializeRegistrationListener()
        {
            this.RegistrationListener = new RegistrationListener(this);
        }

        public void RegisterService(int port)
        {
            try
            {
                NsdServiceInfo serviceInfo = new NsdServiceInfo();
                serviceInfo.Port = port;
                serviceInfo.ServiceName = this.ServiceName;
                serviceInfo.ServiceType = SERVICE_TYPE;

                this.NsdManager.RegisterService(
                        serviceInfo, NsdManager.ProtocolDnsSd, this.RegistrationListener);
            }
            catch (Exception ex)
            {
                
                throw;
            }
        }

        public void DiscoverServices()
        {
            try
            {
                this.NsdManager.DiscoverServices(
                                   SERVICE_TYPE, NsdManager.ProtocolDnsSd, this.DiscoveryListener);
            }
            catch (Exception ex)
            {
                
                throw;
            }
        }

        public void StopDiscovery()
        {
            this.NsdManager.StopServiceDiscovery(this.DiscoveryListener);
        }

        public NsdServiceInfo GetChosenServiceInfo()
        {
            return this.Service;
        }

        public void TearDown()
        {
            this.NsdManager.UnregisterService(this.RegistrationListener);
        }
    }

    internal class DiscoveryListener : Java.Lang.Object, Android.Net.Nsd.NsdManager.IDiscoveryListener
    {       
        private NsdHelper mNsdHelper;
        public DiscoveryListener(NsdHelper helper)
        {
            this.mNsdHelper = helper;
        }
        public void OnDiscoveryStarted(string serviceType)
        {
            Log.Debug(NsdHelper.TAG, "Service discovery started");
        }

        public void OnDiscoveryStopped(string serviceType)
        {
            Log.Info(NsdHelper.TAG, "Discovery stopped: " + serviceType);
        }

        public void OnServiceFound(Android.Net.Nsd.NsdServiceInfo service)
        {
            Log.Debug(NsdHelper.TAG, "Service discovery success" + service);
            if (!service.ServiceType.Equals(NsdHelper.SERVICE_TYPE))
            {
                Log.Debug(NsdHelper.TAG, "Unknown Service Type: " + service.ServiceType);
            }
            else if (service.ServiceName.Equals(mNsdHelper.ServiceName))
            {
                Log.Debug(NsdHelper.TAG, "Same machine: " + mNsdHelper.ServiceName);
            }
            else if (service.ServiceName.Contains(mNsdHelper.ServiceName))
            {
                mNsdHelper.NsdManager.ResolveService(service, mNsdHelper.ResolveListener);
            }
        }

        public void OnServiceLost(Android.Net.Nsd.NsdServiceInfo service)
        {
            Log.Error(NsdHelper.TAG, "service lost" + service);
            if (mNsdHelper.Service == service)
            {
                mNsdHelper.Service = null;
            }
        }

        public void OnStartDiscoveryFailed(string serviceType, Android.Net.Nsd.NsdFailure errorCode)
        {
            Log.Error(NsdHelper.TAG, "Discovery failed: Error code:" + errorCode);
            mNsdHelper.NsdManager.StopServiceDiscovery(this);
        }

        public void OnStopDiscoveryFailed(string serviceType, Android.Net.Nsd.NsdFailure errorCode)
        {
            Log.Error(NsdHelper.TAG, "Discovery failed: Error code:" + errorCode);
            mNsdHelper.NsdManager.StopServiceDiscovery(this);
        }
    }

    internal class RegistrationListener : Java.Lang.Object, Android.Net.Nsd.NsdManager.IRegistrationListener
    {
         private NsdHelper mNsdHelper;
         public RegistrationListener(NsdHelper helper)
        {
            this.mNsdHelper = helper;
        }
        public void OnRegistrationFailed(Android.Net.Nsd.NsdServiceInfo serviceInfo, Android.Net.Nsd.NsdFailure errorCode)
        {
            throw new NotImplementedException();
        }

        public void OnServiceRegistered(Android.Net.Nsd.NsdServiceInfo serviceInfo)
        {
            mNsdHelper.ServiceName = serviceInfo.ServiceName;
        }

        public void OnServiceUnregistered(Android.Net.Nsd.NsdServiceInfo serviceInfo)
        {
            throw new NotImplementedException();
        }

        public void OnUnregistrationFailed(Android.Net.Nsd.NsdServiceInfo serviceInfo, Android.Net.Nsd.NsdFailure errorCode)
        {
            throw new NotImplementedException();
        }
    }

    internal class ResolveListener : Java.Lang.Object, Android.Net.Nsd.NsdManager.IResolveListener
    {
        private NsdHelper mNsdHelper;
        public ResolveListener(NsdHelper helper)
        {
            this.mNsdHelper = helper;
        }
        public void OnResolveFailed(Android.Net.Nsd.NsdServiceInfo serviceInfo, Android.Net.Nsd.NsdFailure errorCode)
        {
            Log.Error(NsdHelper.TAG, "Resolve failed" + errorCode);
        }

        public void OnServiceResolved(Android.Net.Nsd.NsdServiceInfo serviceInfo)
        {
            Log.Error(NsdHelper.TAG, "Resolve Succeeded. " + serviceInfo);

            if (serviceInfo.ServiceName.Equals(mNsdHelper.ServiceName))
            {
                Log.Debug(NsdHelper.TAG, "Same IP.");
                return;
            }
            mNsdHelper.Service = serviceInfo;
        }
    }
}
