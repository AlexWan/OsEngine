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

/* Description
trading robot for osengine

The trend robot on Strategy Ultimate With Sma And ATR.

Buy:
1. The candle closed above the Sma.
2. Ultimate Oscillator is above the BuyValue level.
Sell:
1. The candle closed below the Sma.
2. Ultimate Oscillator is below the SellValue level.

Exit from buy: trailing stop in % of the loy of the candle on which you entered - exit coefficient * Atr.
Exit from sell: trailing stop in % of the high of the candle on which you entered + exit coefficient * Atr.
 */

namespace OsEngine.Robots
{
    [Bot("StrategyUltimateWithSmaAndATR")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyUltimateWithSmaAndATR : BotPanel
    {
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
        private StrategyParameterDecimal _buyValue;
        private StrategyParameterDecimal _sellValue;
        private StrategyParameterInt _periodOneUltimate;
        private StrategyParameterInt _periodTwoUltimate;
        private StrategyParameterInt _periodThreeUltimate;
        private StrategyParameterInt _lengthAtr;
        private StrategyParameterDecimal _exitCoefAtr;
        private StrategyParameterInt _periodSma;

        // Indicator
        private Aindicator _ultimateOsc;
        private Aindicator _ATR;
        private Aindicator _sma;

        // The last value of the indicator
        private decimal _lastSma;
        private decimal _lastATR;
        private decimal _lastUltimate;

        // Exit setting
        private StrategyParameterDecimal _trailingValue;

        public StrategyUltimateWithSmaAndATR(string name, StartProgram startProgram) : base(name, startProgram)
        {
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
            _periodOneUltimate = CreateParameter("PeriodOneUltimate", 7, 10, 300, 1, "Indicator");
            _periodTwoUltimate = CreateParameter("PeriodTwoUltimate", 14, 10, 300, 1, "Indicator");
            _periodThreeUltimate = CreateParameter("PeriodThreeUltimate", 28, 9, 300, 1, "Indicator");
            _buyValue = CreateParameter("Buy Value", 10.0m, 10, 300, 10, "Indicator");
            _sellValue = CreateParameter("Sell Value", 10.0m, 10, 300, 10, "Indicator");
            _lengthAtr = CreateParameter("Length ATR", 14, 7, 48, 7, "Indicator");
            _exitCoefAtr = CreateParameter("Coef Atr", 1, 1m, 10, 1, "Indicator");
            _periodSma = CreateParameter("Period Simple Moving Average", 20, 10, 200, 10, "Indicator");

            // Create indicator CCI
            _ultimateOsc = IndicatorsFactory.CreateIndicatorByName("UltimateOscilator", name + "UltimateOscilator", false);
            _ultimateOsc = (Aindicator)_tab.CreateCandleIndicator(_ultimateOsc, "NewArea");
            ((IndicatorParameterInt)_ultimateOsc.Parameters[0]).ValueInt = _periodOneUltimate.ValueInt;
            ((IndicatorParameterInt)_ultimateOsc.Parameters[1]).ValueInt = _periodTwoUltimate.ValueInt;
            ((IndicatorParameterInt)_ultimateOsc.Parameters[2]).ValueInt = _periodThreeUltimate.ValueInt;
            _ultimateOsc.Save();

            // Create indicator ATR
            _ATR = IndicatorsFactory.CreateIndicatorByName("ATR", name + "Atr", false);
            _ATR = (Aindicator)_tab.CreateCandleIndicator(_ATR, "NewArea");
            ((IndicatorParameterInt)_ATR.Parameters[0]).ValueInt = _lengthAtr.ValueInt;
            _ATR.Save();

            // Create indicator
            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SMA", false);
            _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");
            ((IndicatorParameterInt)_sma.Parameters[0]).ValueInt = _periodSma.ValueInt;
            _sma.Save();

            // Exit setting
            _trailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += OverboughtOversoldCCI_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy Ultimate With Sma And ATR. " +
                "Buy: " +
                "1. The candle closed above the Sma. " +
                "2. Ultimate Oscillator is above the BuyValue level. " +
                "Sell: " +
                "1. The candle closed below the Sma. " +
                "2. Ultimate Oscillator is below the SellValue level. " +
                "Exit from buy: trailing stop in % of the loy of the candle on which you entered - exit coefficient * Atr. " +
                "Exit from sell: trailing stop in % of the high of the candle on which you entered + exit coefficient * Atr.";
        }

        private void OverboughtOversoldCCI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ultimateOsc.Parameters[0]).ValueInt = _periodOneUltimate.ValueInt;
            ((IndicatorParameterInt)_ultimateOsc.Parameters[1]).ValueInt = _periodTwoUltimate.ValueInt;
            ((IndicatorParameterInt)_ultimateOsc.Parameters[2]).ValueInt = _periodThreeUltimate.ValueInt;
            _ultimateOsc.Save();
            _ultimateOsc.Reload();
            ((IndicatorParameterInt)_ATR.Parameters[0]).ValueInt = _lengthAtr.ValueInt;
            _ATR.Save();
            _ATR.Reload();
            ((IndicatorParameterInt)_sma.Parameters[0]).ValueInt = _periodSma.ValueInt;
            _sma.Save();
            _sma.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyUltimateWithSmaAndATR";
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
            if (candles.Count < _periodOneUltimate.ValueInt ||
                candles.Count < _periodTwoUltimate.ValueInt ||
                candles.Count < _periodThreeUltimate.ValueInt)
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
            _lastUltimate = _ultimateOsc.DataSeries[0].Last;
            _lastSma = _sma.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastUltimate > _buyValue.ValueDecimal && _lastSma < lastPrice)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastUltimate < _sellValue.ValueDecimal && _lastSma > lastPrice)
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

            decimal stopPrice;
            
            // The last value of the indicator
            _lastATR = _ATR.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position position = openPositions[i];

                if (position.State != PositionStateType.Open)
                {
                    continue;
                }

                if (position.Direction == Side.Buy) // If the direction of the position is long
                {
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * _trailingValue.ValueDecimal / 100 + _lastATR * _exitCoefAtr.ValueDecimal;
                }
                else // If the direction of the position is short
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * _trailingValue.ValueDecimal / 100;
                }
                _tab.CloseAtTrailingStop(position, stopPrice, stopPrice);
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