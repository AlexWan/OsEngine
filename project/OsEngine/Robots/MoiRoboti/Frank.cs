using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Charts.CandleChart.Indicators;
using System.Threading;
using Microsoft.SqlServer.Server;

namespace OsEngine.Robots.MoiRoboti
{
    public class Frank : BotPanel
    {
        public decimal market_price; // рыночная стоимость товара
        public string kvot_name ; // название квотируемой валюты - инструмента
        public string tovar_name; // название базовая валюты - товара
        private MyBlanks blanks = new MyBlanks("Frank", StartProgram.IsOsTrader); // создание экземпляра класса MyBlanks
        private BotTabSimple _tab; // поле хранения вкладки робота 
        public decimal percent_tovara; // поле хранения % товара 
        public decimal portfolio_sum; // поле хранения суммы в портфеле 
        public decimal start_sum; // поле хранения суммы на старт работы 
        public decimal start_price; // поле хранения цены товара на старт работы 
        public decimal depo; // количество квотируемой в портфеле
        public decimal tovar; // количество товара  в портфеле
        public decimal min_lot; // поле хранящее величину минимального лота для биржи
        bool start_metod_vkl; // поле переключения состояния метода старт 
        public decimal _kom; // поле хранения значения комиссия биржи 

        private StrategyParameterBool vkl_Robota; // поле включения бота 
        private StrategyParameterDecimal veli4_usrednen; // величина усреднения
        private StrategyParameterDecimal deltaUsredn;   //на сколько ниже осуществлять усреднение
        private StrategyParameterInt start_per_depo; // какую часть депозита использовать при старте робота в % 
        private StrategyParameterDecimal min_sum;    //  минимальная сумма возможного ордера на бирже
        private StrategyParameterDecimal do_piram; // сколько пропустить да пирамиды
        private StrategyParameterDecimal slippage; // величина проскальзывание при установки ордеров 
        private StrategyParameterInt profit;       // расстояние до профита тейкпрофита

