using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.Robots.MoiRoboti
{
    public class Storog : BotPanel
    {
        private BotTabSimple _tab; // поле хранения вкладки робота 
        public MovingAverage _ma;     // поле хранения индикатора МА
        private StrategyParameterDecimal slippage; // величина проскальзывание при установки ордеров  
        private StrategyParameterBool vkl_Robota; // поле включения бота 
        private StrategyParameterDecimal _uroven; // уровень цены начала покупки товара
        private StrategyParameterDecimal komis_birgi; // комиссия биржи в %
        //private StrategyParameterInt  part_depo;  // часть депозита для входа
        private StrategyParameterInt part_tovara; // часть товара для продажи
        private StrategyParameterInt profit;       // расстояние до профита
        private StrategyParameterInt dvig;       // Движение вверх, для сдвига счетчика
        private StrategyParameterDecimal min_lot;    //  минимальный объем для входа на бирже
        private StrategyParameterInt vel_ma; // какое значение индикатора махa использовать
        private StrategyParameterDecimal do_piram; // сколько пропустить да пирамиды
        private StrategyParameterInt ot_rinka; // расстояние от рынка (+комиссия)
        private StrategyParameterBool vkl_stopa; // поле включения стопа 
        private StrategyParameterDecimal do_stopa; // сколько пропустить да срабатывания стопа

        public decimal _vol_stop; // объем проданного товара по стопу 
        public decimal price; // текущая  цена центра стакана 
        public decimal _kom; // поле для хранения величины комиссии биржи в пунктах
        public decimal depo; // количество квотируемой в портфеле
        public decimal tovar; // количество товара  в портфеле
        public decimal volum_ma; // последние значение индикатора MA  
        public decimal price_position = 1; // хранение цены последней открытой позиции
        public decimal zakritie; 

        public Storog(string name, StartProgram startProgram) : base(name, startProgram) // конструктор робота тут  
        {
            TabCreate(BotTabType.Simple);  // создание простой вкладки
            _tab = TabsSimple[0]; // записываем первую вкладку в поле

            // инициализация переменных и параметров 
            price = 1;
            _kom = 0;
            zakritie = 0; //последняя позиция закрылась по цене 
            vkl_Robota = CreateParameter("РОБОТ Включен?", false);
            vkl_stopa = CreateParameter("Стоп Включен?", true);
            do_stopa = CreateParameter(" РАСТ. до СТОПА", 10m, 5m, 100m, 5m);
            _uroven = CreateParameter("УРОВЕНЬ Работы", 10000m, 100m, 1000m, 50m);
            slippage = CreateParameter("Велич. проскаль.у ордеров", 1m, 1m, 50m, 5m);
            part_tovara = CreateParameter("ИСПОЛЬЗ Товара Часть(1/?)", 2, 2, 50, 1);
            do_piram = CreateParameter(" РАСТ. до Пирамиды", 10m,5m,100m,5m );
            profit = CreateParameter("ПРОФИТ от рынка На ", 3, 5, 200, 5);
            dvig = CreateParameter("Движение верх забрать ", 130, 5, 200, 5);
            ot_rinka = CreateParameter(" Держаться от рынка", 110, 10,150,10);
            //part_depo = CreateParameter("ИСПОЛЬЗ Часть ДЕПО(1/?)", 10, 2, 50, 1);
            komis_birgi = CreateParameter("КОМ биржи в %", 0.2m, 0, 0.1m, 0.1m);
            min_lot = CreateParameter("МИН объ.орд у биржи(базовой)", 0.001m, 0.001m, 0.05m, 0.001m);
            vel_ma = CreateParameter("MA", 2, 3, 50, 1);  // записываем в переменную параметры 

            // создание и инициализация индикатора МА
            _ma = new MovingAverage(name + "Ma", true);
            _ma = (MovingAverage)_tab.CreateCandleIndicator(_ma, "Prime");
            _ma.Lenght = vel_ma.ValueInt; // присвоение значения 
            _ma.Save();

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent; // подписались на событие завершения новой свечи
            _tab.NewTickEvent += _tab_NewTickEvent;    // тики
            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent; // событие пришел стакан
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent; // событие успешного открытия позиции 
            _tab.PositionClosingSuccesEvent += _tab_PositionClosingSuccesEvent; // событие успешного закрытия позиции 
        }
        private void _tab_PositionClosingSuccesEvent(Position position) //закрылась позиция? перезаписали уровень срабатывания 
        {
            zakritie = _tab.PositionsLast.ClosePrice;
            if (zakritie < _uroven.ValueDecimal)
            {
                _uroven.ValueDecimal = zakritie - _kom;
                Console.WriteLine("По закрытию позиции уровень срабатывания переписали на  " + _uroven.ValueDecimal);
            }
 
        }
        private void _tab_PositionOpeningSuccesEvent(Position position) // позиция успешно открылась
        {

        }
        private void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth)    // начало  торгов 
        {
            List<Position> positions = _tab.PositionsOpenAll;
            if (positions.Count != 0)
            {
                if (price > _tab.PositionsLast.EntryPrice)
                {
                    StopLoss();
                }
            }
            decimal priceOrder = price + profit.ValueInt + slippage.ValueDecimal*_tab.Securiti.PriceStep;
            decimal priceActivation = price + profit.ValueInt;
            if (positions.Count != 0)
            {
                if (price < _tab.PositionsLast.EntryPrice - profit.ValueInt - _kom - slippage.ValueDecimal)
                {
                    _tab.CloseAtTrailingStop(positions[0], priceActivation, priceOrder);
                    Console.WriteLine(" Включился Трейлинг Профит по - " + priceActivation);
                }
            }
            if (vkl_Robota.ValueBool == false)
            {
                return;
            }
            Percent_birgi();
            Balans_tovara();

            if (price < _uroven.ValueDecimal)
            {
                decimal vol = tovar / part_tovara.ValueInt;
                if (_tab.PositionsOpenAll.Count == 0)
                {
                    if (vol > Lot()+ min_lot.ValueDecimal / 4)
                    {
  //объем входа- >>     
                        _tab.SellAtMarket(Okruglenie(vol - min_lot.ValueDecimal / 4));
                        Console.WriteLine(" начали продавать часть товара) по цене " + price + " На объем " + (vol - min_lot.ValueDecimal / 4) * price);
                        Thread.Sleep(1500);
                    }
                }
                if (positions.Count != 0)
                {
                    if (price < _tab.PositionsLast.EntryPrice - _kom - do_piram.ValueDecimal)
                    {
                        Piramid();
                    }
                }
            }
            if (_tab.PositionsOpenAll.Count == 0) // сдвиг уровня работы 
            {
                if (price > _uroven.ValueDecimal  ) // цена выше уровня работы + величина движения + комиссия 
                {
                    decimal r = price - _uroven.ValueDecimal; // вычисляем разницу (на сколько изменилась цена)
                    if (r>dvig.ValueInt)
                    {
                        _uroven.ValueDecimal = _uroven.ValueDecimal +(r - ot_rinka.ValueInt);
                        Console.WriteLine(" Позиций нет, уровень срабатывания поднимаем, теперь  " + _uroven.ValueDecimal);
                    }
                }
            }
        }
        private void _tab_NewTickEvent(Trade trade)
        {
            price = _tab.PriceCenterMarketDepth; // записываем текущую цену рынка
        }
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (candles.Count < _ma.Lenght)
            {
                return;
            }
            volum_ma = _ma.Values[_ma.Values.Count - 1]; // записывается значение индикатора MA 
        }
        void Piramid() // докупаем в позицию SELL при снижении рынка 
        {
            List<Position> positions = _tab.PositionsOpenAll;
            Percent_birgi();
            if (price < _tab.PositionsLast.EntryPrice - _kom - do_piram.ValueDecimal)
            {
                Console.WriteLine("Цена опустилась НА - " + (_tab.PositionsLast.EntryPrice - _kom - do_piram.ValueDecimal));
                if (positions.Count !=0)
                {
                    decimal vol = tovar / part_tovara.ValueInt;
                    if (vol > min_lot.ValueDecimal)
                    {
                        _tab.SellAtMarketToPosition(positions[0], Okruglenie(vol));
                        Console.WriteLine("Допродали  Битка  НА - " + vol * price + " $");
                        Thread.Sleep(1500);
                    }
                }
            }
        }
        void StopLoss() // фиксация  убытков 
        {
            if (vkl_stopa.ValueBool == false)
            {
                return;
            }
            List<Position> positions = _tab.PositionsOpenAll;
            if (positions.Count != 0) 
            {
                if (price > _tab.PositionsLast.EntryPrice + do_stopa.ValueDecimal) // когда рынок выше закупки позиции
                {
                    Console.WriteLine("Вошли в условие выставление стопа " + (_tab.PositionsLast.EntryPrice + do_stopa.ValueDecimal));
                    _tab.CloseAtStop(positions[0], price, price );
                    Console.WriteLine("Выставили СТОПлос по Активация " + price + " с ордером " + price);
                }
            }
        }
        decimal Lot() // расчет минимального лота 
        {
            price = _tab.PriceCenterMarketDepth;
            min_lot.ValueDecimal = Okruglenie(10.1m / price);
            return Okruglenie(10.1m / price);
        }
        decimal Balans_kvot()   // запрос квотируемых средств в портфеле (в USDT) 
        {
            List<PositionOnBoard> poses = _tab.Portfolio.GetPositionOnBoard();
            decimal vol_kvot = 0;
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == "USDT")
                {
                    vol_kvot = poses[i].ValueCurrent;
                    break;
                }
            }
            if (vol_kvot != 0)
            {
                depo = vol_kvot;
            }
            return depo;
        }
        decimal Balans_tovara()   // запрос торгуемых средств в портфеле (в BTC ) 
        {
            List<PositionOnBoard> poses = _tab.Portfolio.GetPositionOnBoard();
            decimal vol_instr = 0;
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == "BTC")
                {
                    vol_instr = poses[i].ValueCurrent;
                }
            }
            if (vol_instr != 0)
            {
                tovar = vol_instr;
            }
            return tovar;
        }
        decimal Percent_birgi() // вычисление % биржи в пунктах для учета в расчетах выставления ордеров 
        {
            decimal price = _tab.PriceCenterMarketDepth;
            return _kom = price / 100 * komis_birgi.ValueDecimal;
        }
        decimal Okruglenie(decimal vol) // округляет децимал до 6 чисел после запятой 
        {
            decimal value = vol;
            int N = 6;
            decimal chah = decimal.Round(value, N, MidpointRounding.ToEven);
            return chah;
        }
        public override string GetNameStrategyType()
        {
            return "Storog";
        }
        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}
