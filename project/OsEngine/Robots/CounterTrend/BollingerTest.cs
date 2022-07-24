using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.Charts.CandleChart.Indicators;
using System.Drawing;
using System;

namespace OsEngine.Robots.CounterTrend
{

    /// <summary>
    /// тестовая стратегия по Bollinger
    /// </summary>

    public class BollingerTest : BotPanel
    {
        public BollingerTest(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _needClose.isActive = false;

            _lastStopCandleIndex = 0;


            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            Volume = CreateParameter("Volume", 0.001m, 0.0005m, 100, 0.0005m);
            Slippage = CreateParameter("Slippage", 0, 0, 50, 1);
            MaxDropDownPercent = CreateParameter("MaxDropDownPercent", 0.33m, 0.0m, 2, 0.01m);

            PeriodLength = CreateParameter("Period Length", 15, 5, 50, 1);
            EmaLength = CreateParameter("Ema Length", 15, 5, 50, 1);
            //EmaType = CreateParameter("Ema Type", 1, 0, 6, 1);

            //PeriodType = CreateParameter("Period Type", 1, 0, 6, 1);

            Deviation = CreateParameter("Deviation", 2.22m, 1m, 10, 0.01m);

            WaitCandleCountForClose = CreateParameter("Wait For Close", 7, 3, 30, 1);

            _bollinger = new Bollinger(name + "Bollinger", false)
            {
                Lenght = PeriodLength.ValueInt,
                Deviation = Deviation.ValueDecimal,
                ColorUp = Color.Blue,
                ColorDown = Color.DarkRed
            };
            _bollinger = _tab.CreateIndicator(_bollinger);

            _ema = new MovingAverage(name + "Ema", false)
            {
                Lenght = EmaLength.ValueInt,
                TypeCalculationAverage = MovingAverageTypeCalculation.Simple,
                ColorBase = Color.DeepSkyBlue
            };
            _ema = _tab.CreateIndicator(_ema);

            ParametrsChangeByUser += EnvelopTrend_ParametersChangeByUser;

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.CandleUpdateEvent += _tab_CandleUpdateEvent;
            _tab.PositionClosingFailEvent += _tab_PositionClosingFailEvent;
            _tab.PositionClosingSuccesEvent += _tab_PositionClosingSuccesEvent;
        }



        private void EnvelopTrend_ParametersChangeByUser()
        {
            _ema.Lenght = EmaLength.ValueInt;
            _ema.TypeCalculationAverage = MovingAverageTypeCalculation.Simple;// (MovingAverageTypeCalculation)EmaType.ValueInt;
            _ema.Reload();

            _bollinger.Lenght = PeriodLength.ValueInt;
            _bollinger.Deviation = Deviation.ValueDecimal;
            _bollinger.Reload();

            _lastStopCandleIndex = 0;
            _needClose.isActive = false;
            _activeCandleIndex = 0;
        }

        /// <summary>
        /// bot name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "BollingerTest";
        }

        /// <summary>
        /// strategy name
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {

        }

        /// <summary>
        /// trade tab
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;


        //indicators индикаторы

        /// <summary>
        /// ema сигнальная
        /// </summary>
        private MovingAverage _ema;

        /// <summary>
        /// Bollinger
        /// </summary>
        private Bollinger _bollinger;

        //settings настройки публичные

        public StrategyParameterInt Slippage;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString Regime;

        public StrategyParameterDecimal MaxDropDownPercent;

        public StrategyParameterDecimal BollingerDeviation;

        public StrategyParameterInt EmaLength;
        public StrategyParameterInt PeriodLength;
        public StrategyParameterDecimal Deviation;

        public StrategyParameterInt EmaType;
        public StrategyParameterInt PeriodType;

        public StrategyParameterInt WaitCandleCountForClose;

        private NeedClose _needClose;
        private int _activeCandleIndex;
        private int _lastStopCandleIndex;


