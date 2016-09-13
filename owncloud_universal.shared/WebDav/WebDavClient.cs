﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.Web.Http;
using Windows.Web.Http.Headers;
using HttpClient = Windows.Web.Http.HttpClient;
using HttpMethod = Windows.Web.Http.HttpMethod;
using HttpRequestMessage = Windows.Web.Http.HttpRequestMessage;
using HttpResponseMessage = Windows.Web.Http.HttpResponseMessage;
using HttpStatusCode = Windows.Web.Http.HttpStatusCode;

namespace OwncloudUniversal.Shared.WebDav
{
    public class WebDavClient
    {
        private readonly NetworkCredential _credential;
        private static readonly HttpMethod PropFind = new HttpMethod("PROPFIND");
        private static readonly HttpMethod MoveMethod = new HttpMethod("MOVE");

        private static readonly HttpMethod MkCol = new HttpMethod("MKCOL");

        private const int HttpStatusCodeMultiStatus = 207;

        // http://webdav.org/specs/rfc4918.html#METHOD_PROPFIND
        private const string PropFindRequestContent =
            "<?xml version=\"1.0\" encoding=\"utf-8\" ?>" +
            "<propfind xmlns=\"DAV:\">" +
            "<allprop/>" +
            //"  <propname/>" +
            //"  <prop>" +
            //"    <creationdate/>" +
            //"    <getlastmodified/>" +
            //"    <displayname/>" +
            //"    <getcontentlength/>" +
            //"    <getcontenttype/>" +
            //"    <getetag/>" +
            //"    <resourcetype/>" +
            //"  </prop> " +
            "</propfind>";
        
        private readonly HttpClient _client;
        private readonly HttpClient _uploadClient;
        private string _server;     

        #region WebDAV connection parameters

        /// <summary>
        /// Specify the WebDAV hostname (required).
        /// </summary>
        public string Server
        {
            get { return _server; }
            set
            {
                value = value.TrimEnd('/');
                _server = value;
            }
        }

        /// <summary>
        /// Specify the UserAgent (and UserAgent version) string to use in requests
        /// </summary>
        public string UserAgent { get; set; }
        
        /// <summary>
        /// Specify the UserAgent (and UserAgent version) string to use in requests
        /// </summary>
        public string UserAgentVersion { get; set; }

        #endregion


        public WebDavClient(NetworkCredential credential = null, TimeSpan? uploadTimeout = null)
        {
            _credential = credential;
            _client = new HttpClient();
            _uploadClient = new HttpClient();
        }

        #region WebDAV operations

        /// <summary>
        /// List all files present on the server.
        /// </summary>
        /// <param name="path">List only files in this path</param>
        /// <param name="depth">Recursion depth</param>
        /// <returns>A list of files (entries without a trailing slash) and directories (entries with a trailing slash)</returns>
        public async Task<IEnumerable<DavItem>> List(string path = "/", int? depth = 1)
        {
            Uri uri = BuildUrl(path);

            // Depth header: http://webdav.org/specs/rfc4918.html#rfc.section.9.1.4
            IDictionary<string, string> headers = new Dictionary<string, string>();
            if (depth != null)
            {
                headers.Add("Depth", depth.ToString());
            }
            HttpResponseMessage response = null;
            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    var bytes = Encoding.UTF8.GetBytes(PropFindRequestContent);
                    memoryStream.Read(bytes, 0, bytes.Length);
                    response = await HttpRequest(uri, PropFind, headers, memoryStream.AsInputStream()).ConfigureAwait(false);
                }
                if (response.StatusCode != HttpStatusCode.Ok &&
                    (int) response.StatusCode != HttpStatusCodeMultiStatus)
                {
                    throw new Exception("Failed retrieving items in folder.");
                }

