using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Windows.Storage;
using Windows.Web.Http.Filters;

namespace OwncloudUniversal.Shared
{
    public static class Configuration
    {
        private static readonly Windows.Storage.ApplicationDataContainer Config = ApplicationData.Current.LocalSettings.CreateContainer(ContainerName, ApplicationDataCreateDisposition.Always);

        private const string ContainerName = "ownCloud";

        public static string ServerUrl => SelectedServer?.Resource;

        public static string UserName => SelectedServer?.UserName;

        public static string Password => SelectedServer?.Password;

        public static PasswordCredential SelectedServer
        {
            get
            {
                var vault = new PasswordVault();
                foreach (var passwordCredential in vault.RetrieveAll())
                {
                    if (passwordCredential.Resource == (string)Config.Values["ServerUrl"] && passwordCredential.UserName == (string)Config.Values["UserName"])
                    {
                        passwordCredential.RetrievePassword();
                        return passwordCredential;
                    }
                }
                return null;
            }
            set
            {
                Config.Values["ServerUrl"] = value.Resource;
                Config.Values["UserName"] = value.UserName;
                var filter = new HttpBaseProtocolFilter();
                foreach (var cookie in filter.CookieManager.GetCookies(new Uri(value.Resource)))
                {
                    filter.CookieManager.DeleteCookie(cookie);
                }
                
            }
        }

        public static long MaxDownloadSize
        {
            get
            {
                if (Config.Values.ContainsKey("MaxDownloadSize"))
                    return (long)Config.Values["MaxDownloadSize"];
                return 500;
            }
            set { Config.Values["MaxDownloadSize"] = value; }
        }

        public static bool IsFirstRun
        {
            get
            {
                if (Config.Values.ContainsKey("IsFirstRun"))
                    return (bool)Config.Values["IsFirstRun"];
                return true;
            }
            set { Config.Values["IsFirstRun"] = value; }
        }

        public static NetworkCredential Credential => new NetworkCredential(UserName, Password);

        /// <summary>
        /// Indicates wether the background task is enabled so it can be registered again after an app update
        /// </summary>
        public static bool IsBackgroundTaskEnabled
        {
            get
            {
                if (Config.Values.ContainsKey("IsBackgroundTaskEnabled"))
                    return (bool)Config.Values["IsBackgroundTaskEnabled"];
                return true;
            }
            set { Config.Values["IsBackgroundTaskEnabled"] = value; }
        }

        public static void Reset()
        {
            //var manager = new UserAccountManager();
            //foreach (var passwordCredential in manager.GetAllAccounts())
            //{
            //    manager.DeleteAccount(passwordCredential);
            //}
            ApplicationData.Current.LocalSettings.DeleteContainer(ContainerName);
            
        }
    }
}
