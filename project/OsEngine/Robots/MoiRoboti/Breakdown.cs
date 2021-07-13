using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OsEngine.Robots.MoiRoboti
{
    public class Breakdown : BotPanel, INotifyPropertyChanged  // типа класс модель 
    {
        private MovingAverage _machka; // поле для сохранения машки
        private decimal _volum_ma; 
        public decimal Volum_ma // свойство переменной (значение) machki
        {
            get => _volum_ma;
            set => Set(ref _volum_ma, value);
        }
 
        private BotTabSimple _tab; // поле хранения вкладки робота 

        private decimal _price; // поле хранения цены
        public decimal Price   // свойство цены
        {
            get => _price;
            set => Set(ref _price, value);
        }

        public Breakdown(string name, StartProgram startProgram) : base(name, startProgram) // конструктор
        {
            TabCreate(BotTabType.Simple);  // создание простой вкладки
            _tab = TabsSimple[0]; // записываем первую вкладку в поле
            _tab.BestBidAskChangeEvent += _tab_BestBidAskChangeEvent;

            _machka = new MovingAverage("Macha", false);
            _machka.Lenght = 5;
            _machka = (MovingAverage)_tab.CreateCandleIndicator(_machka, "Prime");
            _machka.Save();
        }

        public void _tab_BestBidAskChangeEvent(decimal arg1, decimal arg2) // событие обновления лучшей цены
        {
            Price = _tab.MarketDepth.Bids[0].Price;
            Volum_ma = _machka.Values[_machka.Values.Count - 1];
        }

        public override string GetNameStrategyType()
        {
            return "Breakdown";
        }

        public override void ShowIndividualSettingsDialog()
        {
            Breakdown_Ui ui = new Breakdown_Ui(this);
            ui.Show();
        }

 // реализация интерфейса PropertyChanged-событий изменения свойств

        public event PropertyChangedEventHandler PropertyChanged; // событие изменения свойств
   
        protected void СallUpdate(string name)  // сигнализирует об изменении свойств
        {
               PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // сверяет значения любых типов данных и выдает сигнал об изменении 
        protected void Set<T>(ref T field, T value, [CallerMemberName] string name = "") 
        {
            if (!field.Equals(value))
            {
                field = value;
                СallUpdate(name);
            }
        }
    }
}
        
 