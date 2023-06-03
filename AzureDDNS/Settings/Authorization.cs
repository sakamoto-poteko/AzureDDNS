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

        private string _encodedAuthorizationString;
        public string GetBase64AuthorizationString()
        {
            if (_encodedAuthorizationString != null)
                return _encodedAuthorizationString;
            
            if (!Enabled)
                return string.Empty;

            _encodedAuthorizationString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{Password}"));

            return _encodedAuthorizationString;
        }
    }
}
