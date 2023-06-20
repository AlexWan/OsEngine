using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    [Bot("WTestBotSecurities")]
    public class WTestBotSecurities : BotPanel
    {
        public WTestBotSecurities(string name, StartProgram startProgram) : base(name, startProgram)
        {
            List<IServer> servers = ServerMaster.GetServers();

            if(servers != null &&
                servers.Count > 0)
            {
                for(int i = 0;i < servers.Count;i++)
                {
                    List<Security> securities = servers[i].Securities;

                    if(securities != null &&
                        securities.Count > 0)
                    {
                        CheckSecurities(securities, servers[i].ServerType);
                    }

                    servers[i].SecuritiesChangeEvent += WTestBotSecurities_SecuritiesChangeEvent;
                }
            }

            ServerMaster.ServerCreateEvent += ServerMaster_ServerCreateEvent;

            this.DeleteEvent += WTestBotSecurities_DeleteEvent;
        }

        private void WTestBotSecurities_DeleteEvent()
        {
            ServerMaster.ServerCreateEvent -= ServerMaster_ServerCreateEvent;

            List<IServer> servers = ServerMaster.GetServers();

            if (servers != null &&
                servers.Count > 0)
            {
                for (int i = 0; i < servers.Count; i++)
                {
                    servers[i].SecuritiesChangeEvent -= WTestBotSecurities_SecuritiesChangeEvent;
                }
            }

        }

        private void ServerMaster_ServerCreateEvent(IServer server)
        {
            server.SecuritiesChangeEvent += WTestBotSecurities_SecuritiesChangeEvent;
        }

        private void WTestBotSecurities_SecuritiesChangeEvent(List<Security> securities)
        {
            List<IServer> servers = ServerMaster.GetServers();

            if (servers != null &&
                servers.Count > 0)
            {
                for (int i = 0; i < servers.Count; i++)
                {
                    securities = servers[i].Securities;

                    if (securities != null &&
                        securities.Count > 0)
                    {
                        CheckSecurities(securities, servers[i].ServerType);
                    }
                }
            }
        }

        public override string GetNameStrategyType()
        {
            return "WTestBotSecurities";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private void CheckSecurities(List<Security> securities, ServerType serverType)
        {
            _haveError = false;

            for (int i = 0;i < securities.Count;i++)
            {
                if (securities[i] == null)
                {
                    _haveError = true;
                    SendNewLogMessage(serverType + " Security Error 1 . One of Securities in array is null!", LogMessageType.Error);
                }

                try
                {
                    Check(securities[i], serverType);
                }
                catch (Exception ex)
                {
                    _haveError = true;
                    SendNewLogMessage(serverType + " Security Error Unknown" + ex.ToString(), LogMessageType.Error);
                }
            }

            string report = "Check Securities Report \n";
            report += "Server - " + serverType.ToString() + "\n";
            report += "Securities count - " + securities.Count + "\n";
            report += "Have errors? - " + _haveError;

            SendNewLogMessage(report, LogMessageType.Error);

        }

        private bool _haveError = false;

        private void Check(Security security, ServerType serverType)
        {
            // Data Validation

            /*
            2.1.	НЕ обязательные поля
            2.1.1.	Go – это гарантированное обеспечение для фьючерсной площадки МОЕКС. Не нужное
            2.1.2.	OptionType – тип опциона. Не нужно нам пока
            2.1.3.	Strike – тоже опционная тематика. Не нужно
            2.1.4.	Expiration – опционы. Не нужно
            2.1.5.	MinTradeAmount – Минимальный объём возможный для входа. Подаётся в очень малом кол-ве бирж. Можно игнорировать. Но если есть – надо добавлять.
            2.1.6.	PriceLimitLow – минимальная цена для выставления ордера. Подаётся в очень малом кол-ве бирж.
            2.1.7.	PriceLimitHigh – максимальная цена для выставления ордера. Подаётся в очень малом кол-ве бирж.


            2.2.	Обязательные поля
            2.2.1.	Name – имя бумаги повсеместно используемое в платформе
            2.2.2.	NameFull – имя бумаги на случай если оно отличается от Name и имеет какие-то странные префиксы.Нужно на классических площадках и особенно на Американских.
            2.2.3.	NameClass – Класс бумаги. Подробное описание есть ниже. С картинками
            2.2.4.	NameId – Нужно для Международных рынков и некоторых типов брокеров РФ.Например Транзак и Тинькофф. В случае если по бирже нет 
            2.2.5.	Exchange – Биржа по которой эта бумага торгуется
            2.2.6.	State – Торговый статус бумаги.Надо вытаскивать из сообщения это.Очень важно. Если статус не Active – никуда не сохранять и не добавлять.

            2.2.7.	Price Step – шаг цены инструмента.На классических площадках это обычно 1. На число с запятой, вида: 0.1 / 0.0001. 
            2.2.8.	Lot – шаг объёма инструмента.На классических площадках это обычно 1. На крипте число с запятой, вида: 0.1 / 0.0001.
            2.2.9.	PriceStepCost – цена шага цены инструмента. Актуально для фьючерсной секции Московской Биржи. Если на площадке такого понятия нет – то там должно быть указано Price Step.
            2.2.10.	SecurityType.Тип инструмента
            2.2.11.	Decimals  - количество знаков после запятой у цены инструмента
            2.2.12.	DecimalsVolume – количество знаков после запятой у объёма инструмента

            */

            if(string.IsNullOrEmpty(security.Name))
            {
                _haveError |= true;
                SendNewLogMessage(serverType + " Security Error 2 . Name is Empty!", LogMessageType.Error);
            }
            if (string.IsNullOrEmpty(security.NameFull))
            {
                _haveError |= true;
                SendNewLogMessage(serverType + " Security Error 3 . Security Name is Empty!", LogMessageType.Error);
            }
            if (string.IsNullOrEmpty(security.NameClass))
            {
                _haveError |= true;
                SendNewLogMessage(serverType + " Security Error 4 . Security Name is Empty!", LogMessageType.Error);
            }
            if (string.IsNullOrEmpty(security.NameId))
            {
                _haveError |= true;
                SendNewLogMessage(serverType + " Security Error 5 . Security Name is Empty!", LogMessageType.Error);
            }

            if (string.IsNullOrEmpty(security.Exchange))
            {
                _haveError |= true;
                SendNewLogMessage(serverType + " Security Error 6 . Exchange is Empty!", LogMessageType.Error);
            }
            else
            {
                if( security.Exchange.Equals(serverType.ToString()) == false)
                {
                    _haveError |= true;
                    SendNewLogMessage(serverType + " Security Error 7 . Exchange naming Error!", LogMessageType.Error);
                }
            }

            if (string.IsNullOrEmpty(security.State.ToString()))
            {
                _haveError |= true;
                SendNewLogMessage(serverType + " Security Error 9. State is Empty!", LogMessageType.Error);
            }
            else
            {
                if(security.State == SecurityStateType.UnKnown)
                {
                    _haveError |= true;
                    SendNewLogMessage(serverType + " Security Error 10. Sec State is UnKnown!", LogMessageType.Error);
                }
            }

            if (security.SecurityType == SecurityType.None)
            {
                _haveError |= true;
                SendNewLogMessage(serverType + " Security Error 11. SecurityType is None ", LogMessageType.Error);
            }

            if (security.PriceStep <= 0)
            {
                _haveError |= true;
                SendNewLogMessage(serverType + " Security Error 12. PriceState is 0 ", LogMessageType.Error);
            }
            if (security.Lot <= 0)
            {
                _haveError |= true;
                SendNewLogMessage(serverType + " Security Error 13. Lot is 0 ", LogMessageType.Error);
            }
            if (security.PriceStepCost <= 0)
            {
                _haveError |= true;
                SendNewLogMessage(serverType + " Security Error 14. PriceStepCost is 0 ", LogMessageType.Error);
            }

            if (security.Decimals < 0)
            {
                _haveError |= true;
                SendNewLogMessage(serverType + " Security Error 15. Decimals is 0 ", LogMessageType.Error);
            }

            if (security.DecimalsVolume < 0)
            {
                _haveError |= true;
                SendNewLogMessage(serverType + " Security Error 16. DecimalsVolume is 0 ", LogMessageType.Error);
            }

            if(security.PriceStep != 0)
            {
                if(IsCompairDecimalsAndStep(security.Decimals, security.PriceStep) == false)
                {
                    _haveError |= true;
                    SendNewLogMessage(serverType + " Security Error 17. PriceStep and Decimals in conflict ", LogMessageType.Error);
                }
            }

            if (security.Lot != 0)
            {
                if (IsCompairDecimalsAndStep(security.DecimalsVolume, security.Lot) == false)
                {
                    _haveError |= true;
                    SendNewLogMessage(serverType + " Security Error 18. Lot and DecimalsVolume in conflict ", LogMessageType.Error);
                }
            }

        }

        private bool IsCompairDecimalsAndStep(int decimals, decimal step)
        {
            int realDecimals = 0;

            string stepInStr = step.ToStringWithNoEndZero().Replace(",", ".");

            if(stepInStr.Split('.').Length > 1)
            {
                realDecimals = stepInStr.Split('.')[1].Length;

                if(realDecimals != decimals)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
