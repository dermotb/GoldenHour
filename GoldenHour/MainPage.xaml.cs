using Bing.Maps;
using GoldenHour.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


// The Items Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234233

namespace GoldenHour
{
    /// <summary>
    /// A page that displays a collection of item previews.  In the Split Application this page
    /// is used to display and select one of the available groups.
    /// </summary>
    public sealed partial class MainPage : GoldenHour.Common.LayoutAwarePage
    {
		bool usingCurrentPosition = true;
		Geolocator geolocator;
		LocationIcon locationIcon;

	    public MainPage()
        {
			this.InitializeComponent();


			DatePicker.Tapped += DatePicker_Tapped;

			geolocator = new Geolocator();
			locationIcon = new LocationIcon();

			// Add the location icon to map layer so that we can position it.
			map.Children.Add(locationIcon);
			AddCurrentLocationData();
        }

		void DatePicker_Tapped(object sender, TappedRoutedEventArgs e)
		{
			var x = DatePicker.SelectedDate;
			AddCurrentLocationData();
		}


		public void ChangeDate()
		{
			AddCurrentLocationData();

		}

		private async void AddCurrentLocationData()
		{
            if (geolocator.LocationStatus == PositionStatus.Ready)
            {
                Geoposition current = await geolocator.GetGeopositionAsync();

                displayPosition(current.Coordinate.Latitude, current.Coordinate.Longitude, true);
                accuracy.Text = current.Coordinate.Accuracy.ToString();
            }
		}

		private void map_RightTapped_1(object sender, RightTappedRoutedEventArgs e)
		{
			usingCurrentPosition = false;
			Location location = new Location();
			bool succeeded = map.TryPixelToLocation(e.GetPosition(map), out location);

			if (succeeded)
			{
				displayPosition(location.Latitude, location.Longitude, false);
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
	
		}

		private void DisplaySunData(Location location)
		{
			latText.Text = location.Latitude.ToString();
			longText.Text = location.Longitude.ToString();

			DateTime date = DatePicker.SelectedDate; //DateTime.Today;
			bool isSunrise = false;
			bool isSunset = false;
			DateTime sunrise = DateTime.Now;
			DateTime sunset = DateTime.Now;

			SunTimes.LatitudeCoords stLatitude = DecimalLatToDegreesLat(location.Latitude);
			SunTimes.LongitudeCoords stLongitude = DecimalLonToDegreesLon(location.Longitude);

			SunTimes.Instance.CalculateSunRiseSetTimes(location.Latitude, location.Longitude, date, ref sunrise, ref sunset, ref isSunrise, ref isSunset);

			sunRise.Text = sunrise.ToLocalTime().TimeOfDay.ToString();
			sunSet.Text = sunset.ToLocalTime().TimeOfDay.ToString();
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
			AddCurrentLocationData();
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

	}
}
