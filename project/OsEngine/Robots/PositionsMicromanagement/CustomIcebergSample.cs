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

namespace OsEngine.Robots.PositionsMicromanagement
{
    [Bot("CustomIcebergSample")]
    public class CustomIcebergSample : BotPanel
    {
        private BotTabSimple _tab;

        private Aindicator _bollinger;

        public StrategyParameterString Regime;

        public StrategyParameterInt BollingerLength;

        public StrategyParameterDecimal BollingerDeviation;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterDecimal ProfitPercent;

        public StrategyParameterDecimal StopPercent;

        public StrategyParameterInt IcebergSecondsBetweenOrders;

        public StrategyParameterInt IcebergCount;

        public CustomIcebergSample(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            VolumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            BollingerLength = CreateParameter("Bollinger length", 50, 10, 80, 3);
            BollingerDeviation = CreateParameter("Bollinger deviation", 1.1m, 0.5m, 5, 0.1m);

            ProfitPercent = CreateParameter("Profit percent", 0.3m, 1.0m, 50, 4);
            StopPercent = CreateParameter("Stop percent", 0.5m, 1.0m, 50, 4);

            IcebergCount = CreateParameter("Iceberg count ", 5, 1, 50, 4);
            IcebergSecondsBetweenOrders = CreateParameter("Iceberg seconds between orders ", 5, 1, 50, 4);

            _bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _bollinger = (Aindicator)_tab.CreateCandleIndicator(_bollinger, "Prime");
            _bollinger.ParametersDigit[0].Value = BollingerLength.ValueInt;
            _bollinger.ParametersDigit[1].Value = BollingerDeviation.ValueDecimal;
            _bollinger.Save();

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            ParametrsChangeByUser += Event_ParametrsChangeByUser;

            _tab.ManualPositionSupport.DisableManualSupport();

            Description = "Countertrend robot on bollinger indicator. Inside of which an example of entering a position by multiple orders through its own logic is implemented.";
        }

        void Event_ParametrsChangeByUser()
        {
            if (BollingerLength.ValueInt != _bollinger.ParametersDigit[0].Value ||
               _bollinger.ParametersDigit[1].Value != BollingerDeviation.ValueDecimal)
            {
                _bollinger.ParametersDigit[0].Value = BollingerLength.ValueInt;
                _bollinger.ParametersDigit[1].Value = BollingerDeviation.ValueDecimal;
                _bollinger.Reload();
                _bollinger.Save();
            }
        }

        public override string GetNameStrategyType()
        {
            return "CustomIcebergSample";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // logic

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
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
                if (Regime.ValueString == "OnlyClosePosition")
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

        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal bollingerUp = _bollinger.DataSeries[0].Values[_bollinger.DataSeries[0].Values.Count - 1];
            decimal bollingerDown = _bollinger.DataSeries[2].Values[_bollinger.DataSeries[2].Values.Count - 1];

            if (bollingerUp == 0
                || bollingerDown == 0)
            {
                return;
            }

            if (lastPrice > bollingerUp
                && Regime.ValueString != "OnlyLong")
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
                    icebergMaker.OrdersCount = IcebergCount.ValueInt;
                    icebergMaker.SecondsBetweenOrders = IcebergSecondsBetweenOrders.ValueInt;
                    icebergMaker.Tab = _tab;
                    icebergMaker.Side = Side.Sell;
                    icebergMaker.Start();
                }
            }
            if (lastPrice < bollingerDown
                && Regime.ValueString != "OnlyShort")
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
                    icebergMaker.OrdersCount = IcebergCount.ValueInt;
                    icebergMaker.SecondsBetweenOrders = IcebergSecondsBetweenOrders.ValueInt;
                    icebergMaker.Tab = _tab;
                    icebergMaker.Side = Side.Buy;
                    icebergMaker.Start();
                }
            }
        }

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
                profitPrice = position.EntryPrice + position.EntryPrice * (ProfitPercent.ValueDecimal / 100);

                if(lastPrice >= profitPrice)
                {
                    ClosePos(position);
                }
            }
            else if (position.Direction == Side.Sell)
            {
                profitPrice = position.EntryPrice - position.EntryPrice * (ProfitPercent.ValueDecimal / 100);

                if (lastPrice <= profitPrice)
                {
                    ClosePos(position);
                }
            }

            // stop

            decimal stopPrice = 0;

            if (position.Direction == Side.Buy)
            {
                stopPrice = position.EntryPrice - position.EntryPrice * (StopPercent.ValueDecimal / 100);

                if (lastPrice <= stopPrice)
                {
                    ClosePos(position);
                }
            }
            else if (position.Direction == Side.Sell)
            {
                stopPrice = position.EntryPrice + position.EntryPrice * (StopPercent.ValueDecimal / 100);

                if (lastPrice >= stopPrice)
                {
                    ClosePos(position);
                }
            }
            
        }

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
                icebergMaker.OrdersCount = IcebergCount.ValueInt;
                icebergMaker.SecondsBetweenOrders = IcebergSecondsBetweenOrders.ValueInt;
                icebergMaker.Tab = _tab;
                icebergMaker.PositionToClose = position;
                icebergMaker.Start();
            }
        }

        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (VolumeType.ValueString == "Contracts")
            {
                volume = Volume.ValueDecimal;
            }
            else if (VolumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = Volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Securiti.Lot != 0 &&
                        tab.Securiti.Lot > 1)
                    {
                        volume = Volume.ValueDecimal / (contractPrice * tab.Securiti.Lot);
                    }

                    volume = Math.Round(volume, tab.Securiti.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (VolumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (TradeAssetInPortfolio.ValueString == "Prime")
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
                        if (positionOnBoard[i].SecurityNameCode == TradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + TradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (Volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Securiti.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    qty = Math.Round(qty, tab.Securiti.DecimalsVolume);
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
                    curVolume = Math.Round(curVolume, Tab.Securiti.DecimalsVolume);
                    allVolumeInArray += curVolume;
                    volumes.Add(curVolume);
                }

                if (allVolumeInArray != VolumeOnAllOrders)
                {
                    decimal residue = VolumeOnAllOrders - allVolumeInArray;

                    volumes[0] = Math.Round(volumes[0] + residue, Tab.Securiti.DecimalsVolume);
                }

                for (int i = 0; i < volumes.Count; i++)
                {
                    if (Side == Side.Buy)
                    {
                        if (Tab.PositionsOpenAll.Count == 0)
                        {
                            Tab.BuyAtMarket(volumes[i]);
                        }
                        else
                        {
                            Tab.BuyAtMarketToPosition(Tab.PositionsOpenAll[0], volumes[i]);
                        }
                    }
                    if (Side == Side.Sell)
                    {
                        if (Tab.PositionsOpenAll.Count == 0)
                        {
                            Tab.SellAtMarket(volumes[i]);
                        }
                        else
                        {
                            Tab.SellAtMarketToPosition(Tab.PositionsOpenAll[0], volumes[i]);
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

        private void ClosePositionMethod()
        {
            try
            {
                int iterationCount = 0;

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
                    curVolume = Math.Round(curVolume, Tab.Securiti.DecimalsVolume);
                    allVolumeInArray += curVolume;
                    volumes.Add(curVolume);
                }

                if (allVolumeInArray != VolumeOnAllOrders)
                {
                    decimal residue = VolumeOnAllOrders - allVolumeInArray;

                    volumes[0] = Math.Round(volumes[0] + residue, Tab.Securiti.DecimalsVolume);
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