        private struct NeedClose
        {
            public bool isActive;
            public decimal price;
        }


        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            _tab_CandleEvent(candles, true);
        }

        private void _tab_CandleUpdateEvent(List<Candle> candles)
        {
            _tab_CandleEvent(candles, false);
        }


        private void _tab_CandleEvent(List<Candle> candles, bool isClosedCandle)
        {
            _activeCandleIndex = candles.Count - 1;

            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (_bollinger.ValuesUp == null ||
                _bollinger.ValuesUp.Count == 0 ||
                _bollinger.ValuesUp.Count < candles.Count ||
                _ema.Values.Count < candles.Count)
            {
                return;
            }

            List<Position> openPosition = _tab.PositionsOpenAll;


            if (openPosition != null && openPosition.Count != 0)
            {
                for (int i = 0; i < openPosition.Count; i++)
                {
                    LogicClosePosition(openPosition[i], candles, isClosedCandle);
                }
            }

            if (Regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            if (openPosition == null || openPosition.Count == 0)
            {
                LogicOpenPosition(candles, isClosedCandle);
            }
        }

        /// <summary>
        /// position opening logic
        /// логика открытия позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, bool isClosedCandle)
        {
            decimal bollingerUpLast = _bollinger.ValuesUp[_bollinger.ValuesUp.Count - 1];

            decimal bollingerDownLast = _bollinger.ValuesDown[_bollinger.ValuesDown.Count - 1];

            decimal moving = _ema.Values[_ema.Values.Count - 1];

            _needClose.isActive = false;

            //openPositionCandleIndex = candles.Count - 1;

            if (bollingerUpLast == 0 ||
                bollingerDownLast == 0)
            {
                return;
            }

            
            if (_lastStopCandleIndex + 1 >= candles.Count - 1) // не открывать сразу после закрытия
                return;

            decimal close = candles[candles.Count - 1].Close;

            // Sell
            if (close > bollingerUpLast
                && Regime.ValueString != "OnlyLong"
                && moving < bollingerUpLast
                )
            {
                if (
                    _bollinger.ValuesUp[_bollinger.ValuesUp.Count - 1] > _bollinger.ValuesUp[_bollinger.ValuesUp.Count - 2]
                    && _bollinger.ValuesUp[_bollinger.ValuesUp.Count - 2] > _bollinger.ValuesUp[_bollinger.ValuesUp.Count - 3]
                    && _bollinger.ValuesUp[_bollinger.ValuesUp.Count - 3] > _bollinger.ValuesUp[_bollinger.ValuesUp.Count - 4]
                    )
                    _tab.SellAtLimit(Volume.ValueDecimal, close - _tab.Securiti.PriceStep * Slippage.ValueInt);
            }


            // Buy
            if (close < bollingerDownLast)
                if( Regime.ValueString != "OnlyShort")
                if( moving > bollingerDownLast)
                
            {
                if (
                    _bollinger.ValuesDown[_bollinger.ValuesDown.Count - 1] < _bollinger.ValuesDown[_bollinger.ValuesDown.Count - 2]
                    && _bollinger.ValuesDown[_bollinger.ValuesDown.Count - 2] < _bollinger.ValuesDown[_bollinger.ValuesDown.Count - 3]
                    && _bollinger.ValuesDown[_bollinger.ValuesDown.Count - 3] < _bollinger.ValuesDown[_bollinger.ValuesDown.Count - 4]
                    )
                    _tab.BuyAtLimit(Volume.ValueDecimal, close + _tab.Securiti.PriceStep * Slippage.ValueInt);
            }
        }

        /// <summary>
        /// position closing logic
        /// логика закрытия позиции
        /// </summary>
        private void LogicClosePosition(Position position, List<Candle> candles, bool isClosedCandle)
        {
            if (position.State == PositionStateType.Closing)
            {
                return;
            }
            decimal moving = _ema.Values[_ema.Values.Count - 1];
            decimal lastUp = _bollinger.ValuesUp[_bollinger.ValuesUp.Count - 1];
            decimal lastDown = _bollinger.ValuesDown[_bollinger.ValuesDown.Count - 1];

            decimal lastClose = candles[candles.Count - 1].Close;

            decimal deadlineDelta = position.EntryPrice * MaxDropDownPercent.ValueDecimal/100m;

            var openPositionCandleIndex = GetCandleIndexByTime(position.TimeOpen, candles);
            var deltaCandlesCount = (candles.Count - 1) - openPositionCandleIndex;

            int MaxWaitCandleCount = (int)Math.Floor(WaitCandleCountForClose.ValueInt * 1.5m);

            if (MaxWaitCandleCount < deltaCandlesCount && _needClose.isActive == false)
            {
                _tab.CloseAtMarket(position, position.OpenVolume);
                return;
            }


            if (position.Direction == Side.Buy)
            {
                if (position.EntryPrice - deadlineDelta > lastClose)
                {
                    _tab.CloseAtMarket(position, position.OpenVolume);
                    return;
                }

                if (isClosedCandle == true && _needClose.isActive == true)
                {
                    var med = (candles[candles.Count - 1].Close - candles[candles.Count - 1].Low) / 8;

                    if (_needClose.price < candles[candles.Count - 1].Low + med)
                        _needClose.price = candles[candles.Count - 1].Low + med;
                }

                if (_needClose.isActive == true && lastClose < _needClose.price)
                {
                    //_tab.CloseAtLimit(position, lastClose - _tab.Securiti.PriceStep * Slippage.ValueInt, position.OpenVolume);
                    _tab.CloseAtMarket(position, position.OpenVolume);
                    return;
                }

                
                if (lastClose > lastUp && _needClose.isActive == false) // если пересекли верхнюю линию - выход
                {
                    _tab.CloseAtMarket(position, position.OpenVolume);
                    return;
                }


                if (lastClose > moving)
                {
                    if (isClosedCandle == false)
                    {
                        if (_needClose.isActive == false)
                        {
                            _needClose.isActive = true;
                            _needClose.price = lastClose - candles[candles.Count - 1].Open;
                        }
                    }
                    else
                    {
                        if (_needClose.isActive == false)
                            _tab.CloseAtLimit(position, lastClose - _tab.Securiti.PriceStep * Slippage.ValueInt, position.OpenVolume);
                    }
                }
                else
                {
                    if (WaitCandleCountForClose.ValueInt <= deltaCandlesCount && lastClose < position.EntryPrice)
                        _tab.CloseAtMarket(position, position.OpenVolume);
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (position.EntryPrice + deadlineDelta < lastClose)
                {
                    _tab.CloseAtMarket(position, position.OpenVolume);
                    return;
                }

                if (isClosedCandle == true && _needClose.isActive == true)
                {
                    var med = (candles[candles.Count - 1].High - candles[candles.Count - 1].Close) / 8;

                    if (_needClose.price > candles[candles.Count - 1].High - med)
                        _needClose.price = candles[candles.Count - 1].High - med;
                }

                if (_needClose.isActive == true && lastClose > _needClose.price)
                {
                    //_tab.CloseAtLimit(position, lastClose + _tab.Securiti.PriceStep * Slippage.ValueInt, position.OpenVolume);
                    _tab.CloseAtMarket(position, position.OpenVolume);
                    return;
                }

                if (lastClose < lastDown && _needClose.isActive == false) // если пересекли нижнюю линию - выход
                {
                    _tab.CloseAtMarket(position, position.OpenVolume);
                    return;
                }


                if (lastClose < moving)
                {
                    if (isClosedCandle == false)
                    {
                        if (_needClose.isActive == false)
                        {
                            _needClose.isActive = true;
                            _needClose.price = candles[candles.Count - 1].Open;
                        }
                    }
                    else
                    {
                        if (_needClose.isActive == false)
                            _tab.CloseAtLimit(position, lastClose + _tab.Securiti.PriceStep * Slippage.ValueInt, position.OpenVolume);
                    }
                }
                else
                {
                    if (WaitCandleCountForClose.ValueInt <= deltaCandlesCount 
                        && lastClose > position.EntryPrice)
                        _tab.CloseAtMarket(position, position.OpenVolume);
                }
            }
        }

        private int GetCandleIndexByTime(DateTime tagetTime, List<Candle> candles)
        {
            int res = candles.Count - 1;

            if (tagetTime < DateTime.Now.AddDays(-100))
                return candles.Count - 1;

            var duration = candles[1].TimeStart - candles[0].TimeStart;

            for (int i = candles.Count - 1; i > 0; i--)
            {
                if (tagetTime >= candles[i].TimeStart && tagetTime < candles[i].TimeStart.Add(duration))
                {
                    res = i;
                    break;
                }
            }

            return res;
        }


        private void _tab_PositionClosingFailEvent(Position position)
        {
            if (position.CloseActiv)
            {
                _tab.CloseAtMarket(position, position.OpenVolume);
            }
        }

        private void _tab_PositionClosingSuccesEvent(Position obj)
        {
            _needClose.isActive = false;
            _lastStopCandleIndex = _activeCandleIndex;
        }

    }
}
