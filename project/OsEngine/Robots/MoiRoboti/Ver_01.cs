using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.MoiRoboti
{
    public class Ver_01 : BotPanel
    {
        private BotTabSimple _vkl; // поле хранения вкладки робота 

 // сохраняемые настройки робота

        public bool Vkl = true; // поле включения бокса
        public decimal zn_stop_los;  // через сколько позиций усреднения фиксировать убыток
        public decimal OtProfit;   // через сколько от рынка выставлять тек профит 
        public decimal komis_birgi; // указывать комиссию биржи в процентах
        public int vel_machki; // какое значение индикатора махa использовать
        public decimal veli4_usrednen; // величина усреднения
        public int dola_depa;  // количество частей для входа 
        public decimal DeltaVerx ; // количество шагов вверх для выставления профита
        public decimal DeltaUsredn ; //на сколько ниже осуществлять усреднение 
        public int count_candels_hi; // сколько хаев свечей учитывать

 // глобальные переменные для логики

        public int old_vol_dv;  // значение доли входа при старте
        public decimal Depo; // текущий баланс портфеля базовой 
        public decimal tek_bal_potfela; // текущий баланс портфеля квотируемой 
        public decimal _vol_okrug; // используется для передачи округленного значения 
        public decimal _kom; // расчетная величина комиссии биржы в пунктах 
        public int _mnog; // для вычисления количества входов Piramida
        private MovingAverage _machka; // поле для сохранения машки
        public decimal volum_ma; // переменная с значением machki
        public decimal last_hi_candl; // хранить хай последней свечи
        public int count; // количество свечек 
        public decimal chah_price; // хранить шах цены

        public Ver_01(string name, StartProgram startProgram)  // это конструктор робота
            : base(name, startProgram)
        {
            Load(); // загрузка настроек 
            old_vol_dv = dola_depa; // стартовая инициализация частей входа берем из файла

            TabCreate(BotTabType.Simple);       // создание простой вкладки
            _vkl = TabsSimple[0];
            _vkl.MarketDepthUpdateEvent += _vkl_MarketDepthUpdateEvent;  // событие нового стакана
            _vkl.PositionNetVolumeChangeEvent += _vkl_PositionNetVolumeChangeEvent; // событие изменения обьема в позиции
            _vkl.CandleFinishedEvent += _vkl_CandleFinishedEvent;
            _machka = new MovingAverage("Macha", false);
            _machka.Lenght = vel_machki;
            _machka = (MovingAverage)_vkl.CreateCandleIndicator(_machka, "Prime");
            _machka.Save();
        }
        private void _vkl_CandleFinishedEvent(List<Candle> candles) //событие закрытия свечи
        {
            count = candles.Count;
            last_hi_candl = candles[candles.Count - 1].High;  // беру хай последней свечи
 // можно использовать как уровень срабатывания испраить на 3
            count_candels_hi = 3;
            for (int i = candles.Count-1; i > candles.Count - count_candels_hi; i--) 
            {
                if (last_hi_candl < candles[i].High) // проверяются хаи последних свечек 
                {
                    last_hi_candl = candles[i].High; // запоминаю наивысший 
                }
            }
        }
        private void _vkl_PositionNetVolumeChangeEvent(Position pos_volum) // обработка события изменения объема в позиции
        {
            Dola_depa();
            Rac4et_baz_bal();
            Kol_Trad();
            Percent_birgi();
            ZaprosBalahca();
        }
   // Методы входящие в логику робота
        void Save_prifit() // для выставления профита портфеля // добавить! в релиз + _kom
        {
            Percent_birgi(); // расчет величины в пунктах  процента биржи 
            List<Position> positions = _vkl.PositionsOpenAll;
  
            if (_vkl.MarketDepth.Bids[0].Price > _vkl.PositionsLast.EntryPrice + Step(DeltaVerx+OtProfit) + _kom ) // добавить! в релиз + _kom
            {
                _vkl.CloseAtTrailingStop(positions[0], _vkl.MarketDepth.Bids[0].Price + Step(OtProfit)+_kom, _vkl.MarketDepth.Bids[0].Price - Step(OtProfit));
            }
            Piramida();
        }
        void Usrednenie() // усреднение позиций при снижении рынка // добавь! в релиз + _kom
        {
            List<Position> positions = _vkl.PositionsOpenAll;

            Percent_birgi(); // расчет величины в пунктах  процента биржи 
            Kol_Trad();
            if (_vkl.PositionsLast.EntryPrice > _vkl.MarketDepth.Asks[0].Price + Step(DeltaUsredn)* _mnog + _kom * _mnog)// условия усреднения _kom +
            {
                if (Price_can_trade() > _vkl.MarketDepth.Asks[0].Price + Step(DeltaUsredn) * _mnog + _kom )
                {
                    if (volum_ma < _vkl.MarketDepth.Asks[0].Price)
                    {
                        ZaprosBalahca();
                        Rac4et_baz_bal();
                        if (tek_bal_potfela / dola_depa * veli4_usrednen > 10.1m)
                        {
                            _vkl.BuyAtMarketToPosition(positions[0], Okreglenie(Depo / dola_depa * veli4_usrednen));
                            Kol_Trad();
                            Mnog();
                            Percent_birgi();
                            _vkl_PositionNetVolumeChangeEvent(positions[0]);
                        }
                        Dola_depa();
                    }
                }
            }
        }
        void Piramida() // метод докупа позиции в безубытке добавь !!! _kom + 
        {/*
            Percent_birgi(); // расчет величины в пунктах  процента биржи 
            List<Position> positions = _vkl.PositionsOpenAll;
            if (positions[0].State != PositionStateType.Open)
            {
                return;
            }
            if (_vkl.MarketDepth.Bids[0].Price > _vkl.PositionsLast.EntryPrice + Step(DeltaVerx + OtProfit) + _kom * _mnog )
            {
                if (_vkl.PositionsOpenAll.Count != 0)
                {
                    ZaprosBalahca();
                    if (tek_bal_potfela > 10.2m)
                    {
                        if (tek_bal_potfela > 1000m)
                        {
                            decimal v = Depo / 4;
                            {
                                _vkl.BuyAtMarketToPosition(positions[0], v);
                                _vkl.CloseAtTrailingStop(positions[0], _vkl.MarketDepth.Bids[0].Price - Step(OtProfit / 2), _vkl.MarketDepth.Bids[0].Price - Step(OtProfit / 2));
                                Thread.Sleep(3000);
                                Mnog();
                                return;
                            }
                        }
                        if (tek_bal_potfela < 300m && tek_bal_potfela > 100m)
                        {
                            decimal v = Depo / 2;
                            {
                                _vkl.BuyAtMarketToPosition(positions[0], v);
                                _vkl.CloseAtTrailingStop(positions[0], _vkl.MarketDepth.Bids[0].Price - Step(OtProfit), _vkl.MarketDepth.Bids[0].Price - Step(OtProfit));
                                Mnog();
                                Thread.Sleep(3000);
                                return;
                            }
                        }
                        if (tek_bal_potfela < 100m)
                        {
                            decimal v = Depo;
                            {
                                _vkl.BuyAtMarketToPosition(positions[0], v);
                                _vkl.CloseAtTrailingStop(positions[0], _vkl.MarketDepth.Bids[0].Price - Step(OtProfit), _vkl.MarketDepth.Bids[0].Price - Step(OtProfit));
                                Mnog();
                                Thread.Sleep(3000);
                            }
                        }
                    }
                }
                 Save_profit();
            }*/
        }
        void StopLoss() // фиксация  убытков 
        {
            List<Position> positions = _vkl.PositionsOpenAll;
 
            if (_vkl.MarketDepth.Asks[0].Price + Step(_kom / 2)  < _vkl.PositionsLast.EntryPrice) //
            {
                Kol_Trad();
                int аznach =  Kol_Trad();
                if (аznach == zn_stop_los)
                {
                    _vkl.CloseAtStop(positions[0], _vkl.MarketDepth.Asks[0].Price, _vkl.MarketDepth.Asks[0].Price - Step(OtProfit + DeltaUsredn) - _kom/2);
                    Thread.Sleep(3000);
                    Vkl = false; // после выставления стоплоса выключаем робот 
                }
            }

        }
        private void _vkl_MarketDepthUpdateEvent(MarketDepth marketDepth) // начинается работа с этого метода 
        {
            if (count < _machka.Lenght)
            {
                return;
            }
            List<Position> positions = _vkl.PositionsOpenAll;
            volum_ma = _machka.Values[_machka.Values.Count - 1];
            Percent_birgi(); // расчет величины в пунктах  процента биржи 
            if (_vkl.PositionsOpenAll.Count != 0) //
            {
                if (_vkl.MarketDepth.Bids[0].Price > _vkl.PositionsLast.EntryPrice) // цена выше закупки профитимся
                {
                    Save_prifit();
                    Piramida();
                }
                if (_vkl.MarketDepth.Asks[0].Price < _vkl.PositionsLast.EntryPrice) //цена ниже закупки усредняемся 
                {
                    Usrednenie();
                    StopLoss();
                }
            }
            if (Vkl == false) // выключение работает как закрытие открытой позиции 
                               //с наименьшим убытком (проценты биржи) и не дает возможности открыть новую
            {
                if (_vkl.PositionsOpenAll.Count != 0) 
                {
                    
                    if (_vkl.MarketDepth.Bids[0].Price > _vkl.PositionsLast.EntryPrice)
                    {
                        if (_vkl.PositionsLast.ProfitPortfolioPunkt > 0.1m) 
                        {
                            _vkl.CloseAllAtMarket();
                        } 
                    }
                    Thread.Sleep(5000);
                } 
                return;
            }
            if (_vkl.PositionsOpenAll.Count == 0 ) // если  не в рынке, открываемся по рынку 
            {
                if (volum_ma < _vkl.MarketDepth.Asks[0].Price) // если цена выше уровня машки
                {
                    if (last_hi_candl < _vkl.MarketDepth.Asks[0].Price) // если цена выше последнего хая 
                    {
                        _mnog = 1; // начальная инициализация  для вычисления количества входов Piramida
                        dola_depa = old_vol_dv; //при отсутствие позиции присваивается значение взятое при старте  оса (из файла)
                        ZaprosBalahca();
                        Rac4et_baz_bal();
                        if (tek_bal_potfela / dola_depa > 10.1m)
                        {
                            _vkl.BuyAtMarket(Okreglenie(Depo / dola_depa));
                            Kol_Trad();
                            _vkl_PositionNetVolumeChangeEvent(positions[0]);
                        }
                    }
                 }
            }
        }
        decimal Percent_birgi() // вычисление % биржи для учитывания в расчетах выставления ордеров 
        {
            decimal price = _vkl.MarketDepth.Asks[0].Price;
            decimal percent = price / 100 * komis_birgi;
            return _kom = percent;
        }
        int Mnog() // используется для подсчета входов методом Piramida 
        {
            _mnog = _mnog+1; 
            return _mnog;
        }
        int Kol_Trad() // вычисляет количество трейдов в позиции
        {
            int b = _vkl.PositionsLast.MyTrades.Count;
            return b;
        }
        public decimal Price_can_trade()  // получает значение цены последних трейдов, если их нет возвращает цену рынка 
        {
            if (_vkl.PositionsLast.MyTrades.Count > 0 && _vkl.PositionsLast.MyTrades != null)
            {
                int asd = _vkl.PositionsLast.MyTrades.Count;
                return _vkl.PositionsLast.MyTrades[asd-1].Price;
            }
            else return _vkl.MarketDepth.Asks[0].Price;
        }
        decimal Dola_depa() // уменьшает долю входа на количество осуществленных трейдов  
        {
            int a = dola_depa - Kol_Trad();
            if (a >=1)
            {
                return dola_depa = a;
            }
            return 1;
        }
        decimal ZaprosBalahca()   // запрос квотируемых средств в портфеле (в USDT) 
        {   
            List<PositionOnBoard> poses = _vkl.Portfolio.GetPositionOnBoard();

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
        decimal Step(decimal suma)
        {
            if (suma > 0)
            {
                decimal pr = _vkl.PriceCenterMarketDepth;
                decimal c = _vkl.Securiti.PriceStep;
                decimal rez = pr  /100m * c;
                return rez;
            }
            return 0;
            
        }
        decimal Rac4et_baz_bal() // расчет базовой валюты в портфеле и запись его в поле Depo
        {
            decimal price = _vkl.MarketDepth.Asks[0].Price;
            ZaprosBalahca();
            decimal kvot= tek_bal_potfela;
            Depo = kvot / price;
            return Depo;
        }
        decimal Okreglenie(decimal vol) // округляет децимал до 6 чисел после запятой 
        {
            decimal value = vol;
            int N = 6;
            decimal chah = decimal.Round(value, N, MidpointRounding.ToEven);
            return _vol_okrug = chah;
        } 
  // сервисные методы 
        public override string GetNameStrategyType() // метод возвращающий имя робота
        {
            return "Ver_01";
        }
        public override void ShowIndividualSettingsDialog() // диалог с индивид настройками робота
        {
             Ver_01_Ui ui = new Ver_01_Ui(this);
             ui.Show();
        }
        public void Save() // сохранение настроек робота в файл
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    writer.WriteLine(Vkl);
                    writer.WriteLine (zn_stop_los);
                    writer.WriteLine(OtProfit);
                    writer.WriteLine(vel_machki);
                    writer.WriteLine(komis_birgi);
                    writer.WriteLine(veli4_usrednen);
                    writer.WriteLine(dola_depa);
                    writer.WriteLine(DeltaVerx);
                    writer.WriteLine(DeltaUsredn);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }
        private void Load() // выгрузка сохраненных настроек в робота 
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    Vkl = Convert.ToBoolean(reader.ReadLine());
                    zn_stop_los = Convert.ToDecimal(reader.ReadLine());
                    OtProfit = Convert.ToDecimal(reader.ReadLine());
                    vel_machki = Convert.ToInt32(reader.ReadLine());
                    komis_birgi = Convert.ToDecimal(reader.ReadLine());
                    veli4_usrednen = Convert.ToDecimal(reader.ReadLine());
                    dola_depa = Convert.ToInt32(reader.ReadLine());
                    DeltaVerx = Convert.ToDecimal(reader.ReadLine());
                    DeltaUsredn = Convert.ToDecimal(reader.ReadLine());

                    reader.Close();
                }

            }
            catch (Exception)
            {
                // ignore
            }
        }
    }
}
