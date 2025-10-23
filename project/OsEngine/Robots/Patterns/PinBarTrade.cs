/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Language;

/*Discription
Trading robot for osengine

Trend robot on the PinBar Trade.

Buy:
1. The closing price must be **greater than or equal to** the level located at 1/3 of the candle's range from the bottom.
2. The opening price must also be **greater than or equal to** this level.
3. The moving average (SMA) must be **below** the current closing price, indicating an upward trend.

Sell:
1. The closing price must be **less than or equal to** the level located at 1/3 of the candle's range from the top.
2. The opening price must also be **less than or equal to** this level.
3. The moving average (SMA) must be **above** the current closing price, indicating a downward trend.

Exit: by trailing stop.
*/

namespace OsEngine.Robots.Patterns
{
    [Bot("PinBarTrade")] // We create an attribute so that we don't write anything to the BotFactory
    public class PinBarTrade : BotPanel
    {
        private BotTabSimple _tab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _smaPeriod;

        // Indicator
        private Aindicator _sma;

        // Exit settings
        private StrategyParameterDecimal _trailStop;

        public PinBarTrade(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            _slippage = CreateParameter("Slippage %", 0, 0, 20, 1m);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 1, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _smaPeriod = CreateParameter("Sma Period", 100, 10, 50, 500);
            
            // Exit settings
            _trailStop = CreateParameter("Trail stop %", 0.5m, 0, 20, 1m);

            // Create indicator Sma
            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "sma", false);
            _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");
            _sma.ParametersDigit[0].Value = _smaPeriod.ValueInt;
            _sma.Save();

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += PinBarTrade_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel74;
        }

        private void PinBarTrade_ParametrsChangeByUser()
        {
            if (_sma.ParametersDigit[0].Value != _smaPeriod.ValueInt)
            {
                _sma.ParametersDigit[0].Value = _smaPeriod.ValueInt;
                _sma.Reload();
            }
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PinBarTrade";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (_sma.DataSeries[0].Values.Count < _sma.ParametersDigit[0].Value + 2)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);
                }
            }

            if (_regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            decimal lastClose = candles[candles.Count - 1].Close;
            decimal lastOpen = candles[candles.Count - 1].Open;
            decimal lastHigh = candles[candles.Count - 1].High;
            decimal lastLow = candles[candles.Count - 1].Low;
            decimal lastSma = _sma.DataSeries[0].Last;

            if (lastClose >= lastHigh - ((lastHigh - lastLow) / 3) && lastOpen >= lastHigh - ((lastHigh - lastLow) / 3)
                && lastSma < lastClose 
                && _regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
            {
                _tab.BuyAtLimit(GetVolume(_tab), lastClose + lastClose * (_slippage.ValueDecimal / 100));
            }

            if (lastClose <= lastLow + ((lastHigh - lastLow) / 3) && lastOpen <= lastLow + ((lastHigh - lastLow) / 3)
                && lastSma > lastClose 
                && _regime.ValueString != "OnlyLong") // if the mode is not only long, we enter the short
            {
                _tab.SellAtLimit(GetVolume(_tab), lastClose - lastClose * (_slippage.ValueDecimal / 100));
            }
        }

        // Logic close position 
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.State != PositionStateType.Open
                ||
                (position.CloseOrders != null 
                && position.CloseOrders.Count > 0)
                )
            {
                return;
            }

            decimal lastClose = candles[candles.Count - 1].Close;

            decimal stop = 0;
            decimal stopWithSlippage = 0;

            if(position.Direction == Side.Buy) // If the direction of the position is long
            {
                stop = lastClose - lastClose * (_trailStop.ValueDecimal / 100);
                stopWithSlippage = stop - stop * (_slippage.ValueDecimal / 100);
            }
            else // If the direction of the position is short
            {
                stop = lastClose + lastClose * (_trailStop.ValueDecimal/100);
                stopWithSlippage = stop + stop * (_slippage.ValueDecimal/100);
            }

            _tab.CloseAtTrailingStop(position, stop, stopWithSlippage);
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