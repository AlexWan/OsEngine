using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Charts.CandleChart.Indicators;
using System.Threading;

namespace OsEngine.Robots.MoiRoboti
{
    public class Depozit : BotPanel
    {
        // поля для хранения данных 
        public MovingAverage _ma;     // поле хранения индикатора МА
        private BotTabSimple _vklad; // поле хранения вкладки робота 
        private StrategyParameterDecimal slippage; // величина проскальзывание при установки ордеров  
        private StrategyParameterBool vkl_Robota; // поле включения бокса 
        private StrategyParameterInt zn_stop_los;    // через сколько позиций усреднения фиксировать убыток
        private StrategyParameterBool vkl_stop_los; // отключение функции стоплоса
        private StrategyParameterInt profit;       // расстояние до профита тейкпрофита
        private StrategyParameterBool vkl_profit; // отключение функции выставления профита
        private StrategyParameterDecimal komis_birgi; // указывать комиссию биржи в процентах
        private StrategyParameterDecimal veli4_usrednen; // величина усреднения
        private StrategyParameterBool vkl_usrednen;     // отключение функции усреднения
        private StrategyParameterInt dola_depa;        // количество частей для входа 
        private StrategyParameterDecimal deltaVerx;   // количество шагов вверх для выставления профита
        private StrategyParameterBool vkl_piramid;   // отключение функции пирамиды
        private StrategyParameterDecimal deltaUsredn;   //на сколько ниже осуществлять усреднение 
        private StrategyParameterInt count_candels_hi; // сколько хаев свечей учитывать
        private StrategyParameterInt stop;            // расстояние до стоплоса
        private StrategyParameterDecimal min_lot;    //  минимальный объем для входа на бирже
        private StrategyParameterInt vel_ma; // какое значение индикатора махa использовать
        private StrategyParameterInt n_min; // количество минут для метода подсчета объема
        private StrategyParameterBool vkl_vol_trade; // выключение подсчета объемов торгов
        private StrategyParameterInt volum_alarm;  // величина объема при достижении которого все выключается 
        private StrategyParameterInt volum_piramid; // величина объема при достижении которого включается пирамида

        // глобальные переменные используемые в логике
        public decimal last_hi_candl;    // значение хая последних свечей
        public decimal tek_bal_potfela; // текущий баланс портфеля квотируемой валюты
        public decimal Depo;      // текущий баланс портфеля базовой 
        public decimal volum_ma; // последние значение индикатора MA  
        public decimal _mnog;   // множитель 
        public decimal _kom; // для хранения величины комиссии биржи  
        public int vol_dv;  // изменяемое значение доли депо
        bool piramid_stop = true;  // для отключение усреднения по объему 
        bool alarm = true;
        public decimal price; // записываем текущую цену рынка

