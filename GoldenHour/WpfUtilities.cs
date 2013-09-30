using Sunriser;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace Sunriser.Common
{

    public static class WpfUtilities
    {
        internal static T FindResource<T>(this FrameworkElement e, string name)
        {
            object value = null;
            DependencyObject d = e;
            while (d != null)
            {
                FrameworkElement f = d as FrameworkElement;
                if (f != null)
                {
                    if (f.Resources.TryGetValue(name, out value))
                    {
                        return (T)value;
                    }
                }
                d = VisualTreeHelper.GetParent(d);
            }

            if (App.Current.Resources.TryGetValue(name, out value))
            {
                return (T)value;
            }

            return default(T);
        }
    }

    public class ShortDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            DateTime date = (DateTime)value;
            return date.ToString("dd MMMM yyyy");
			//return date.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            string s = (string)value;
            DateTime d;
            if (DateTime.TryParse(s, out d))
            {
                return d;
            }
            return DateTime.Today;
        }
    }
}
