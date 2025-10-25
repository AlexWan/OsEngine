/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Robots;
using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/*Discription
Trading robot for osengine

Trend robot at the intersection of two Vwma (all cars and different outputs) and Adaptive Look Back.

Buy: fast Ema is higher than slow Vwma and price is higher than fast Vwma + entry coefficient * Adaptive Look Back.

Sell: fast Ema is lower than average Vvma and price is lower than fast Ma - entry coefficient * Adaptive Look back .

Exit: reverse intersection of Wma.
*/

namespace OsEngine.Robots
{
    [Bot("IntersectionOfTwoVwmaAndAdaptiveLookBack")]
    public class IntersectionOfTwoVwmaAndAdaptiveLookBack : BotPanel
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

        // Indicator Settings
        private StrategyParameterInt _periodVwmaFast;
        private StrategyParameterInt _periodVwmaSlow;
        private StrategyParameterInt _periodALB;
        private StrategyParameterDecimal _coefEntryALB;

        // Indicator
        private Aindicator _ALB;
        private Aindicator _vwma1;
        private Aindicator _vwma2;

        // The last value of the indicators      
        private decimal _lastALB;
        private decimal _lastVwmaFasts;
        private decimal _lastVwmaSlow;
   
        public IntersectionOfTwoVwmaAndAdaptiveLookBack(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator Settings
            _periodALB = CreateParameter("Adaptive Look Back", 5, 1, 10, 1 , "Indicator");
            _coefEntryALB = CreateParameter("CoefEntryALB", 0.2m, 0.01m, 2, 0.02m, "Indicator");
            _periodVwmaFast = CreateParameter("Fast Vwma1 period", 250, 50, 500, 20, "Indicator");
            _periodVwmaSlow = CreateParameter("Slow Vwma2 period", 1000, 500, 1500, 100, "Indicator");

            // Create indicator Adaptive Look Back
            _ALB = IndicatorsFactory.CreateIndicatorByName("AdaptiveLookBack", name + "ALB", false);
            _ALB = (Aindicator)_tab.CreateCandleIndicator(_ALB, "NewArea");
            ((IndicatorParameterInt)_ALB.Parameters[0]).ValueInt = _periodALB.ValueInt;
            _ALB.Save();

            // Create indicator Vwma1
            _vwma1 = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "Vwma1", false);
            _vwma1 = (Aindicator)_tab.CreateCandleIndicator(_vwma1, "Prime");
            ((IndicatorParameterInt)_vwma1.Parameters[0]).ValueInt = _periodVwmaFast.ValueInt;
            _vwma1.DataSeries[0].Color = Color.Red;
            _vwma1.Save();
           
            // Create indicator Vwma2
            _vwma2 = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "Vwma2", false);
            _vwma2 = (Aindicator)_tab.CreateCandleIndicator(_vwma2, "Prime");
            ((IndicatorParameterInt)_vwma2.Parameters[0]).ValueInt = _periodVwmaSlow.ValueInt;
            _vwma2.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfTwoVwmaAndAdaptiveLookBack_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel214;
        }

        // Indicator Update event
        private void IntersectionOfTwoVwmaAndAdaptiveLookBack_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ALB.Parameters[0]).ValueInt = _periodALB.ValueInt;
            _ALB.Save();
            _ALB.Reload();

            ((IndicatorParameterInt)_vwma1.Parameters[0]).ValueInt = _periodVwmaFast.ValueInt;
            _vwma1.Save();
            _vwma1.Reload();

            ((IndicatorParameterInt)_vwma2.Parameters[0]).ValueInt = _periodVwmaSlow.ValueInt;
            _vwma2.Save();
            _vwma2.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionOfTwoVwmaAndAdaptiveLookBack";
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
            if (candles.Count < _periodALB.ValueInt || candles.Count < _coefEntryALB.ValueDecimal || candles.Count < _periodVwmaFast.ValueInt || candles.Count < _periodVwmaSlow.ValueInt)
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
            List<Position> openPositions = _tab.PositionsOpenAll;

            // The last value of the indicators
            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;
            decimal lastPrice = candles[candles.Count - 1].Close;

            // He last value of the indicator           
            _lastALB = _ALB.DataSeries[0].Last;
            _lastVwmaFasts = _vwma1.DataSeries[0].Last;
            _lastVwmaSlow = _vwma2.DataSeries[0].Last;
            
            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastVwmaFasts > _lastVwmaSlow && lastPrice > _lastVwmaFasts + _coefEntryALB.ValueDecimal * _lastALB)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastVwmaFasts < _lastVwmaSlow && lastPrice < _lastVwmaFasts - _coefEntryALB.ValueDecimal * _lastALB)
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

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            // He last value of the indicator
            _lastVwmaFasts = _vwma1.DataSeries[0].Last;
            _lastVwmaSlow = _vwma2.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // if the direction of the position is long
                {
                    if (_lastVwmaFasts < _lastVwmaSlow)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (_lastVwmaFasts > _lastVwmaSlow)
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