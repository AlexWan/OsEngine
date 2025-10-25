/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Linq;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Strategy OsMa and MACD.

Buy: When the previous value of the OsMa histogram was below zero, and the current value was above zero, 
and the MACD histogram was above zero.

Sell: When the previous value of the OsMa histogram was above zero, and the current value was below zero, 
and the MACD histogram was below zero.

Exit:
From buy: When the OsMa histogram value is below zero.
From sell: When the OsMa histogram value is above zero.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyOsMaAndMACD")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    internal class StrategyOsMaAndMACD : BotPanel
    {
        // Reference to the main trading tab
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _startTradeTime;
        private StrategyParameterTimeOfDay _endTradeTime;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings 
        private StrategyParameterInt _lenghtFastLineOsMa;
        private StrategyParameterInt _lenghtSlowLineOsMa;
        private StrategyParameterInt _lenghtSignalLineOsMa;
        private StrategyParameterInt _lengthFastLineMACD;
        private StrategyParameterInt _lengthSlowLineMACD;
        private StrategyParameterInt _lengthSignalLineMACD;

        // Indicators
        private Aindicator _OsMa;
        private Aindicator _MACD;

        public StrategyOsMaAndMACD(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _lenghtFastLineOsMa = CreateParameter("OsMa Fast Length", 12, 10, 100, 10, "Indicator");
            _lenghtSlowLineOsMa = CreateParameter("OsMa Slow Length", 26, 20, 300, 10, "Indicator");
            _lenghtSignalLineOsMa = CreateParameter("OsMa Signal Length", 9, 9, 300, 10, "Indicator");
            _lengthFastLineMACD = CreateParameter("MACD Fast Length", 12, 10, 100, 10, "Indicator");
            _lengthSlowLineMACD = CreateParameter("MACD Slow Length", 26, 20, 300, 10, "Indicator");
            _lengthSignalLineMACD = CreateParameter("MACD Signal Length", 9, 10, 300, 10, "Indicator");

            // Create indicator OsMa
            _OsMa = IndicatorsFactory.CreateIndicatorByName("OsMa", name + "OsMa", false);
            _OsMa = (Aindicator)_tab.CreateCandleIndicator(_OsMa, "OsMaArea");
            ((IndicatorParameterInt)_OsMa.Parameters[0]).ValueInt = _lenghtFastLineOsMa.ValueInt;
            ((IndicatorParameterInt)_OsMa.Parameters[1]).ValueInt = _lenghtSlowLineOsMa.ValueInt;
            ((IndicatorParameterInt)_OsMa.Parameters[2]).ValueInt = _lenghtSignalLineOsMa.ValueInt;
            _OsMa.Save();

            // Create indicator MACD
            _MACD = IndicatorsFactory.CreateIndicatorByName("MACD", name + "Macd", false);
            _MACD = (Aindicator)_tab.CreateCandleIndicator(_MACD, "MacdArea");
            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = _lengthFastLineMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = _lengthSlowLineMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = _lengthSignalLineMACD.ValueInt;
            _MACD.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += _strategyOsMa_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel264;
        }        

        private void _strategyOsMa_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_OsMa.Parameters[0]).ValueInt = _lenghtFastLineOsMa.ValueInt;
            ((IndicatorParameterInt)_OsMa.Parameters[1]).ValueInt = _lenghtSlowLineOsMa.ValueInt;
            ((IndicatorParameterInt)_OsMa.Parameters[2]).ValueInt = _lenghtSignalLineOsMa.ValueInt;
            _OsMa.Save();
            _OsMa.Reload();

            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = _lengthFastLineMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = _lengthSlowLineMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = _lengthSignalLineMACD.ValueInt;
            _MACD.Save();
            _MACD.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "StrategyOsMaAndMACD";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Finished Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count <= _lengthFastLineMACD.ValueInt || candles.Count <= _lengthSlowLineMACD.ValueInt ||
                candles.Count <= _lengthSignalLineMACD.ValueInt || candles.Count <= _lenghtFastLineOsMa.ValueInt || 
                candles.Count <= _lenghtSlowLineOsMa.ValueInt ||  candles.Count <= _lenghtSignalLineOsMa.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (_startTradeTime.Value > _tab.TimeServerCurrent ||
                _endTradeTime.Value < _tab.TimeServerCurrent)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            // If there are positions, then go to the position closing method
            if (openPositions != null && openPositions.Count != 0)
            {
                LogicClosePosition(candles);
            }

            // If the position closing mode, then exit the method
            if (_regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            // If there are no positions, then go to the position opening method
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles);
            }
        }

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            // The last value of the indicator
            decimal lastMACD = _MACD.DataSeries[0].Last;
            decimal lastOsMa = _OsMa.DataSeries[2].Last;

            // The prev value of the indicator
            decimal prevOsMa = _OsMa.DataSeries[2].Values[_OsMa.DataSeries[2].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastMACD > 0 && lastOsMa > 0 && prevOsMa < 0)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastMACD < 0 && lastOsMa < 0 && prevOsMa > 0)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        // Logic close position 
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            // The last value of the indicator
            decimal lastMACD = _MACD.DataSeries[0].Last;
            decimal lastOsMa = _OsMa.DataSeries[2].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is long
                {
                    if (lastOsMa < 0)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (lastOsMa > 0)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice + _slippage, openPositions[i].OpenVolume);
                    }
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