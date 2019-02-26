/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Entity
{
    /// <summary>
    /// класс хранящий в себе данные для высылки запросов через прокси сервера
    /// </summary>
    public class ProxyHolder
    {
        /// <summary>
        /// айпи
        /// </summary>
        public string Ip;

        /// <summary>
        /// имя пользователя
        /// </summary>
        public string UserName;

        /// <summary>
        /// пароль доступа
        /// </summary>
        public string UserPassword;

        /// <summary>
        /// взять строку для сохранения 
        /// </summary>
        /// <returns></returns>
        public string GetStringToSave()
        {
            string result = Ip + "%";

            result += UserName + "%";
            result += UserPassword;

            return result;
        }

        /// <summary>
        /// загрузить состояние прокси из стороки
        /// </summary>
        /// <param name="saveStr"></param>
        public void LoadFromString(string saveStr)
        {
            Ip = saveStr.Split('%')[0];
            UserName = saveStr.Split('%')[1];
            UserPassword = saveStr.Split('%')[2];
        }

    }
}
