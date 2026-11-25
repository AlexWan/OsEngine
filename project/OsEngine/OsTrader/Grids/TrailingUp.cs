/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace OsEngine.OsTrader.Grids
{
    public class TrailingUp
    {
        #region Service

        public TrailingUp(TradeGrid grid)
        {
            _grid = grid;
        }

        protected TradeGrid _grid;

        public void Delete()
        {
            _grid = null;
        }

        public bool TrailingUpIsOn;

        public decimal TrailingUpStep;

        public decimal TrailingUpLimit;

        public bool TrailingUpCanMoveExitOrder;

        public bool TrailingDownIsOn;

        public decimal TrailingDownStep;

        public decimal TrailingDownLimit;

        public bool TrailingDownCanMoveExitOrder;

        // Признаки процентного режима. По умолчанию false — поля Step/Limit читаются как раньше, в абсолютных ценах

        public bool TrailingUpStepIsPercent;

        public bool TrailingUpLimitIsPercent;

        public bool TrailingDownStepIsPercent;

        public bool TrailingDownLimitIsPercent;

        // Рантайм-анкоры процентного лимита: крайняя цена направления на момент включения лимита.
        // Сериализуются в 14-польную строку сегмента, чтобы коридор переживал перезапуск

        private decimal _trailingUpLimitAnchor;

        private decimal _trailingDownLimitAnchor;

        // Теневые поля ленивого детекта включения лимита: хранят прошлое состояние,
        // переход false→true видим только на проверке — на нём анкор снимается заново.
        // LoadFromString выставляет их по загруженному флагу, иначе первая проверка
        // после загрузки ложно детектировала бы переход и молча пересняла анкор

        private bool _upLimitPctWasOn;

        private bool _downLimitPctWasOn;

        // Транзиентный сигнал: анкор переснят, сегмент надо сохранить.
        // Единственный потребитель — TradeGrid.cs:1091, он добирает сигнал в _needToSave
        // и сбрасывает его после передачи. Из потока проверок Save не зовём

        public bool AnchorRefreshedNeeded;

        public virtual string GetSaveString()
        {
            string result = "";

            result += TrailingUpIsOn + "@";
            result += TrailingUpStep + "@";
            result += TrailingUpLimit + "@";

            result += TrailingDownIsOn + "@";
            result += TrailingDownStep + "@";
            result += TrailingDownLimit + "@";
            result += TrailingUpCanMoveExitOrder + "@";
            result += TrailingDownCanMoveExitOrder + "@";
            result += TrailingUpStepIsPercent + "@";
            result += TrailingUpLimitIsPercent + "@";
            result += TrailingDownStepIsPercent + "@";
            result += TrailingDownLimitIsPercent + "@";
            // анкоры — машинные значения цены, пишены строго InvariantCulture.
            // Старые decimal-поля выше не трогаем: они пользовательский ввод, default culture.
            // Хвостовой @ после 13-го слота не пишем: Split('@') обязан давать ровно 14 полей
            result += _trailingUpLimitAnchor.ToString(CultureInfo.InvariantCulture) + "@";
            result += _trailingDownLimitAnchor.ToString(CultureInfo.InvariantCulture); // шесть резервных полей: bool-признаки режимов 8-11, анкоры 12-13

            return result;
        }

        public virtual void LoadFromString(string value)
        {
            try
            {
                string[] values = value.Split('@');

                TrailingUpIsOn = Convert.ToBoolean(values[0]);
                TrailingUpStep = values[1].ToDecimal();
                TrailingUpLimit = values[2].ToDecimal();

                TrailingDownIsOn = Convert.ToBoolean(values[3]);
                TrailingDownStep = values[4].ToDecimal();
                TrailingDownLimit = values[5].ToDecimal();

                // guard по длине: укороченные строки легаси сохраняют дефолты вместо IndexOutOfRange в общем catch

                if (values.Length > 6 && string.IsNullOrEmpty(values[6]) == false)
                {
                    TrailingUpCanMoveExitOrder = Convert.ToBoolean(values[6]);
                }
                if (values.Length > 7 && string.IsNullOrEmpty(values[7]) == false)
                {
                    TrailingDownCanMoveExitOrder = Convert.ToBoolean(values[7]);
                }
                if (values.Length > 8 && string.IsNullOrEmpty(values[8]) == false)
                {
                    TrailingUpStepIsPercent = Convert.ToBoolean(values[8]);
                }
                if (values.Length > 9 && string.IsNullOrEmpty(values[9]) == false)
                {
                    TrailingUpLimitIsPercent = Convert.ToBoolean(values[9]);
                }
                if (values.Length > 10 && string.IsNullOrEmpty(values[10]) == false)
                {
                    TrailingDownStepIsPercent = Convert.ToBoolean(values[10]);
                }
                if (values.Length > 11 && string.IsNullOrEmpty(values[11]) == false)
                {
                    TrailingDownLimitIsPercent = Convert.ToBoolean(values[11]);
                }
                // анкоры читаются в любой культуре с точкой — пара к InvariantCulture в GetSaveString.
                // NumberStyles.Float без AllowThousands: «1,100» не должен тихо становиться 1100
                if (values.Length > 12 && string.IsNullOrEmpty(values[12]) == false)
                {
                    _trailingUpLimitAnchor = decimal.Parse(values[12], NumberStyles.Float, CultureInfo.InvariantCulture);
                }
                if (values.Length > 13 && string.IsNullOrEmpty(values[13]) == false)
                {
                    _trailingDownLimitAnchor = decimal.Parse(values[13], NumberStyles.Float, CultureInfo.InvariantCulture);
                }

                _upLimitPctWasOn = TrailingUpLimitIsPercent;
                _downLimitPctWasOn = TrailingDownLimitIsPercent;
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Logic

        /// <summary>
        /// Effective step of percent mode: percent of the direction's current extreme grid price
        /// </summary>
        /// <param name="basePrice">extreme grid price of the direction</param>
        /// <param name="percent">step in percent</param>
        public static decimal StepFromPercent(decimal basePrice, decimal percent)
        {
            return basePrice * percent / 100m;
        }

        /// <summary>
        /// Effective limit of percent mode: wall (Up) or floor (Down) at percent from the anchor —
        /// the grid position when the percent limit was enabled
        /// </summary>
        /// <param name="anchor">limit anchor</param>
        /// <param name="percent">limit in percent</param>
        /// <param name="isUp">direction</param>
        public static decimal LimitFromPercent(decimal anchor, decimal percent, bool isUp)
        {
            if (isUp)
            {
                return anchor * (1m + percent / 100m);
            }

            return anchor * (1m - percent / 100m);
        }

        public virtual bool TryTrailingGrid()
        {
            if (TrailingUpIsOn == false
                && TrailingDownIsOn == false)
            {
                return false;
            }

            List<Candle> candles = _grid.Tab.CandlesAll;

            if (candles == null
                || candles.Count == 0)
            {
                return false;
            }

            decimal lastPrice = candles[candles.Count - 1].Close;

            if (lastPrice == 0)
            {
                return false;
            }

            bool trailUpIsDone = false;
            bool trailDownIsDone = false;

            if (TrailingUpIsOn == true
                && TrailingUpStep != 0 
                && TrailingUpLimit != 0)
            {
                trailUpIsDone = TrailingUpMethod(lastPrice);
            }

            if (TrailingDownIsOn == true
                 && TrailingDownStep != 0
                && TrailingDownLimit != 0)
            {
                trailDownIsDone = TrailingDownMethod(lastPrice);
            }

            if(trailUpIsDone == true 
                || trailDownIsDone == true)
            {
                return true;
            }

            return false;
        }

        private bool TrailingUpMethod(decimal lastPrice)
        {
            decimal maxPriceGrid = MaxGridPrice;

            if(maxPriceGrid == 0)
            {
                return false;
            }

            // % of current extreme grid price, recalculated on every check

            decimal step = TrailingUpStepIsPercent
                ? StepFromPercent(maxPriceGrid, TrailingUpStep)
                : TrailingUpStep;

            // effective limit: in percent mode it is the wall from the anchor, the absolute guards below use it

            decimal upLimit = TrailingUpLimit;

            if (TrailingUpLimitIsPercent)
            {
                // ленивый пересъём при включении: снятие чекбокса видно только на прошлой проверке

                if (_upLimitPctWasOn == false)
                {
                    _upLimitPctWasOn = true;
                    _trailingUpLimitAnchor = maxPriceGrid;
                    AnchorRefreshedNeeded = true;
                }

                if (_trailingUpLimitAnchor <= 0)
                {
                    // Нулевой анкор: Save с flag=true до первой проверки записал анкор 0; после загрузки
                    // wasOn=true и переход false→true невозможен — лениво снимаем анкор с текущей крайней,
                    // иначе %-лимит после перезапуска мёртв молча навсегда. Крайняя здесь уже >0
                    _trailingUpLimitAnchor = maxPriceGrid;
                    _upLimitPctWasOn = true;
                    AnchorRefreshedNeeded = true;

                    SendNewLogMessage(OsLocalization.Trader.Label787, LogMessageType.System);
                }

                // stale-anchor rule before the guard comparisons: the extreme is already at or beyond
                // the wall — external grid move (AutoStarter rebuild) or shrunk Limit%. Anchor is
                // re-taken from the current extreme, otherwise the wall gives silent dead trailing

                upLimit = LimitFromPercent(_trailingUpLimitAnchor, TrailingUpLimit, true);

                if (maxPriceGrid >= upLimit)
                {
                    _trailingUpLimitAnchor = maxPriceGrid;
                    upLimit = maxPriceGrid;
                    AnchorRefreshedNeeded = true;

                    SendNewLogMessage(OsLocalization.Trader.Label787, LogMessageType.System);
                }
            }
            else
            {
                // тень сбрасывается на проверке с выключенным флагом, иначе следующее включение
                // не даст перехода false→true и анкор остался бы от старого коридора
                _upLimitPctWasOn = false;
            }

            if(lastPrice < maxPriceGrid)
            {
                return false;
            }

            if(maxPriceGrid >= upLimit)
            {
                return false;
            }

            decimal different = lastPrice - maxPriceGrid;

            if (different < step)
            {
                return false;
            }

            if (maxPriceGrid + different >= upLimit)
            {
                return false;
            }

            int stepsToUp = Convert.ToInt32(Math.Round(different / step, 0));

            decimal upValue = stepsToUp * step;

            ShiftGridUpOnValue(upValue);

            return true;
        }

        private bool TrailingDownMethod(decimal lastPrice)
        {
            decimal minPriceGrid = MinGridPrice;

            if (minPriceGrid == 0)
            {
                return false;
            }

            // % of current extreme grid price, recalculated on every check

            decimal step = TrailingDownStepIsPercent
                ? StepFromPercent(minPriceGrid, TrailingDownStep)
                : TrailingDownStep;

            // effective limit: in percent mode it is the floor from the anchor, the absolute guards below use it

            decimal downLimit = TrailingDownLimit;

            if (TrailingDownLimitIsPercent)
            {
                // ленивый пересъём при включении: снятие чекбокса видно только на прошлой проверке

                if (_downLimitPctWasOn == false)
                {
                    _downLimitPctWasOn = true;
                    _trailingDownLimitAnchor = minPriceGrid;
                    AnchorRefreshedNeeded = true;
                }

                if (_trailingDownLimitAnchor <= 0)
                {
                    // Зеркало Up: ленивый съём нуля с текущей крайней, иначе %-пол мёртв после перезапуска
                    _trailingDownLimitAnchor = minPriceGrid;
                    _downLimitPctWasOn = true;
                    AnchorRefreshedNeeded = true;

                    SendNewLogMessage(OsLocalization.Trader.Label787, LogMessageType.System);
                }

                // stale-anchor rule before the guard comparisons: the extreme is already at or below
                // the floor — external grid move (AutoStarter rebuild) or shrunk Limit%. Anchor is
                // re-taken from the current extreme, otherwise the floor gives silent dead trailing

                downLimit = LimitFromPercent(_trailingDownLimitAnchor, TrailingDownLimit, false);

                if (minPriceGrid <= downLimit)
                {
                    _trailingDownLimitAnchor = minPriceGrid;
                    downLimit = minPriceGrid;
                    AnchorRefreshedNeeded = true;

                    SendNewLogMessage(OsLocalization.Trader.Label787, LogMessageType.System);
                }
            }
            else
            {
                // зеркало Up: тень сбрасывается на проверке с выключенным флагом
                _downLimitPctWasOn = false;
            }

            if (lastPrice > minPriceGrid)
            {
                return false;
            }

            if (minPriceGrid <= downLimit)
            {
                return false;
            }

            decimal different = minPriceGrid - lastPrice;

            if (different < step)
            {
                return false;
            }

            if (minPriceGrid - different <= downLimit)
            {
                return false;
            }

            int stepsToDown = Convert.ToInt32(Math.Round(different / step,0));

            decimal downValue = stepsToDown * step;

            ShiftGridDownOnValue(downValue);

            return true;
        }

        public decimal MaxGridPrice
        {
            get
            {
                List<TradeGridLine> lines = _grid.GridCreator.Lines;

                if (lines == null || lines.Count == 0)
                {
                    return 0;
                }

                decimal maxPriceGrid = decimal.MinValue;

                for(int i = 0;i < lines.Count;i++)
                {
                    if (lines[i].PriceEnter >  maxPriceGrid)
                    {
                        maxPriceGrid = lines[i].PriceEnter;
                    }
                }

                if(maxPriceGrid ==  decimal.MinValue)
                {
                    return 0;
                }

                return maxPriceGrid;
            }
        }

        public decimal MinGridPrice
        {
            get
            {
                List<TradeGridLine> lines = _grid.GridCreator.Lines;

                if (lines == null || lines.Count == 0)
                {
                    return 0;
                }

                decimal minPriceGrid = decimal.MaxValue;

                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].PriceEnter < minPriceGrid)
                    {
                        minPriceGrid = lines[i].PriceEnter;
                    }
                }

                if (minPriceGrid == decimal.MaxValue)
                {
                    return 0;
                }

                return minPriceGrid;
            }
        }

        public void ShiftGridDownOnValue(decimal value)
        {
            List<TradeGridLine> lines = _grid.GridCreator.Lines;

            if (lines == null || lines.Count == 0)
            {
                return;
            }

            for(int i = 0;i < lines.Count;i++)
            {
                TradeGridLine line = lines[i];
                line.CanReplaceExitOrder = TrailingDownCanMoveExitOrder;
                line.PriceEnter -= value;
                line.PriceExit -= value;
            }
        }

        public void ShiftGridUpOnValue(decimal value)
        {
            List<TradeGridLine> lines = _grid.GridCreator.Lines;

            if (lines == null || lines.Count == 0)
            {
                return;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                TradeGridLine line = lines[i];
                line.CanReplaceExitOrder = TrailingUpCanMoveExitOrder;
                line.PriceEnter += value;
                line.PriceExit += value;
            }
        }

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
}
