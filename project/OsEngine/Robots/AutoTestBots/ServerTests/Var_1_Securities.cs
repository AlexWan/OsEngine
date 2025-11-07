using OsEngine.Entity;
using OsEngine.Market;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Var_1_Securities : AServerTester
    {
        public override void Process()
        {
            List<Security> securities = Server.Securities;

            if (securities != null &&
                securities.Count > 0)
            {
                CheckSecurities(securities, Server.ServerType);
                SetNewServiceInfo("Securities count: " + securities.Count);
            }
            else
            {
                SetNewError("Security Error 0. No securities found");
            }

            TestEnded();
        }

        private void CheckSecurities(List<Security> securities, ServerType serverType)
        {
            for (int i = 0; i < securities.Count; i++)
            {
                if (securities[i] == null)
                {
                    SetNewError(serverType + " Security Error 1 . One of Securities in array is null!");
                }

                try
                {
                    Check(securities[i], serverType);
                }
                catch (Exception ex)
                {
                    SetNewError(serverType + " Security Error Unknown " + ex.ToString());
                }
            }
        }

        private void Check(Security security, ServerType serverType)
        {
            // Data Validation

            /*
            2.1.	НЕ обязательные поля
            2.1.1.	Go – это гарантированное обеспечение для фьючерсной площадки МОЕКС. Не нужное
            2.1.2.	OptionType – тип опциона. Не нужно нам пока
            2.1.3.	Strike – тоже опционная тематика. Не нужно
            2.1.4.	Expiration – опционы. Не нужно
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
            2.2.13. VolumeDecimals – шаг объёма
            2.2.14.	MinTradeAmount – Минимальный объём возможный для входа.

            */

            if (string.IsNullOrEmpty(security.Name))
            {
                SetNewError(serverType + " Security Error 2 . Name is Empty!");
            }
            if (string.IsNullOrEmpty(security.NameFull))
            {
                SetNewError(serverType + " Security Error 3 . Security NameFull is Empty!");
            }
            if (string.IsNullOrEmpty(security.NameClass))
            {
                SetNewError(serverType + " Security Error 4 . Security NameClass is Empty!");
            }
            if (string.IsNullOrEmpty(security.NameId))
            {
                SetNewError(serverType + " Security Error 5 . Security NameId is Empty!");
            }

            if (string.IsNullOrEmpty(security.Exchange))
            {
                SetNewError(serverType + " Security Error 6 . Exchange is Empty!");
            }

            if (string.IsNullOrEmpty(security.State.ToString()))
            {
                SetNewError(serverType + " Security Error 8. State is Empty!");
            }
            else
            {
                if (security.State == SecurityStateType.UnKnown)
                {
                    SetNewError(serverType + " Security Error 9. Sec State is UnKnown!");
                }
            }

            if (security.SecurityType == SecurityType.None)
            {
                SetNewError(serverType + " Security Error 10. SecurityType is None ");
            }

            if (security.PriceStep <= 0)
            {
                SetNewError(serverType + " Security Error 11. PriceStep is 0 ");
            }
            if (security.Lot <= 0)
            {
                SetNewError(serverType + " Security Error 12. Lot is 0 ");
            }
            if (security.PriceStepCost <= 0)
            {
                SetNewError(serverType + " Security Error 13. PriceStepCost is 0 ");
            }

            if (security.Decimals < 0)
            {
                SetNewError(serverType + " Security Error 14. Decimals is 0 ");
            }

            if (security.DecimalsVolume < 0)
            {
                SetNewError(serverType + " Security Error 15. DecimalsVolume is 0 ");
            }

            if (security.PriceStep != 0)
            {
                if (IsCompairDecimalsAndStep(security.Decimals, security.PriceStep) == false)
                {
                    SetNewError(serverType + " Security Error 16. PriceStep and Decimals in conflict ");
                }
            }

            if (security.VolumeStep == 0)
            {
                SetNewError(serverType + " Security Error 17. VolumeStep is 0 ");
            }

            if (security.MinTradeAmount == 0)
            {
                SetNewError(serverType + " Security Error 18. MinTradeAmount is 0 ");
            }

            if(security.MarginBuy != 0
                && security.MarginSell == 0)
            {
                SetNewError(serverType + " Security Error 19. security.MarginSell is 0 ");
            }
        }

        private bool IsCompairDecimalsAndStep(int decimals, decimal step)
        {
            int realDecimals = 0;

            string stepInStr = step.ToStringWithNoEndZero().Replace(",", ".");

            if (stepInStr.Split('.').Length > 1)
            {
                realDecimals = stepInStr.Split('.')[1].Length;

                if (realDecimals != decimals)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
