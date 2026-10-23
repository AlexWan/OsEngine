/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;

namespace OsEngine.Market.Servers.TelegramNews
{
    /// <summary>
    /// News from Telegram connector. Allows robot to distinguish edited posts:
    /// Source and TimeMessage are the same as in the original news, IsEdited is true
    /// </summary>
    public class TelegramNewsMessage : News
    {
        public bool IsEdited { get; set; }
    }
}
