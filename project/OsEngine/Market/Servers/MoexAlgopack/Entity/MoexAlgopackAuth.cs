using System;
using System.Net;

namespace OsEngine.Market.Servers.MoexAlgopack.Entity
{
    public class MoexAlgopackAuth
    {
        private string _urlAuth = "https://passport.moex.com/authenticate";
        private string _urlUri = "https://www.moex.com";
        private string _username;
        private string _password;
        public HttpStatusCode last_status;
        public string last_status_text;
        public Cookie Passport = new Cookie();
        
        public MoexAlgopackAuth(string user_name, string passwd)
        {
            _username = user_name;
            _password = passwd;
            Auth();
        }

        public void Auth()
        {
            try
            {
                HttpWebRequest authReq = (HttpWebRequest)WebRequest.Create(_urlAuth);
                HttpWebResponse authResponse;
                authReq.CookieContainer = new CookieContainer();

                byte[] binData;
                binData = System.Text.Encoding.UTF8.GetBytes(_username + ":" + _password);
                string sAuth64 = Convert.ToBase64String(binData, Base64FormattingOptions.None);
                authReq.Headers.Add(HttpRequestHeader.Authorization, "Basic " + sAuth64);

                authResponse = (HttpWebResponse)authReq.GetResponse();
                authResponse.Close();
                last_status = authResponse.StatusCode;

                CookieContainer cookiejar = new CookieContainer();
                cookiejar.Add(authResponse.Cookies);

                // find the Passport cookie for a given domain URI
                Uri myuri = new Uri(_urlUri);
                CookieCollection cookies = cookiejar.GetCookies(myuri);

                for (int i = 0; i < cookies.Count; i++)
                {
                    Cookie cook = cookies[i];
                    if (cook.Name == "MicexPassportCert")
                    {
                        Passport = cook;
                        break;
                    }
                }
                last_status_text = Passport.Name != "MicexPassportCert" ? "Passport cookie not found" : "OK";
            }
            catch (WebException e)
            {
                if (e.Status == WebExceptionStatus.ProtocolError)
                {
                    last_status = ((HttpWebResponse)e.Response).StatusCode;
                    last_status_text = ((HttpWebResponse)e.Response).StatusDescription;
                }
                else
                {
                    last_status = HttpStatusCode.BadRequest;
                    last_status_text = e.Message;
                }
            }
            catch (Exception e)
            {
                last_status = HttpStatusCode.BadRequest;
                last_status_text = e.Message;
            }
        }

        public bool IsRealTime()
        {
            if (Passport == null || (Passport != null && Passport.Expired))
            {
                Auth();
            }

            if (Passport != null && !Passport.Expired && Passport.Name == "MicexPassportCert")
            {
                return true;
            }
            
            return false;
        }
    }
}
