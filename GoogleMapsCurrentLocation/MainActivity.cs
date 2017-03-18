using System;
using Android.App;
using Android.Widget;
using Android.OS;
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.Gms.Common.Apis;
using Android.Gms.Common;
using Android.Locations;
using Android.Gms.Location;
using System.Threading.Tasks;
using Android.Util;
using Android.Content;
using Android.Runtime;

namespace GoogleMapsCurrentLocation
{
    [Activity(Label = "GoogleMapsCurrentLocation", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity,
        IOnMapReadyCallback,
        GoogleApiClient.IConnectionCallbacks,
        GoogleApiClient.IOnConnectionFailedListener,
        Android.Gms.Location.ILocationListener
    {

        #region Initialize Variables
        
        protected const string TAG = "GoogleMaps-CurrentLocation";

        // kwdikos gia Dialog Location Settings
        protected const int REQUEST_CHECK_SETTINGS = 0x1;

        // xronoi gia Updates SetInterval, SetFastestInterval
        public const long UPDATE_INTERVAL_IN_MILLISECONDS = 1000;
        public const long FASTEST_UPDATE_INTERVAL_IN_MILLISECONDS = UPDATE_INTERVAL_IN_MILLISECONDS / 2;

        // Keys for storing activity state in the Bundle.
        protected const string KEY_REQUESTING_LOCATION_UPDATES = "requesting-location-updates";
        protected const string KEY_LOCATION = "location";
        protected const string KEY_LAST_UPDATED_TIME_STRING = "last-updated-time-string";

        //Provides the entry point to Google Play services.
        GoogleApiClient mGoogleApiClient;

        //Stores parameters for requests to the FusedLocationProviderApi.
        LocationRequest mLocationRequest;

        //Specifies the types of location services the client is interested in using.
        LocationSettingsRequest mLocationSettingsRequest;

        //Represents a geographical location.
        Location mCurrentLocation;

        // Latitude and Longitude Values from location
        LatLng mLatLong;

        // Camera
        CameraUpdate mCamera;


        // Gia emfanisi toy xarth
        private GoogleMap mGoogleMap;

        bool isGooglePlayServicesInstallled;
        bool mRequestingLocationUpdates;
        protected String mLastUpdateTime;
        // gia na kanei zoom stin topothesia mono meta to OK apo to Dialog
        bool afterLocationDialog;

        Button StartTracking, StopTracking;

        #endregion

        #region "Lifecycle Methods"
        
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            mRequestingLocationUpdates = false;
            mLastUpdateTime = "";

            // Update values using data stored in the Bundle.
            UpdateValuesFromBundle(bundle);

            // Check the device for Google Play Services
            isGooglePlayServicesInstallled = IsGooglePlayServicesInstallled();
            if (!isGooglePlayServicesInstallled)
            {
                Toast.MakeText(this, "Google Play Services not Installed", ToastLength.Long).Show();
                Finish();
            }
            
            BuildGoogleApiClient();
            CreateLocationRequest();
            BuildLocationSettingsRequest();

            SetUpMap();
            
            // Check User Location Settings
            CheckLocationSettings();

        }
        
        protected override void OnStart()
        {
            base.OnStart();
            mGoogleApiClient.Connect();
        }
        
        protected override async void OnResume()
        {
            base.OnResume();
            if (mGoogleApiClient.IsConnected) {
                await StartLocationUpdates();
            }
        }

        protected override async void OnPause()
        {
            base.OnPause();
            if (mGoogleApiClient.IsConnected) {
                await StopLocationUpdates();
            }
        }

        protected override void OnStop()
        {
            base.OnStop();
            mGoogleApiClient.Disconnect();
        }

        // Called when the Activity is destroyed
        protected override void OnSaveInstanceState(Bundle outState)
        {
            // Save the Values
            outState.PutBoolean(KEY_REQUESTING_LOCATION_UPDATES, mRequestingLocationUpdates);
            outState.PutParcelable(KEY_LOCATION, mCurrentLocation);
            outState.PutString(KEY_LAST_UPDATED_TIME_STRING, mLastUpdateTime);
            base.OnSaveInstanceState(outState);
        }

        #endregion

        #region User Methods

        private void UpdateValuesFromBundle(Bundle bundle)
        {
            if (bundle != null) {
                //  mRequestingLocationUpdates Value
                if (bundle.KeySet().Contains(KEY_REQUESTING_LOCATION_UPDATES))
                {
                    mRequestingLocationUpdates = bundle.GetBoolean(KEY_REQUESTING_LOCATION_UPDATES);
                }

                // mCurrentLocation Value
                if (bundle.KeySet().Contains(KEY_LOCATION))
                {
                    mCurrentLocation = (Location)bundle.GetParcelable(KEY_LOCATION);
                }

                // mLastUpdateTime Value
                if (bundle.KeySet().Contains(KEY_LAST_UPDATED_TIME_STRING))
                {
                    mLastUpdateTime = bundle.GetString(KEY_LAST_UPDATED_TIME_STRING);
                }
            }
        }

        protected bool IsGooglePlayServicesInstallled()
        {
            int queryResult = GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(this);
            if (queryResult == ConnectionResult.Success)
            {
                // Google Play Services Installed on Device
                return true;
            }
            // else not Installed
            return false;
        }
        
        protected void BuildGoogleApiClient()
        {
            mGoogleApiClient = new GoogleApiClient.Builder(this)
                .AddConnectionCallbacks(this)
                .AddOnConnectionFailedListener(this)
                .AddApi(LocationServices.API)
                .Build();
        }

        private void CreateLocationRequest()
        {
            mLocationRequest = new LocationRequest();
            mLocationRequest.SetInterval(UPDATE_INTERVAL_IN_MILLISECONDS);
            mLocationRequest.SetFastestInterval(FASTEST_UPDATE_INTERVAL_IN_MILLISECONDS);
            mLocationRequest.SetPriority(LocationRequest.PriorityHighAccuracy);
        }

        private void BuildLocationSettingsRequest()
        {
            //get the current location settings of a user's device
            LocationSettingsRequest.Builder builder = new LocationSettingsRequest.Builder();
            builder.AddLocationRequest(mLocationRequest);
            mLocationSettingsRequest = builder.Build();

        }

        private void SetUpMap()
        {
            if (mGoogleMap == null)
            {
                // Initialize the map
                FragmentManager.FindFragmentById<MapFragment>(Resource.Id.map).GetMapAsync(this);
            }
        }
        
        private void CheckLocationSettings()
        {
            Task.Run( 
                async() => await CheckLocationSettingsAsync()
            ); 
        }

        private async Task CheckLocationSettingsAsync()
        {
            LocationSettingsResult locationSettingResult = await LocationServices.SettingsApi.CheckLocationSettingsAsync(mGoogleApiClient, mLocationSettingsRequest);
            // Antoistixo toy onResult(LocationSettingsResult result)
            await HanleResult(locationSettingResult);
        }

        private async Task HanleResult(LocationSettingsResult locationSettingResult)
        {
            Statuses status = locationSettingResult.Status;

            switch (status.StatusCode)
            {
                case CommonStatusCodes.Success:
                    Log.Info(TAG, "All location settings are satisfied.");
                    await StartLocationUpdates();
                    break;
                case CommonStatusCodes.ResolutionRequired:
                    Log.Info(TAG, "Location settings are not satisfied. Show the user a dialog to" +
                 "upgrade location settings ");
                    try {
                        // Show the dialog by calling startResolutionForResult(),
                        // and check the result in onActivityResult().
                        status.StartResolutionForResult(this, REQUEST_CHECK_SETTINGS);
                    }
                    catch (IntentSender.SendIntentException) {
                        Log.Info(TAG, "PendingIntent unable to execute request.");
                    }
                    break;
                case LocationSettingsStatusCodes.SettingsChangeUnavailable:
                    Log.Info(TAG, "Location settings are inadequate, and cannot be fixed here. Dialog " +
           "not created.");
                    break;
            }

        }

        // Molis klisei to Dialog kaleitai h OnActivityResult
        protected override async void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
        {
            switch (requestCode)
            {
                case REQUEST_CHECK_SETTINGS:
                    switch (resultCode)
                    {
                        case Result.Canceled:
                            Log.Info(TAG, "User chose not to make required location settings changes.");
                            break;
                        case Result.Ok:
                            Log.Info(TAG, "User agreed to make required location settings changes.");
                            await StartLocationUpdates();
                            afterLocationDialog = true;
                            break;
                    }
                    break;
            }

            // base.OnActivityResult(requestCode, resultCode, data);
        }

        private async Task StartLocationUpdates()
        {
            await LocationServices.FusedLocationApi.RequestLocationUpdates(
                mGoogleApiClient, mLocationRequest, this);
            mRequestingLocationUpdates = true;
        }

        private async Task StopLocationUpdates() {
            await LocationServices.FusedLocationApi.RemoveLocationUpdates(
                mGoogleApiClient, this);
            mRequestingLocationUpdates = false;
        }

        private void AnimateCameraToLocation(LatLng LatLong, int zoomValue)
        {
            mCamera = CameraUpdateFactory.NewLatLngZoom(LatLong, zoomValue);
            mGoogleMap.AnimateCamera(mCamera);
        }
        
        #endregion

        #region Interface Mehtods

        public void OnMapReady(GoogleMap googleMap)
        {
            mGoogleMap = googleMap;
            mGoogleMap.UiSettings.ZoomControlsEnabled = true;
            mGoogleMap.MyLocationEnabled = true;
        }

        public void OnConnected(Bundle connectionHint)
        {
            if (mCurrentLocation == null) {
                mCurrentLocation = LocationServices.FusedLocationApi.GetLastLocation(mGoogleApiClient);
            }
            //animate to location
            if (mCurrentLocation != null) {
                mLatLong = new LatLng(mCurrentLocation.Latitude, mCurrentLocation.Longitude);
                AnimateCameraToLocation(mLatLong, 17);
            }
           
        }
        
        public void OnConnectionSuspended(int cause)
        {

        }

        public void OnConnectionFailed(ConnectionResult result)
        {

        }

        public void OnLocationChanged(Location location)
        {
            mCurrentLocation = location;
            
            if (afterLocationDialog) {
                // gia na mhn epistrefei se kathe allagh
                afterLocationDialog = false;
                //animate to location
                if (mCurrentLocation != null) {
                    mLatLong = new LatLng(mCurrentLocation.Latitude, mCurrentLocation.Longitude);
                    AnimateCameraToLocation(mLatLong, 17);
                }
            }

        }

        #endregion

    }
}

