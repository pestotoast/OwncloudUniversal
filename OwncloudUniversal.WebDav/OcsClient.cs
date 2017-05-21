using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Security.Credentials;
using Newtonsoft.Json;
using OwncloudUniversal.Shared;
using OwncloudUniversal.WebDav.Model;
using HttpMethod = Windows.Web.Http.HttpMethod;
using HttpStatusCode = Windows.Web.Http.HttpStatusCode;

namespace OwncloudUniversal.WebDav
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
                    var version = status.Versionstring.Trim('.');
                    //how can i properly check if its an ownCloud or a nextCloud Server???
                    if (version.StartsWith("11") || version.StartsWith("12")) 
                        status.Edition = "nextcloud";
                    else
                        status.Edition = "owncloud";
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
                input = input.TrimEnd('/').ToLower();
                if (input.EndsWith("owncloud") || input.EndsWith("nextcloud"))
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

        public async Task<Account> GetUserInfoAsync()
        {
            var acc = new Account();
            var serverStatus = await GetServerStatusAsync(_serverUrl.ToString());
            acc.Credentials = new PasswordCredential(_serverUrl.ToString(), _credential.UserName, _credential.Password);
            acc.AvatarUrl = _serverUrl.ToString().TrimEnd('/').Replace("remote.php/webdav", "index.php/avatar/") + _credential.UserName + "/" + 64;
            Uri uri;
            if (serverStatus.Edition == "owncloud")
            {
                uri = new Uri(_serverUrl.ToString().Replace("remote.php/webdav", "ocs/v1.php/cloud/users") + "/" + _credential.UserName);
            }
            else
            {
                uri = new Uri(_serverUrl.ToString().Replace("remote.php/webdav", "ocs/v1.php/cloud/user"));
            }

            var headers = new Dictionary<string, string> { { "OCS-APIRequest", "true" } };
            var request = new WebDavRequest(new NetworkCredential(_credential.UserName, _credential.Password), uri, HttpMethod.Get, null, headers);
            var response = await request.SendAsync();
            var stream = await response.Content.ReadAsInputStreamAsync();
            XmlParser.ParseGetUser(acc, stream.AsStreamForRead());
            return acc;
        }
    }
}
