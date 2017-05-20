using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Credentials;

namespace OwncloudUniversal.Shared
{
    public class UserAccountManager
    {
        public PasswordCredential AddAccount(string url, string userName, string password)
        {
            var credential = new PasswordCredential(url,userName,password);
            var vault = new PasswordVault();
            DeleteAccount(credential);
            vault.Add(credential);
            return credential;
        }

        public PasswordCredential GetAccount(string url, string userName)
        {
            var vault = new PasswordVault();
            foreach (var passwordCredential in vault.RetrieveAll())
            {
                if (passwordCredential.Resource == url && passwordCredential.UserName == userName)
                {
                    passwordCredential.RetrievePassword();
                    return passwordCredential;
                }
            }
            return null;
        }

        public List<PasswordCredential> GetAllAccounts()
        {
            var vault = new PasswordVault();
            return vault.RetrieveAll().ToList();
        }

        public void DeleteAccount(PasswordCredential credential)
        {
            var vault = new PasswordVault();
            var credentials = vault.RetrieveAll();
            foreach (var pc in credentials)
            {
                if (pc.Resource == credential.Resource && pc.UserName == credential.UserName)
                    vault.Remove(pc);
            }
        }
    }
}
