/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;

namespace OsEngine.Market.Servers.Entity
{

    /// <summary>
    /// class for sending bids and asks
    /// класс для рассылки бида с аском
    /// </summary>
    public class BidAskSender
    {
        public decimal Bid;

        public decimal Ask;

        public Security Security;
    }

}
