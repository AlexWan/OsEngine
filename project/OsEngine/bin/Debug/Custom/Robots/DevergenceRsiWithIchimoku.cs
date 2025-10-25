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
Trend-following robot based on RSI divergence with Ichimoku confirmation.

Buy conditions:
Price forms lower lows, while RSI forms higher lows (bullish divergence).
Ichimoku confirmation: Senkou Span A crosses above Senkou Span B (Kumo twist up).

Sell conditions:
Price forms higher highs, while RSI forms lower highs (bearish divergence).
Ichimoku confirmation: Senkou Span A crosses below Senkou Span B (Kumo twist down).

Exit from long:
Stop-loss below the minimum of the last N candles.
Take-profit at the maximum of the last N candles.

Exit from short:
Stop-loss above the maximum of the last N candles.
Take-profit at the minimum of the last N candles.
*/

namespace OsEngine.Robots.AO
{
    [Bot("DevergenceRsiWithIchimoku")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    public class DevergenceRsiWithIchimoku : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _startTradeTime;
        private StrategyParameterTimeOfDay _endTradeTime;

        // GetVolume Parameter
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicators Settings 
        private StrategyParameterInt _tenkanLength;
        private StrategyParameterInt _kijunLength;
        private StrategyParameterInt _senkouLength;
        private StrategyParameterInt _chinkouLength;
        private StrategyParameterInt _offset;
        private StrategyParameterInt _periodZigZag;
        private StrategyParameterInt _periodRsi;

        // Indicators
        Aindicator _ichomoku;
        Aindicator _zigZag;
        Aindicator _zigZagRsi;

        // The last value of the indicator
        private decimal _lastSenkouA;
        private decimal _lastSenkouB;

        // The prev value of the indicator
        private decimal _prevSenkouA;
        private decimal _prevSenkouB;

        // Exit Settings
        private StrategyParameterInt _stopCandles;
        private StrategyParameterInt _profitCandles;

        // Counter
        decimal Cnt;

        public DevergenceRsiWithIchimoku(string name, StartProgram startProgram) : base(name, startProgram)
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
            _tenkanLength = CreateParameter("Tenkan Length", 9, 1, 50, 3, "Indicator");
            _kijunLength = CreateParameter("Kijun Length", 26, 1, 50, 4, "Indicator");
            _senkouLength = CreateParameter("Senkou Length", 52, 1, 100, 8, "Indicator");
            _chinkouLength = CreateParameter("Chinkou Length", 26, 1, 50, 4, "Indicator");
            _offset = CreateParameter("Offset", 26, 1, 50, 4, "Indicator");
            _periodZigZag = CreateParameter("Period ZigZag", 10, 10, 300, 10, "Indicator");
            _periodRsi = CreateParameter("Period CCI", 10, 10, 300, 10, "Indicator");

            // Create indicator _Ichomoku
            _ichomoku = IndicatorsFactory.CreateIndicatorByName("Ichimoku", name + "Ichimoku", false);
            _ichomoku = (Aindicator)_tab.CreateCandleIndicator(_ichomoku, "Prime");
            ((IndicatorParameterInt)_ichomoku.Parameters[0]).ValueInt = _tenkanLength.ValueInt;
            ((IndicatorParameterInt)_ichomoku.Parameters[1]).ValueInt = _kijunLength.ValueInt;
            ((IndicatorParameterInt)_ichomoku.Parameters[2]).ValueInt = _senkouLength.ValueInt;
            ((IndicatorParameterInt)_ichomoku.Parameters[3]).ValueInt = _chinkouLength.ValueInt;
            ((IndicatorParameterInt)_ichomoku.Parameters[4]).ValueInt = _offset.ValueInt;
            _ichomoku.Save();

            // Create indicator ZigZag
            _zigZag = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "ZigZag", false);
            _zigZag = (Aindicator)_tab.CreateCandleIndicator(_zigZag, "Prime");
            ((IndicatorParameterInt)_zigZag.Parameters[0]).ValueInt = _periodZigZag.ValueInt;
            _zigZag.Save();

            // Create indicator ZigZag Rsi
            _zigZagRsi = IndicatorsFactory.CreateIndicatorByName("ZigZagRsi", name + "ZigZagRsi", false);
            _zigZagRsi = (Aindicator)_tab.CreateCandleIndicator(_zigZagRsi, "NewArea");
            ((IndicatorParameterInt)_zigZagRsi.Parameters[0]).ValueInt = _periodRsi.ValueInt;
            ((IndicatorParameterInt)_zigZagRsi.Parameters[1]).ValueInt = _periodZigZag.ValueInt;
            _zigZagRsi.Save();

