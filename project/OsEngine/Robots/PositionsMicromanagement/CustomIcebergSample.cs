/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using OsEngine.Logging;
using System.Threading;
using OsEngine.Language;

/* Description
trading robot for osengine

Countertrend robot on bollinger indicator. 

Inside of which an example of entering a position by multiple orders through its own logic is implemented.
 */

namespace OsEngine.Robots.PositionsMicromanagement
{
    [Bot("CustomIcebergSample")] // We create an attribute so that we don't write anything to the BotFactory
    public class CustomIcebergSample : BotPanel
    {
        private BotTabSimple _tab;

        // Basic setting
        private StrategyParameterString _regime;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;
        
        // Indicator
        private Aindicator _bollinger;

        // Indicator settings
        private StrategyParameterInt _bollingerLength;
        private StrategyParameterDecimal _bollingerDeviation;

        // Exit settings
        private StrategyParameterDecimal _profitPercent;
        private StrategyParameterDecimal _stopPercent;
        private StrategyParameterInt _icebergSecondsBetweenOrders;
        private StrategyParameterInt _icebergCount;

        public CustomIcebergSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic setting
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _bollingerLength = CreateParameter("Bollinger length", 50, 10, 80, 3);
            _bollingerDeviation = CreateParameter("Bollinger deviation", 1.1m, 0.5m, 5, 0.1m);

            // Exit settings
            _profitPercent = CreateParameter("Profit percent", 0.3m, 1.0m, 50, 4);
            _stopPercent = CreateParameter("Stop percent", 0.5m, 1.0m, 50, 4);
            _icebergCount = CreateParameter("Iceberg count ", 5, 1, 50, 4);
            _icebergSecondsBetweenOrders = CreateParameter("Iceberg seconds between orders ", 5, 1, 50, 4);

            // Create indicator Bollinger
            _bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _bollinger = (Aindicator)_tab.CreateCandleIndicator(_bollinger, "Prime");
            _bollinger.ParametersDigit[0].Value = _bollingerLength.ValueInt;
            _bollinger.ParametersDigit[1].Value = _bollingerDeviation.ValueDecimal;
            _bollinger.Save();

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += Event_ParametrsChangeByUser;

            _tab.ManualPositionSupport.DisableManualSupport();

            Description = OsLocalization.Description.DescriptionLabel81;
        }

        void Event_ParametrsChangeByUser()
        {
            if (_bollingerLength.ValueInt != _bollinger.ParametersDigit[0].Value ||
               _bollinger.ParametersDigit[1].Value != _bollingerDeviation.ValueDecimal)
            {
                _bollinger.ParametersDigit[0].Value = _bollingerLength.ValueInt;
                _bollinger.ParametersDigit[1].Value = _bollingerDeviation.ValueDecimal;
                _bollinger.Reload();
                _bollinger.Save();
            }
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CustomIcebergSample";
        }

