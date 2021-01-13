using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Charts.CandleChart.Indicators;
using System.Threading;
using OsEngine.Market;
using OsEngine.Market.Servers;



namespace OsEngine.Robots.MoiRoboti
{
    public class Hftone : BotPanel
    {
        public Hftone(string name, StartProgram startProgram) : base(name, startProgram) // конструктор 
        {
            ServerMaster.ServerCreateEvent += ServerMaster_ServerCreateEvent;
        }

        private void ServerMaster_ServerCreateEvent(IServer newServer)
        {
            Servers.Add(newServer);
            newServer.PortfoliosChangeEvent += _server_PortfoliosChangeEvent;
            newServer.SecuritiesChangeEvent += _server_SecuritiesChangeEvent;
        }

        public void ChangeServer(ServerType serverType)
        {
            IServer newServer =null;
            List<IServer> allServer = ServerMaster.GetServers();
            for ( int i=0; i<allServer.Count; i++)
            {
                if (serverType == allServer[i].ServerType )
                {
                    newServer = allServer[i];
                    break;
                }
            }
            if (newServer == null)
            {
                return;
            }
            Portfolios = newServer.Portfolios;
            Securities = newServer.Securities;
        }

        private void _server_SecuritiesChangeEvent(List<Security> securities)
        {
            Securities = securities;
        }

        private void _server_PortfoliosChangeEvent(List<Portfolio> portfolios)
        {
            Portfolios = portfolios;
        }

        public List<Portfolio> Portfolios;

        public List<Security> Securities;

        public List<IServer> Servers = new List<IServer>();

        public override string GetNameStrategyType()
        {
            return "Hftone";
        }

        public override void ShowIndividualSettingsDialog()
        {
            HftoneUi ui = new HftoneUi(this);
            ui.Show();
        }

        public void SendOrder(ServerType server, string security, string portfolio, decimal price, decimal volume, Side orderSide )
        {
            IServer myServer = null;
            for (int i = 0; i < Servers.Count; i++)
            {
                if (Servers[i].ServerType == server)
                {
                    myServer = Servers[i];
                    break;
                }
            }
            if (myServer == null)
            {
                return;
            }
            Order order = new Order();
            order.SecurityNameCode = security;
            order.PortfolioNumber = portfolio;
            order.Price = price;
            order.Volume = volume;
            order.Side = orderSide;
            order.NumberUser = NumberGen.GetNumberOrder(this.StartProgram);

            myServer.ExecuteOrder(order);
        }
    }
}
