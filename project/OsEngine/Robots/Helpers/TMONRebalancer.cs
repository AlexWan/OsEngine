
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Threading;


namespace OsEngine.Robots
{
    [Bot("TMONRebalancer")]
    public class TMONRebalancer : BotPanel
    {
        public TMONRebalancer(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _regimeParameter = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyClose" });

            _minBalance = CreateParameter("Minimum balance", 5000m, 5000m, 5000m, 5000m);
            _allowedSpreadSize = CreateParameter("Allowed spread size(%)", 0.01m, 0.01m, 0.01m, 0.01m);

            _timeToBuy = CreateParameterTimeOfDay("Time to buy", 23, 0, 0, 0);
            _timeToSell = CreateParameterTimeOfDay("Time to sell", 10, 05, 0, 0);

            _tradeMonday = CreateParameterCheckBox("Trading on Monday", true);
            _tradeTuesday = CreateParameterCheckBox("Trading on Tuesday", true);
            _tradeWednesday = CreateParameterCheckBox("Trading on Wednesday", true);
            _tradeThursday = CreateParameterCheckBox("Trading on Thursday", true);
            _tradeFriday = CreateParameterCheckBox("Trading on Friday", true);
            _tradeSaturday = CreateParameterCheckBox("Trading on Saturday", false);
            _tradeSunday = CreateParameterCheckBox("Trading on Sunday", false);

            _rebalanceNowButton = CreateParameterButton("Rebalance now");
            _rebalanceNowButton.UserClickOnButtonEvent += _rebalanceNowButton_UserClickOnButtonEvent;

            if (startProgram == StartProgram.IsOsTrader)
            {
                Thread thread = new Thread(StartThread);
                thread.IsBackground = true;
                thread.Start();
            }
            else
            {
                _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            }
        }

        private void _rebalanceNowButton_UserClickOnButtonEvent()
        {
            RebalanceLogic();
            _rebalanceNow = true;
        }

        #region Work thread

