using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.OnScriptIndicators
{
    [Bot("SmaTrendSample")]
    public class SmaTrendSample : BotPanel
    {
        public SmaTrendSample(string name, StartProgram startProgram)
        : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On"});
            Volume = CreateParameter("Volume", 3, 1.0m, 50, 4);
            Slippage = CreateParameter("Slippage", 0, 0, 20, 1);
            SmaLength = CreateParameter("Sma length", 30, 0, 20, 1);
            BaseStopPercent = CreateParameter("Base Stop Percent", 0.3m, 1.0m, 50, 4);

            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "sma", false);
            _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");
            _sma.ParametersDigit[0].Value = SmaLength.ValueInt;
            _sma.Save();

            EnvelopLength = CreateParameter("Envelop length", 30, 0, 20, 1);

            EnvelopDeviation = CreateParameter("Envelop Deviation", 1, 1.0m, 50, 4);

            _envelop = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "env", false);
            _envelop = (Aindicator)_tab.CreateCandleIndicator(_envelop, "Prime");
            _envelop.ParametersDigit[0].Value = EnvelopLength.ValueInt;
            _envelop.ParametersDigit[1].Value = EnvelopDeviation.ValueDecimal;
            _envelop.Save();

            ParametrsChangeByUser += SmaTrendSample_ParametrsChangeByUser;

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot SmaTrendSample " +
                "Buy: lastCandlePrice > smaValue and lastCandlePrice > upChannel." +
                "Sell: lastCandlePrice < smaValue and lastCandlePrice < downChannel." +
                "Exit: By TralingStop";
        }

        private void SmaTrendSample_ParametrsChangeByUser()
        {
            if(SmaLength.ValueInt != _sma.ParametersDigit[0].Value)
            {
                _sma.ParametersDigit[0].Value = SmaLength.ValueInt;
                _sma.Save();
                _sma.Reload();
            }

            if (EnvelopLength.ValueInt != _envelop.ParametersDigit[0].Value ||
                EnvelopDeviation.ValueDecimal != _envelop.ParametersDigit[1].Value)
            {
                _envelop.ParametersDigit[0].Value = EnvelopLength.ValueInt;
                _envelop.ParametersDigit[1].Value = EnvelopDeviation.ValueDecimal;
                _envelop.Save();
                _envelop.Reload();
            }

        }

        public override string GetNameStrategyType()
        {
            return "SmaTrendSample";
        }

        public override void ShowIndividualSettingsDialog()
        {
            
        }

        /// <summary>
        /// trade tab
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //indicators индикаторы

        private Aindicator _sma;

        private Aindicator _envelop;

        //settings настройки публичные

        public StrategyParameterInt Slippage;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString Regime;

        public StrategyParameterInt SmaLength;

        public StrategyParameterInt EnvelopLength;

        public StrategyParameterDecimal EnvelopDeviation;

        public StrategyParameterDecimal BaseStopPercent;

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if(candles.Count < SmaLength.ValueInt +1)
            {
                return;
            }

            if(Regime.ValueString == "Off")
            {
                return;
            }

            List<Position> poses = _tab.PositionsOpenAll;

            if(poses.Count == 0)
            {
                OpenPositionLogic(candles);
            }
            else
            {
                ClosePositionLogic(poses[0],candles);
            }
        }

        private void OpenPositionLogic(List<Candle> candles)
        {
            decimal smaValue = _sma.DataSeries[0].Last;

            if(smaValue == 0)
            {
                return;
            }

            decimal lastCandlePrice = candles[candles.Count-1].Close;

            decimal upChannel = _envelop.DataSeries[0].Last;
            decimal downChannel = _envelop.DataSeries[2].Last;

            if(upChannel == 0 ||
                downChannel == 0)
            {
                return;
            }

            if (lastCandlePrice > smaValue &&
                lastCandlePrice > upChannel)
            {
                _tab.BuyAtLimit(Volume.ValueDecimal, _tab.PriceBestAsk + _tab.Securiti.PriceStep * Slippage.ValueInt);
            }
            if (lastCandlePrice < smaValue &&
                lastCandlePrice < downChannel)
            {
                _tab.SellAtLimit(Volume.ValueDecimal, _tab.PriceBestBid + _tab.Securiti.PriceStep * Slippage.ValueInt);
            }

        }

        private void ClosePositionLogic(Position position, List<Candle> candles)
        {
            if(position.State == PositionStateType.Closing)
            {
                return;
            }

            decimal stopPrice = position.EntryPrice - position.EntryPrice * BaseStopPercent.ValueDecimal/100;

            if (position.Direction == Side.Sell)
            {
                stopPrice = position.EntryPrice + position.EntryPrice * BaseStopPercent.ValueDecimal / 100;
            }

            decimal smaValue = _sma.DataSeries[0].Last;

            decimal lastCandlePrice = candles[candles.Count - 1].Close;

            if (position.Direction == Side.Buy &&
                smaValue > stopPrice 
                && lastCandlePrice > smaValue)
            {
                stopPrice = smaValue;
            }
            if (position.Direction == Side.Sell &&
                smaValue < stopPrice 
                && lastCandlePrice < smaValue)
            {
                stopPrice = smaValue;
            }

            decimal priceOrder = stopPrice;

            if(StartProgram == StartProgram.IsOsTrader)
            {
                if (position.Direction == Side.Buy)
                {
                    priceOrder = priceOrder - _tab.Securiti.PriceStep * Slippage.ValueInt;
                }
                if (position.Direction == Side.Sell)
                {
                    priceOrder = priceOrder + _tab.Securiti.PriceStep * Slippage.ValueInt;
                }
            }

            _tab.CloseAtTrailingStop(position, stopPrice, priceOrder);

        }
    }
}
