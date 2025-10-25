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

/*Discription
Trading robot for osengine

Trend robot on the Strategy Two Sma, Stochastic And MacdLine.

Buy:
1. Fast Sma is higher than Slow Sma;
2. The price is higher than the fast Sma;
3. Stochastic line K (blue) is above the signal line (red) and the stochastic value is above 25 (blue line);
4. Macd line (green) above the signal line (red);
Sell:
1. Fast Sma is lower than Slow Sma;
2. The price is lower than the fast Sma;
3. Stochastic line K (blue) is below the signal line (red) and the stochastic value is below 80 (blue line);
4. Macd line (green) below the signal line (red);
Exit: 
From buy: Stochastic K line (blue) below the signal line (red);
From sell: Stochastic K line (blue) above the signal line (red).
*/

namespace OsEngine.Robots
{
    [Bot("StrategyTwoSmaStochasticAndMacdLine")] //We create an attribute so that we don't write anything in the Boot factory
    public class StrategyTwoSmaStochasticAndMacdLine : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _timeStart;
        private StrategyParameterTimeOfDay _timeEnd;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator Settings
        private StrategyParameterInt _fastPeriod;
        private StrategyParameterInt _slowPeriod;
        private StrategyParameterInt _signalPeriod;
        private StrategyParameterInt _periodSmaFast;
        private StrategyParameterInt _periodSmaSlow;
        private StrategyParameterInt _stochasticPeriod1;
        private StrategyParameterInt _stochasticPeriod2;
        private StrategyParameterInt _stochasticPeriod3;

        // Indicator
        private Aindicator _smaFast;
        private Aindicator _smaSlow;
        private Aindicator _macdLine;
        private Aindicator _stochastic;

        //The last value of the indicators
        private decimal _lastSmaFast;
        private decimal _lastSmaSlow;
        private decimal _lastMacdSignal;
        private decimal _lastMacdGreen;
        private decimal _lastBlueStoh;
        private decimal _lastRedStoh;

