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
Trading robot for osengine.

Contrtrend robot on WilliamsRange, Ema and CoG.

Buy:
When the price is below the Ema indicator, the WilliamsRange indicator leaves the overbought zone, 
crossing the -20 mark from bottom to top, and the main line of the CoG indicator is above the signal line.

Sell:
When the price is above the Ema indicator, the WilliamsRange indicator leaves the oversold zone, crossing the -80 mark from top to bottom and the main line of the CoG indicator is below the signal line.

Exit: 
From purchases, the candle closed above the Ema indicator.
From sales, the candle closed below the Ema indicator.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyEmaWRAndCoG")] // We create an attribute so that we don't write anything to the BotFactory
    internal class StrategyEmaWRAndCoG : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterString _orderType;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _lengthCog;
        private StrategyParameterInt _periodWilliams;
        private StrategyParameterInt _lengthEma;

        // Indicator
        private Aindicator _cog;
        private Aindicator _williams;
        private Aindicator _ema;

        // Non trade periods
        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;

        public StrategyEmaWRAndCoG(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // non trade periods
            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 05 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1OnOff = true;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2Start = new TimeOfDay() { Hour = 13, Minute = 54 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2End = new TimeOfDay() { Hour = 14, Minute = 6 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2OnOff = false;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3Start = new TimeOfDay() { Hour = 18, Minute = 1 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3End = new TimeOfDay() { Hour = 23, Minute = 58 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3OnOff = true;

            _tradePeriodsSettings.TradeInSunday = false;
            _tradePeriodsSettings.TradeInSaturday = false;

            _tradePeriodsSettings.Load();

            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _orderType = CreateParameter("Order type", "Market", new[] { "Market", "Limit" }, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Base");
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4, "Base");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Base");

            // Indicator settings
            _lengthCog = CreateParameter("CoG Length", 5, 5, 50, 1, "Indicator");
            _periodWilliams = CreateParameter("Williams Length", 14, 50, 300, 1, "Indicator");
            _lengthEma = CreateParameter("Ema Length", 15, 50, 300, 1, "Indicator");

            // non trade period button
            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods", "Base");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            // Create indicator CoG
            _cog = IndicatorsFactory.CreateIndicatorByName("COG_CentreOfGravity_Oscr", name + "CoG", false);
            _cog = (Aindicator)_tab.CreateCandleIndicator(_cog, "CogArea");
            ((IndicatorParameterInt)_cog.Parameters[0]).ValueInt = _lengthCog.ValueInt;
            _cog.Save();

            // Creating an indicator WilliamsRange
            _williams = IndicatorsFactory.CreateIndicatorByName("WilliamsRange", name + "WilliamsRange", false);
            _williams = (Aindicator)_tab.CreateCandleIndicator(_williams, "WRArea");
            ((IndicatorParameterInt)_williams.Parameters[0]).ValueInt = _periodWilliams.ValueInt;
            _williams.Save();

            // Creating an indicator Ssma
            _ema = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema", false);
            _ema = (Aindicator)_tab.CreateCandleIndicator(_ema, "Prime");
            ((IndicatorParameterInt)_ema.Parameters[0]).ValueInt = _lengthEma.ValueInt;
            _ema.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyWRAndCoG_ParametrsChangeByUser;

            // subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel247;
        }

        // non trade period button click
        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        private void StrategyWRAndCoG_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_cog.Parameters[0]).ValueInt = _lengthCog.ValueInt;
            _cog.Save();
            _cog.Reload();
            ((IndicatorParameterInt)_williams.Parameters[0]).ValueInt = _periodWilliams.ValueInt;
            _williams.Save();
            _williams.Reload();
            ((IndicatorParameterInt)_ema.Parameters[0]).ValueInt = _lengthEma.ValueInt;
            _ema.Save();
            _ema.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "StrategyEmaWRAndCoG";
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
            if (candles.Count < _lengthEma.ValueInt || candles.Count < _lengthCog.ValueInt ||
                candles.Count < _periodWilliams.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (_tradePeriodsSettings.CanTradeThisTime(candles[^1].TimeStart) == false)
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
            decimal lastCog = _cog.DataSeries[0].Last;
            decimal lastCogSignal = _cog.DataSeries[1].Last;
            decimal lastEma = _ema.DataSeries[0].Last;
            decimal lastWilliams = _williams.DataSeries[0].Last;
            decimal lastPrice = candles[candles.Count - 1].Close;

            // The prev value of the indicator
            decimal prevWilliams = _williams.DataSeries[0].Values[_williams.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Security.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastCog > lastCogSignal && lastPrice < lastEma
                       && prevWilliams > -20 && lastWilliams < -20)
                    {
                        if (_orderType == "Limit")
                        {
                            _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                        }
                        else
                        {
                            _tab.BuyAtMarket(GetVolume(_tab));
                        }
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastCog < lastCogSignal && lastPrice > lastEma
                        && prevWilliams < -80 && lastWilliams > -80)
                    {
                        if (_orderType == "Limit")
                        {
                            _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage);
                        }
                        else
                        {
                            _tab.SellAtMarket(GetVolume(_tab));
                        }
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal lastEma = _ema.DataSeries[0].Last;
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = this._slippage.ValueDecimal * _tab.Security.PriceStep;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    if (lastPrice > lastEma)
                    {
                        if (_orderType == "Limit")
                        {
                            _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                        }
                        else
                        {
                            _tab.CloseAtMarket(pos, pos.OpenVolume);
                        }
                    }
                }
                else // If the direction of the position is short
                {
                    if (lastPrice < lastEma)
                    {
                        if (_orderType == "Limit")
                        {
                            _tab.CloseAtLimit(pos, lastPrice + _slippage, pos.OpenVolume);
                        }
                        else
                        {
                            _tab.CloseAtMarket(pos, pos.OpenVolume);
                        }
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