                using (var stream = await response.Content.ReadAsInputStreamAsync())
                {
                    var items = ResponseParser.ParseItems(stream.AsStreamForRead());

                    if (items == null)
                    {
                        throw new Exception("Failed deserializing data returned from server.");
                    }

                    var listUrl = uri.ToString();

                    var result = new List<DavItem>(items.Count());
                    foreach (var item in items)
                    {
                        string currentPath = BuildUrl(path).AbsolutePath;
                        if (currentPath.Substring(currentPath.Length - 1) != "/")
                            currentPath += "/";
                        if (item.Href != currentPath)
                            result.Add(item);
                    }
                    return result;
                }

            }
            finally
            {
                if (response != null)
                    response.Dispose();
            }
        }

        /// <summary>
        /// List all files present on the server.
        /// </summary>
        /// <returns>A list of files (entries without a trailing slash) and directories (entries with a trailing slash)</returns>
        public async Task<DavItem> GetFolder(string path = "/")
        {
            var listUri = BuildUrl(path);
            return await Get(listUri, path).ConfigureAwait(false);
        }

        /// <summary>
        /// List all files present on the server.
        /// </summary>
        /// <returns>A list of files (entries without a trailing slash) and directories (entries with a trailing slash)</returns>
        public async Task<DavItem> GetFile(string path = "/")
        {
            var listUri = BuildUrl(path);
            return await Get(listUri, path).ConfigureAwait(false);
        }


        /// <summary>
        /// List all files present on the server.
        /// </summary>
        /// <returns>A list of files (entries without a trailing slash) and directories (entries with a trailing slash)</returns>
        private async Task<DavItem> Get(Uri listUri, string path)
        {

            // Depth header: http://webdav.org/specs/rfc4918.html#rfc.section.9.1.4
            IDictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("Depth", "0");
            HttpResponseMessage response = null;
            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    var bytes = Encoding.UTF8.GetBytes(PropFindRequestContent);
                    memoryStream.Read(bytes, 0, bytes.Length);
                    response = await HttpRequest(listUri, PropFind, headers, memoryStream.AsInputStream()).ConfigureAwait(false);
                }

                if (response.StatusCode != HttpStatusCode.Ok &&
                    (int) response.StatusCode != HttpStatusCodeMultiStatus)
                {
                    throw new Exception(string.Format("Failed retrieving item/folder (Status Code: {0})", response.StatusCode));
                }

                using (var stream = await response.Content.ReadAsInputStreamAsync())
                {
                    var result = ResponseParser.ParseItem(stream.AsStreamForRead());

                    if (result == null)
                    {
                        throw new Exception("Failed deserializing data returned from server.");
                    }

                    return result;
                }
            }
            finally
            {
                if (response != null)
                    response.Dispose();
            }
        }

        /// <summary>
        /// Download a file from the server
        /// </summary>
        /// <param name="remoteFilePath">Source path and filename of the file on the server</param>
        public async Task<Stream> Download(string remoteFilePath)
        {
            // Should not have a trailing slash.
            var downloadUri = BuildUrl(remoteFilePath);
            HttpResponseMessage response = null;
            using (IInputStream stream = new InMemoryRandomAccessStream())
            {
                var dictionary = new Dictionary<string, string> { { "translate", "f" } };
                response = await HttpRequest(downloadUri, HttpMethod.Get, dictionary, stream);
                if (response.StatusCode != HttpStatusCode.Ok)
                {
                    throw new Exception("Failed retrieving file.");
                }
            }
            return (await response.Content.ReadAsInputStreamAsync()).AsStreamForRead(16*1024);
        }

        /// <summary>
        /// Download a file from the server
        /// </summary>
        /// <param name="remoteFilePath">Source path and filename of the file on the server</param>
        /// <param name="content"></param>
        /// <param name="name"></param>
        public async Task<bool> Upload(string remoteFilePath, IInputStream content, string name)
        {
            // Should not have a trailing slash.
            var uploadUri = BuildUrl(remoteFilePath.TrimEnd('/') + "/" + name.TrimStart('/'));

            HttpResponseMessage response = null;

            try
            {
                response = await HttpUploadRequest(uploadUri, HttpMethod.Put, content).ConfigureAwait(false);

                if (response.StatusCode != HttpStatusCode.Ok &&
                    response.StatusCode != HttpStatusCode.NoContent &&
                    response.StatusCode != HttpStatusCode.Created)
                {
                    throw new Exception("Failed uploading file.");
                }

                return response.IsSuccessStatusCode;
            }
            finally
            {
                if (response != null)
                    response.Dispose();
            }

        }


        /// <summary>
        /// Create a directory on the server
        /// </summary>
        /// <param name="remotePath">Destination path of the directory on the server</param>
        /// <param name="name"></param>
        public async Task<bool> CreateDir(string remotePath, string name)
        {
            // Should not have a trailing slash.
            var dirUri = BuildUrl(remotePath.TrimEnd('/') + "/" + name.TrimStart('/'));

            HttpResponseMessage response = null;

            try
            {
                response = await HttpRequest(dirUri, MkCol).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new Exception("Folder Conflict");

                if (response.StatusCode != HttpStatusCode.Ok &&
                    response.StatusCode != HttpStatusCode.NoContent &&
                    response.StatusCode != HttpStatusCode.Created)
                {
                    throw new Exception("Failed creating folder.");
                }

                return response.IsSuccessStatusCode;
            }
            finally
            {
                if (response!= null)
                    response.Dispose();
            }
        }

        public async Task DeleteFolder(string href)
        {
            var listUri = BuildUrl(href);
            await Delete(listUri).ConfigureAwait(false);
        }

        public async Task DeleteFile(string href)
        {
            var listUri = BuildUrl(href);
            await Delete(listUri).ConfigureAwait(false);
        }


        private async Task Delete(Uri listUri)
        {
            var response = await HttpRequest(listUri, HttpMethod.Delete).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.Ok &&
                response.StatusCode != HttpStatusCode.NoContent)
            {
                throw new Exception("Failed deleting item.");
            }
        }

        #endregion

        #region Server communication

        /// <summary>
        /// Perform the WebDAV call and fire the callback when finished.
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="method"></param>
        /// <param name="headers"></param>
        /// <param name="content"></param>
        private async Task<HttpResponseMessage> HttpRequest(Uri uri, Windows.Web.Http.HttpMethod method, IDictionary<string, string> headers = null, IInputStream content = null)
        {
            using (HttpRequestMessage request = new Windows.Web.Http.HttpRequestMessage(method, uri))
            {
                request.Headers.Connection.Add(new HttpConnectionOptionHeaderValue("Keep-Alive"));
                request.Headers.UserAgent.Add(HttpProductInfoHeaderValue.Parse("Mozilla/5.0"));

                var buffer = Windows.Security.Cryptography.CryptographicBuffer.ConvertStringToBinary(_credential.UserName + ":" + _credential.Password, Windows.Security.Cryptography.BinaryStringEncoding.Utf8);
                string base64token = Windows.Security.Cryptography.CryptographicBuffer.EncodeToBase64String(buffer);

                request.Headers.Authorization = new HttpCredentialsHeaderValue("Basic", base64token);
                if (headers != null)
                {
                    foreach (string key in headers.Keys)
                    {
                        request.Headers.Add(key, headers[key]);
                    }
                }

                // Need to send along content?
                if (content != null)
                {
                    request.Content = new HttpStreamContent(content);
                    request.Content.Headers.ContentType = new HttpMediaTypeHeaderValue("text/xml");
                }

                    var response = await _client.SendRequestAsync(request, HttpCompletionOption.ResponseHeadersRead);
  
                
                return response;
            }
        }

        /// <summary>
        /// Perform the WebDAV call and fire the callback when finished.
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="headers"></param>
        /// <param name="method"></param>
        /// <param name="content"></param>
        private async Task<HttpResponseMessage> HttpUploadRequest(Uri uri, HttpMethod method, IInputStream content, IDictionary<string, string> headers = null)
        {
            using (var request = new HttpRequestMessage(method, uri))
            {
                request.Headers.Connection.Add(HttpConnectionOptionHeaderValue.Parse("Keep-Alive"));
                request.Headers.UserAgent.Add(HttpProductInfoHeaderValue.Parse("Mozilla/5.0"));

                var credentialBuffer = Windows.Security.Cryptography.CryptographicBuffer.ConvertStringToBinary(_credential.UserName + ":" + _credential.Password, Windows.Security.Cryptography.BinaryStringEncoding.Utf8);
                string base64token = Windows.Security.Cryptography.CryptographicBuffer.EncodeToBase64String(credentialBuffer);

                request.Headers.Authorization = new HttpCredentialsHeaderValue("Basic", base64token);
                if (headers != null)
                {
                    foreach (string key in headers.Keys)
                    {
                        request.Headers.Add(key, headers[key]);
                    }
                }

                // Need to send along content?
                if (content != null)
                {
                    request.Content = new HttpStreamContent(content);
                }
                var client = _uploadClient ?? _client;
                var response =  await client.SendRequestAsync(request, HttpCompletionOption.ResponseHeadersRead);
                return response;
            }
        }

        private Uri BuildUrl(string path)
        {
            Uri baseUri = new Uri(_server);
            return new Uri(baseUri, path);
        }

        #endregion
    }
}
