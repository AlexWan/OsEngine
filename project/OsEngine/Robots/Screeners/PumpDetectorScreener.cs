/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The trend robot on Pump Detector Screener.

Buy: If the price change exceeds the specified threshold (MoveToEntry)

Exit: by stop and profit.
 */

namespace OsEngine.Robots.Screeners
{
    [Bot("PumpDetectorScreener")] // We create an attribute so that we don't write anything to the BotFactory
    public class PumpDetectorScreener : BotPanel
    {
        private BotTabScreener _tabScreener;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _maxPositions;
        private StrategyParameterInt _secondsToAnalyze;
        private StrategyParameterDecimal _moveToEntry;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Exit settings
        private StrategyParameterDecimal _profitPercent;
        private StrategyParameterDecimal _ptopPercent;

        public PumpDetectorScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];
            _tabScreener.NewTickEvent += _tabScreener_NewTickEvent;
            _tabScreener.PositionOpeningSuccesEvent += _tabScreener_PositionOpeningSuccesEvent;

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On"});
            _maxPositions = CreateParameter("Max positions", 5, 0, 20, 1);
            _secondsToAnalyze = CreateParameter("Seconds to analyze", 2, 0, 20, 1);
            _moveToEntry = CreateParameter("Move to entry", 1m, 0, 20, 1);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Exit settings
            _profitPercent = CreateParameter("Profit percent", 1.5m, 0, 20, 1m);
            _ptopPercent = CreateParameter("Stop percent", 0.5m, 0, 20, 1m);

            Description = OsLocalization.Description.DescriptionLabel93;

            if (startProgram == StartProgram.IsTester)
            {
                List<IServer> servers = ServerMaster.GetServers();

                if (servers != null 
                    && servers.Count > 0
                     && servers[0].ServerType == ServerType.Tester)
                {
                    TesterServer server = (TesterServer)servers[0];
                    server.TestingStartEvent += Server_TestingStartEvent;
                }
            }
        }

        private void Server_TestingStartEvent()
        {
            _checkMoveTimes.Clear();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PumpDetectorScreener";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // trade logic

        private List<CheckMoveTime> _checkMoveTimes = new List<CheckMoveTime>();

        // Opening logic
        private void _tabScreener_NewTickEvent(Trade newTrade, BotTabSimple tab)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (_tabScreener.PositionsOpenAll.Count >= _maxPositions.ValueInt)
            {
                return;
            }

            if (tab.PositionsOpenAll.Count > 0)
            {
                return;
            }

            CheckMoveTime myTime = null;

            for(int i = 0;i < _checkMoveTimes.Count;i++)
            {
                if (_checkMoveTimes[i].SecName == tab.Connector.SecurityName 
                    && _checkMoveTimes[i].SecClass == tab.Connector.SecurityClass)
                {
                    myTime = _checkMoveTimes[i]; 
                    break;
                }
            }

            if(myTime == null)
            {
                myTime = new CheckMoveTime();
                myTime.SecClass = tab.Connector.SecurityClass;
                myTime.SecName = tab.Connector.SecurityName;
                _checkMoveTimes.Add(myTime);
            }

            if(myTime.LastCheckTime.AddSeconds(_secondsToAnalyze.ValueInt) >= newTrade.Time)
            {
                return;
            }

            myTime.LastCheckTime = newTrade.Time;

            decimal startPrice = decimal.MaxValue;

            List<Trade> trades = tab.Trades;

            int secondsCount = 0;
            int secondNow = newTrade.Time.Second;

            for(int i =  trades.Count - 1; i >= 0; i--)
            {
                if (trades[i].Price < startPrice)
                {
                    startPrice = trades[i].Price;
                }

                if (trades[i].Time.Second != secondNow)
                {
                    secondsCount++;
                    secondNow = trades[i].Time.Second;
                }

                if(secondsCount > _secondsToAnalyze.ValueInt)
                {
                    break;
                }
            }

            if(startPrice == decimal.MaxValue)
            {
                return;
            }
            
            decimal movePercent = (newTrade.Price - startPrice) / (startPrice / 100);

            if(movePercent > _moveToEntry.ValueDecimal)
            {
                tab.BuyAtMarket(GetVolume(tab));
            }
        }

        // Logic close position
        private void _tabScreener_PositionOpeningSuccesEvent(Position position, BotTabSimple tab)
        {
            decimal stopPrice = position.EntryPrice - position.EntryPrice * (_ptopPercent.ValueDecimal / 100);
            decimal profitOrderPrice = position.EntryPrice + position.EntryPrice * (_profitPercent.ValueDecimal / 100);

            tab.CloseAtStopMarket(position, stopPrice);
            tab.CloseAtProfitMarket(position, profitOrderPrice);
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume.ValueDecimal;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (_volumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (_tradeAssetInPortfolio.ValueString == "Prime")
                {
                    portfolioPrimeAsset = myPortfolio.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                    if (positionOnBoard == null)
                    {
                        return 0;
                    }

                    for (int i = 0; i < positionOnBoard.Count; i++)
                    {
                        if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                     && tab.Security.PriceStep != tab.Security.PriceStepCost
                     && tab.PriceBestAsk != 0
                     && tab.Security.PriceStep != 0
                     && tab.Security.PriceStepCost != 0)
                    {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                        qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                    }
                    qty = Math.Round(qty, tab.Security.DecimalsVolume);
                }
                else
                {
                    qty = Math.Round(qty, 7);
                }

                return qty;
            }

            return volume;
        }
    }

    public class CheckMoveTime
    {
        public string SecName;

        public string SecClass;

        public DateTime LastCheckTime;
    }
}