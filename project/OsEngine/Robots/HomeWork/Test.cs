using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;
using System.Threading;

namespace OsEngine.Robots.HomeWork
{
    [Bot("Test")]
    public class Test : BotPanel
    {
        private BotTabSimple _tab;
        private decimal _priceBid;
        private decimal _volume = 10;
        private int _step = 2;

        public Test(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            
            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;
            _tab.OrderUpdateEvent += _tab_OrderUpdateEvent;
            

            StartThread();
        }

        private void MyServer_NewOrderIncomeEvent(Order obj)
        {
            SendNewLogMessage($"Цена в MyServer_NewOrderIncomeEvent: {obj.Price}, exvol = {obj.VolumeExecute}", Logging.LogMessageType.Error);

            /*if (_tab.PositionsOpenAll.Count > 0)
            {
                for (int i = 0; i < _tab.PositionsOpenAll.Count; i++)
                {
                    if (_tab.PositionsOpenAll[i].OpenOrders[0].NumberMarket == obj.NumberMarket)
                    {
                        _tab.PositionsOpenAll[0].OpenOrders[0].Price = obj.Price;
                    }
                }
            }  */          
        }

        private void StartThread()
        {
            Thread worker = new Thread(StartPaintChart) { IsBackground = true };
            worker.Start();
        }

        private void StartPaintChart()
        {
            bool isConnected = false;
            while (true)
            {
                if (!_tab.Connector.IsReadyToTrade)
                {
                    Thread.Sleep(500);
                    continue;
                }
                else
                {
                    if (!isConnected)
                    {
                        _tab.Connector.MyServer.NewOrderIncomeEvent += MyServer_NewOrderIncomeEvent;
                        isConnected = true;
                    }
                }
                if (_priceBid ==  0)
                {
                    continue;
                }
                if (_step > 5)
                {
                    //_step = 1;
                }
                /*if (_tab.PositionsOpenAll.Count > 0)
                {
                    _tab.PositionsOpenAll[0].OpenOrders[0].Volume++;
                    _tab.ChangeOrderPrice(_tab.PositionsOpenAll[0].OpenOrders[0], _priceBid - 1 * _step);
                    SendNewLogMessage($"Отправляем новую цену: {_priceBid - 1 * _step} и новый объем {_tab.PositionsOpenAll[0].OpenOrders[0].Volume}", Logging.LogMessageType.Error);
                    _step++;
                }*/

                Thread.Sleep(5000);
            }
        }

        private void _tab_OrderUpdateEvent(Order obj)
        {
            SendNewLogMessage($"Цена в OrderUpdateEvent: {obj.Price}, exvol = {obj.VolumeExecute}", Logging.LogMessageType.Error);
            SendNewLogMessage($"Цена в PositionsOpenAll: {_tab.PositionsOpenAll[0].OpenOrders[0].Price}", Logging.LogMessageType.Error);            
        }

        private void _tab_MarketDepthUpdateEvent(MarketDepth obj)
        {
            _priceBid = obj.Bids[0].Price;
            if (_tab.PositionsOpenAll.Count == 0)
            {
                _tab.BuyAtLimit(_volume, _priceBid +600);
                SendNewLogMessage($"Открываем ордер с ценой: {_priceBid + 100}", Logging.LogMessageType.Error);
            }            
        }

        public override string GetNameStrategyType()
        {
            return "Test";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

    }
}