        // Show setting GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (_bollinger.DataSeries[0].Values == null
                || _bollinger.DataSeries[1].Values == null)
            {
                return;
            }

            if (_bollinger.DataSeries[0].Values.Count < _bollinger.ParametersDigit[0].Value + 2
                || _bollinger.DataSeries[1].Values.Count < _bollinger.ParametersDigit[1].Value + 2)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                if (_regime.ValueString == "OnlyClosePosition")
                {
                    return;
                }
                LogicOpenPosition(candles);
            }
            else
            {
                LogicClosePosition(candles, openPositions[0]);
            }
        }

        // Opening position logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal bollingerUp = _bollinger.DataSeries[0].Values[_bollinger.DataSeries[0].Values.Count - 1];
            decimal bollingerDown = _bollinger.DataSeries[1].Values[_bollinger.DataSeries[1].Values.Count - 1];

            if (bollingerUp == 0
                || bollingerDown == 0)
            {
                return;
            }

            if (lastPrice > bollingerUp
                && _regime.ValueString != "OnlyLong")
            {
                if (StartProgram == StartProgram.IsTester 
                    || StartProgram == StartProgram.IsOsOptimizer)
                {
                    _tab.SellAtMarket(GetVolume(_tab));
                }
                else if (StartProgram == StartProgram.IsOsTrader)
                {
                    IcebergMaker icebergMaker = new IcebergMaker();
                    icebergMaker.VolumeOnAllOrders = GetVolume(_tab);
                    icebergMaker.OrdersCount = _icebergCount.ValueInt;
                    icebergMaker.SecondsBetweenOrders = _icebergSecondsBetweenOrders.ValueInt;
                    icebergMaker.Tab = _tab;
                    icebergMaker.Side = Side.Sell;
                    icebergMaker.Start();
                }
            }
            if (lastPrice < bollingerDown
                && _regime.ValueString != "OnlyShort")
            {
                if (StartProgram == StartProgram.IsTester
                   || StartProgram == StartProgram.IsOsOptimizer)
                {
                    _tab.BuyAtMarket(GetVolume(_tab));
                }
                else if (StartProgram == StartProgram.IsOsTrader)
                {
                    IcebergMaker icebergMaker = new IcebergMaker();
                    icebergMaker.VolumeOnAllOrders = GetVolume(_tab);
                    icebergMaker.OrdersCount = _icebergCount.ValueInt;
                    icebergMaker.SecondsBetweenOrders = _icebergSecondsBetweenOrders.ValueInt;
                    icebergMaker.Tab = _tab;
                    icebergMaker.Side = Side.Buy;
                    icebergMaker.Start();
                }
            }
        }

        // Close position logic
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            decimal lastPrice = candles[candles.Count-1].Close;

            // profit

            decimal profitPrice = 0;

            if (position.Direction == Side.Buy)
            {
                profitPrice = position.EntryPrice + position.EntryPrice * (_profitPercent.ValueDecimal / 100);

                if(lastPrice >= profitPrice)
                {
                    ClosePos(position);
                }
            }
            else if (position.Direction == Side.Sell)
            {
                profitPrice = position.EntryPrice - position.EntryPrice * (_profitPercent.ValueDecimal / 100);

                if (lastPrice <= profitPrice)
                {
                    ClosePos(position);
                }
            }

            // stop

            decimal stopPrice = 0;

            if (position.Direction == Side.Buy)
            {
                stopPrice = position.EntryPrice - position.EntryPrice * (_stopPercent.ValueDecimal / 100);

                if (lastPrice <= stopPrice)
                {
                    ClosePos(position);
                }
            }
            else if (position.Direction == Side.Sell)
            {
                stopPrice = position.EntryPrice + position.EntryPrice * (_stopPercent.ValueDecimal / 100);

                if (lastPrice >= stopPrice)
                {
                    ClosePos(position);
                }
            }
            
        }

        // Close position
        private void ClosePos(Position position)
        {
            if(StartProgram == StartProgram.IsTester ||
                StartProgram == StartProgram.IsOsOptimizer)
            {
                _tab.CloseAtMarket(position, position.OpenVolume);
            }
            else if(StartProgram == StartProgram.IsOsTrader)
            {
                IcebergMaker icebergMaker = new IcebergMaker();
                icebergMaker.OrdersCount = _icebergCount.ValueInt;
                icebergMaker.SecondsBetweenOrders = _icebergSecondsBetweenOrders.ValueInt;
                icebergMaker.Tab = _tab;
                icebergMaker.PositionToClose = position;
                icebergMaker.Start();
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

    public class IcebergMaker
    {
        public int OrdersCount;

        public int SecondsBetweenOrders;

        public decimal VolumeOnAllOrders;

        public BotTabSimple Tab;

        public Side Side;

        public Position PositionToClose;

        public Position OpeningPosition;

        public void Start()
        {
            if (PositionToClose == null)
            {
                Thread worker = new Thread(OpenPositionMethod);
                worker.Start();
            }
            else
            {
                Thread worker = new Thread(ClosePositionMethod);
                worker.Start();
            }
        }

        // Opening position logic
        private void OpenPositionMethod()
        {
            try
            {
                if (OrdersCount < 1)
                {
                    OrdersCount = 1;
                }

                List<decimal> volumes = new List<decimal>();

                decimal allVolumeInArray = 0;

                for (int i = 0; i < OrdersCount; i++)
                {
                    decimal curVolume = VolumeOnAllOrders / OrdersCount;
                    curVolume = Math.Round(curVolume, Tab.Security.DecimalsVolume);
                    allVolumeInArray += curVolume;
                    volumes.Add(curVolume);
                }

                if (allVolumeInArray != VolumeOnAllOrders)
                {
                    decimal residue = VolumeOnAllOrders - allVolumeInArray;

                    volumes[0] = Math.Round(volumes[0] + residue, Tab.Security.DecimalsVolume);
                }

                for (int i = 0; i < volumes.Count; i++)
                {
                    if (Side == Side.Buy)
                    {
                        if (OpeningPosition == null)
                        {
                            OpeningPosition = Tab.BuyAtMarket(volumes[i]);
                        }
                        else
                        {
                            Tab.BuyAtMarketToPosition(OpeningPosition, volumes[i]);
                        }
                    }
                    if (Side == Side.Sell)
                    {
                        if (OpeningPosition == null)
                        {
                            OpeningPosition = Tab.SellAtMarket(volumes[i]);
                        }
                        else
                        {
                            Tab.SellAtMarketToPosition(OpeningPosition, volumes[i]);
                        }
                    }
                    Thread.Sleep(SecondsBetweenOrders * 1000);
                }
            }
            catch (Exception error)
            {
                Tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // Close position logic
        private void ClosePositionMethod()
        {
            try
            {
                if (OrdersCount < 1)
                {
                    OrdersCount = 1;
                }

                VolumeOnAllOrders = PositionToClose.OpenVolume;

                List<decimal> volumes = new List<decimal>();

                decimal allVolumeInArray = 0;

                for (int i = 0; i < OrdersCount; i++)
                {
                    decimal curVolume = VolumeOnAllOrders / OrdersCount;
                    curVolume = Math.Round(curVolume, Tab.Security.DecimalsVolume);
                    allVolumeInArray += curVolume;
                    volumes.Add(curVolume);
                }

                if (allVolumeInArray != VolumeOnAllOrders)
                {
                    decimal residue = VolumeOnAllOrders - allVolumeInArray;

                    volumes[0] = Math.Round(volumes[0] + residue, Tab.Security.DecimalsVolume);
                }

                for (int i = 0; i < volumes.Count; i++)
                {
                    Tab.CloseAtMarket(PositionToClose, volumes[i]);

                    Thread.Sleep(SecondsBetweenOrders * 1000);
                }
            }
            catch (Exception error)
            {
                Tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
    }
}