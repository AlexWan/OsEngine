using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Logging;

namespace OsEngine.Robots.HomeWork
{
    [Bot("SmartGrid")]
    public class SmartGrid : BotPanel
    {
        private BotTabSimple _tab;
        private WindowsFormsHost _host;
        private StrategyParameterString _regime;
        private decimal _lastPriceCandle;

        public SmartGrid(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.ManualPositionSupport.DisableManualSupport();

            _regime = CreateParameter("Regime", "Off", new string[] { "Off", "On" });

            this.ParamGuiSettings.Title = "SmartGrid Parameters";
            this.ParamGuiSettings.Height = 300;
            this.ParamGuiSettings.Width = 600;
            CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab("SmartGrid Parameters");
            customTab.AddChildren(_host);

            Thread worker = new Thread(StartRobot) { IsBackground = true };
            worker.Start();
        }
               
        private void StartRobot()
        {
            try
            {
                bool connectorOn = false;
                bool buildGrid = false;

                while (true)
                {
                    Thread.Sleep(500);

                    if (_tab == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                    if (_tab.Securiti == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                    if (!_tab.Connector.IsReadyToTrade)
                    {
                        continue;
                    }
                    if (!connectorOn)
                    {
                        connectorOn = true;
                        _tab.Connector.MyServer.NewOrderIncomeEvent += MyServer_NewOrderIncomeEvent;
                        _tab.CandleUpdateEvent += _tab_CandleUpdateEvent;                        
                    }

                    if (_regime.ValueString == "On")
                    {
                        if (_lastPriceCandle != 0 ||
                            !buildGrid)
                        {
                            
                            BuildGrid();
                            buildGrid = true;
                        }                        
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void BuildGrid()
        {
            
        }

        private void _tab_CandleUpdateEvent(List<Candle> obj)
        {
            _lastPriceCandle = obj[obj.Count - 1].Close;
        }

        private void MyServer_NewOrderIncomeEvent(Order order)
        {
            throw new NotImplementedException();
        }

        public override string GetNameStrategyType()
        {
            return "SmartGrid";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
    public class ListOrders
    {
        public decimal PriceOrder { get; set; }
        public Side SideOrder { get; set; }
        public decimal VolumeOrder { get; set; }
        public decimal ExecuteVolume { get; set; }
        public decimal PriceCounterOrder { get; set; }
        public OrdersType OrderType { get; set; }
        public string NumberMarket { get; set; }

        public ListOrders(decimal priceOrder, Side sideOrder, decimal volumeOrder, decimal executeVolume, decimal priceCounterOrder, OrdersType orderType, string numberMarket)
        {
            PriceOrder = priceOrder;
            SideOrder = sideOrder;
            VolumeOrder = volumeOrder;
            ExecuteVolume = executeVolume;
            PriceCounterOrder = priceCounterOrder;
            OrderType = orderType;
            NumberMarket = numberMarket;
        }
    }
    public enum OrdersType
    {
        MainOrder,
        CounterOrder,
        None
    }
}