            // Exit Settings
            _stopCandles = CreateParameter("Stop Candel", 1, 5, 200, 5, "Exit");
            _profitCandles = CreateParameter("Profit Candel", 1, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += DevergenceRsiWithIchimoku_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel306;
        }

        private void DevergenceRsiWithIchimoku_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ichomoku.Parameters[0]).ValueInt = _tenkanLength.ValueInt;
            ((IndicatorParameterInt)_ichomoku.Parameters[1]).ValueInt = _kijunLength.ValueInt;
            ((IndicatorParameterInt)_ichomoku.Parameters[2]).ValueInt = _senkouLength.ValueInt;
            ((IndicatorParameterInt)_ichomoku.Parameters[3]).ValueInt = _chinkouLength.ValueInt;
            ((IndicatorParameterInt)_ichomoku.Parameters[4]).ValueInt = _offset.ValueInt;
            _ichomoku.Save();
            _ichomoku.Reload();
            ((IndicatorParameterInt)_zigZag.Parameters[0]).ValueInt = _periodZigZag.ValueInt;
            _zigZag.Save();
            _zigZag.Reload();
            ((IndicatorParameterInt)_zigZagRsi.Parameters[0]).ValueInt = _periodRsi.ValueInt;
            ((IndicatorParameterInt)_zigZagRsi.Parameters[1]).ValueInt = _periodZigZag.ValueInt;
            _zigZagRsi.Save();
            _zigZagRsi.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "DevergenceRsiWithIchimoku";
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
            if (candles.Count < _tenkanLength.ValueInt ||
                candles.Count < _kijunLength.ValueInt ||
                candles.Count < _senkouLength.ValueInt ||
                candles.Count < _chinkouLength.ValueInt ||
                candles.Count < _offset.ValueInt ||
                candles.Count < _stopCandles.ValueInt ||
                candles.Count < _profitCandles.ValueInt ||
                candles.Count < _periodZigZag.ValueInt ||
                candles.Count < _periodRsi.ValueInt)
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
                Cnt = 0;
            }
        }

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            // The last value of the indicator
            _lastSenkouA = _ichomoku.DataSeries[3].Last;
            _lastSenkouB = _ichomoku.DataSeries[4].Last;

            // The prev value of the indicator
            _prevSenkouA = _ichomoku.DataSeries[3].Values[_ichomoku.DataSeries[3].Values.Count - 2];
            _prevSenkouB = _ichomoku.DataSeries[4].Values[_ichomoku.DataSeries[4].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            List<decimal> zzHigh = _zigZag.DataSeries[2].Values;
            List<decimal> zzLow = _zigZag.DataSeries[3].Values;

            List<decimal> zzRsiLow = _zigZagRsi.DataSeries[4].Values;
            List<decimal> zzRsiHigh = _zigZagRsi.DataSeries[3].Values;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_prevSenkouA < _prevSenkouB && _lastSenkouA > _lastSenkouB && DevirgenceBuy(zzLow, zzRsiLow, zzRsiHigh) == true)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), lastPrice + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_prevSenkouA > _prevSenkouB && _lastSenkouA < _lastSenkouB && DevirgenceSell(zzHigh, zzRsiHigh, zzRsiLow) == true)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), lastPrice - _slippage);
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

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (Cnt == 1)
                {
                    return;
                }

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is buy
                { 
                    _tab.CloseAtProfit(pos, MaxPrice(candles, _profitCandles.ValueInt), MaxPrice(candles, _profitCandles.ValueInt) + _slippage);
                    _tab.CloseAtStop(pos, MinPrice(candles,_stopCandles.ValueInt), MinPrice(candles, _stopCandles.ValueInt) - _slippage);
                    Cnt = 1;
                }
                else // If the direction of the position is sell
                {
                     _tab.CloseAtProfit(pos, MinPrice(candles, _profitCandles.ValueInt), MinPrice(candles, _profitCandles.ValueInt) - _slippage);
                    _tab.CloseAtStop(pos, MaxPrice(candles, _stopCandles.ValueInt), MaxPrice(candles, _stopCandles.ValueInt) + _slippage);
                    Cnt = 1;
                }
            }
        }

        // Method for finding divergence
        private bool DevirgenceBuy(List<decimal> zzLow, List<decimal> zzRsiLow, List<decimal> zzRsiHigh)
        {
            decimal zzLowOne = 0;
            decimal zzLowTwo = 0;
            decimal zzRsiLowOne = 0;
            decimal zzRsiLowTwo = 0;

            int indexOne = 0;
            int indexTwo = 0;
            int indexHigh = 0;

            for (int i = zzRsiHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzRsiHigh[i] != 0)
                {
                    cnt++;
                    indexHigh = i;
                }

                if (cnt == 1)
                {
                    break;
                }
            }

            for (int i = zzLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzLow[i] != 0 && zzLowOne == 0)
                {
                    zzLowOne = zzLow[i];
                    cnt++;
                    indexOne = i;
                }

                if (zzLow[i] != 0 && indexOne != i && zzLowTwo == 0)
                {
                    zzLowTwo = zzLow[i];
                    cnt++;
                }

                if (cnt == 2)
                {
                    break;
                }
            }

            for (int i = zzRsiLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzRsiLow[i] != 0 && zzRsiLowOne == 0)
                {
                    zzRsiLowOne = zzRsiLow[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzRsiLow[i] != 0 && indexTwo != i && zzRsiLowTwo == 0)
                {
                    zzRsiLowTwo = zzRsiLow[i];
                    cnt++;
                }

                if (cnt == 2)
                {
                    break;
                }
            }

            decimal cntLow = 0;

            if (zzLowOne < zzLowTwo && zzLowOne != 0 && indexTwo < indexHigh)
            {
                cntLow++;
            }

            if (zzRsiLowOne > zzRsiLowTwo && zzRsiLowOne != 0)
            {
                cntLow++;
            }

            if (cntLow == 2)
            {
                return true;
            }

            return false;
        }

        // Method for finding divergence
        private bool DevirgenceSell(List<decimal> zzHigh, List<decimal> zzRsiHigh, List<decimal> zzRsiLow)
        {
            decimal zzHighOne = 0;
            decimal zzHighTwo = 0;
            decimal zzRsiHighOne = 0;
            decimal zzRsiHighTwo = 0;

            int indexOne = 0;
            int indexTwo = 0;
            int indexLow = 0;

            for (int i = zzRsiLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzRsiLow[i] != 0)
                {
                    cnt++;
                    indexLow = i;
                }

                if (cnt == 1)
                {
                    break;
                }
            }

            for (int i = zzHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzHigh[i] != 0 && zzHighOne == 0)
                {
                    zzHighOne = zzHigh[i];
                    cnt++;
                    indexOne = i;
                }

                if (zzHigh[i] != 0 && indexOne != i && zzHighTwo == 0)
                {
                    zzHighTwo = zzHigh[i];
                    cnt++;
                }

                if (cnt == 2)
                {
                    break;
                }
            }

            for (int i = zzRsiHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzRsiHigh[i] != 0 && zzRsiHighOne == 0)
                {
                    zzRsiHighOne = zzRsiHigh[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzRsiHigh[i] != 0 && indexTwo != i && zzRsiHighTwo == 0)
                {
                    zzRsiHighTwo = zzRsiHigh[i];
                    cnt++;
                }

                if (cnt == 2)
                {
                    break;
                }
            }

            decimal cntHigh = 0;

            if (zzHighOne > zzHighTwo && zzHighTwo != 0 && indexTwo < indexLow)
            {
                cntHigh++;
            }

            if (zzRsiHighOne < zzRsiHighTwo && zzRsiHighOne != 0)
            {
                cntHigh++;
            }

            if (cntHigh == 2)
            {
                return true;
            }

            return false;
        }

        private decimal MaxPrice(List<Candle> candles, int period)
        {
            decimal max = 0;
            for (int i = 1; i <= period; i++)
            {
                if (max < candles[candles.Count - i].Close)
                {
                    max = candles[candles.Count - i].Close;
                }
            }
            return max;
        }

        private decimal MinPrice(List<Candle> candles, int period)
        {
            decimal min = decimal.MaxValue;
            for (int i = 1; i <= period; i++)
            {
                if (min > candles[candles.Count - i].Close)
                {
                    min = candles[candles.Count - i].Close;
                }
            }
            return min;
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