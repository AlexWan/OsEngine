using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

// 1 добавляем коннектор тинькова и сервера в юзинги
using OsEngine.Market.Servers.Tinkoff;
using OsEngine.Market.Servers;

namespace OsEngine.Robots.TechSapmles
{
    [Bot("TinkoffGetRealCandlesSample")]
    public class TinkoffGetRealCandlesSample : BotPanel
    {
        public TinkoffGetRealCandlesSample(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            //создание вкладки
            TabCreate(BotTabType.Simple);
            // 2 подписываемся на завершение свечи
            TabsSimple[0].CandleFinishedEvent += CandleEngine_CandleFinishedEvent;
        }

        private void CandleEngine_CandleFinishedEvent(System.Collections.Generic.List<Candle> candles)
        {
            // 3 берём сервер у коннектора
            IServer server = TabsSimple[0].Connector.MyServer;

            if (server.ServerType != ServerType.Tinkoff)
            { // 4 если севрер не Тинькофф - выходим
                return;
            }

            TinkoffServer tServer = (TinkoffServer)server;
            // 5 обновляем свечи прямо из сервера. 
            // заставляем сервак принудительно запросить свечки в том виде в котором их видит биржа
            tServer.UpDateCandleSeries(TabsSimple[0].Connector.CandleSeries);
        }

        public override string GetNameStrategyType()
        {
            return "TinkoffGetRealCandlesSample";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}