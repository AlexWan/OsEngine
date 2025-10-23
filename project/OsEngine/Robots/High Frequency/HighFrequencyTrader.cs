/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.Robots.High_Frequency
{
    // Robot analyzing the density of the market depth
    [Bot("HighFrequencyTrader")] // We create an attribute so that we don't write anything to the BotFactory
    public class HighFrequencyTrader : BotPanel
    {
        // Tab to trade
        private BotTabSimple _tab;

        // Basic settings
        private StrategyParameterString _regime;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Levels to marketDepth analyze
        private StrategyParameterInt _maxLevelsInMarketDepth;

        // Exit settings
        private StrategyParameterInt _stop;
        private StrategyParameterInt _profit;

        public HighFrequencyTrader(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create tabs
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic setting
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyClosePosition" });

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Levels to marketDepth analyze
            _maxLevelsInMarketDepth = CreateParameter("MaxLevelsInMarketDepth", 5, 3, 15, 1);

            // Exit settings
            _stop = CreateParameter("Stop", 5, 5, 15, 1);
            _profit = CreateParameter("Profit", 5, 5, 20, 1);

            // Subscribe event
            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
            _tab.PositionClosingFailEvent += _tab_PositionClosingFailEvent;

            // Create worker area
            Task task = new Task(ClosePositionThreadArea);
            task.Start();

            Description = OsLocalization.Description.DescriptionLabel43;

            DeleteEvent += HighFrequencyTrader_DeleteEvent;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "HighFrequencyTrader";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic

        // Last time check marketDepth
        private DateTime _lastCheckTime = DateTime.MinValue;

        // New marketDepth event
        void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (marketDepth.Asks == null || marketDepth.Asks.Count == 0 ||
                marketDepth.Bids == null || marketDepth.Bids.Count == 0)
            {
                return;
            }

            if (_tab.PositionsOpenAll.Find(pos => pos.State == PositionStateType.Open ||
                pos.State == PositionStateType.Closing
                ) != null)
            {
                return;
            }

            if (StartProgram == StartProgram.IsOsTrader &&
                _lastCheckTime.AddSeconds(1) > DateTime.Now)
            { // in real trade, check marketDepth once at second
                return;
            }

            _lastCheckTime = DateTime.Now;

            Position positionBuy = _tab.PositionsOpenAll.Find(pos => pos.Direction == Side.Buy);
            Position positionSell = _tab.PositionsOpenAll.Find(pos => pos.Direction == Side.Sell);

            // Buy

            decimal buyPrice = 0;
            int lastVolume = 0;

            for (int i = 0; i < marketDepth.Bids.Count && i < _maxLevelsInMarketDepth.ValueInt; i++)
            {
                if (marketDepth.Bids[i].Bid > lastVolume)
                {
                    buyPrice = marketDepth.Bids[i].Price.ToDecimal() + _tab.Security.PriceStep;
                    lastVolume = Convert.ToInt32(marketDepth.Bids[i].Bid);
                }
            }

            if (positionBuy != null &&
                positionBuy.OpenOrders[0].Price != buyPrice &&
                positionBuy.State != PositionStateType.Open &&
                positionBuy.State != PositionStateType.Closing)
            {
                if (StartProgram == StartProgram.IsOsTrader)
                {
                    _positionsToClose.Add(positionBuy);
                }
                else
                {
                    _tab.CloseAllOrderToPosition(positionBuy);
                }
                _tab.BuyAtLimit(GetVolume(_tab), buyPrice);
            }

            if (positionBuy == null)
            {
                _tab.BuyAtLimit(GetVolume(_tab), buyPrice);
            }

            // Sell

            decimal sellPrice = 0;
            int lastVolumeInAsk = 0;

            for (int i = 0; i < marketDepth.Asks.Count && i < _maxLevelsInMarketDepth.ValueInt; i++)
            {
                if (marketDepth.Asks[i].Ask > lastVolumeInAsk)
                {
                    sellPrice = marketDepth.Asks[i].Price.ToDecimal() - _tab.Security.PriceStep;
                    lastVolumeInAsk = Convert.ToInt32(marketDepth.Asks[i].Ask);
                }
            }

            if (positionSell != null &&
                positionSell.OpenOrders[0].Price != sellPrice &&
                positionSell.State != PositionStateType.Open &&
                positionSell.State != PositionStateType.Closing)
            {
                if (StartProgram == StartProgram.IsOsTrader)
                {
                    _positionsToClose.Add(positionSell);
                }
                else
                {
                    _tab.CloseAllOrderToPosition(positionSell);
                }

                _tab.SellAtLimit(GetVolume(_tab), sellPrice);
            }

            if (positionSell == null)
            {
                _tab.SellAtLimit(GetVolume(_tab), sellPrice);
            }
        }

        // Successful position opening
        void _tab_PositionOpeningSuccesEvent(Position position)
        {
            if (position.Direction == Side.Buy)
            {
                _tab.CloseAtStop(position, position.EntryPrice - _stop.ValueInt * _tab.Security.PriceStep, position.EntryPrice - _stop.ValueInt * _tab.Security.PriceStep);
                _tab.CloseAtProfit(position, position.EntryPrice + _profit.ValueInt * _tab.Security.PriceStep, position.EntryPrice + _profit.ValueInt * _tab.Security.PriceStep);
            }

            if (position.Direction == Side.Sell)
            {
                _tab.CloseAtStop(position, position.EntryPrice + _stop.ValueInt * _tab.Security.PriceStep, position.EntryPrice + _stop.ValueInt * _tab.Security.PriceStep);
                _tab.CloseAtProfit(position, position.EntryPrice - _profit.ValueInt * _tab.Security.PriceStep, position.EntryPrice - _profit.ValueInt * _tab.Security.PriceStep);
            }

            List<Position> positions = _tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].Number == position.Number)
                {
                    continue;
                }

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    _positionsToClose.Add(positions[i]);
                }
                else
                {
                    _tab.CloseAllOrderToPosition(positions[i]);
                }
            }
        }

        // The position is not closed and warrants are withdrawn from it
        void _tab_PositionClosingFailEvent(Position position)
        {
            if (position.CloseActive)
            {
                return;
            }
            _tab.CloseAtMarket(position, position.OpenVolume);
        }

        // withdrawal orders in real connection
        private void HighFrequencyTrader_DeleteEvent()
        {
            _isDeleted = true;
        }

        private bool _isDeleted = false;

        // Positions to be recalled
        List<Position> _positionsToClose = new List<Position>();

        // Place of work where orders are recalled in a real connection
        private async void ClosePositionThreadArea()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(1000);

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if(_isDeleted)
                    {
                        return;
                    }

                    for (int i = 0; _positionsToClose != null && i < _positionsToClose.Count; i++)
                    {
                        Position pos = _positionsToClose[i];

                        if (pos.State != PositionStateType.Opening)
                        {
                            continue;
                        }

                        if (pos.OpenOrders != null &&
                            pos.OpenOrders.Count > 0 &&
                            !string.IsNullOrWhiteSpace(pos.OpenOrders[0].NumberMarket))
                        {
                            _tab.CloseAllOrderToPosition(pos);
                            _positionsToClose.RemoveAt(i);
                            i--;
                        }
                    }
                }
                catch(Exception e)
                {
                    Thread.Sleep(5000);
                    _tab.SetNewLogMessage(e.ToString(),Logging.LogMessageType.Error);
                }
            }
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
}