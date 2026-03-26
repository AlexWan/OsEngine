/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using OsEngine.Market.Servers;
using OsEngine.Market;
using System.Threading;
using OsEngine.Language;

/* Description
A robot that helps you buy and sell security at a specific time interval
Робот помогающий покупать и продавать активы с определённым временным интервалом
*/

namespace OsEngine.Robots.Helpers
{
    [Bot("DcaTimeBot")]
    public class DcaTimeBot : BotPanel
    {
        private BotTabSimple _tab;

        // Basic settings

        private StrategyParameterString _regime;
        private StrategyParameterBool _logIsOn;
        private StrategyParameterString _timeIntervalType;
        private StrategyParameterInt _timeInterval;

        // GetVolume settings

        private StrategyParameterInt _ordersCountStart;
        private StrategyParameterInt _ordersCountCurrent;
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        public DcaTimeBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            _tab = TabCreate<BotTabSimple>();

            if(startProgram == StartProgram.IsOsTrader)
            {
                _regime = CreateParameter("Режим работы", "Off", new[] { "Off", "OnBuy", "OnSell" });
                if(_regime.ValueString != "Off")
                { // выключаем на старте программы и при создании робота
                    _regime.ValueString = "Off";
                }

                _logIsOn = CreateParameter("Логирование в экстренный лог", true);
                _timeIntervalType = CreateParameter("Тип интервала", "Seconds", new[] { "Seconds", "Minutes", "Hours", "Days" });
                _timeInterval = CreateParameter("Интервал", 10, 1, 10, 1);

                _ordersCountStart = CreateParameter("Кол-во ордеров. Старт", 10, 1, 10, 1);
                _ordersCountCurrent = CreateParameter("Кол-во ордеров. Осталось", 0, 1, 10, 1);

                _volumeType = CreateParameter("Тип объёма", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
                _volume = CreateParameter("Значение объёма", 1, 1.0m, 50, 4);
                _tradeAssetInPortfolio = CreateParameter("Средсва в портфеле", "Prime");

                StrategyParameterButton buttonGetStatus = CreateParameterButton("Показать статус робота");
                buttonGetStatus.UserClickOnButtonEvent += ButtonGetStatus_UserClickOnButtonEvent;

                Thread worker = new Thread(TradeLogic);
                worker.Start();

                this.ParametrsChangeByUser += DcaTimeBot_ParametrsChangeByUser;
                this.DeleteEvent += DcaTimeBot_DeleteEvent;

                // non trade periods

                _tradePeriodsSettings = new NonTradePeriods(name);

                _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0 };
                _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 05 };
                _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1OnOff = true;

                _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2Start = new TimeOfDay() { Hour = 13, Minute = 54 };
                _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2End = new TimeOfDay() { Hour = 14, Minute = 6 };
                _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2OnOff = false;

                _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3Start = new TimeOfDay() { Hour = 18, Minute = 1 };
                _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3End = new TimeOfDay() { Hour = 23, Minute = 58 };
                _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3OnOff = true;

                _tradePeriodsSettings.TradeInSunday = false;
                _tradePeriodsSettings.TradeInSaturday = false;

                _tradePeriodsSettings.Load();

                _tradePeriodsShowDialogButton = CreateParameterButton("Не торговые периоды");
                _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;
                _isLoaded = true;
            }

