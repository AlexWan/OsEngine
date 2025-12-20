/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/* Description
Робот предназначен для покупки TMON вечером на свободные средства и продаже всего объема TMON утром.

The robot is designed to buy TMON in the evening with available funds and sell the entire volume of TMON in the morning.
 */

namespace OsEngine.Robots
{
    [Bot("TmonRebalancer")]
    public class TmonRebalancer : BotPanel
    {
        private BotTabSimple _tab;

        private StrategyParameterString _regimeParameter;
        private StrategyParameterDecimal _minBalance;
        private StrategyParameterDecimal _allowedSpreadSize;
        private StrategyParameterInt _icebergCount;

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
        private bool _botClosePositionToday;

        public TmonRebalancer(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _regimeParameter = CreateParameter("Regime", "Off", new[] { "Off", "RebalancingTwiceADay", "RebalancingOnceADay", "OnlyClose" });
            _minBalance = CreateParameter("Minimum balance", 5000m, 5000m, 5000m, 5000m);

            _icebergCount = CreateParameter("Iceberg count", 1, 1, 50, 1);
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

            Description = OsLocalization.ConvertToLocString(
                "En:The robot is designed to buy TMON in the evening with available funds and sell the entire volume of TMON in the morning._" +
                "Ru:Робот предназначен для покупки TMON вечером на свободные средства и продаже всего объема TMON утром._");
        }

        private void _rebalanceNowButton_UserClickOnButtonEvent()
        {
            _botClosePositionToday = true;
            Task.Run(RebalanceLogic);
        }

        #region Work thread for Real

        private DateTime _timeLast = DateTime.MinValue;

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

                    if (_tab.IsConnected == false
                        || _tab.IsReadyToTrade == false)
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

                    if (_regimeParameter.ValueString == "OnlyClose")
                    {
                        ClosePositions();
                        _regimeParameter.ValueString = "Off";
                        continue;
                    }

                    if (CheckDayOfWeek() == false)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_regimeParameter.ValueString == "RebalancingOnceADay")
                    {
                        if (_timeToBuy.Value < TimeServer)
                        {
                            if (_timeLast.AddDays(1) < TimeServer)
                            {
                                RebalanceLogic();
                                _timeLast = TimeServer;
                                continue;
                            }
                            else
                            {
                                Thread.Sleep(1000);
                                continue;
                            }
                        }
                        else
                        {
                            Thread.Sleep(1000);
                            continue;
                        }
                    }

                    if (_timeToSell.Value > _timeToBuy.Value)
                    {
                        SendNewLogMessage(
                            OsLocalization.ConvertToLocString(
                           "En:The time is incorrect!!! The time for buying should be longer than the time for selling_" +
                           "Ru:Неправильно указано время!!! Время для покупки должно быть больше, чем время для продажи_")
                            , Logging.LogMessageType.Error);

                        Thread.Sleep(10000);
                        continue;
                    }

                    if (_timeToSell.Value < TimeServer
                        && _timeToBuy.Value > TimeServer
                        && _botClosePositionToday == false)
                    {
                        ClosePositions();
                        _botClosePositionToday = true;
                        continue;
                    }

                    if (_timeToBuy.Value < TimeServer
                        && _botClosePositionToday == true)
                    {
                        RebalanceLogic();
                        _botClosePositionToday = false;
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

        #region Candlestick completion event for Tester

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

            if (_regimeParameter.ValueString == "RebalancingOnceADay")
            {
                if (_timeToBuy.Value < TimeServer)
                {
                    if (_timeLast.AddDays(1) < TimeServer
                        || _timeLast > candles[^1].TimeStart
                        || _timeLast == DateTime.MinValue)
                    {
                        RebalanceLogic();
                        _timeLast = TimeServer;
                        return;
                    }
                }
            }
            else // _regimeParameter.ValueString == "RebalancingTwiceADay"
            {
                if (_timeToSell.Value < TimeServer && _timeToBuy.Value > TimeServer && _botClosePositionToday == false)
                {
                    ClosePositions();
                    return;
                }

                if (_timeToBuy.Value < TimeServer)
                {
                    RebalanceLogic();
                    _botClosePositionToday = false;
                    return;
                }
            }
        }

        #endregion

        #region Logic

        private void RebalanceLogic()
        {
            if (_tab.Portfolio == null)
            {
                return;
            }

            if (StartProgram == StartProgram.IsOsTrader)
            {
                if (_tab.Portfolio.PositionOnBoard == null)
                {
                    return;
                }

                if (!CheckSpread())
                {
                    return;
                }

                if (_tab.Connector.MyServer.ServerType != ServerType.TInvest
                 || _tab.Security.Name != "TMON@")
                {
                    SendNewLogMessage(OsLocalization.ConvertToLocString(
                               "En:The robot is only intended for rebalancing TMON with the T-Investments broker and for testing._" +
                               "Ru:Робот предназначен только для ребалансировки TMON у брокера Т-Инвестиции и для запуска в тестере_")
                               , Logging.LogMessageType.Error);
                    Thread.Sleep(10000);
                    return;
                }

            }

            decimal balance = GetPortfolioValue();

            if (balance == 0)
            {
                return;
            }

            decimal freeBalance = Math.Abs(balance - Math.Max(_minBalance.ValueDecimal, 500));

            decimal volume = GetVolume(freeBalance);

            if (volume <= 0)
            {
                return;
            }

            if (balance > _minBalance.ValueDecimal)
            {
                if (_icebergCount.ValueInt <= 1)
                {
                    if (_tab.PositionOpenLong.Count > 0)
                    {
                        _tab.BuyAtLimitToPosition(_tab.PositionOpenLong[0], _tab.PriceBestAsk, volume);
                    }
                    else
                    {
                        _tab.BuyAtLimit(volume, _tab.PriceBestAsk);
                    }
                }
                else
                {
                    if (_tab.PositionOpenLong.Count > 0)
                    {
                        _tab.BuyAtIcebergToPosition(_tab.PositionOpenLong[0], _tab.PriceBestAsk, volume, _icebergCount.ValueInt);
                    }
                    else
                    {
                        _tab.BuyAtIceberg(volume, _tab.PriceBestAsk, _icebergCount.ValueInt);
                    }
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
            if (!CheckSpread())
            {
                return;
            }

            if (_tab.PositionsOpenAll.Count > 0)
            {
                for (int i = 0; i < _tab.PositionsOpenAll.Count; i++)
                {
                    _tab.CloseAtMarket(_tab.PositionsOpenAll[i], _tab.PositionsOpenAll[i].OpenVolume);
                }
            }
        }

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
                                return positions[i].ValueCurrent - positions[i].ValueBlocked;
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
    }
}
