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

Trend robot based on Envelopes and EOM Watcher indicators.

Buy:
When the candle closes above the upper line of the Envelopes indicator, and the EOM Watcher indicator is above zero.

Sell:
When the candle closes below the lower line of the Envelopes indicator, and the EOM Watcher indicator is below zero.

Exit from buy:
When the candle closed below the lower line of the Envelopes indicator.

Exit from sell:
When the candle closed above the upper line of the Envelopes indicator.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyEomWatcherAndEnvelopes")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    internal class StrategyEomWatcherAndEnvelopes : BotPanel
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
        private StrategyParameterInt _lengthEomW;
        private StrategyParameterInt _envelopsLength;
        private StrategyParameterDecimal _envelopsDeviation;

        // Indicators
        private Aindicator _eomW;
        private Aindicator _envelop;

        public StrategyEomWatcherAndEnvelopes(string name, StartProgram startProgram) : base(name, startProgram)
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
            _lengthEomW = CreateParameter("Length EomW", 24, 5, 100, 5, "Indicator");
            _envelopsLength = CreateParameter("Envelops Length", 21, 7, 48, 7, "Indicator");
            _envelopsDeviation = CreateParameter("Envelops Deviation", 1.0m, 1, 5, 0.1m, "Indicator");

            // Create indicator EOMW
            _eomW = IndicatorsFactory.CreateIndicatorByName("EaseOfMovement_Watcher", name + "EOMW", false);
            _eomW = (Aindicator)_tab.CreateCandleIndicator(_eomW, "EomWArea");
            ((IndicatorParameterInt)_eomW.Parameters[0]).ValueInt = _lengthEomW.ValueInt;
            _eomW.Save();

            // Create indicator Envelops
            _envelop = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "Envelops", false);
            _envelop = (Aindicator)_tab.CreateCandleIndicator(_envelop, "Prime");
            ((IndicatorParameterInt)_envelop.Parameters[0]).ValueInt = _envelopsLength.ValueInt;
            ((IndicatorParameterDecimal)_envelop.Parameters[1]).ValueDecimal = _envelopsDeviation.ValueDecimal;
            _envelop.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyEomWatcher_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel250;
        }

        private void StrategyEomWatcher_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_eomW.Parameters[0]).ValueInt = _lengthEomW.ValueInt;
            _eomW.Save();
            _eomW.Reload();

            ((IndicatorParameterInt)_envelop.Parameters[0]).ValueInt = _envelopsLength.ValueInt;
            ((IndicatorParameterDecimal)_envelop.Parameters[1]).ValueDecimal = _envelopsDeviation.ValueDecimal;
            _envelop.Save();
            _envelop.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "StrategyEomWatcherAndEnvelopes";
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
            if (candles.Count <= _lengthEomW.ValueInt ||
                candles.Count <= _envelopsLength.ValueInt)
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
            decimal lastEOMWUp = _eomW.DataSeries[0].Last; // Series EaseOfMovement Up
            decimal lastEOMWDown = _eomW.DataSeries[1].Last; // Series EaseOfMovement Down
            decimal lastUpLine = _envelop.DataSeries[0].Last;
            decimal lastDownLine = _envelop.DataSeries[2].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal slippage = _slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastEOMWUp > 0 && lastPrice > lastUpLine)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastEOMWDown < 0 && lastPrice < lastDownLine)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            // The last value of the indicator
            decimal lastUpLine = _envelop.DataSeries[0].Last;
            decimal lastDownLine = _envelop.DataSeries[2].Last;

            decimal slippage = _slippage.ValueDecimal * _tab.Securiti.PriceStep;
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
                    if (lastPrice < lastDownLine)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (lastPrice > lastUpLine)
                    {
                        _tab.CloseAtLimit(pos, lastPrice + slippage, pos.OpenVolume);
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