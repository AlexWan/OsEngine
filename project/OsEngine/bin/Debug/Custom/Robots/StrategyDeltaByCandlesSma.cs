/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Windows;

/* Description
Trading robot for OsEngine.

Demo trend strategy based on DeltaByCandles.

Indicator:
- DataSeries[0] = Sma Delta
- DataSeries[1] = Delta
- DataSeries[2] = Sum volume
- DataSeries[3] = Buy volume
- DataSeries[4] = Sell volume

Buy:
Delta crosses Sma Delta from bottom to top.

Sell:
Delta crosses Sma Delta from top to bottom.

Exit long:
Delta crosses Sma Delta from top to bottom.

Exit short:
Delta crosses Sma Delta from bottom to top.

Important:
DeltaByCandles requires trades inside candles. In Tester Light / Bot Station Light,
you need tick/trade data enabled. Without candle.Trades the indicator cannot calculate values.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyDeltaByCandlesSma")]
    public class StrategyDeltaByCandlesSma : BotPanel
    {
        private BotTabSimple _tab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _slippage;

        // Volume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _lengthSmaDelta;

        // Indicator
        private Aindicator _deltaByCandles;

        // Last values
        private decimal _lastPrice;
        private decimal _lastDelta;
        private decimal _lastSmaDelta;
        private decimal _prevDelta;
        private decimal _prevSmaDelta;

        private bool _noTradesMessageSent;

        public StrategyDeltaByCandlesSma(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            _slippage = CreateParameter("Slippage", 0, 0, 20, 1);

            // Volume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 2, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _lengthSmaDelta = CreateParameter("Length Sma Delta", 10, 2, 200, 1);

            // Create indicator DeltaByCandles
            _deltaByCandles = IndicatorsFactory.CreateIndicatorByName("DeltaByCandles", name + "DeltaByCandles", false);
            _deltaByCandles = (Aindicator)_tab.CreateCandleIndicator(_deltaByCandles, "DeltaArea");

            ((IndicatorParameterInt)_deltaByCandles.Parameters[0]).ValueInt = _lengthSmaDelta.ValueInt;

            _deltaByCandles.Save();

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += Bot_CandleFinishedEvent;

            // Subscribe to the parameter change event
            ParametrsChangeByUser += StrategyDeltaByCandlesSma_ParametrsChangeByUser;

            Description =
                "Demo trend strategy based on DeltaByCandles. " +
                "Long: Delta crosses Sma Delta upward. " +
                "Short: Delta crosses Sma Delta downward. " +
                "Requires tick/trade data inside candles.";
        }

        // Parameters changed by user
        private void StrategyDeltaByCandlesSma_ParametrsChangeByUser()
        {
            if (_deltaByCandles == null)
            {
                return;
            }

            ((IndicatorParameterInt)_deltaByCandles.Parameters[0]).ValueInt = _lengthSmaDelta.ValueInt;

            _deltaByCandles.Save();
            _deltaByCandles.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyDeltaByCandlesSma";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show(OsLocalization.Trader.Label55);
        }

        // Logic
        private void Bot_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles == null ||
                candles.Count < _lengthSmaDelta.ValueInt + 2)
            {
                return;
            }

            Candle lastCandle = candles[candles.Count - 1];

            if (lastCandle.Trades == null ||
                lastCandle.Trades.Count == 0)
            {
                if (_noTradesMessageSent == false)
                {
                    SendNewLogMessage(
                        "DeltaByCandles can`t calculate values. No trades inside candles. Enable tick/trade data in DataSetting.",
                        Logging.LogMessageType.Error);

                    _noTradesMessageSent = true;
                }

                return;
            }

            _noTradesMessageSent = false;

            if (_deltaByCandles == null ||
                _deltaByCandles.DataSeries == null ||
                _deltaByCandles.DataSeries.Count < 2 ||
                _deltaByCandles.DataSeries[0].Values == null ||
                _deltaByCandles.DataSeries[1].Values == null)
            {
                return;
            }

            List<decimal> smaDeltaValues = _deltaByCandles.DataSeries[0].Values;
            List<decimal> deltaValues = _deltaByCandles.DataSeries[1].Values;

            if (smaDeltaValues.Count < _lengthSmaDelta.ValueInt + 2 ||
                deltaValues.Count < _lengthSmaDelta.ValueInt + 2)
            {
                return;
            }

            int lastIndex = deltaValues.Count - 1;

            _lastPrice = lastCandle.Close;

            _lastSmaDelta = smaDeltaValues[lastIndex];
            _lastDelta = deltaValues[lastIndex];

            _prevSmaDelta = smaDeltaValues[lastIndex - 1];
            _prevDelta = deltaValues[lastIndex - 1];

            bool isCrossUp = _prevDelta <= _prevSmaDelta && _lastDelta > _lastSmaDelta;
            bool isCrossDown = _prevDelta >= _prevSmaDelta && _lastDelta < _lastSmaDelta;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null &&
                openPositions.Count > 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(openPositions[i], isCrossUp, isCrossDown);
                }
            }

            if (_regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            openPositions = _tab.PositionsOpenAll;

            if (openPositions == null ||
                openPositions.Count == 0)
            {
                LogicOpenPosition(isCrossUp, isCrossDown);
            }
        }

        // Open position logic
        private void LogicOpenPosition(bool isCrossUp, bool isCrossDown)
        {
            decimal volume = GetVolume(_tab, _volume.ValueDecimal);

            if (volume <= 0)
            {
                return;
            }

            if (isCrossUp &&
                _regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtLimit(volume, _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep);
            }

            if (isCrossDown &&
                _regime.ValueString != "OnlyLong")
            {
                _tab.SellAtLimit(volume, _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep);
            }
        }

        // Close position logic
        private void LogicClosePosition(Position position, bool isCrossUp, bool isCrossDown)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            if (position.Direction == Side.Buy &&
                isCrossDown)
            {
                _tab.CloseAtLimit(position, _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep, position.OpenVolume);
            }

            if (position.Direction == Side.Sell &&
                isCrossUp)
            {
                _tab.CloseAtLimit(position, _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep, position.OpenVolume);
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab, decimal volumeParameter)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = volumeParameter;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;

                if (contractPrice == 0)
                {
                    return 0;
                }

                volume = volumeParameter / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                        tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = volumeParameter / (contractPrice * tab.Security.Lot);
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

                decimal moneyOnPosition = portfolioPrimeAsset * (volumeParameter / 100);

                if (tab.PriceBestAsk == 0 ||
                    tab.Security.Lot == 0)
                {
                    return 0;
                }

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                        && tab.Security.PriceStep != tab.Security.PriceStepCost
                        && tab.PriceBestAsk != 0
                        && tab.Security.PriceStep != 0
                        && tab.Security.PriceStepCost != 0)
                    {
                        // Calculation of the number of contracts for futures and options on MOEX
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
