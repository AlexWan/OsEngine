/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Entity
{
    /// <summary>
    /// a class that stores data for sending requests through a proxy server
    /// </summary>
    public class ProxyHolder
    {
        /// <summary>
        /// IP
        /// </summary>
        public string Ip;

        /// <summary>
        /// user name
        /// </summary>
        public string UserName;

        /// <summary>
        /// user password
        /// </summary>
        public string UserPassword;

        /// <summary>
        /// to take a line to save 
        /// </summary>
        public string GetStringToSave()
        {
            string result = Ip + "%";

            result += UserName + "%";
            result += UserPassword;

            return result;
        }

        /// <summary>
        /// Load proxy status from a side
        /// </summary>
        public void LoadFromString(string saveStr)
        {
            Ip = saveStr.Split('%')[0];
            UserName = saveStr.Split('%')[1];
            UserPassword = saveStr.Split('%')[2];
        }

    }
}
