/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.Trend
{
    /// <summary>
    /// Bill Williams' trend strategy on the Alligator and fractals
    /// трендовая стратегия Билла Вильямса на Аллигаторе и фракталах
    /// </summary>
    public class StrategyBillWilliams : BotPanel
    {
        public StrategyBillWilliams(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _tab.CandleFinishedEvent += Bot_CandleFinishedEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            Slippage = CreateParameter("Slipage", 0, 0, 20, 1);
            VolumeFirst = CreateParameter("FirstInterVolume", 3, 1.0m, 50, 1);
            VolumeSecond = CreateParameter("SecondInterVolume", 1, 1.0m, 50, 1);
            MaximumPositions = CreateParameter("MaxPoses", 1, 1, 10, 1);
            AlligatorFastLineLength = CreateParameter("AlligatorFastLineLength", 3, 3, 30, 1);
            AlligatorMiddleLineLength = CreateParameter("AlligatorMiddleLineLength", 10, 10, 70, 5);
            AlligatorSlowLineLength = CreateParameter("AlligatorSlowLineLength", 40, 40, 150, 10);

            _alligator = new Alligator(name + "Alligator", false);
            _alligator = (Alligator)_tab.CreateCandleIndicator(_alligator, "Prime");
            _alligator.Save();

            _alligator.LenghtDown = AlligatorSlowLineLength.ValueInt;
            _alligator.LenghtBase = AlligatorMiddleLineLength.ValueInt;
            _alligator.LenghtUp = AlligatorFastLineLength.ValueInt;

            _fractal = new Fractal(name + "Fractal", false);
            _fractal = (Fractal)_tab.CreateCandleIndicator(_fractal, "Prime");

            _aO = new AwesomeOscillator(name + "AO", false);
            _aO = (AwesomeOscillator)_tab.CreateCandleIndicator(_aO, "AoArea");
            _aO.Save();

            ParametrsChangeByUser += StrategyBillWilliams_ParametrsChangeByUser;
        }

        /// <summary>
        /// Parameters changed by user
        /// параметры изменены юзером
        /// </summary>
        void StrategyBillWilliams_ParametrsChangeByUser()
        {
            if (AlligatorSlowLineLength.ValueInt != _alligator.LenghtDown ||
                AlligatorMiddleLineLength.ValueInt != _alligator.LenghtBase ||
                AlligatorFastLineLength.ValueInt != _alligator.LenghtUp)
            {
                _alligator.LenghtDown = AlligatorSlowLineLength.ValueInt;
                _alligator.LenghtBase = AlligatorMiddleLineLength.ValueInt;
                _alligator.LenghtUp = AlligatorFastLineLength.ValueInt;
                _alligator.Reload();
            }
        }

        /// <summary>
        /// unique name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "Williams Band";
        }

        /// <summary>
        /// show settings GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show(OsLocalization.Trader.Label55);
        }

        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        // indicators / индикаторы

        private Alligator _alligator;

        private Fractal _fractal;

        private AwesomeOscillator _aO;

        // public settings / настройки публичные

        /// <summary>
        /// Alligator's fast line length
        /// длинна быстрой линии аллигатора
        /// </summary>
        public StrategyParameterInt AlligatorFastLineLength;

        /// <summary>
        /// Alligator midline length
        /// длинна средней линии аллигатора
        /// </summary>
        public StrategyParameterInt AlligatorMiddleLineLength;

        /// <summary>
        /// alligator slowline length 
        /// длинна медленной линии аллигатора
        /// </summary>
        public StrategyParameterInt AlligatorSlowLineLength;

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public StrategyParameterInt Slippage;

        /// <summary>
        /// volume for the first entry
        /// объём для первого входа
        /// </summary>
        public StrategyParameterDecimal VolumeFirst;

        /// <summary>
        /// volume for subsequent inputs / 
        /// объём для последующих входов
        /// </summary>
        public StrategyParameterDecimal VolumeSecond;

        /// <summary>
        /// maximum positions count
        /// максимальное количество позиций
        /// </summary>
        public StrategyParameterInt MaximumPositions;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public StrategyParameterString Regime;

        // переменные, нужные для торговли
        // variables needed for trading

        private decimal _lastPrice;

        private decimal _lastUpAlligator;

        private decimal _lastMiddleAlligator;

        private decimal _lastDownAlligator;

        private decimal _lastFractalUp;

        private decimal _lastFractalDown;

        private decimal _lastAo;

        private decimal _secondAo;

        private decimal _thirdAo;

        // logic / логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Bot_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (StartProgram == StartProgram.IsOsTrader
                && DateTime.Now.Hour < 10)
            {
                return;
            }

            if (_alligator.ValuesUp == null ||
                _alligator.Values == null ||
                _alligator.ValuesDown == null ||
                _fractal == null ||
                _alligator.LenghtBase > candles.Count ||
                _alligator.LenghtDown > candles.Count ||
                _alligator.LenghtUp > candles.Count)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastUpAlligator = _alligator.ValuesUp[_alligator.ValuesUp.Count - 1];
            _lastMiddleAlligator = _alligator.Values[_alligator.Values.Count - 1];
            _lastDownAlligator = _alligator.ValuesDown[_alligator.ValuesDown.Count - 1];

            for (int i = _fractal.ValuesUp.Count - 1; i > -1; i--)
            {
                if (_fractal.ValuesUp[i] != 0)
                {
                    _lastFractalUp = _fractal.ValuesUp[i];
                    break;
                }
            }

            for (int i = _fractal.ValuesDown.Count - 1; i > -1; i--)
            {
                if (_fractal.ValuesDown[i] != 0)
                {
                    _lastFractalDown = _fractal.ValuesDown[i];
                    break;
                }
            }

            _lastAo = _aO.Values[_aO.Values.Count - 1];

            if (_aO.Values.Count > 3)
            {
                _secondAo = _aO.Values[_aO.Values.Count - 2];
                _thirdAo = _aO.Values[_aO.Values.Count - 3];
            }

            if (_lastUpAlligator == 0 ||
                _lastMiddleAlligator == 0 ||
                _lastDownAlligator == 0)
            {
                return;
            }


            // распределяем логику в зависимости от текущей позиции
            //we distribute logic depending on the current position

            List<Position> openPosition = _tab.PositionsOpenAll;

            if (openPosition != null && openPosition.Count != 0
                && candles[candles.Count - 1].TimeStart.Hour <= 18)
            {
                for (int i = 0; i < openPosition.Count; i++)
                {
                    LogicClosePosition(openPosition[i], candles);
                }
            }

            if (Regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            if (openPosition == null || openPosition.Count == 0
                && candles[candles.Count - 1].TimeStart.Hour >= 11
                && candles[candles.Count - 1].TimeStart.Hour <= 18)
            {
                LogicOpenPosition();
            }
            else if (openPosition.Count != 0 && openPosition.Count < MaximumPositions.ValueInt
                     && candles[candles.Count - 1].TimeStart.Hour >= 11
                     && candles[candles.Count - 1].TimeStart.Hour <= 18)
            {
                LogicOpenPositionSecondary(openPosition[0].Direction);
            }


        }

        /// <summary>
        /// open positin logic
        /// логика открытия позиции
        /// </summary>
        private void LogicOpenPosition()
        {
            if (_lastPrice > _lastUpAlligator && _lastPrice > _lastMiddleAlligator && _lastPrice > _lastDownAlligator
                && _lastPrice > _lastFractalUp
                && Regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtLimit(VolumeFirst.ValueDecimal, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
            }
            if (_lastPrice < _lastUpAlligator && _lastPrice < _lastMiddleAlligator && _lastPrice < _lastDownAlligator
                && _lastPrice < _lastFractalDown
                && Regime.ValueString != "OnlyLong")
            {
                _tab.SellAtLimit(VolumeFirst.ValueDecimal, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
            }
        }

        /// <summary>
        /// open position logic. After first position
        /// логика открытия позиции после первой 
        /// </summary>
        private void LogicOpenPositionSecondary(Side side)
        {
            if (side == Side.Buy && Regime.ValueString != "OnlyShort")
            {
                if (_secondAo < _lastAo &&
                    _secondAo < _thirdAo)
                {
                    _tab.BuyAtLimit(VolumeSecond.ValueDecimal, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }

            if (side == Side.Sell && Regime.ValueString != "OnlyLong")
            {
                if (_secondAo > _lastAo &&
                    _secondAo > _thirdAo)
                {
                    _tab.SellAtLimit(VolumeSecond.ValueDecimal, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
                }
            }
        }

        /// <summary>
        /// close position logic
        /// логика закрытия позиции
        /// </summary>
        private void LogicClosePosition(Position position, List<Candle> candles)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            if (position.Direction == Side.Buy)
            {
                if (_lastPrice < _lastMiddleAlligator)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep, position.OpenVolume);
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastPrice > _lastMiddleAlligator)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep, position.OpenVolume);
                }
            }
        }
    }
}
