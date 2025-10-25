/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Strategy Price Channel With Rsi And CoG.

Buy:
When the Rsi indicator is above 50 and CoG is above the level from the parameters, 
we place a pending buy order along the top line of the PriceChannel indicator.

Sell:
When the Rsi indicator is below 50 and CoG is below the level from the parameters, 
we place a pending sell order along the lower line of the PriceChannel indicator.

Exit from buy: 
We set a trailing stop as a percentage of the low of the candle at which we entered and along the lower border of the PriceChannel indicator.
The calculation method that is closest to the current price is selected.
Exit from sell: 
We set a trailing stop as a percentage of the high of the candle at which we entered and along the upper border of the PriceChannel indicator. 
The calculation method that is closest to the current price is selected.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyPCRsiAndCoG")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    internal class StrategyPCRsiAndCoG : BotPanel
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

        // Indicator Settings 
        private StrategyParameterInt _lengthCog;
        private StrategyParameterInt _lengthRSI;
        private StrategyParameterInt _pcUpLength;
        private StrategyParameterInt _pcDownLength;
        private StrategyParameterDecimal _entryLevel;

        // Indicator
        private Aindicator _cog;
        private Aindicator _RSI;
        private Aindicator _PC;

        // Exit Settings
        private StrategyParameterDecimal _trailingValue;

        public StrategyPCRsiAndCoG(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
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
            _lengthCog = CreateParameter("CoG Length", 14, 5, 50, 1, "Indicator");
            _lengthRSI = CreateParameter("RSI Length", 14, 5, 80, 1, "Indicator");
            _pcUpLength = CreateParameter("Up Line Length", 21, 7, 48, 7, "Indicator");
            _pcDownLength = CreateParameter("Down Line Length", 21, 7, 48, 7, "Indicator");
            _entryLevel = CreateParameter("Entry Level for CoG", 0.5m, 0.1m, 1, 0.1m, "Indicator");

            // Create indicator RSI
            _RSI = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
            _RSI = (Aindicator)_tab.CreateCandleIndicator(_RSI, "RsiArea");
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = _lengthRSI.ValueInt;
            _RSI.Save();

            // Create indicator CoG
            _cog = IndicatorsFactory.CreateIndicatorByName("COG_CentreOfGravity_Oscr", name + "CoG", false);
            _cog = (Aindicator)_tab.CreateCandleIndicator(_cog, "CogArea");
            ((IndicatorParameterInt)_cog.Parameters[0]).ValueInt = _lengthCog.ValueInt;
            _cog.Save();

            // Create indicator PC
            _PC = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PC", false);
            _PC = (Aindicator)_tab.CreateCandleIndicator(_PC, "Prime");
            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = _pcUpLength.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = _pcDownLength.ValueInt;
            _PC.Save();

            // Exit Settings
            _trailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += _strategyPCRsiAndCoG_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Successful position opening event
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;

            Description = OsLocalization.Description.DescriptionLabel267;
        }

        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
            _tab.SellAtStopCancel();
            _tab.BuyAtStopCancel();
        }

        private void _strategyPCRsiAndCoG_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = _lengthRSI.ValueInt;
            _RSI.Save();
            _RSI.Reload();

            ((IndicatorParameterInt)_cog.Parameters[0]).ValueInt = _lengthCog.ValueInt;
            _cog.Save();
            _cog.Reload();

            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = _pcUpLength.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = _pcDownLength.ValueInt;
            _PC.Save();
            _PC.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "StrategyPCRsiAndCoG";
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
            if (candles.Count <= _lengthRSI.ValueInt +21 ||candles.Count <= _lengthCog.ValueInt ||
                candles.Count <= _pcUpLength.ValueInt || candles.Count <= _pcDownLength.ValueInt)
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
            decimal lastCog = _cog.DataSeries[0].Last;
            decimal lastRSI = _RSI.DataSeries[0].Last;
            decimal upChannel = _PC.DataSeries[0].Last;
            decimal downChannel = _PC.DataSeries[1].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastCog > _entryLevel.ValueDecimal && lastRSI > 50)
                    {
                        _tab.BuyAtStopCancel();
                        _tab.BuyAtStop(GetVolume(_tab), upChannel + _slippage, upChannel, StopActivateType.HigherOrEqual);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastCog < _entryLevel.ValueDecimal && lastRSI < 50)
                    {
                        _tab.SellAtStopCancel();
                        _tab.SellAtStop(GetVolume(_tab), downChannel - _slippage, downChannel, StopActivateType.LowerOrEqual);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal upChannel = _PC.DataSeries[0].Last;
            decimal downChannel = _PC.DataSeries[1].Last;

            decimal stopPrice;
            decimal stop_level = 0;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * _trailingValue.ValueDecimal / 100;
                    stop_level = stopPrice > downChannel ? stopPrice : downChannel;
                }
                else // If the direction of the position is short
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * _trailingValue.ValueDecimal / 100;
                    stop_level = stopPrice < upChannel ? stopPrice : upChannel;
                }
                _tab.CloseAtTrailingStop(pos, stop_level, stop_level);
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