using OwncloudUniversal.WebDav.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Credentials;

namespace OwncloudUniversal.WebDav
{
    public class UserAccountManager
    {
        public async Task<Account> AddAccountAsync(string url, string userName, string password)
        {
            var credential = new PasswordCredential(url,userName,password);
            var vault = new PasswordVault();
            DeleteAccount(new Account { Credentials = credential });
            vault.Add(credential);
            return await GetAccount(url, userName);
        }

        public async Task<Account> GetAccount(string url, string userName)
        {
            var vault = new PasswordVault();
            var credential = new PasswordCredential();
            foreach (var passwordCredential in vault.RetrieveAll())
            {
                if (passwordCredential.Resource == url && passwordCredential.UserName == userName)
                {
                    passwordCredential.RetrievePassword();
                    credential = passwordCredential;
                }
            }
            credential.RetrievePassword();
            var client = new OcsClient(new Uri(url), new NetworkCredential(userName, credential.Password));
            var account = await client.GetUserInfoAsync();
            return account;
        }

        public async Task<List<Account>> GetAllAccountsAsync()
        {
            var vault = new PasswordVault();
            var result = new List<Account>();
            foreach (var passwordCredential in vault.RetrieveAll())
            {
                try
                {
                    passwordCredential.RetrievePassword();
                    var client = new OcsClient(new Uri(passwordCredential.Resource), new NetworkCredential(passwordCredential.UserName, passwordCredential.Password));
                    result.Add(await client.GetUserInfoAsync());
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }
            return result;
        }

        public void DeleteAccount(Account account)
        {
            var vault = new PasswordVault();
            var credentials = vault.RetrieveAll();
            foreach (var pc in credentials)
            {
                if (pc.Resource == account.Credentials.Resource && pc.UserName == account.Credentials.UserName)
                    vault.Remove(pc);
            }
        }
    }
}
