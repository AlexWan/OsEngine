using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.Collections.Generic;

namespace OsEngine.Robots.MyBots.VolumeReversal
{

    [Bot("VolumeReversal")]

    internal class VolumeReversal : BotPanel

    {

        public VolumeReversal(string name, StartProgram startProgram) : base(name, startProgram)
        {
            this.TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Mode  = this.CreateParameter("Mode", "Edit", new[] { "Edit", "Trade" });

            _risk = CreateParameter("_risk %", 1m, 0.1m, 10m, 0.1m);
            _profitKoef = CreateParameter("_profitKoef", 3m, 0.1m, 10m, 0.1m);
            _countDownCandles = CreateParameter("_countDownCandles", 1, 1, 5, 1);
            _koefVolume = CreateParameter("_koefVolume", 2m, 2m, 10m, 0.5m);
            _countCandlesAveregeVolume = CreateParameter("_countCandlesAveregeVolume", 10, 5, 50, 1); 
            // _averegeVolume = CreateParameter("_averegeVolume", 1m, 1m, 10m, 1m);

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;

        }



        #region Fields ==============================

        private BotTabSimple _tab;

        private StrategyParameterString Mode;

        /// <summary>
        /// риск на сделку
        /// </summary>
        private StrategyParameterDecimal _risk;

        /// <summary>
        /// во сколько раз тейк больше риска
        /// </summary>
        private StrategyParameterDecimal _profitKoef;

        /// <summary>
        /// кол-во падающих свечей перед объемным разворотом
        /// </summary>
        private StrategyParameterInt _countDownCandles;

        /// <summary>
        /// во сколько раз объем превышает средний
        /// </summary>
        private StrategyParameterDecimal _koefVolume;

        /// <summary>
        /// средний объем за сколько свечей
        /// </summary>
        private decimal _averegeVolume;

        /// <summary>
        /// кол-во свечей для вычисления среднего объема
        /// </summary>
        private StrategyParameterInt _countCandlesAveregeVolume;

        /// <summary>
        ///  количество пунктов до стоплос
        /// </summary>
        private decimal _punkts = 0;

        private decimal _lowCandle = 0;


        #endregion



        #region Methods ==============================

        //  свеча закрылась и все начинаем считать
        private void _tab_CandleFinishedEvent(System.Collections.Generic.List<Candle> candles)
        {

            if (candles.Count < _countDownCandles.ValueInt + 1
                || candles.Count < _countCandlesAveregeVolume.ValueInt+1)
            {
                return;
            }

            // считаем средний объем. с предпоследней свечи на кол-во свечей из настроек. сумма всех их объемов
            _averegeVolume = 0;
            for (int i = candles.Count - 2; i > candles.Count - _countCandlesAveregeVolume.ValueInt -2; i--)
            {
                _averegeVolume += candles[i].Volume;
            }
            _averegeVolume /= _countCandlesAveregeVolume.ValueInt;


            List<Position> positions = _tab.PositionOpenLong;
            if (positions.Count > 0)
            {
                return ;
            }

            // проверки условий
            Candle candle = candles[candles.Count - 1];
            if (candle.Close < ((candle.High + candle.Low) / 2) // если закрытие меньше середины свечи
                || candle.Volume < _averegeVolume * _koefVolume.ValueDecimal) // или объем свечи меньше чем средний объем
            {
                return ; // уходим
            }

            // перебираем количество падающих свечей 
            for (int i = candles.Count - 2; i > (candles.Count - 2 - _countDownCandles.ValueInt); i--)
            {
                if (candles[i].Close > candles[i].Open) // если свеча растет
                {
                    return; // уходим
                }

            }



            // расстояние между закрытием и минимум свечи
            _punkts = (candle.Close - candle.Low) / _tab.Securiti.PriceStep;
            if (_punkts < 5)
            {
                return;
            }
            // риск в деньгах для одного лота
            decimal amountStop = _punkts * _tab.Securiti.PriceStepCost;

            // риск в деньгах 
            decimal amountRisk = _tab.Portfolio.ValueBegin * _risk.ValueDecimal / 100;

            // расчет объема на вход
            decimal volume = amountRisk / amountStop;

            // гарантийное обеспечение
            decimal go = 5000;
            // если го > 1 то мы не в тестере (в тестере го = 1)
            if (_tab.Securiti.Go > 1)
            {
                go = _tab.Securiti.Go;
            }

            // расчет макс загрузки депо  лотов из расчета на депозита
            decimal maxLot = _tab.Portfolio.ValueBegin / go;

            // если объем лотов не превышен то открываем сделку
            if (volume < maxLot)
            {
                _lowCandle = candle.Low;

                // вход по рынку
                _tab.BuyAtMarket(volume);
            }


        }

        // сделка произошла и ставим тейк и стоп
        private void _tab_PositionOpeningSuccesEvent(Position pos)
        {

            decimal priceTake = pos.EntryPrice + _punkts * _profitKoef.ValueDecimal;

            // ставим тейк
            _tab.CloseAtProfit(pos, priceTake, priceTake);
            // ставим стоп на минимум свечи расчета и в запас 100 пунктов
            _tab.CloseAtStop(pos, _lowCandle, _lowCandle - 100 * _tab.Securiti.PriceStep);

        }


        public override string GetNameStrategyType()
        {
            return nameof(VolumeReversal);
        }

        public override void ShowIndividualSettingsDialog()
        {
            //WindowVolumeReversal window = new WindowVolumeReversal();

            //window.LotTextBlock.Text = "Lot = " + Lot.ValueInt;
            //window.StopTextBlock.Text = "Stop = " + Stop.ValueInt;
            //window.TakeTextBlock.Text = "Take = " + Take.ValueInt;

            //window.ShowDialog();

        }
        #endregion

    }
}
