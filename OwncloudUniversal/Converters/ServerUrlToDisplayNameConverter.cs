using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using Microsoft.Graphics.Canvas.Text;

namespace OwncloudUniversal.Converters
{
    class ServerUrlToDisplayNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!(value is string s)) return null;
            var uri = new Uri(s, UriKind.Absolute);
            return uri.Host;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
