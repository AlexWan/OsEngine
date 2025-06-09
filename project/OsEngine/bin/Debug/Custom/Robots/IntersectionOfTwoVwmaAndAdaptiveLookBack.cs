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
        private StrategyParameterString Regime;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator Settings
        private StrategyParameterInt PeriodVwmaFast;
        private StrategyParameterInt PeriodVwmaSlow;
        private StrategyParameterInt PeriodALB;
        private StrategyParameterDecimal CoefEntryALB;

        // Indicator
        private Aindicator ALB;
        private Aindicator Vwma1;
        private Aindicator Vwma2;

        // The last value of the indicators      
        private decimal _lastALB;
        private decimal _lastVwmaFasts;
        private decimal _lastVwmaSlow;
   
        public IntersectionOfTwoVwmaAndAdaptiveLookBack(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            StartTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            EndTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator Settings
            PeriodALB = CreateParameter("Adaptive Look Back", 5, 1, 10, 1 , "Indicator");
            CoefEntryALB = CreateParameter("CoefEntryALB", 0.2m, 0.01m, 2, 0.02m, "Indicator");
            PeriodVwmaFast = CreateParameter("Fast Vwma1 period", 250, 50, 500, 20, "Indicator");
            PeriodVwmaSlow = CreateParameter("Slow Vwma2 period", 1000, 500, 1500, 100, "Indicator");

            // Create indicator Adaptive Look Back
            ALB = IndicatorsFactory.CreateIndicatorByName("AdaptiveLookBack", name + "ALB", false);
            ALB = (Aindicator)_tab.CreateCandleIndicator(ALB, "NewArea");
            ((IndicatorParameterInt)ALB.Parameters[0]).ValueInt = PeriodALB.ValueInt;
            ALB.Save();

            // Create indicator Vwma1
            Vwma1 = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "Vwma1", false);
            Vwma1 = (Aindicator)_tab.CreateCandleIndicator(Vwma1, "Prime");
            ((IndicatorParameterInt)Vwma1.Parameters[0]).ValueInt = PeriodVwmaFast.ValueInt;
            Vwma1.DataSeries[0].Color = Color.Red;
            Vwma1.Save();
           
            // Create indicator Vwma2
            Vwma2 = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "Vwma2", false);
            Vwma2 = (Aindicator)_tab.CreateCandleIndicator(Vwma2, "Prime");
            ((IndicatorParameterInt)Vwma2.Parameters[0]).ValueInt = PeriodVwmaSlow.ValueInt;
            Vwma2.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfTwoVwmaAndAdaptiveLookBack_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot at the intersection of two Vwma (all cars and different outputs) and Adaptive Look Back." +
             "Buy: fast Ema is higher than slow Vwma and price is higher than fast Vwma +entry coefficient* Adaptive Look Back." +
            "Sell: fast Ema is lower than average Vvma and price is lower than fast Ma -entry coefficient* Adaptive Look back ." +
             "Exit: reverse intersection of Wma.";
        }

        // Indicator Update event
        private void IntersectionOfTwoVwmaAndAdaptiveLookBack_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)ALB.Parameters[0]).ValueInt = PeriodALB.ValueInt;
            ALB.Save();
            ALB.Reload();

            ((IndicatorParameterInt)Vwma1.Parameters[0]).ValueInt = PeriodVwmaFast.ValueInt;
            Vwma1.Save();
            Vwma1.Reload();

            ((IndicatorParameterInt)Vwma2.Parameters[0]).ValueInt = PeriodVwmaSlow.ValueInt;
            Vwma2.Save();
            Vwma2.Reload();
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
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < PeriodALB.ValueInt || candles.Count < CoefEntryALB.ValueDecimal || candles.Count < PeriodVwmaFast.ValueInt || candles.Count < PeriodVwmaSlow.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (StartTradeTime.Value > _tab.TimeServerCurrent ||
                EndTradeTime.Value < _tab.TimeServerCurrent)
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
            if (Regime.ValueString == "OnlyClosePosition")
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
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
            decimal lastPrice = candles[candles.Count - 1].Close;

            // He last value of the indicator           
            _lastALB = ALB.DataSeries[0].Last;
            _lastVwmaFasts = Vwma1.DataSeries[0].Last;
            _lastVwmaSlow = Vwma2.DataSeries[0].Last;
            
            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastVwmaFasts > _lastVwmaSlow && lastPrice > _lastVwmaFasts + CoefEntryALB.ValueDecimal * _lastALB)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastVwmaFasts < _lastVwmaSlow && lastPrice < _lastVwmaFasts - CoefEntryALB.ValueDecimal * _lastALB)
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
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            // He last value of the indicator
            _lastVwmaFasts = Vwma1.DataSeries[0].Last;
            _lastVwmaSlow = Vwma2.DataSeries[0].Last;

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

