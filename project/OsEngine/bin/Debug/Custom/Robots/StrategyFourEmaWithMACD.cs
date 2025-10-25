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
using System.Drawing;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Strategy Four Ema With MACD.

Buy:
1. Ema1 is higher than Ema2 and the price is higher than Ema3 and Ema4;
2. MACD histogram > 0.

Sell:
1. Ema1 is lower than Ema2 and the price is lower than Ema3 and Ema4;
2. MACD histogram < 0.

Exit from a long position:
The trailing stop is placed at the minimum 
for the period specified for the trailing stop and transferred (slides) to new price lows, also for the specified period.

Exit from the short position:
The trailing stop is placed at the maximum 
for the period specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyFourEmaWithMACD")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    public class StrategyFourEmaWithMACD : BotPanel
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

        // Indicators settings
        private StrategyParameterInt _emaPeriod1;
        private StrategyParameterInt _emaPeriod2;
        private StrategyParameterInt _emaPeriod3;
        private StrategyParameterInt _emaPeriod4;
        private StrategyParameterInt _fastLineLengthMACD;
        private StrategyParameterInt _slowLineLengthMACD;
        private StrategyParameterInt _signalLineLengthMACD;

        // Indicators
        private Aindicator _ema1;
        private Aindicator _ema2;
        private Aindicator _ema3;
        private Aindicator _ema4;
        private Aindicator _MACD;

        // The last value of the indicator
        private decimal _lastMACD;
        private decimal _OnelastEma;
        private decimal _TwolastEma;
        private decimal _ThreelastEma;
        private decimal _FourlastEma;

        // Exit setting
        private StrategyParameterInt _trailBars;

        public StrategyFourEmaWithMACD(string name, StartProgram startProgram) : base(name, startProgram)
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

            // Indicators settings
            _emaPeriod1 = CreateParameter("Ema Length 1", 10, 7, 48, 7, "Indicator");
            _emaPeriod2 = CreateParameter("Ema Length 2", 20, 7, 48, 7, "Indicator");
            _emaPeriod3 = CreateParameter("Ema Length 3", 30, 7, 48, 7, "Indicator");
            _emaPeriod4 = CreateParameter("Ema Length 4", 40, 7, 48, 7, "Indicator");
            _fastLineLengthMACD = CreateParameter("MACD Fast Length", 16, 10, 300, 7, "Indicator");
            _slowLineLengthMACD = CreateParameter("MACD Slow Length", 32, 10, 300, 10, "Indicator");
            _signalLineLengthMACD = CreateParameter("MACD Signal Length", 8, 10, 300, 10, "Indicator");

            // Create indicator Ema1
            _ema1 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA1", false);
            _ema1 = (Aindicator)_tab.CreateCandleIndicator(_ema1, "Prime");
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _emaPeriod1.ValueInt;
            _ema1.Save();

            // Create indicator Ema2
            _ema2 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA2", false);
            _ema2 = (Aindicator)_tab.CreateCandleIndicator(_ema2, "Prime");
            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _emaPeriod2.ValueInt;
            _ema2.Save();

            // Create indicator Ema3
            _ema3 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA3", false);
            _ema3 = (Aindicator)_tab.CreateCandleIndicator(_ema3, "Prime");
            ((IndicatorParameterInt)_ema3.Parameters[0]).ValueInt = _emaPeriod3.ValueInt;
            _ema3.Save();

            // Create indicator Ema4
            _ema4 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA4", false);
            _ema4 = (Aindicator)_tab.CreateCandleIndicator(_ema4, "Prime");
            ((IndicatorParameterInt)_ema4.Parameters[0]).ValueInt = _emaPeriod4.ValueInt;
            _ema4.Save();

            // Create indicator MACD
            _MACD = IndicatorsFactory.CreateIndicatorByName("MACD", name + "MACD", false);
            _MACD = (Aindicator)_tab.CreateCandleIndicator(_MACD, "NewArea");
            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = _fastLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = _slowLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = _signalLineLengthMACD.ValueInt;
            _MACD.Save();

            // Exit setting
            _trailBars = CreateParameter("Trail Bars", 5, 1, 50, 1, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyFourEmaWithMACD_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel254;
        }

        private void StrategyFourEmaWithMACD_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _emaPeriod1.ValueInt;
            _ema1.Save();
            _ema1.Reload();

            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _emaPeriod2.ValueInt;
            _ema2.Save();
            _ema2.Reload();

            ((IndicatorParameterInt)_ema3.Parameters[0]).ValueInt = _emaPeriod3.ValueInt;
            _ema3.Save();
            _ema3.Reload();

            ((IndicatorParameterInt)_ema4.Parameters[0]).ValueInt = _emaPeriod4.ValueInt;
            _ema4.Save();
            _ema4.Reload();

            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = _fastLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = _slowLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = _signalLineLengthMACD.ValueInt;
            _MACD.Save();
            _MACD.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyFourEmaWithMACD";
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
            if (candles.Count <= _emaPeriod1.ValueInt ||
                candles.Count <= _emaPeriod2.ValueInt ||
                candles.Count <= _emaPeriod3.ValueInt ||
                candles.Count <= _emaPeriod4.ValueInt ||
                candles.Count <= _fastLineLengthMACD.ValueInt ||
                candles.Count <= _slowLineLengthMACD.ValueInt + 6 ||
                candles.Count <= _signalLineLengthMACD.ValueInt)
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
            _lastMACD = _MACD.DataSeries[0].Last;
            _OnelastEma = _ema1.DataSeries[0].Last;
            _TwolastEma = _ema2.DataSeries[0].Last;
            _ThreelastEma = _ema3.DataSeries[0].Last;
            _FourlastEma = _ema4.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastMACD > 0 && _OnelastEma > _TwolastEma && lastPrice > _ThreelastEma && lastPrice > _FourlastEma)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastMACD < 0 && _OnelastEma < _TwolastEma && lastPrice < _ThreelastEma && lastPrice < _FourlastEma)
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

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    decimal price = GetPriceStop(Side.Buy, candles, candles.Count - 1);

                    if (price == 0)
                    {
                        return;
                    }

                    _tab.CloseAtTrailingStop(pos, price, price - _slippage);
                }
                else // If the direction of the position is short
                {
                    decimal price = GetPriceStop(Side.Sell, candles, candles.Count - 1);

                    if (price == 0)
                    {
                        return;
                    }

                    _tab.CloseAtTrailingStop(pos, price, price + _slippage);
                }
            }
        }

        private decimal GetPriceStop(Side side, List<Candle> candles, int index)
        {
            if (candles == null || index < _trailBars.ValueInt)
            {
                return 0;
            }

            if (side == Side.Buy)
            {
                decimal price = decimal.MaxValue;

                for (int i = index; i > index - _trailBars.ValueInt; i--)
                {
                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }
                return price;
            }

            if (side == Side.Sell)
            {
                decimal price = 0;

                for (int i = index; i > index - _trailBars.ValueInt; i--)
                {
                    if (candles[i].High > price)
                    {
                        price = candles[i].High;
                    }
                }

                return price;
            }
            return 0;
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