        public Frank(string name, StartProgram startProgram) : base(name, startProgram) // конструктор
        {
            TabCreate(BotTabType.Simple);  // создание простой вкладки
            _tab = TabsSimple[0]; // записываем первую вкладку в поле
            kvot_name = "USDT";  // тут надо указать - инициализировать название квотируемой валюты (деньги)
            tovar_name = "BTC"; // тут надо указать - инициализировать название товара
            start_metod_vkl = true; // инициализация знач. метода старт

            vkl_Robota = CreateParameter("РОБОТ Включен?", false);
            slippage = CreateParameter("Велич. проскаль.у ордеров", 5m, 1m, 200m, 5m);
            profit = CreateParameter("ТЭЙКПРОФИТ от рынка На ", 15, 5, 50, 5);
            veli4_usrednen = CreateParameter("Усред.уваелич в раз ", 0.1m, 0.1m, 0.5m, 0.1m);
            do_piram = CreateParameter(" РАСТ. до Пирамиды", 20m, 5m, 200m, 5m);
            deltaUsredn = CreateParameter("УСРЕДнять через", 20m, 5m, 50m, 5m);
            start_per_depo = CreateParameter("Начинать с ? % депо)", 5, 5, 20, 5);
            min_sum = CreateParameter("МИН сумма орд.на бирже($)", 10.1m, 10.1m, 10.1m, 10.1m);

            _tab.BestBidAskChangeEvent += _tab_BestBidAskChangeEvent; // событие изменения лучших цен
            _tab.OrderUpdateEvent += _tab_OrderUpdateEvent; // событие обновления ордеров 
            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;

        }
        private void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth) // событие стакана
        {
            market_price = _tab.PriceCenterMarketDepth;
        }
        private void _tab_OrderUpdateEvent(Order order) // событие обновления ордера 
        {
            Switching_mode();
            Console.WriteLine(" Событие обновления ордера!");
        }
        private void _tab_BestBidAskChangeEvent(decimal bid, decimal ask) // событие изменения лучших цен
        {
            
            if (vkl_Robota.ValueBool == false )
            {
                return;
            }
            Start();
  
            if (start_price +_kom <= market_price)
            {
                Save_profit();
                Piramid();
            }
            else if (start_price - _kom > market_price)
            {
                Usrednenie();
            }
        }
        void Save_profit() // для выставления профита портфеля 
        {
            blanks.Percent_birgi(); // расчет величины в пунктах  процента биржи 
            List<Position> positions = _tab.PositionsOpenAll;
            if (positions.Count != 0)
            {
                decimal zen = _tab.PositionsLast.EntryPrice;
                if (market_price > zen + profit.ValueInt + _kom + slippage.ValueDecimal * _tab.Securiti.PriceStep)
                {
                    _tab.CloseAtTrailingStop(positions[0], _tab.PriceCenterMarketDepth - profit.ValueInt,
                        _tab.PriceCenterMarketDepth - profit.ValueInt - slippage.ValueDecimal * _tab.Securiti.PriceStep);

                    Console.WriteLine(" Включился трейлинг Прибыли по цене "
                        + (_tab.PriceCenterMarketDepth - profit.ValueInt - slippage.ValueDecimal * _tab.Securiti.PriceStep));
                }
            }
        }
        void Start() // метод с которого робот начинает работу  
        {
            if (start_metod_vkl == false) // для выключения метода  
            {
                return;
            }
            if (vkl_Robota.ValueBool == false)
            {
                return;
            }
            Percent_tovara();
            int a = start_per_depo.ValueInt;
            if ( a < percent_tovara )
            {
                return;
            }
            List<Position> positions = _tab.PositionsOpenAll;
            if (positions.Count != 0)
            {
                return;
            }
            start_sum = Portfolio_sum();
            Console.WriteLine(" записали сумму портфеля "+ start_sum);
            start_price = market_price;
            Console.WriteLine(" записали стартовую цену  " + start_price);

            decimal vol = MyBlanks.Okruglenie(blanks.Balans_kvot() / 100 * a, 2);
            if (vol > min_sum.ValueDecimal)
            {
                market_price = _tab.PriceCenterMarketDepth;
                vol = vol / market_price;
                vol = MyBlanks.Okruglenie(vol,5);
                _tab.BuyAtLimit(vol, market_price);
                Console.WriteLine(" Стартуем ордером на = " + MyBlanks.Okruglenie(vol * market_price, 5)  + " $ по цене " + market_price);
            }
            if (positions.Count != 0)
            {
                //start_metod_vkl = false;
                //Console.WriteLine(" выключили метод старт");
            } 
        } 
        void Switching_mode() // метод переключения режимов работы 
        {
            Percent_tovara();
            if (90 <= percent_tovara) //  режим выхода 
            {

            }
            if ( 70 >= percent_tovara && percent_tovara < 90 ) //  режим фиксации прибыли
            {

            }
            if (50 <= percent_tovara && percent_tovara < 70) // режим разворота и ожидания прибыли
            {

            }
            if (30 <= percent_tovara && percent_tovara < 50) // режим набора товара и ожидания прибыли
            {

            }
            if (start_per_depo.ValueInt <= percent_tovara && percent_tovara < 30) // режим набора товара
            {

            }
            if (1 >= percent_tovara) // режим старт 
            {

            }
        }
        void Usrednenie() // усреднение позиций при снижении рынка 
        {
            List<Position> positions = _tab.PositionsOpenAll;
            if (Price_kon_trade() > market_price + deltaUsredn.ValueDecimal + blanks.Percent_birgi() )
            {
                min_lot = blanks.Lot(min_sum.ValueDecimal);
                Balans_kvot(kvot_name);
                Balans_tovara(tovar_name);
                if (VolumForUsred() > min_lot)
                {
                    _tab.BuyAtMarketToPosition(positions[0], MyBlanks.Okruglenie(VolumForUsred(), 2));

                    Console.WriteLine("Усреднились НА - " + VolumForUsred() * _tab.PriceBestAsk + " $");
                    // Thread.Sleep(1500);
                }
            }
        }
        void Piramid() // докуп в позицию 
        {
            List<Position> positions = _tab.PositionsOpenAll;
            blanks.Percent_birgi();
            if (positions.Count != 0) 
            {
                decimal zen = _tab.PositionsLast.EntryPrice;
                if (market_price > zen + _kom + do_piram.ValueDecimal)
                {
                    decimal vol = VolumForPiramid();
                    if (vol > min_lot)
                    {
                        _tab.BuyAtMarketToPosition(positions[0], MyBlanks.Okruglenie(vol,8));
                        Console.WriteLine(" Пирамида- докупили НА - " + MyBlanks.Okruglenie(vol, 8) * market_price + " $");
                        //Thread.Sleep(1500);
                    }
                }
            }
        }
        decimal Price_kon_trade()  // получает значение цены последнего трейда, если его нет возвращает цену рынка 
        {
            List<Position> positions = _tab.PositionsOpenAll;
            if (positions.Count ==0)
            {
                return _tab.PriceCenterMarketDepth;
            }
            if (positions.Count != 0)
            {
                if (_tab.PositionsLast.MyTrades.Count != 0)
                {
                    int asd = _tab.PositionsLast.MyTrades.Count;
                    return _tab.PositionsLast.MyTrades[asd - 1].Price;
                }
            }
            return _tab.PriceCenterMarketDepth; 
        }
        decimal Balans_kvot(string kvot_name)   // запрос квотируемых средств в портфеле - название присваивается в kvot_name
        {
            List<PositionOnBoard> poses = _tab.Portfolio.GetPositionOnBoard();
            decimal vol_kvot = 0;
            decimal vol_kvot_blok = 0;
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == kvot_name)
                {
                    vol_kvot = poses[i].ValueCurrent;
                    vol_kvot_blok = poses[i].ValueBlocked;
                    break;
                }
            }
            if (vol_kvot != 0)
            {
                depo = vol_kvot + vol_kvot_blok;
            }
            Console.WriteLine(" Депо сейчас  " + depo + " $");
            return depo;
        }
        decimal Balans_tovara(string tovar_name)   // запрос торгуемых средств в портфеле (в BTC ) название присваивается в tovar_name
        {
            List<PositionOnBoard> poses = _tab.Portfolio.GetPositionOnBoard();
            decimal vol_instr = 0;
            decimal vol_instr_blok = 0;
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == tovar_name)
                {
                    vol_instr = poses[i].ValueCurrent;  // текущий объем 
                    vol_instr_blok = poses[i].ValueBlocked; // блокированный объем 
                }
            }
            if (vol_instr != 0)
            {
                tovar = vol_instr + vol_instr_blok;
            }
            Console.WriteLine(" Баланс товара =  " + tovar + " BTC На  " + tovar * market_price + "$");
            return tovar;
        }
        decimal Portfolio_sum() // расчет начального состояния портфеля по торгуемой паре
        {
            Balans_kvot(kvot_name);
            Balans_tovara(tovar_name);
            portfolio_sum = MyBlanks.Okruglenie(depo + tovar * market_price, 2);
            return portfolio_sum;
        }
        decimal Percent_tovara() // расчет % объема купленного товара в портфеле 
        {
            decimal st = Portfolio_sum();
            decimal kv = Balans_kvot(kvot_name);
            decimal rasxod = st - kv;
            decimal per = rasxod / st * 100;
            percent_tovara = MyBlanks.Okruglenie(per, 2);
            Console.WriteLine(" процент товара сейчас "+ percent_tovara);
            return percent_tovara;
        }
        decimal VolumForUsred() // рассчитывает объем для усреднения покупок 
        {
            Balans_tovara(tovar_name);
            decimal uge = _tab.PositionsLast.MaxVolume; // максимальный объем в позиции
            decimal dob = uge * veli4_usrednen.ValueDecimal; // добавляем объема 
            decimal vol = uge + dob;
            if (depo < vol)
            {
                return depo;
            }
            else return vol;
        }
        decimal VolumForPiramid() // рассчитывает объем для пирамиды 
        {
            Percent_tovara();
            if (30 <= percent_tovara && percent_tovara < 70) // режим набора товара и ожидания прибыли
            {
                Balans_kvot(kvot_name);
                decimal vol = depo / 100 * start_per_depo.ValueInt;
                return vol / market_price;
            }
            if (start_per_depo.ValueInt <= percent_tovara && percent_tovara < 30) // режим набора товара
            {
                decimal uge = _tab.PositionsLast.MaxVolume; // максимальный объем в позиции
                decimal vol = uge / 2;
                if (vol <= min_lot)
                {
                    return min_lot;
                }
                return vol;
            }
            return min_lot ;
        }
        public override string GetNameStrategyType()
        {
            return "Frank";
        }
        public override void ShowIndividualSettingsDialog()
        {
            
        }
    }
}
