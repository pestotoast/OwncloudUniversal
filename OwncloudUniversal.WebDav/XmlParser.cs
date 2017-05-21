using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using OwncloudUniversal.WebDav.Model;

namespace OwncloudUniversal.WebDav
{
    internal static class XmlParser
    {
        public static List<DavItem> ParsePropfind(Stream stream)
        {
            List<DavItem> davItems = new List<DavItem>();
            XNamespace namepace = "DAV:" ;
            XDocument doc = XDocument.Load(stream);
            var elements = from response in doc.Descendants(namepace + "response") select response;
            foreach (var xElement in elements)
            {
                var davItem = new DavItem();
                davItem.Href = xElement.Element(namepace + "href")?.Value;
                davItem.Etag = xElement.Descendants(namepace + "gettag").FirstOrDefault()?.Value;
                davItem.ChangeKey = xElement.Descendants(namepace + "getetag").FirstOrDefault()?.Value;
                davItem.LastModified = Convert.ToDateTime(xElement.Descendants(namepace + "getlastmodified").FirstOrDefault()?.Value);
                davItem.ContentType = xElement.Descendants(namepace + "getcontenttype").FirstOrDefault()?.Value;
                davItem.IsCollection = (bool)!xElement.Descendants(namepace + "resourcetype").FirstOrDefault()?.IsEmpty;
                davItem.Size = Convert.ToUInt64(davItem.IsCollection ? xElement.Descendants(namepace + "quota-used-bytes").FirstOrDefault()?.Value : xElement.Descendants(namepace + "getcontentlength").FirstOrDefault()?.Value);
                string href = xElement.Element(namepace + "href")?.Value.TrimEnd('/');
                davItem.DisplayName = WebUtility.UrlDecode(href?.Substring(href.LastIndexOf('/') + 1));
                if (davItem.IsCollection && davItem.ContentType == null)
                    davItem.ContentType = "text/directory";
                davItems.Add(davItem);
            }
            return davItems;
        }

        public static void ParseGetUser(Account account, Stream stream)
        {
            XDocument doc = XDocument.Load(stream);
            Debug.WriteLine(doc);
            var elements = from data in doc.Descendants(XName.Get("data")) select data;
            foreach (var xElement in elements)
            {
                account.DisplayName = xElement.Element(XName.Get("displayname"))?.Value;//owncloud
                if(account.DisplayName is null)
                    account.DisplayName = xElement.Element(XName.Get("display-name"))?.Value;//nextcloud
                account.TotalStorage = Convert.ToUInt64(xElement.Descendants(XName.Get("total")).FirstOrDefault()?.Value);
                account.UsedStorage = Convert.ToUInt64(xElement.Descendants(XName.Get("used")).FirstOrDefault()?.Value);
                account.FreeStorage = Convert.ToUInt64(xElement.Descendants(XName.Get("free")).FirstOrDefault()?.Value);
                account.PercentUsed = Convert.ToDouble(xElement.Descendants(XName.Get("relative")).FirstOrDefault()?.Value, CultureInfo.InvariantCulture);
            }
        }
    }
}