            Description = OsLocalization.ConvertToLocString(
            "Eng:A robot that helps you buy and sell security at a specific time interval_" +
            "Ru:Робот помогающий покупать и продавать активы с определённым временным интервалом_");

        }

        private void DcaTimeBot_DeleteEvent()
        {
            _isDeleted = true;
        }

        private bool _isDeleted;

        private bool _isLoaded;

        private void DcaTimeBot_ParametrsChangeByUser()
        {
            if (StartProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            if(_isLoaded == false)
            {
                return;
            }

            LogMessageType typeOfMessage = LogMessageType.System;

            if(_logIsOn.ValueBool == true)
            {
                typeOfMessage = LogMessageType.Error;
            }

            if(_regime.ValueString == "Off"
                && _ordersCountCurrent.ValueInt != 0)
            {
                string message = "Dca bot is OFF. Name: " + this.NameStrategyUniq + "\n";

                SendNewLogMessage(message, typeOfMessage);
                _ordersCountCurrent.ValueInt = 0;
            }
            else if((_regime.ValueString == "OnBuy" || _regime.ValueString == "OnSell")
                && _ordersCountCurrent.ValueInt == 0)
            {
                UpdateInterval();
                string message = "Dca bot is ON. Name: " + this.NameStrategyUniq + "\n";
                message += "Start orders count: " + _ordersCountStart.ValueInt + "\n";
                message += "Volume type: " + _volumeType.ValueString + " Volume value: " + _volume.ValueDecimal + "\n";
                message += "Interval type: " + _timeIntervalType.ValueString + " Interval value: " + _timeInterval.ValueInt + "\n";
                message += "Interval time: " + _interval.ToString();

                SendNewLogMessage(message, typeOfMessage);
                _lastScenarioStartTime = DateTime.Now;
                _lastScenarioOrderTime = DateTime.MinValue;
                _ordersCountCurrent.ValueInt = _ordersCountStart.ValueInt;
            }
        }

        private void ButtonGetStatus_UserClickOnButtonEvent()
        {
            if (StartProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            LogMessageType typeOfMessage = LogMessageType.System;

            if (_logIsOn.ValueBool == true)
            {
                typeOfMessage = LogMessageType.Error;
            }

            UpdateInterval();

            string message = "Dca bot status. Bot Name " + this.NameStrategyUniq + "\n";
            message += "Regime: " + _regime.ValueString + "\n";

            if(_regime.ValueString != "Off")
            {
                message += "Start scenario time: " + _lastScenarioStartTime.ToString() + "\n";
                message += "Start orders count: " + _ordersCountStart.ValueInt + "\n";
                message += "Current orders count: " + _ordersCountCurrent.ValueInt + "\n";
                message += "Last order time: " + _lastScenarioOrderTime.ToString() + "\n";
                message += "Volume type: " + _volumeType.ValueString + " Volume value: " + _volume.ValueDecimal + "\n";
                message += "Interval type: " + _timeIntervalType.ValueString + " Interval value: " + _timeInterval.ValueInt + "\n";
                message += "Interval time: " + _interval.ToString();
            }

            SendNewLogMessage(message, typeOfMessage);

        }

        #region Non trade periods

        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;

        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        #endregion

        #region Logic

        private DateTime _lastScenarioStartTime;

        private DateTime _lastScenarioOrderTime;

        private TimeSpan _interval;

        private void UpdateInterval()
        {
            int value = _timeInterval.ValueInt;

            if (value <= 0)
            {
                value = 1;
            }
            // "Seconds", "Minutes", "Hours", "Days" 
            if (_timeIntervalType == "Seconds")
            {
                _interval = TimeSpan.FromSeconds(value);
            }
            else if (_timeIntervalType == "Minutes")
            {
                _interval = TimeSpan.FromMinutes(value);
            }
            else if (_timeIntervalType == "Hours")
            {
                _interval = TimeSpan.FromHours(value);
            }
            else if (_timeIntervalType == "Days")
            {
                _interval = TimeSpan.FromDays(value);
            }
        }

        private void TradeLogic()
        {
            while(true)
            {
                try
                {
                    Thread.Sleep(1000);

                    if(_isDeleted == true)
                    {
                        return;
                    }

                    if (_isLoaded == false)
                    {
                        continue;
                    }

                    if(_regime.ValueString == "Off")
                    {
                        continue;
                    }

                    if (_tradePeriodsSettings.CanTradeThisTime(_tab.TimeServerCurrent) == false)
                    {
                        continue;
                    }

                    if(_tab.IsConnected == false)
                    {
                        continue;
                    }

                    if(_ordersCountCurrent.ValueInt == 0)
                    {
                        _isLoaded = false;
                        LogMessageType typeOfMessage = LogMessageType.System;

                        if (_logIsOn.ValueBool == true)
                        {
                            typeOfMessage = LogMessageType.Error;
                        }

                        string message = "Dca bot is OFF. Scenario Ended. Bot name: " + this.NameStrategyUniq + "\n";

                        SendNewLogMessage(message, typeOfMessage);

                        _regime.ValueString = "Off";

                        _tab._journal.Clear();

                        this.ParamGuiSettings.RePaintParameterTables();

                        _isLoaded = true;
                        continue;
                    }

                    bool needToEntry = false;

                    if(_lastScenarioOrderTime == DateTime.MinValue)
                    {
                        needToEntry = true;
                    }

                    if (needToEntry == false)
                    {
                        UpdateInterval();

                        if(_lastScenarioOrderTime.Add(_interval) < DateTime.Now)
                        {
                            needToEntry = true;
                        }
                    }

                    if(needToEntry == true)
                    {
                        _isLoaded = false;
                        _lastScenarioOrderTime = DateTime.Now;
                        _ordersCountCurrent.ValueInt = _ordersCountCurrent.ValueInt - 1;

                        decimal volume = GetVolume(_tab);

                        if (_regime.ValueString == "OnBuy")
                        {
                            _tab.BuyAtMarket(volume);
                        }
                        else if (_regime.ValueString == "OnSell")
                        {
                            _tab.SellAtMarket(volume);
                        }

                        LogMessageType typeOfMessage = LogMessageType.System;

                        if (_logIsOn.ValueBool == true)
                        {
                            typeOfMessage = LogMessageType.Error;
                        }

                        string message = "Dca. New order. Bot name: " + this.NameStrategyUniq + "\n";
                        message += "Side: " + _regime.ValueString.Replace("On", "") + "\n";
                        message += "Left orders: " + _ordersCountCurrent.ValueInt;

                        SendNewLogMessage(message, typeOfMessage);
                        this.ParamGuiSettings.RePaintParameterTables();
                        _isLoaded = true;
                    }
                }
                catch (Exception ex)
                {
                    SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                    _isLoaded = true;
                } 
            }
        }

        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume.ValueDecimal;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (_volumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (_tradeAssetInPortfolio.ValueString == "Prime")
                {
                    portfolioPrimeAsset = myPortfolio.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                    if (positionOnBoard == null)
                    {
                        return 0;
                    }

                    for (int i = 0; i < positionOnBoard.Count; i++)
                    {
                        if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                       && tab.Security.PriceStep != tab.Security.PriceStepCost
                       && tab.PriceBestAsk != 0
                       && tab.Security.PriceStep != 0
                       && tab.Security.PriceStepCost != 0)
                    {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                        qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                    }
                    qty = Math.Round(qty, tab.Security.DecimalsVolume);
                }
                else
                {
                    qty = Math.Round(qty, 7);
                }

                return qty;
            }

            return volume;
        }

        #endregion
    }
}