        public Depozit(string name, StartProgram startProgram) // конструктор робота
             : base(name, startProgram)
        {
            // инициализация переменных содержащих параметры стратегий 
            vkl_Robota = CreateParameter("РОБОТ Включен?", false);
            slippage = CreateParameter("Велич. проскаль.у ордеров", 1m, 1m, 50m, 5m);
            profit = CreateParameter("ТЭЙКПРОФИТ от рынка На ", 10, 5, 50, 5);
            vkl_profit = CreateParameter("ТЕЙКПРОФИТ включен ЛИ ?", true);
            veli4_usrednen = CreateParameter("ОБЪ.усред Уваел.НА ", 1m, 1m, 5m, 0.1m);
            vkl_usrednen = CreateParameter("УСРЕДНЕНИЕ включено ЛИ?", true);
            deltaUsredn = CreateParameter("УСРЕДнять через", 10m, 5m, 50m, 5m);
            dola_depa = CreateParameter("Часть депозита на вход", 10, 5, 100, 1);
            vkl_piramid = CreateParameter("ПИРАМИДА Включена ЛИ?", false);
            deltaVerx = CreateParameter("ПИРАМИДИТЬСЯ через ", 10m, 5m, 50m, 5m);
            vkl_stop_los = CreateParameter("Включен ЛИ СТОПЛОС ?", true);
            zn_stop_los = CreateParameter("СТОП после скок УСРЕД ", 10, 5, 50, 5);
            stop = CreateParameter("СТОПЛОС ниже на", 25, 5, 100, 5);
            vel_ma = CreateParameter("MA", 7, 3, 50, 1);  // записываем в переменную параметры 
            komis_birgi = CreateParameter("Биржа ЕСТ % учесть", 0.2m, 1m, 1m, 1m);
            min_lot = CreateParameter("МИН объ.орд у биржи(базовой)", 0.001m, 0.001m, 0.05m, 0.001m);
            vkl_vol_trade = CreateParameter("СЧИТАТЬ ЛИ объем торгов", true);
            n_min = CreateParameter("скок минут считать объем", 1, 1, 20, 1);
            count_candels_hi = CreateParameter("Скок.Хаев.св.читать(вход)", 2, 1, 50, 1);
            volum_alarm = CreateParameter("АВАРИЙНЫЙ ОБЪЕМ ПРОДАЖ", 450, 150, 1000, 50);
            volum_piramid = CreateParameter("Объем покуп.для ПИрамиды", 350, 150, 550, 50);


            TabCreate(BotTabType.Simple);       // создание простой вкладки
            _vklad = TabsSimple[0]; // записываем первую вкладку в поле

            // создание и инициализация индикатора МА
            _ma = new MovingAverage(name + "Ma", false);
            _ma = (MovingAverage)_vklad.CreateCandleIndicator(_ma, "Prime");
            _ma.Lenght = vel_ma.ValueInt; // присвоение значения 
            _ma.Save();

            _vklad.CandleFinishedEvent += _vklad_CandleFinishedEvent; // подписались на событие завершения новой свечи
            _vklad.NewTickEvent += _vklad_NewTickEvent;     // тики
            _vklad.MarketDepthUpdateEvent += _vklad_MarketDepthUpdateEvent; // событие пришел стакан
            _vklad.PositionOpeningSuccesEvent += _vklad_PositionOpeningSuccesEvent; // событие успешного закрытия позиции 
            _vklad.PositionNetVolumeChangeEvent += _vklad_PositionNetVolumeChangeEvent; // изменился объем в позиции
            
        }
        private void _vklad_MarketDepthUpdateEvent(MarketDepth marketDepth) // логика работы запускается тут 
        {
            if (_vklad.PositionsOpenAll.Count != 0) //
            {
                if (_vklad.MarketDepth.Bids[0].Price > _vklad.PositionsLast.EntryPrice) // цена выше закупки профитимся
                {
                    Save_prifit(); // забрать профит
                }
                if (vkl_Robota.ValueBool == false)
                {
                    StopLoss(); // фиксировать убыток
                }
            }
            if (vkl_Robota.ValueBool == false) // выключаем работу робота
            {
                return;
            }
            if (_vklad.PositionsOpenAll.Count != 0)
            {
                if (_vklad.MarketDepth.Asks[0].Price < _vklad.PositionsLast.EntryPrice) //цена ниже закупки усредняемся 
                {
                    Usrednenie();
                    StopLoss(); // фиксировать убыток
                }
            }
            if (volum_ma > 0)
            {
                if (_vklad.PositionsOpenAll.Count == 0) // если  не в рынке, открываемся по рынку 
                {
                    if (volum_ma < _vklad.MarketDepth.Asks[0].Price) // если цена выше уровня машки
                    {
                        if (last_hi_candl < _vklad.MarketDepth.Asks[0].Price) // если цена выше последнего хая 
                        {
                            Lot();
                            ZaprosBalahca();
                            Rac4et_baz_bal();
                            if (tek_bal_potfela / dola_depa.ValueInt > min_lot.ValueDecimal*price)
                            {
                                _vklad.BuyAtMarket(Okreglenie(Depo / dola_depa.ValueInt));
                                Console.WriteLine("Открылись по цене" + _vklad.PriceBestAsk + " НА - " + (Depo / dola_depa.ValueInt) * _vklad.PriceBestAsk + " $");
                            }
                        }
                    }
                }
            }
        }
        private void _vklad_PositionNetVolumeChangeEvent(Position position) // изменился объем в позиции
        {
            Price_kon_trade();
            Kol_Trad();
            Lot();
        }
        private void _vklad_PositionOpeningSuccesEvent(Position position) // успешное закрытие позиции
        {
            _mnog = 1;
            piramid_stop = true;
            alarm = true;
            Lot();
        }

        private DateTime dateTrade; // время трейда
        decimal bid_vol_tr;  // объем покупок
        decimal ask_vol_tr; // объем продаж
        decimal all_volum_trade_min; //все объемы за N минуту
        
