using System;
using System.Net;

namespace collectResourcesOfHACG
{
    class WebClient : System.Net.WebClient
    {
        public const int Timeout = 5;
        protected override WebRequest GetWebRequest(Uri address)
        {
            HttpWebRequest request = (HttpWebRequest)base.GetWebRequest(address);
            request.Timeout = 1000 * Timeout;
            request.ReadWriteTimeout = 1000 * Timeout;
            return request;
        }
    }
}