        private void StartThread()
        {
            while (true)
            {
                Thread.Sleep(30000);

                try
                {
                    if (_regimeParameter.ValueString == "Off")
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_tab.Security == null
                        || _tab.CandlesAll == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (CheckDayOfWeek() == false)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_timeToSell.Value < TimeServer && _timeToBuy.Value > TimeServer && _rebalanceNow == false)
                    {
                        ClosePositions();
                        continue;
                    }

                    if (_timeToBuy.Value < TimeServer)
                    {
                        RebalanceLogic();
                        _rebalanceNow = false;
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        #endregion

        #region Candlestick completion event 

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regimeParameter.ValueString == "Off")
            {
                return;
            }

            if (candles == null
                || candles.Count == 0)
            {
                return;
            }

            if (CheckDayOfWeek() == false)
            {
                return;
            }

            if (_timeToSell.Value < TimeServer && _timeToBuy.Value > TimeServer && _rebalanceNow == false)
            {
                ClosePositions();
                return;
            }

            if (_timeToBuy.Value < TimeServer)
            {
                RebalanceLogic();
                _rebalanceNow = false;
                return;
            }
        }

        #endregion

        #region Logic robot

        private void RebalanceLogic()
        {
            if (!CheckSpread())
            {
                return;
            }

            if (_tab.Portfolio.PositionOnBoard == null)
            {
                return;
            }

            if (_tab.Portfolio == null)
            {
                return;
            }

            decimal balance = GetPortfolioValue();

            if (balance == 0)
            {
                return;
            }

            decimal volume = Math.Abs(balance - _minBalance.ValueDecimal);
            volume = GetVolume(volume);

            if (volume <= 0)
            {
                return;
            }

            if (balance > _minBalance.ValueDecimal)
            {
                if (_tab.PositionOpenLong.Count > 0)
                {
                    _tab.BuyAtMarketToPosition(_tab.PositionOpenLong[0], volume);
                }
                else
                {
                    _tab.BuyAtMarket(volume);
                }
            }
            else if (_tab.PositionOpenLong.Count > 0
                && balance < _minBalance.ValueDecimal)
            {
                if (volume > _tab.PositionOpenLong[0].OpenVolume)
                {
                    volume = _tab.PositionOpenLong[0].OpenVolume;
                }

                _tab.CloseAtMarket(_tab.PositionOpenLong[0], volume);
            }
        }

        private void ClosePositions()
        {
            if (_tab.PositionsOpenAll.Count > 0)
            {
                for (int i = 0; i < _tab.PositionsOpenAll.Count; i++)
                {
                    _tab.CloseAtMarket(_tab.PositionsOpenAll[i], _tab.PositionsOpenAll[i].OpenVolume);
                }
            }
        }

        #endregion

        #region Position on board

        private decimal GetPortfolioValue()
        {
            try
            {
                decimal volume = 0;

                if (_tab.Connector.MyServer.ServerType == ServerType.TInvest)
                {
                    List<PositionOnBoard> positions = _tab.Portfolio.GetPositionOnBoard();

                    decimal portfolioValue = _tab.Portfolio.ValueCurrent;

                    if (positions.Count > 0)
                    {
                        for (int i = 0; i < positions.Count; i++)
                        {
                            if (positions[i].SecurityNameCode == "rub")
                            {
                                return positions[i].ValueCurrent;
                            }
                        }
                    }
                }
                else if (_tab.Connector.MyServer.ServerType == ServerType.Tester)
                {
                    List<PositionOnBoard> positions = _tab.Portfolio.GetPositionOnBoard();

                    decimal portfolioValue = _tab.Portfolio.ValueCurrent;
                    decimal volumToPosition = GetVolumeToPositions();
                    volume = portfolioValue - volumToPosition;
                }

                return volume;
            }
            catch (Exception ex)
            {
                SendNewLogMessage("GetPortfolioValue: " + ex.Message, Logging.LogMessageType.Error);
            }

            return 0;
        }

        private decimal GetVolumeToPositions()
        {
            decimal volumToPosition = 0;

            for (int i = 0; i < OsTraderMaster.Master.PanelsArray.Count; i++)
            {
                List<Position> positionsBot = OsTraderMaster.Master.PanelsArray[i].OpenPositions;

                for (int j = 0; j < positionsBot.Count; j++)
                {
                    decimal margin = GetMarginSecurities(positionsBot[j].SecurityName, positionsBot[j].Direction);

                    if (margin > 1)
                    {
                        volumToPosition += positionsBot[j].OpenVolume * margin * positionsBot[j].Lots;
                    }
                    else if (margin <= 1 && _tab.Connector.MyServer.ServerType == ServerType.Tester)
                    {
                        volumToPosition += positionsBot[j].OpenVolume * positionsBot[j].EntryPrice * positionsBot[j].Lots;
                    }
                }
            }

            return volumToPosition;
        }

        private decimal GetMarginSecurities(string nameSecurity, Side side)
        {
            List<Security> securities = _tab.Connector.MyServer.Securities;

            for (int i = 0; i < securities.Count; i++)
            {
                if (securities[i].Name == nameSecurity)
                {
                    return side == Side.Buy ? securities[i].MarginBuy : securities[i].MarginSell;
                }
            }

            return 0;
        }

        #endregion

        #region Volume

        private decimal GetVolume(decimal balance)
        {
            decimal contractPrice = _tab.PriceBestAsk;
            decimal volume = balance / contractPrice;

            if (StartProgram == StartProgram.IsOsTrader)
            {
                IServerPermission serverPermission = ServerMaster.GetServerPermission(_tab.Connector.ServerType);

                if (serverPermission != null &&
                    serverPermission.IsUseLotToCalculateProfit &&
                _tab.Security.Lot != 0 &&
                    _tab.Security.Lot > 1)
                {
                    volume = balance / (contractPrice * _tab.Security.Lot);
                }

                volume = Math.Round(volume, _tab.Security.DecimalsVolume);
            }
            else // Tester or Optimizer
            {
                volume = Math.Round(volume, 6);
            }

            return volume;
        }

        #endregion

        #region Trade time

        private bool CheckDayOfWeek()
        {
            DayOfWeek currentdDayOfWeek = TimeServer.DayOfWeek;

            if (currentdDayOfWeek == DayOfWeek.Monday && _tradeMonday == false) return false;
            else if (currentdDayOfWeek == DayOfWeek.Tuesday && _tradeTuesday == false) return false;
            else if (currentdDayOfWeek == DayOfWeek.Wednesday && _tradeWednesday == false) return false;
            else if (currentdDayOfWeek == DayOfWeek.Thursday && _tradeThursday == false) return false;
            else if (currentdDayOfWeek == DayOfWeek.Friday && _tradeFriday == false) return false;
            else if (currentdDayOfWeek == DayOfWeek.Saturday && _tradeSaturday == false) return false;
            else if (currentdDayOfWeek == DayOfWeek.Sunday && _tradeSunday == false) return false;
            else return true;
        }

        #endregion

        #region Fields

        BotTabSimple _tab;

        private StrategyParameterString _regimeParameter;
        private StrategyParameterDecimal _minBalance;
        private StrategyParameterDecimal _allowedSpreadSize;
        private StrategyParameterTimeOfDay _timeToBuy;
        private StrategyParameterTimeOfDay _timeToSell;
        private StrategyParameterCheckBox _tradeMonday;
        private StrategyParameterCheckBox _tradeTuesday;
        private StrategyParameterCheckBox _tradeWednesday;
        private StrategyParameterCheckBox _tradeThursday;
        private StrategyParameterCheckBox _tradeFriday;
        private StrategyParameterCheckBox _tradeSaturday;
        private StrategyParameterCheckBox _tradeSunday;
        private StrategyParameterButton _rebalanceNowButton;
        private bool _rebalanceNow;

        #endregion

        #region Helpers

        private bool CheckSpread()
        {
            try
            {
                decimal bestAsk = _tab.PriceBestAsk;
                decimal bestBid = _tab.PriceBestBid;

                decimal absoluteSpread = bestAsk - bestBid;
                decimal midPrice = (bestAsk + bestBid) / 2;

                if (midPrice == 0)
                {
                    return false;
                }

                decimal spreadPercent = Math.Round(absoluteSpread / midPrice * 100, 4);

                if (spreadPercent > _allowedSpreadSize.ValueDecimal && _allowedSpreadSize != 0)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                return false;
            }
        }

        public override string GetNameStrategyType()
        {
            return "TMONRebalancer";
        }

        #endregion
    }
}
