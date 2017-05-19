using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;
using OwncloudUniversal.Shared;
using OwncloudUniversal.WebDav.Model;

namespace OwncloudUniversal.Services
{
    static class ExceptionHandlerService
    {
        public static  async void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            var exception = e.Exception;
            //Debug.WriteLine(exception.GetType());

            ContentDialog dia = new ContentDialog();
            dia.Content = App.ResourceLoader.GetString("UnhandledExceptionMessage");
            dia.Title = App.ResourceLoader.GetString("Oops");
            dia.PrimaryButtonText = App.ResourceLoader.GetString("ok");
            if ((uint)exception.HResult == 0x80072EE7)//server not found
            {
                dia.Content = App.ResourceLoader.GetString("ServerNotFound");
            }

            else if (exception.GetType() == typeof(WebDavException))//-2146233088
            {
                var ex = (WebDavException)exception;
                switch (ex.HttpStatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        dia.Content = App.ResourceLoader.GetString("UnauthorizedMessage");
                        break;
                    case HttpStatusCode.NotFound:
                        dia.Content = App.ResourceLoader.GetString("NotFoundMessage");
                        break;
                    case HttpStatusCode.InternalServerError:
                        dia.Content = App.ResourceLoader.GetString("InternalServerErrorMessage");
                        break;
                    case HttpStatusCode.Forbidden:
                        dia.Content = App.ResourceLoader.GetString("ForbiddenErrorMessage");
                        break;
                    default:
                        dia.Content = App.ResourceLoader.GetString("ServiceUnavailableMessage");
                        break;
                }
                if(ex.Message == HttpStatusCode.Forbidden.ToString())
                    dia.Content = App.ResourceLoader.GetString("ForbiddenErrorMessage");
            }
            await LogHelper.Write($"{e.Exception.GetType()}: {e.Message} \r\n{exception.StackTrace}");
            var indicator = new IndicatorService();
            indicator.HideBar();
            await dia.ShowAsync();
        }
    }
}
