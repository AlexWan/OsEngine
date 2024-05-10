using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.Indicators;

namespace OsEngine.Robots.HomeWork
{
    [Bot("HomeWork4_IndicatorsTrend")]
    public class HomeWork4_IndicatorsTrend : BotPanel
    {
        private BotTabSimple _tab;

        private StrategyParameterString _regime;
        private StrategyParameterDecimal _volumePosition;
        private StrategyParameterDecimal _slippage;

        private StrategyParameterInt _smaLenght;
        private StrategyParameterInt _bollingerLenght;

        private Aindicator _sma;
        private Aindicator _bollinger;
        public HomeWork4_IndicatorsTrend(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage", 1000m, 0, 20, 1, "Base");
            _volumePosition = CreateParameter("Volume", 10m, 10, 1000, 10, "Base");

            _smaLenght = CreateParameter("Lenght Sma", 14, 10, 300, 10, "Indicators");
            _bollingerLenght = CreateParameter("Lenght Bollinger", 21, 10, 300, 10, "Indicators");

            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SMA", false);
            _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");
            ((IndicatorParameterInt)_sma.Parameters[0]).ValueInt = _smaLenght.ValueInt;
            _sma.Save();

            _bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _bollinger = (Aindicator)_tab.CreateCandleIndicator(_bollinger, "Prime");
            ((IndicatorParameterInt)_bollinger.Parameters[0]).ValueInt = _bollingerLenght.ValueInt;
            _bollinger.Save();

            ParametrsChangeByUser += HomeWork4_IndicatorsTrend_ParametrsChangeByUser;
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

        }

        private void HomeWork4_IndicatorsTrend_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_sma.Parameters[0]).ValueInt = _smaLenght.ValueInt;
            _sma.Save();
            _sma.Reload();

            ((IndicatorParameterInt)_bollinger.Parameters[0]).ValueInt = _bollingerLenght.ValueInt;
            _bollinger.Save();
            _bollinger.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "HomeWork4_IndicatorsTrend";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < _smaLenght.ValueInt || candles.Count < _bollingerLenght.ValueInt)
            {
                return;
            }

            List<Position> positions = _tab.PositionsOpenAll;

            
            if (positions.Count != null && positions.Count != 0)
            {
                LogicClosePosition(candles, positions);
            }

            if (positions.Count == null || positions.Count == 0)
            {
                LogicOpenPosition(candles);
            }
        }

        private void LogicClosePosition(List<Candle> candles, List<Position> positions)
        {
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal lastSma = _sma.DataSeries[0].Last;

            if (positions[0].Direction == Side.Buy)
            {
                if (lastPrice < lastSma)
                {
                    _tab.CloseAtLimit(positions[0], lastPrice + _slippage.ValueDecimal, positions[0].OpenVolume);
                }
            }
            else
            {
                if (lastPrice > lastSma)
                {
                    _tab.CloseAtLimit(positions[0], lastPrice - _slippage.ValueDecimal, positions[0].OpenVolume);
                }
            }            
        }

        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal lastCandle = candles[candles.Count - 1].Close;
            decimal bollingerValueUp = _bollinger.DataSeries[0].Last;
            decimal bollingerValueDown = _bollinger.DataSeries[1].Last;

            if (bollingerValueDown == 0 || bollingerValueUp == 0)
            {
                return;
            }

            if (lastCandle > bollingerValueUp)
            {
                _tab.BuyAtLimit(_volumePosition.ValueDecimal, _tab.PriceBestAsk + _slippage.ValueDecimal);
            }

            if (lastCandle < bollingerValueDown)
            {
                _tab.SellAtLimit(_volumePosition.ValueDecimal, _tab.PriceBestBid - _slippage.ValueDecimal);
            }
        }
    }
}
