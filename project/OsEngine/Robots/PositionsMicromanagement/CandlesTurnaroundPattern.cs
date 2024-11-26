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

namespace OsEngine.Robots.PositionsMicromanagement
{
    [Bot("CandlesTurnaroundPattern")]
    public class CandlesTurnaroundPattern : BotPanel
    {
        private BotTabSimple _tab;

        private Aindicator _atr;

        public StrategyParameterString Regime;

        public StrategyParameterInt AtrLength;

        public StrategyParameterDecimal AtrMultToEntry;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterDecimal StopPercent;

        public StrategyParameterDecimal ExitOnePercent;

        public StrategyParameterDecimal ExitTwoPercent;

        public StrategyParameterDecimal ExitThreePercent;

        public CandlesTurnaroundPattern(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "OnlyLong", "OnlyClosePosition" });

            VolumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 3, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            AtrLength = CreateParameter("Atr length", 25, 10, 80, 3);
            AtrMultToEntry = CreateParameter("Atr mult to entry", 0.3m, 0.1m, 80, 0.1m);

            StopPercent = CreateParameter("Stop percent", 0.9m, 1.0m, 50, 4);
            ExitOnePercent = CreateParameter("Exit one percent", 0.3m, 1.0m, 50, 4);
            ExitTwoPercent = CreateParameter("Exit two percent", 0.6m, 1.0m, 50, 4);
            ExitThreePercent = CreateParameter("Exit three percent", 0.9m, 1.0m, 50, 4);

            _atr = IndicatorsFactory.CreateIndicatorByName("ATR", name + "Atr", false);
            _atr = (Aindicator)_tab.CreateCandleIndicator(_atr, "atr");
            _atr.ParametersDigit[0].Value = AtrLength.ValueInt;
            _atr.Save();

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            ParametrsChangeByUser += Event_ParametrsChangeByUser;

            _tab.ManualPositionSupport.DisableManualSupport();

            Description = "Example of a robot that sequentially closes a position through 3 limit orders";
        }

        void Event_ParametrsChangeByUser()
        {
            if (AtrLength.ValueInt != _atr.ParametersDigit[0].Value)
            {
                _atr.ParametersDigit[0].Value = AtrLength.ValueInt;
                _atr.Reload();
                _atr.Save();
            }
        }

        public override string GetNameStrategyType()
        {
            return "CandlesTurnaroundPattern";
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

            if (_atr.DataSeries[0].Values == null)
            {
                return;
            }

            if (_atr.DataSeries[0].Values.Count < _atr.ParametersDigit[0].Value + 2)
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
            decimal lastAtr = _atr.DataSeries[0].Values[_atr.DataSeries[0].Values.Count - 1];

            if (lastAtr == 0)
            {
                return;
            }

            decimal minMoveCandleToEntry = lastAtr * AtrMultToEntry.ValueDecimal;

            Candle lastCandle = candles[candles.Count - 1];
            Candle prevCandle = candles[candles.Count - 2];

            decimal lastCandleBody = lastCandle.Body;
            decimal prevCandleBody = prevCandle.Body;

            if (lastCandleBody > minMoveCandleToEntry
                && prevCandleBody > minMoveCandleToEntry
                && lastCandle.IsUp
                && prevCandle.IsDown)
            {
                _tab.BuyAtMarket(GetVolume(_tab));
            }
        }

        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.State == PositionStateType.Opening
                || position.SignalTypeClose == "StopActivate")
            {
                return;
            }

            // 1 Stop order

            if(position.StopOrderPrice == 0)
            {
                decimal price = 0;

                if (position.Direction == Side.Buy)
                {
                    price = position.EntryPrice - position.EntryPrice * (StopPercent.ValueDecimal / 100);
                }
                else if (position.Direction == Side.Sell)
                {
                    price = position.EntryPrice + position.EntryPrice * (StopPercent.ValueDecimal / 100);
                }

                _tab.CloseAtStopMarket(position, price, "StopActivate");
            }


            // count the number of executed orders for closing

            int executeCloseOrdersCount = 0;

            for(int i =0; position.CloseOrders != null && i < position.CloseOrders.Count;i++)
            {
                if (position.CloseOrders[i].State == OrderStateType.Done)
                {
                    executeCloseOrdersCount++;
                }
            }

            if (position.CloseActiv == false &&
                (position.CloseOrders == null ||
                 executeCloseOrdersCount == 0))
            {// First Exit

                decimal orderPrice = position.EntryPrice + position.EntryPrice * (ExitOnePercent.ValueDecimal/100);
                _tab.CloseAtLimit(position, orderPrice, position.MaxVolume / 3);
            }
            else if(position.CloseActiv == false &&
                executeCloseOrdersCount == 1)
            { // Second Exit

                decimal orderPrice = position.EntryPrice + position.EntryPrice * (ExitTwoPercent.ValueDecimal / 100);
                _tab.CloseAtLimit(position, orderPrice, position.MaxVolume / 3);
            }
            else if (position.CloseActiv == false &&
                executeCloseOrdersCount == 2)
            { // Third exit

                decimal orderPrice = position.EntryPrice + position.EntryPrice * (ExitThreePercent.ValueDecimal / 100);
                _tab.CloseAtLimit(position, orderPrice, position.OpenVolume);
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
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = Volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
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

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
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