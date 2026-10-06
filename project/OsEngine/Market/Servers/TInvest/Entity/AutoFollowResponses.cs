/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.TInvest.Entity
{
    // DTO под T-Invest Autofollow API (REST, JSON).
    // Числа и даты храним строками, конвертация через ToDecimal()/DateTime.Parse на месте использования.

    public class AfStrategiesResponse
    {
        public List<AfStrategy> strategies { get; set; }
    }

    public class AfStrategy
    {
        public string strategyId { get; set; }

        public string title { get; set; }

        public string status { get; set; } // draft / active / frozen / closed
    }

    public class AfInstrumentsResponse
    {
        public List<AfInstrument> instruments { get; set; }
    }

    public class AfInstrument
    {
        public string uid { get; set; }

        public string positionUid { get; set; }

        public string ticker { get; set; }

        public string classCode { get; set; }

        public string currency { get; set; }

        public string instrumentType { get; set; } // bond / share / etf / currency / futures / option / sp

        public string lot { get; set; }

        public string minPriceIncrement { get; set; }

        public string nominal { get; set; }

        public string initialNominal { get; set; }

        public string name { get; set; }

        public string nominalCurrency { get; set; }

        public string exchange { get; set; }

        public string expirationDate { get; set; }
    }

    public class AfPortfolioPositionResponse
    {
        public List<AfPosition> positions { get; set; }

        public List<AfMoney> money { get; set; }
    }

    public class AfPosition
    {
        public string instrumentUid { get; set; }

        public string positionUid { get; set; }

        public string ticker { get; set; }

        public string instrumentType { get; set; }

        public string lots { get; set; }

        public string changedAt { get; set; }
    }

    public class AfMoney
    {
        public string currency { get; set; }

        public string quantity { get; set; }
    }

    public class AfSignalsResponse
    {
        public List<AfSignal> signals { get; set; }
    }

    public class AfSignal
    {
        public string signalId { get; set; }

        public string instrumentUid { get; set; }

        public string positionUid { get; set; }

        public string ticker { get; set; }

        public string direction { get; set; } // buy / sell

        public string instrumentType { get; set; }

        public string lotsRequested { get; set; }

        public string lotsExecuted { get; set; }

        public string currency { get; set; }

        public string totalAmount { get; set; }

        public string signalName { get; set; }
    }

    public class AfStopSignalsResponse
    {
        public List<AfStopSignal> stopSignals { get; set; }
    }

    public class AfStopSignal
    {
        public string stopSignalId { get; set; }

        public string instrumentUid { get; set; }

        public string positionUid { get; set; }

        public string ticker { get; set; }

        public string stopOrderType { get; set; } // take_profit / stop_loss

        public string direction { get; set; } // buy / sell

        public string lots { get; set; }

        public string instrumentType { get; set; }

        public string stopPrice { get; set; }

        public string currency { get; set; }

        public string totalAmount { get; set; }

        public string createDate { get; set; }

        public string expireDate { get; set; }
    }

    public class AfPostSignalRequest
    {
        public string instrumentId { get; set; }

        public string direction { get; set; } // buy / sell

        public decimal lots { get; set; }
    }

    public class AfPostSignalResponse
    {
        public string signalId { get; set; }

        public string requestAcceptanceType { get; set; } // full / fractional
    }

    public class AfPostStopSignalRequest
    {
        public string instrumentId { get; set; }

        public string stopOrderType { get; set; } // take_profit / stop_loss

        public string direction { get; set; } // buy / sell

        public decimal lots { get; set; }

        public decimal stopPrice { get; set; }

        public string expireDate { get; set; }
    }

    public class AfPostStopSignalResponse
    {
        public string stopSignalId { get; set; }
    }

    public class AfErrorResponse
    {
        public string errorId { get; set; }

        public string errorCode { get; set; }

        public string errorMessage { get; set; }
    }
}