        private void _vklad_NewTickEvent(Trade trade) // событие новых тиков для счета объема торгов
        {
            price = _vklad.PriceCenterMarketDepth; // записываем текущую цену рынка
            if (vkl_vol_trade.ValueBool == false)
            {
                return;
            }
            List<Position> positions = _vklad.PositionsOpenAll;
            DateTime time_add_n_min;
            time_add_n_min = dateTrade.AddMinutes(n_min.ValueInt);
            if (trade.Time < time_add_n_min)
            {
                if (trade.Side == Side.Buy)
                {
                    decimal b = trade.Volume;
                    bid_vol_tr = bid_vol_tr + b;
                }
                if (trade.Side == Side.Sell)
                {
                    decimal a = trade.Volume;
                    ask_vol_tr = ask_vol_tr + a;
                }
                all_volum_trade_min = bid_vol_tr + ask_vol_tr;
            }
            else
            {
                dateTrade = trade.Time;
                all_volum_trade_min = 0;
                bid_vol_tr = 0;
                ask_vol_tr = 0;
            }
            if ( bid_vol_tr >  volum_piramid.ValueInt)
            {
                Price_kon_trade();
                if (volum_ma > Price_kon_trade() && positions.Count > 0)
                {
                    if (piramid_stop == false)
                    {
                        return;
                    }
                    _vklad.BuyAtMarketToPosition(positions[0], Okreglenie(VolumForUsred()));
                    Kol_Trad();
                    Mnog();
                    Console.WriteLine("Докупились при объеме покупок больше "+ volum_piramid.ValueInt +" по- " + Price_kon_trade()+ "НА " + VolumForUsred()*_vklad.PriceBestAsk);
                    piramid_stop = false;
                    bid_vol_tr = 0;
                }
            }
 
            if (ask_vol_tr > volum_alarm.ValueInt && positions.Count > 0) // условие для аварийного выключения
            {
                if (alarm == false )
                {
                    return;
                }
                slippage.ValueDecimal = slippage.ValueDecimal + 1m;
                _vklad.CloseAtStop(positions[0], _vklad.MarketDepth.Asks[0].Price, _vklad.MarketDepth.Asks[0].Price - slippage.ValueDecimal);
                vkl_Robota.ValueBool = false; // после выставления стопа выключаем робот 
                Console.WriteLine(" Аварийное выключение!!! ОБЪЕМЫ продаж больше "+ volum_alarm.ValueInt+ "  РОБОТ ВЫКЛЮЧЕН по цене - " + Price_kon_trade());
                slippage.ValueDecimal = slippage.ValueDecimal - 1m;
                Console.WriteLine("Вернули проскальзыванию начальное значение - " + slippage.ValueDecimal);
                if (positions.Count == 0)
                {
                    alarm = false;
                    bid_vol_tr = 0;
                }
            }
        }
        private void _vklad_CandleFinishedEvent(List<Candle> candles) // тут присваивается значения индикатору МА и величине хая последних свечей
        {
            if (candles.Count < _ma.Lenght)
            {
                return;
            }
            volum_ma = _ma.Values[_ma.Values.Count - 1]; // записывается значение индикатора MA 
            // смотрим хаи 
            last_hi_candl = candles[candles.Count - 1].High;  // беру хай последней свечи
            int a = count_candels_hi.ValueInt;              // количество учитываемых свечей 
            for (int i = candles.Count - 1; i > candles.Count - a; i--)
            {
                if (last_hi_candl < candles[i].High) // проверяются хаи последних свечек 
                {
                    last_hi_candl = candles[i].High; // запоминаю наивысший 
                }
            }
        }
        void Save_prifit() // для выставления профита портфеля 
        {
            if (vkl_profit.ValueBool == false)
            {
                return;
            }
            Percent_birgi(); // расчет величины в пунктах  процента биржи 
            List<Position> positions = _vklad.PositionsOpenAll;

            if (_vklad.MarketDepth.Bids[0].Price > _vklad.PositionsLast.EntryPrice + profit.ValueInt + _kom + slippage.ValueDecimal * _vklad.Securiti.PriceStep)
            {
                _vklad.CloseAtTrailingStop(positions[0], _vklad.MarketDepth.Bids[0].Price - profit.ValueInt, _vklad.MarketDepth.Asks[0].Price - profit.ValueInt - slippage.ValueDecimal * _vklad.Securiti.PriceStep);
            }
        }
        void Usrednenie() // усреднение позиций при снижении рынка 
        {
            if (vkl_usrednen.ValueBool == false)
            {
                return;
            }
            List<Position> positions = _vklad.PositionsOpenAll;
            Percent_birgi();
            Kol_Trad();
            if (Price_kon_trade() > _vklad.MarketDepth.Asks[0].Price + (deltaUsredn.ValueDecimal + _kom) * _mnog)
            {
                if (volum_ma < _vklad.MarketDepth.Asks[0].Price)
                {
                    Price_kon_trade();
                    ZaprosBalahca();
                    Rac4et_baz_bal();
                    if (VolumForUsred() > min_lot.ValueDecimal)
                    {
                        _vklad.BuyAtMarketToPosition(positions[0], Okreglenie(VolumForUsred()));
                        Kol_Trad();
                        Mnog();
                        Price_kon_trade();
                        Console.WriteLine("Усреднились НА - " + VolumForUsred()*_vklad.PriceBestAsk+" $");
                        Thread.Sleep(1500);
                    }
                }
            }
        }
        void StopLoss() // фиксация  убытков 
        {
            if (vkl_stop_los.ValueBool == false) // если в настройках выбрано фальшь метод не работает
            {
                return;
            }
            List<Position> positions = _vklad.PositionsOpenAll;
            if (_vklad.MarketDepth.Asks[0].Price + stop.ValueInt < _vklad.PositionsLast.EntryPrice) //
            {
                Kol_Trad();
                int znach = Kol_Trad();
                if (znach == zn_stop_los.ValueInt)
                {
                    _vklad.CloseAtStop(positions[0], _vklad.MarketDepth.Asks[0].Price, _vklad.MarketDepth.Asks[0].Price - slippage.ValueDecimal* _vklad.Securiti.PriceStep);
                    Thread.Sleep(3000);
                    vkl_Robota.ValueBool = false; // после выставления стопа выключаем робот 
                }
            }
            if (_vklad.MarketDepth.Asks[0].Price < _vklad.PositionsLast.EntryPrice) // когда рынок ниже закупки позиции
            {
                if (vkl_Robota.ValueBool == false && positions.Count >0) // когда робот вЫключен и осталась открытая позиция выставляется стоп
                {
                    _vklad.CloseAtStop(positions[0], _vklad.MarketDepth.Asks[0].Price , _vklad.MarketDepth.Asks[0].Price - slippage.ValueDecimal*_vklad.Securiti.PriceStep);
                }
            }
        }
        decimal VolumForUsred()
        {
            decimal vol = 0;
            vol = _vklad.PositionsLast.MaxVolume;
            if (Depo < vol* veli4_usrednen.ValueDecimal)
            {
                return Depo;
            }
            return vol * veli4_usrednen.ValueDecimal;
        }
        decimal Okreglenie(decimal vol) // округляет децимал до 6 чисел после запятой 
        {
            decimal value = vol;
            int N = 6;
            decimal chah = decimal.Round(value, N, MidpointRounding.ToEven);
            return chah;
        }
        public decimal Price_kon_trade()  // получает значение цены последних трейдов, если их нет возвращает цену рынка 
        {
 // ругается тут на отсутствие объекта 
            if (_vklad.PositionsLast.MyTrades.Count != 0) 
            {
                int asd = _vklad.PositionsLast.MyTrades.Count;
                return _vklad.PositionsLast.MyTrades[asd - 1].Price;
            }
            else return _vklad.MarketDepth.Asks[0].Price;
        }
        int Kol_Trad() // вычисляет количество трейдов в позиции
        {
            int b = _vklad.PositionsLast.MyTrades.Count;
            return b;
        }
        decimal Mnog() // используется для подсчета входов методом Piramida 
        {
            _mnog = _mnog + 1;
            return _mnog;
        }
        public void Сount_volum()  // счетчик объема торгов по тикам 
        {
        }
        decimal ZaprosBalahca()   // запрос квотируемых средств в портфеле (в USDT) 
        {
            List<PositionOnBoard> poses = _vklad.Portfolio.GetPositionOnBoard();
            decimal vol_usdt = 0;
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == "USDT")
                {
                    vol_usdt = poses[i].ValueCurrent;
                    break;
                }
            }
            if (vol_usdt != 0)
            {
                tek_bal_potfela = vol_usdt;
            }
            return tek_bal_potfela;
        }
        decimal Lot() // расчет минимального лота 
        {
            price = _vklad.PriceCenterMarketDepth;
            min_lot.ValueDecimal = Okreglenie( 10.1m / price);
            Console.WriteLine(" Минимальный лот = " + min_lot.ValueDecimal);
            return Okreglenie( 10.1m / price);
        }
        decimal Rac4et_baz_bal() // расчет базовой валюты в портфеле и запись его в поле Depo
        {
            decimal price = _vklad.MarketDepth.Asks[0].Price;
            ZaprosBalahca();
            decimal kvot = tek_bal_potfela;
            Depo = kvot / price;
            return Depo;
        }
        decimal Percent_birgi() // вычисление % биржи в пунктах для учета в расчетах выставления ордеров 
        {
            _kom = 0;
            decimal price = _vklad.MarketDepth.Asks[0].Price;
            return _kom = price / 100 * komis_birgi.ValueDecimal;
        }
        public override string GetNameStrategyType()
        {
            return "Depozit";
        }
        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}
