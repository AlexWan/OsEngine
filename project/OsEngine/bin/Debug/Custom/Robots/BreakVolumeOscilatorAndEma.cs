/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/*Discription
Trading robot for osengine

Trend robot on the Break VolumeOscilator And Ema.

Buy: the Volume Oscillator indicator line is above 0 and the price is your Ema.

Sell: the Volume Oscillator indicator line is below 0 and the price is below Ema.

Exit: on the opposite signal.
*/

namespace OsEngine.Robots
{
    [Bot("BreakVolumeOscilatorAndEma")] //We create an attribute so that we don't write anything in the Bot factory
    public class BreakVolumeOscilatorAndEma : BotPanel
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
        private StrategyParameterInt _emaPeriod;
        private StrategyParameterInt _lenghtVolOsSlow;
        private StrategyParameterInt _lenghtVolOsFast;

        // Indicator
        private Aindicator _ema;
        private Aindicator _volumeOsc;

        //The last value of the indicators
        private decimal _lastEma;
        private decimal _lastVolume;

        public BreakVolumeOscilatorAndEma(string name, StartProgram startProgram) : base(name, startProgram)
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
            _emaPeriod = CreateParameter("Ema period", 15, 50, 300, 1, "Indicator");
            _lenghtVolOsSlow = CreateParameter("LenghtVolOsSlow", 20, 20, 300, 1, "Indicator");
            _lenghtVolOsFast = CreateParameter("LenghtVolOsFast", 10, 10, 300, 1, "Indicator");

            // Create indicator Ema
            _ema = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA", false);
            _ema = (Aindicator)_tab.CreateCandleIndicator(_ema, "Prime");
            ((IndicatorParameterInt)_ema.Parameters[0]).ValueInt = _emaPeriod.ValueInt;
            _ema.Save();

            // Create indicator VoluneOscilator
            _volumeOsc = IndicatorsFactory.CreateIndicatorByName("VolumeOscilator", name + "VolumeOscilator", false);
            _volumeOsc = (Aindicator)_tab.CreateCandleIndicator(_volumeOsc, "NewArea1");
            ((IndicatorParameterInt)_volumeOsc.Parameters[0]).ValueInt = _lenghtVolOsSlow.ValueInt;
            ((IndicatorParameterInt)_volumeOsc.Parameters[1]).ValueInt = _lenghtVolOsFast.ValueInt;
            _volumeOsc.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakVolumeOscilatorAndEma_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel171;
        }

        // Indicator Update event
        private void BreakVolumeOscilatorAndEma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ema.Parameters[0]).ValueInt = _emaPeriod.ValueInt;
            _ema.Save();
            _ema.Reload();
            ((IndicatorParameterInt)_volumeOsc.Parameters[0]).ValueInt = _lenghtVolOsSlow.ValueInt;
            ((IndicatorParameterInt)_volumeOsc.Parameters[1]).ValueInt = _lenghtVolOsFast.ValueInt;
            _volumeOsc.Save();
            _volumeOsc.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakVolumeOscilatorAndEma";
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
            if (candles.Count < _emaPeriod.ValueInt)
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
                _lastEma = _ema.DataSeries[0].Last;
                _lastVolume = _volumeOsc.DataSeries[0].Last;

                decimal lastPrice = candles[candles.Count - 1].Close;

                // Long
                if (_regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {
                    if (lastPrice > _lastEma && _lastVolume > 0)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // if the mode is not only long, we enter the short
                {
                    if (lastPrice < _lastEma && _lastVolume < 0)
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
            _lastEma = _ema.DataSeries[0].Last;
            _lastVolume = _volumeOsc.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is long
                {
                    if (lastPrice < _lastEma && _lastVolume < 0)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (lastPrice > _lastEma && _lastVolume > 0)
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