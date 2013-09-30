using Bing.Maps;
using Sunriser.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.UI.ApplicationSettings;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;


// The Items Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234233

namespace Sunriser
{
    /// <summary>
    /// A page that displays a collection of item previews.  In the Split Application this page
    /// is used to display and select one of the available groups.
    /// </summary>
    public sealed partial class MainPage : Sunriser.Common.LayoutAwarePage
    {
//		bool usingCurrentPosition = true;
		Geolocator geolocator;
		LocationIcon locationIcon;
        Location currentLocation;

	    public MainPage()
        {
			this.InitializeComponent();
            SettingsPane.GetForCurrentView().CommandsRequested += GoldenHour_CommandsRequested;
			DatePicker.Tapped += DatePicker_Tapped;

			geolocator = new Geolocator();
			locationIcon = new LocationIcon();

			// Add the location icon to map layer so that we can position it.
			map.Children.Add(locationIcon);
			GetCurrentLocation();
        }

        void GoldenHour_CommandsRequested(SettingsPane sender, SettingsPaneCommandsRequestedEventArgs args)
        {
            // Add an About command
            SettingsCommand commandAbout = new SettingsCommand("about", "About This App", (x) =>
            {
                Popup popup = BuildSettingsItem(new AboutPage(), 346);
                popup.IsOpen = true;
            });

            args.Request.ApplicationCommands.Add(commandAbout);

            // Add an Privacy command
            SettingsCommand commandPrivacy = new SettingsCommand("privacy", "Privacy Policy", (x) =>
            {
                Popup popup = BuildSettingsItem(new PrivacyPage(), 346);
                popup.IsOpen = true;
            }); 

            args.Request.ApplicationCommands.Add(commandPrivacy);
 
        }

        private Popup BuildSettingsItem(UserControl u, int w)
        {
            Popup p = new Popup();
            p.IsLightDismissEnabled = true;
            p.ChildTransitions = new TransitionCollection();
            p.ChildTransitions.Add(new PaneThemeTransition()
            {
                Edge = (SettingsPane.Edge == SettingsEdgeLocation.Right) ?
                        EdgeTransitionLocation.Right :
                        EdgeTransitionLocation.Left
            });

            u.Width = w;
            u.Height = Window.Current.Bounds.Height;
            p.Child = u;

            p.SetValue(Canvas.LeftProperty, SettingsPane.Edge == SettingsEdgeLocation.Right ? (Window.Current.Bounds.Width - w) : 0);
            p.SetValue(Canvas.TopProperty, 0);

            return p;
        }

		void DatePicker_Tapped(object sender, TappedRoutedEventArgs e)
		{
			var x = DatePicker.SelectedDate;
			AddLocationData(false);
		}

        public void ChangeDate()
		{
			AddLocationData(false);

		}

		private async void GetCurrentLocation()
		{
            try
            {
                Geoposition current = await geolocator.GetGeopositionAsync();
                status.Text = string.Format("Accuracy (m): {0}",current.Coordinate.Accuracy.ToString());
                currentLocation = new Location(current.Coordinate.Latitude, current.Coordinate.Longitude);
                AddLocationData(true);
            }
            catch (Exception ex)
            {
                status.Text = "Failed to find your current location";
            }
		}

        private void AddLocationData(bool home)
        {
            if (currentLocation != null)
            {
                displayPosition(currentLocation.Latitude, currentLocation.Longitude, home);
            }
            else
            {
                status.Text = "Location resolution failure";
            }
        }

		private void map_RightTapped_1(object sender, RightTappedRoutedEventArgs e)
		{
			//usingCurrentPosition = false;
			Location location = new Location();
			bool succeeded = map.TryPixelToLocation(e.GetPosition(map), out location);

			if (succeeded)
			{
				displayPosition(location.Latitude, location.Longitude, false);
                currentLocation = location;
			}
		}

		private void displayPosition(double lat, double lon, bool home)
		{
			Location location = new Location(lat, lon);
			MapLayer.SetPosition(locationIcon, location);

			if (home)
			{
				map.SetView(location, 15.0f);
			}

			DisplaySunData(location);
            status.Text = "";
	
		}

		private void DisplaySunData(Location location)
		{
			latText.Text = string.Format("{0}{1}{2}", location.Latitude.ToString("000.000;000.000"), "\u00B0", location.Latitude > 0 ? "N":"S");
            longText.Text = string.Format("{0}{1}{2}", location.Longitude.ToString("000.000;000.000"), "\u00B0", location.Longitude > 0 ? "E" : "W");

			DateTime date = DatePicker.SelectedDate; //DateTime.Today;
			bool isSunrise = false;
			bool isSunset = false;
			DateTime sunrise = DateTime.Now;
			DateTime sunset = DateTime.Now;

			SunTimes.LatitudeCoords stLatitude = DecimalLatToDegreesLat(location.Latitude);
			SunTimes.LongitudeCoords stLongitude = DecimalLonToDegreesLon(location.Longitude);

			SunTimes.Instance.CalculateSunRiseSetTimes(location.Latitude, location.Longitude, date, ref sunrise, ref sunset, ref isSunrise, ref isSunset);

			sunRise.Text = string.Format("{0}:{1}", sunrise.ToLocalTime().TimeOfDay.Hours.ToString("00"), sunrise.ToLocalTime().TimeOfDay.Minutes.ToString("00"));
			sunSet.Text = string.Format("{0}:{1}",sunset.ToLocalTime().TimeOfDay.Hours.ToString("00"), sunset.ToLocalTime().TimeOfDay.Minutes.ToString("00"));
		}

		private SunTimes.LatitudeCoords DecimalLatToDegreesLat(double lat)
		{
			int sec = (int)Math.Round(lat * 3600);
			int deg = sec / 3600;
			sec = Math.Abs(sec % 3600);
			int min = sec / 60;
			sec %= 60;

			SunTimes.LatitudeCoords stLat = new SunTimes.LatitudeCoords(deg, min, sec, (lat > 0)? SunTimes.LatitudeCoords.Direction.North: SunTimes.LatitudeCoords.Direction.South);
			return stLat;
		}

		private SunTimes.LongitudeCoords DecimalLonToDegreesLon(double lon)
		{
			int sec = (int)Math.Round(lon * 3600);
			int deg = sec / 3600;
			sec = Math.Abs(sec % 3600);
			int min = sec / 60;
			sec %= 60;

			SunTimes.LongitudeCoords stLat = new SunTimes.LongitudeCoords(deg, min, sec, (lon > 0)? SunTimes.LongitudeCoords.Direction.East : SunTimes.LongitudeCoords.Direction.West);
			return stLat;
		}


		private void CurrentCalc_Click(object sender, RoutedEventArgs e)
		{
            GetCurrentLocation();
		}

        /// <summary>
        /// Populates the page with content passed during navigation.  Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="navigationParameter">The parameter value passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested.
        /// </param>
        /// <param name="pageState">A dictionary of state preserved by this page during an earlier
        /// session.  This will be null the first time a page is visited.</param>
        protected override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
        {

        }

        private void map_PointerPressedOverride(object sender, PointerRoutedEventArgs e)
        {
            //usingCurrentPosition = false;
			Location location = new Location();
            bool succeeded = map.TryPixelToLocation(e.GetCurrentPoint(this.map).Position, out location);

			if (succeeded)
			{
				displayPosition(location.Latitude, location.Longitude, false);
                currentLocation = location;
			}

        }

	}
}
