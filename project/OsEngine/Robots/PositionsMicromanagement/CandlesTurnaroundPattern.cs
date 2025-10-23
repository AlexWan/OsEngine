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
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Candles Turnaround Pattern.

Buy:The last candle is a fast, large-bodied bullish candle, and the previous candle is a slow, large-bodied bearish candle.

Exit:The position is closed by placing stop-loss orders if the price moves against the position beyond a certain percentage, and
by setting multiple limit orders at increasing profit levels—first at a small profit target, then at a higher one, and finally
closing the remaining volume at the highest target.
 */

namespace OsEngine.Robots.PositionsMicromanagement
{
    [Bot("CandlesTurnaroundPattern")] // We create an attribute so that we don't write anything to the BotFactory
    public class CandlesTurnaroundPattern : BotPanel
    {
        private BotTabSimple _tab;

        // Basic settings
        private StrategyParameterString _regime;
        
        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;
        
        // Indicator settings
        private StrategyParameterInt _atrLength;
        private StrategyParameterDecimal _atrMultToEntry;
        
        // Indicator
        private Aindicator _atr;

        // Exit settings
        private StrategyParameterDecimal _stopPercent;
        private StrategyParameterDecimal _exitOnePercent;
        private StrategyParameterDecimal _exitTwoPercent;
        private StrategyParameterDecimal _exitThreePercent;

        public CandlesTurnaroundPattern(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "OnlyLong", "OnlyClosePosition" });

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 3, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _atrLength = CreateParameter("Atr length", 25, 10, 80, 3);
            _atrMultToEntry = CreateParameter("Atr mult to entry", 0.3m, 0.1m, 80, 0.1m);

            // Create indicator ATR
            _atr = IndicatorsFactory.CreateIndicatorByName("ATR", name + "Atr", false);
            _atr = (Aindicator)_tab.CreateCandleIndicator(_atr, "atr");
            _atr.ParametersDigit[0].Value = _atrLength.ValueInt;
            _atr.Save();

            // Exit settings
            _stopPercent = CreateParameter("Stop percent", 0.9m, 1.0m, 50, 4);
            _exitOnePercent = CreateParameter("Exit one percent", 0.3m, 1.0m, 50, 4);
            _exitTwoPercent = CreateParameter("Exit two percent", 0.6m, 1.0m, 50, 4);
            _exitThreePercent = CreateParameter("Exit three percent", 0.9m, 1.0m, 50, 4);

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += Event_ParametrsChangeByUser;

            _tab.ManualPositionSupport.DisableManualSupport();

            Description = OsLocalization.Description.DescriptionLabel80;
        }

        void Event_ParametrsChangeByUser()
        {
            if (_atrLength.ValueInt != _atr.ParametersDigit[0].Value)
            {
                _atr.ParametersDigit[0].Value = _atrLength.ValueInt;
                _atr.Reload();
                _atr.Save();
            }
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CandlesTurnaroundPattern";
        }

        // Show settings GUI
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
            decimal lastAtr = _atr.DataSeries[0].Values[_atr.DataSeries[0].Values.Count - 1];

            if (lastAtr == 0)
            {
                return;
            }

            decimal minMoveCandleToEntry = lastAtr * _atrMultToEntry.ValueDecimal;

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

        // Close position logic
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
                    price = position.EntryPrice - position.EntryPrice * (_stopPercent.ValueDecimal / 100);
                }
                else if (position.Direction == Side.Sell)
                {
                    price = position.EntryPrice + position.EntryPrice * (_stopPercent.ValueDecimal / 100);
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

            if (position.CloseActive == false &&
                (position.CloseOrders == null ||
                 executeCloseOrdersCount == 0))
            {// First Exit

                decimal orderPrice = position.EntryPrice + position.EntryPrice * (_exitOnePercent.ValueDecimal/100);
                _tab.CloseAtLimit(position, orderPrice, position.MaxVolume / 3);
            }
            else if(position.CloseActive == false &&
                executeCloseOrdersCount == 1)
            { // Second Exit

                decimal orderPrice = position.EntryPrice + position.EntryPrice * (_exitTwoPercent.ValueDecimal / 100);
                _tab.CloseAtLimit(position, orderPrice, position.MaxVolume / 3);
            }
            else if (position.CloseActive == false &&
                executeCloseOrdersCount == 2)
            { // Third exit

                decimal orderPrice = position.EntryPrice + position.EntryPrice * (_exitThreePercent.ValueDecimal / 100);
                _tab.CloseAtLimit(position, orderPrice, position.OpenVolume);
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