        public StrategyTwoSmaStochasticAndMacdLine(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _timeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _timeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator Settings
            _fastPeriod = CreateParameter("Fast period", 12, 50, 300, 1, "Indicator");
            _slowPeriod = CreateParameter("Slow period", 26, 50, 300, 1, "Indicator");
            _signalPeriod = CreateParameter("Signal Period", 9, 50, 300, 1, "Indicator");
            _periodSmaFast = CreateParameter("Period SMA Fast", 100, 10, 300, 10, "Indicator");
            _periodSmaSlow = CreateParameter("Period SMA Slow", 200, 10, 300, 10, "Indicator");
            _stochasticPeriod1 = CreateParameter("Stochastic Period One", 10, 10, 300, 10, "Indicator");
            _stochasticPeriod2 = CreateParameter("Stochastic Period Two", 20, 10, 300, 10, "Indicator");
            _stochasticPeriod3 = CreateParameter("Stochastic Period Three", 30, 10, 300, 10, "Indicator");

            // Create indicator SmaFast
            _smaFast = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaFast", false);
            _smaFast = (Aindicator)_tab.CreateCandleIndicator(_smaFast, "Prime");
            ((IndicatorParameterInt)_smaFast.Parameters[0]).ValueInt = _periodSmaFast.ValueInt;
            _smaFast.Save();

            // Create indicator SmaSlow
            _smaSlow = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaSlow", false);
            _smaSlow = (Aindicator)_tab.CreateCandleIndicator(_smaSlow, "Prime");
            _smaSlow.DataSeries[0].Color = System.Drawing.Color.Yellow;
            ((IndicatorParameterInt)_smaSlow.Parameters[0]).ValueInt = _periodSmaSlow.ValueInt;
            _smaSlow.Save();

            // Create indicator macd
            _macdLine = IndicatorsFactory.CreateIndicatorByName("MacdLine", name + "MacdLine", false);
            _macdLine = (Aindicator)_tab.CreateCandleIndicator(_macdLine, "NewArea");
            ((IndicatorParameterInt)_macdLine.Parameters[0]).ValueInt = _fastPeriod.ValueInt;
            ((IndicatorParameterInt)_macdLine.Parameters[1]).ValueInt = _slowPeriod.ValueInt;
            ((IndicatorParameterInt)_macdLine.Parameters[2]).ValueInt = _signalPeriod.ValueInt;
            _macdLine.Save();

            // Create indicator Stochastic
            _stochastic = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "Stochastic", false);
            _stochastic = (Aindicator)_tab.CreateCandleIndicator(_stochastic, "NewArea0");
            ((IndicatorParameterInt)_stochastic.Parameters[0]).ValueInt = _stochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_stochastic.Parameters[1]).ValueInt = _stochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_stochastic.Parameters[2]).ValueInt = _stochasticPeriod3.ValueInt;
            _stochastic.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyTwoSmaStochasticAndMacdLine_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel284;
        }

        // Indicator Update event
        private void StrategyTwoSmaStochasticAndMacdLine_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_macdLine.Parameters[0]).ValueInt = _fastPeriod.ValueInt;
            ((IndicatorParameterInt)_macdLine.Parameters[1]).ValueInt = _slowPeriod.ValueInt;
            ((IndicatorParameterInt)_macdLine.Parameters[2]).ValueInt = _signalPeriod.ValueInt;
            _macdLine.Save();
            _macdLine.Reload();
            ((IndicatorParameterInt)_smaFast.Parameters[0]).ValueInt = _periodSmaFast.ValueInt;
            _smaFast.Save();
            _smaFast.Reload();
            ((IndicatorParameterInt)_smaSlow.Parameters[0]).ValueInt = _periodSmaSlow.ValueInt;
            _smaSlow.Save();
            _smaSlow.Reload();
            ((IndicatorParameterInt)_stochastic.Parameters[0]).ValueInt = _stochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_stochastic.Parameters[1]).ValueInt = _stochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_stochastic.Parameters[2]).ValueInt = _stochasticPeriod3.ValueInt;
            _stochastic.Save();
            _stochastic.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyTwoSmaStochasticAndMacdLine";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Completion Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _fastPeriod.ValueInt)
            {
                return;
            }

            // If the time does not match, we exit
            if (_timeStart.Value > _tab.TimeServerCurrent ||
                _timeEnd.Value < _tab.TimeServerCurrent)
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
            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // The last value of the indicators               
                _lastMacdSignal = _macdLine.DataSeries[1].Last;
                _lastMacdGreen = _macdLine.DataSeries[0].Last;
                _lastSmaFast = _smaFast.DataSeries[0].Last;
                _lastSmaSlow = _smaSlow.DataSeries[0].Last;
                _lastBlueStoh = _stochastic.DataSeries[0].Last;
                _lastRedStoh = _stochastic.DataSeries[1].Last;

                decimal lastPrice = candles[candles.Count - 1].Close;

                // Long
                if (_regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {
                    if (_lastMacdGreen > _lastMacdSignal &&
                        _lastSmaFast > _lastSmaSlow &&
                        lastPrice > _lastSmaFast &&
                        _lastBlueStoh > _lastRedStoh &&
                        _lastBlueStoh > 25)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // if the mode is not only long, we enter the short
                {
                    if (_lastMacdGreen < _lastMacdSignal &&
                        _lastSmaFast < _lastSmaSlow &&
                        lastPrice < _lastSmaFast &&
                        _lastBlueStoh < _lastRedStoh &&
                        _lastBlueStoh < 80)
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

            // The last value of the indicators               
            _lastBlueStoh = _stochastic.DataSeries[0].Last;
            _lastRedStoh = _stochastic.DataSeries[1].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is long
                {
                    if (_lastBlueStoh < _lastRedStoh)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (_lastBlueStoh > _lastRedStoh)
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