/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Разные базовые сути сеток:
// 1) Выход по всей сетке через общий профит и стоп. Как способ открытия позиции           // OpenPosition
// 2) Открытие сеткой / закрытие сеткой. Как способ открытия позиции и закрытия позиции    // Mirror
// 3) По каждому открытию отдельный выход. Как маркет-мейкинг инструмента в одну сторону.  // MarketMaking

// Какие бывают общие настройки у сеток
// Объём: Мартингейл / Равномерно
// Объём в: Контракты / Валюта контракта / Процент депозита
// Размер сетки: Равномерный / с мультипликатором
// Количество ордеров в рынке: int
// Шаг сетки указывать: Абсолют / Проценты
// Не торговые периоды: Временная блокировка по не торговым периодам + торговые дни + торговля по отведённому времени.
// Способ входа в логику: Раз в N секунд / На каждом трейде
// Автоочистка журнала: вкл/выкл / кол-во закрытых позиций в журнале

// Переход сетки в режим только закрытие:
// 1) Бесконечная (Циклическая). По умолчанию. Вообще не останавливается.
// 2) В количестве сработавших ордеров / 3) Движение вверх /
// 4) Движение вниз / 5) По времени

// Переход сетки в режим выключена
// 0) При отсутствии позиции в режиме "только закрытии". По умолчанию


namespace OsEngine.OsTrader.Grids
{
    public class TradeGrid
    {
        #region Service

        public TradeGrid(StartProgram startProgram, BotTabSimple tab)
        {
            Tab = tab;

        }

        public int Number;

        public BotTabSimple Tab;

        public string GetSaveString()
        {
            string result = "";

            result += Number + "@";
            result += GridType + "@";
            result += Regime + "@";

            return result;
        }

        public void LoadFromString(string value)
        {
            string[] values = value.Split('@');

            Number = Convert.ToInt32(values[0]);
            Enum.TryParse(values[1], out GridType);
            Enum.TryParse(values[1], out Regime);

        }

        public void Delete()
        {
            Tab = null;
        }

        #endregion

        #region Settings

        public TradeGridPrimeType GridType;

        public TradeGridRegime Regime;

        #endregion

        #region Grid lines creation and storage



        #endregion

        #region Trade logic



        #endregion

        #region Log

        public void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                ServerMaster.SendNewLogMessage(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public enum TradeGridPrimeType
    {
        OpenPosition,
        Mirror,
        MarketMaking,
    }

    public enum TradeGridCloseType
    {
        Cycle,
        ActivatedOrdersCount,
        MoveUp,
        MoveDown,
        ByTime
    }

    public enum TradeGridRegime
    {
        Off,
        On,
        CloseOnly,
        CloseForced
    }
}
