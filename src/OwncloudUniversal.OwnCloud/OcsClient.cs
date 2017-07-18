using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Newtonsoft.Json;
using OwncloudUniversal.OwnCloud.Model;
using OwncloudUniversal.Synchronization;
using HttpMethod = Windows.Web.Http.HttpMethod;
using HttpStatusCode = Windows.Web.Http.HttpStatusCode;

namespace OwncloudUniversal.OwnCloud
{
    public class OcsClient
    {
        private readonly Uri _serverUrl;
        private readonly NetworkCredential _credential;

        public OcsClient(Uri serverUrl, NetworkCredential credential)
        {
            _serverUrl = serverUrl;
            _credential = credential;
        }

        public static async Task<ServerStatus> GetServerStatusAsync(string input)
        {
            var url = _BuildStatusUrl(input);
            if (url == null)
                return null;
            WebDavRequest request = new WebDavRequest(new NetworkCredential(string.Empty, string.Empty), url, HttpMethod.Get);
            var response = await request.SendAsync();
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Serverstatus response: {content}");
                try
                {
                    var status = Newtonsoft.Json.JsonConvert.DeserializeObject<ServerStatus>(content);
                    status.ResponseCode = response.StatusCode.ToString();
                    return status;
                }
                catch (JsonException)
                {
                    return new ServerStatus() {ResponseCode = HttpStatusCode.NotFound.ToString()};
                }
            }
            return new ServerStatus() {ResponseCode = response.StatusCode.ToString()};
        }

        private static Uri _BuildStatusUrl(string input)
        {
            if (Uri.IsWellFormedUriString(input, UriKind.Absolute))
            {
                input = input.TrimEnd('/');
                if (input.EndsWith("owncloud") || input.EndsWith("nextcloud") || input.EndsWith("ownCloud"))
                {
                    input += "/status.php";
                }
                else if (input.EndsWith("remote.php/webdav"))
                {
                    input = input.Replace("remote.php/webdav", "status.php");
                }
                else
                {
                    input += "/status.php";
                }
                if (!(input.StartsWith("http://") || input.StartsWith("https://")))
                {
                    input = "http://" + input;
                }
                if (Uri.IsWellFormedUriString(input, UriKind.Absolute))
                {
                    return new Uri(input, UriKind.Absolute);
                }

            }
            return null;
        }

        public async Task<HttpStatusCode> CheckUserLoginAsync()
        {
            var url = GetWebDavUrl(_serverUrl.ToString());
            var request = new WebDavRequest(_credential, new Uri(url), HttpMethod.Head);
            Windows.Web.Http.HttpResponseMessage response;
            try
            {
                response = await request.SendAsync();
            }
            catch (Exception)
            {
                return HttpStatusCode.SeeOther;
            }
            return response.StatusCode;
        }

        public static string GetWebDavUrl(string url)
        {
            var statusUrl = _BuildStatusUrl(url);
            return statusUrl.ToString().Replace("status.php", "remote.php/webdav");
        }
    }
}
