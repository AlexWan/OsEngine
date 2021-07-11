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

namespace OsEngine.Robots.MoiRoboti
{
    public class Breakdown : BotPanel, INotifyPropertyChanged  // типа класс модель 
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private BotTabSimple _tab; // поле хранения вкладки робота 

        private decimal _price =0; // поле хранения цены

        public decimal Price 
        {   
            get { return _price; }
            set
            {
                _price = value;
                СallUpdate(nameof(Price));
            }
        }

        protected  void СallUpdate(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Price)));
        }


        public Breakdown(string name, StartProgram startProgram) : base(name, startProgram) // конструктор
        {
            TabCreate(BotTabType.Simple);  // создание простой вкладки
            _tab = TabsSimple[0]; // записываем первую вкладку в поле
            _tab.BestBidAskChangeEvent += _tab_BestBidAskChangeEvent;
        }

        public void _tab_BestBidAskChangeEvent(decimal arg1, decimal arg2)
        {
            _price = _tab.MarketDepth.Bids[0].Price;
            string a1 = Price.ToString();
            СallUpdate(a1);
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

    }
}
