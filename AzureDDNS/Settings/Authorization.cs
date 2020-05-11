using System;
using System.Collections.Generic;
using System.Text;

namespace AzureDDNS.Settings
{
    public class Authorization
    {
        public bool Enabled { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        private string encodedAuthorizationString;
        public string GetBase64AuthorizationString()
        {
            if (encodedAuthorizationString == null)
            {
                if (!Enabled)
                    return string.Empty;

                encodedAuthorizationString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{Password}"));
            }

            return encodedAuthorizationString;
        }
    }
}
