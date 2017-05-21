using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Credentials;

namespace OwncloudUniversal.WebDav.Model
{
    public class Account
    {
        public PasswordCredential Credentials { get; set; }
        public string DisplayName { get; set; }
        public int TotalStorage { get; set; }
        public int UsedStorage { get; set; }
        public string AvatarUrl { get; set; }
    }
}
