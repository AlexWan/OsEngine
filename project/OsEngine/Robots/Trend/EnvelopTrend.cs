/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.Trend
{
    /// <summary>
    /// Trend strategy based on indicator Envelop
    /// Трендовая стратегия на основе индикатора конверт(Envelop)
    /// </summary>
    public class EnvelopTrend : BotPanel
    {
        public EnvelopTrend(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;

            _envelop = new Envelops(name + "Envelop", false);
            _envelop = (Envelops)_tab.CreateCandleIndicator(_envelop, "Prime");
            _envelop.Save();

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            Slippage = CreateParameter("Slippage", 0, 0, 20, 1);
            Volume= CreateParameter("Volume", 0.1m, 0.1m, 50, 0.1m);
            EnvelopDeviation = CreateParameter("Envelop Deviation", 0.3m, 0.3m, 4, 0.3m);
            EnvelopMovingLength = CreateParameter("Envelop Moving Length", 10, 10, 200, 5);
            TrailStop = CreateParameter("Trail Stop", 0.1m, 0.1m, 5, 0.1m);

            _envelop.Deviation = EnvelopDeviation.ValueDecimal;
            _envelop.MovingAverage.Lenght = EnvelopMovingLength.ValueInt;

            ParametrsChangeByUser += EnvelopTrend_ParametrsChangeByUser;
        }

        private void EnvelopTrend_ParametrsChangeByUser()
        {
            _envelop.Deviation = EnvelopDeviation.ValueDecimal;
            _envelop.MovingAverage.Lenght = EnvelopMovingLength.ValueInt;
            _envelop.Reload();
        }

        // public settings / настройки публичные

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public StrategyParameterInt Slippage;

        /// <summary>
        /// volume for entry
        /// объём для входа
        /// </summary>
        public StrategyParameterDecimal Volume;

        /// <summary>
        /// Envelop deviation from center moving average 
        /// Envelop отклонение от скользящей средней
        /// </summary>
        public StrategyParameterDecimal EnvelopDeviation;

        /// <summary>
        /// moving average length in Envelop 
        /// длинна скользящей средней в конверте
        /// </summary>
        public StrategyParameterInt EnvelopMovingLength;

        /// <summary>
        /// Trail stop length in percent
        /// длинна трейлинг стопа в процентах
        /// </summary>
        public StrategyParameterDecimal TrailStop;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public StrategyParameterString Regime;


        // indicators / индикаторы

        private Envelops _envelop;

        // trade logic

        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
            _tab.BuyAtStopCancel();
            _tab.SellAtStopCancel();

            if (position.Direction == Side.Buy)
            {
                decimal activationPrice = _envelop.ValuesUp[_envelop.ValuesUp.Count - 1] -
                    _envelop.ValuesUp[_envelop.ValuesUp.Count - 1] * (TrailStop.ValueDecimal / 100);

                decimal orderPrice = activationPrice - _tab.Securiti.PriceStep * Slippage.ValueInt;

                _tab.CloseAtTrailingStop(position,
                    activationPrice, orderPrice);
            }
            if (position.Direction == Side.Sell)
            {
                decimal activationPrice = _envelop.ValuesDown[_envelop.ValuesDown.Count - 1] +
                    _envelop.ValuesDown[_envelop.ValuesDown.Count - 1] * (TrailStop.ValueDecimal / 100);

                decimal orderPrice = activationPrice + _tab.Securiti.PriceStep * Slippage.ValueInt;

                _tab.CloseAtTrailingStop(position,
                    activationPrice, orderPrice);
            }


        }

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString != "On")
            {
                return;
            }

            if(candles.Count +5 < _envelop.MovingAverage.Lenght)
            {
                return;
            }

            List<Position> positions = _tab.PositionsOpenAll;

            if(positions.Count == 0)
            { // open logic
                _tab.BuyAtStop(Volume.ValueDecimal,
                    _envelop.ValuesUp[_envelop.ValuesUp.Count - 1] + 
                    Slippage.ValueInt * _tab.Securiti.PriceStep,
                    _envelop.ValuesUp[_envelop.ValuesUp.Count - 1],
                    StopActivateType.HigherOrEqual,1);

                _tab.SellAtStop(Volume.ValueDecimal,
                     _envelop.ValuesDown[_envelop.ValuesDown.Count - 1] -
                     Slippage.ValueInt * _tab.Securiti.PriceStep,
                    _envelop.ValuesDown[_envelop.ValuesDown.Count - 1],
                    StopActivateType.LowerOrEqyal, 1);
            }
            else
            { // trail stop logic

                if(positions[0].State != PositionStateType.Open)
                {
                    return;
                }

                if(positions[0].Direction == Side.Buy)
                {
                    decimal activationPrice = _envelop.ValuesUp[_envelop.ValuesUp.Count - 1] -
                        _envelop.ValuesUp[_envelop.ValuesUp.Count - 1] * (TrailStop.ValueDecimal / 100);

                    decimal orderPrice = activationPrice - _tab.Securiti.PriceStep * Slippage.ValueInt;

                    _tab.CloseAtTrailingStop(positions[0],
                        activationPrice, orderPrice);
                }
                if (positions[0].Direction == Side.Sell)
                {
                    decimal activationPrice = _envelop.ValuesDown[_envelop.ValuesDown.Count - 1] +
                        _envelop.ValuesDown[_envelop.ValuesDown.Count - 1] * (TrailStop.ValueDecimal / 100);

                    decimal orderPrice = activationPrice + _tab.Securiti.PriceStep * Slippage.ValueInt;

                    _tab.CloseAtTrailingStop(positions[0],
                        activationPrice, orderPrice);
                }
            }
        }

        public override string GetNameStrategyType()
        {
            return "EnvelopTrend";
        }

        public override void ShowIndividualSettingsDialog()
        {
           
        }

        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;
    }
}
