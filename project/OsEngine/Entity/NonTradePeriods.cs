/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using OsEngine.Market;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Documents;

namespace OsEngine.Entity
{
    public class NonTradePeriods
    {
        #region Service

        public NonTradePeriods(string name)
        {
            NameUnique = name + "nonTradePeriod";

            NonTradePeriodGeneral = new NonTradePeriodInDay();

            NonTradePeriodMonday = new NonTradePeriodInDay();
            NonTradePeriodTuesday = new NonTradePeriodInDay();
            NonTradePeriodWednesday = new NonTradePeriodInDay();
            NonTradePeriodThursday = new NonTradePeriodInDay();
            NonTradePeriodFriday = new NonTradePeriodInDay();
            NonTradePeriodSaturday = new NonTradePeriodInDay();
            NonTradePeriodSunday = new NonTradePeriodInDay();

            Load();
        }

        public string NameUnique;

        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameUnique + ".txt", false))
                {

                    List<string> array = GetFullSaveArray();

                    for(int i = 0;i < array.Count;i++)
                    {
                        writer.WriteLine(array[i]);
                    }

                    writer.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        public void Load()
        {
            if (!File.Exists(@"Engine\" + NameUnique + ".txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameUnique + ".txt"))
                {
                    LoadFromStringDays(reader.ReadLine());

                    NonTradePeriodGeneral.LoadFromString(reader.ReadLine());

                    if(reader.EndOfStream == false)
                    {
                        NonTradePeriodMonday.LoadFromString(reader.ReadLine());
                        NonTradePeriodTuesday.LoadFromString(reader.ReadLine());
                        NonTradePeriodWednesday.LoadFromString(reader.ReadLine());
                        NonTradePeriodThursday.LoadFromString(reader.ReadLine());
                        NonTradePeriodFriday.LoadFromString(reader.ReadLine());
                        NonTradePeriodSaturday.LoadFromString(reader.ReadLine());
                        NonTradePeriodSunday.LoadFromString(reader.ReadLine());
                    }

                    reader.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        public List<string> GetFullSaveArray()
        {
            List<string> array = new List<string>();

            array.Add(GetSaveStringDays());
            array.Add(NonTradePeriodGeneral.GetSaveString());
            array.Add(NonTradePeriodMonday.GetSaveString());
            array.Add(NonTradePeriodTuesday.GetSaveString());
            array.Add(NonTradePeriodWednesday.GetSaveString());
            array.Add(NonTradePeriodThursday.GetSaveString());
            array.Add(NonTradePeriodFriday.GetSaveString());
            array.Add(NonTradePeriodSaturday.GetSaveString());
            array.Add(NonTradePeriodSunday.GetSaveString());

            return array;
        }

        public void LoadFromSaveArray(List<string> array)
        {
            try
            {
                LoadFromStringDays(array[0]);

                NonTradePeriodGeneral.LoadFromString(array[1]);

                NonTradePeriodMonday.LoadFromString(array[2]);
                NonTradePeriodTuesday.LoadFromString(array[3]);
                NonTradePeriodWednesday.LoadFromString(array[4]);
                NonTradePeriodThursday.LoadFromString(array[5]);
                NonTradePeriodFriday.LoadFromString(array[6]);
                NonTradePeriodSaturday.LoadFromString(array[7]);
                NonTradePeriodSunday.LoadFromString(array[8]);
            }
            catch
            {
                // ignore
            }
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(@"Engine\" + NameUnique + ".txt"))
                {
                    File.Delete(@"Engine\" + NameUnique + ".txt");
                }
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Dialog window

        private NonTradePeriodsUi _ui;

        public void ShowDialog()
        {
            if(_ui != null )
            {
                if(_ui.WindowState == System.Windows.WindowState.Minimized)
                {
                    _ui.WindowState = System.Windows.WindowState.Normal;
                }
                _ui.Activate();
            }
            else
            {
                _ui = new NonTradePeriodsUi(this);
                _ui.Show();
                _ui.Closed += _ui_Closed;
            }

        }

        private void _ui_Closed(object sender, EventArgs e)
        {
            _ui = null;
        }

        #endregion

        #region Trading days

        public bool TradeInMonday = true;

        public bool TradeInTuesday = true;

        public bool TradeInWednesday = true;

        public bool TradeInThursday = true;

        public bool TradeInFriday = true;

        public bool TradeInSaturday = true;

        public bool TradeInSunday = true;

        public string GetSaveStringDays()
        {
            string result = "";

            result += TradeInMonday + "@";
            result += TradeInTuesday + "@";
            result += TradeInWednesday + "@";
            result += TradeInThursday + "@";
            result += TradeInFriday + "@";
            result += TradeInSaturday + "@";
            result += TradeInSunday + "@";
            result += "@";
            result += "@";
            result += "@";
            result += "@";
            result += "@"; // пять пустых полей в резерв

            return result;
        }

        public void LoadFromStringDays(string value)
        {
            try
            {
                string[] values = value.Split('@');

                TradeInMonday = Convert.ToBoolean(values[0]);
                TradeInTuesday = Convert.ToBoolean(values[1]);
                TradeInWednesday = Convert.ToBoolean(values[2]);
                TradeInThursday = Convert.ToBoolean(values[3]);
                TradeInFriday = Convert.ToBoolean(values[4]);
                TradeInSaturday = Convert.ToBoolean(values[5]);
                TradeInSunday = Convert.ToBoolean(values[6]);
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Non trade periods

        public NonTradePeriodInDay NonTradePeriodGeneral;

        public NonTradePeriodInDay NonTradePeriodMonday;

        public NonTradePeriodInDay NonTradePeriodTuesday;

        public NonTradePeriodInDay NonTradePeriodWednesday;

        public NonTradePeriodInDay NonTradePeriodThursday;

        public NonTradePeriodInDay NonTradePeriodFriday;

        public NonTradePeriodInDay NonTradePeriodSaturday;

        public NonTradePeriodInDay NonTradePeriodSunday;

        #endregion

        #region Logic

        public bool CanTradeThisTime(DateTime curTime)
        {
            // Периоды

            if(NonTradePeriodGeneral.CanTradeThisTime(curTime) == false)
            {
                return false;
            }

            if (curTime.DayOfWeek == DayOfWeek.Monday &&
                NonTradePeriodMonday.CanTradeThisTime(curTime) == false)
            {
                return false;
            }

            if (curTime.DayOfWeek == DayOfWeek.Tuesday && 
                NonTradePeriodTuesday.CanTradeThisTime(curTime) == false)
            {
                return false;
            }

            if (curTime.DayOfWeek == DayOfWeek.Wednesday && 
                NonTradePeriodWednesday.CanTradeThisTime(curTime) == false)
            {
                return false;
            }

            if (curTime.DayOfWeek == DayOfWeek.Thursday && 
                NonTradePeriodThursday.CanTradeThisTime(curTime) == false)
            {
                return false;
            }

            if (curTime.DayOfWeek == DayOfWeek.Friday && 
                NonTradePeriodFriday.CanTradeThisTime(curTime) == false)
            {
                return false;
            }

            if (curTime.DayOfWeek == DayOfWeek.Saturday && 
                NonTradePeriodSaturday.CanTradeThisTime(curTime) == false)
            {
                return false;
            }

            if (curTime.DayOfWeek == DayOfWeek.Sunday && 
                NonTradePeriodSunday.CanTradeThisTime(curTime) == false)
            {
                return false;
            }


            // дни

            if (TradeInMonday == false
              && curTime.DayOfWeek == DayOfWeek.Monday)
            {
                return false;
            }

            if (TradeInTuesday == false
                && curTime.DayOfWeek == DayOfWeek.Tuesday)
            {
                return false;
            }

            if (TradeInWednesday == false
                && curTime.DayOfWeek == DayOfWeek.Wednesday)
            {
                return false;
            }

            if (TradeInThursday == false
                && curTime.DayOfWeek == DayOfWeek.Thursday)
            {
                return false;
            }

            if (TradeInFriday == false
                && curTime.DayOfWeek == DayOfWeek.Friday)
            {
                return false;
            }

            if (TradeInSaturday == false
                && curTime.DayOfWeek == DayOfWeek.Saturday)
            {
                return false;
            }

            if (TradeInSunday == false
                && curTime.DayOfWeek == DayOfWeek.Sunday)
            {
                return false;
            }

            return true;
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

    public class NonTradePeriodInDay
    {
        public NonTradePeriodInDay()
        {
            NonTradePeriod1Start = new TimeOfDay();
            NonTradePeriod1End = new TimeOfDay() { Hour = 7, Minute = 0 };

            NonTradePeriod2Start = new TimeOfDay() { Hour = 9, Minute = 0 };
            NonTradePeriod2End = new TimeOfDay() { Hour = 10, Minute = 5 };

            NonTradePeriod3Start = new TimeOfDay() { Hour = 13, Minute = 54 };
            NonTradePeriod3End = new TimeOfDay() { Hour = 14, Minute = 6 };

            NonTradePeriod4Start = new TimeOfDay() { Hour = 18, Minute = 40 };
            NonTradePeriod4End = new TimeOfDay() { Hour = 19, Minute = 6 };

            NonTradePeriod5Start = new TimeOfDay() { Hour = 23, Minute = 40 };
            NonTradePeriod5End = new TimeOfDay() { Hour = 23, Minute = 59 };
        }

        public bool NonTradePeriod1OnOff;
        public TimeOfDay NonTradePeriod1Start;
        public TimeOfDay NonTradePeriod1End;

        public bool NonTradePeriod2OnOff;
        public TimeOfDay NonTradePeriod2Start;
        public TimeOfDay NonTradePeriod2End;

        public bool NonTradePeriod3OnOff;
        public TimeOfDay NonTradePeriod3Start;
        public TimeOfDay NonTradePeriod3End;

        public bool NonTradePeriod4OnOff;
        public TimeOfDay NonTradePeriod4Start;
        public TimeOfDay NonTradePeriod4End;

        public bool NonTradePeriod5OnOff;
        public TimeOfDay NonTradePeriod5Start;
        public TimeOfDay NonTradePeriod5End;

        public string GetSaveString()
        {
            string result = "";

            result += NonTradePeriod1OnOff + "@";
            result += NonTradePeriod1Start + "@";
            result += NonTradePeriod1End + "@";
            result += NonTradePeriod2OnOff + "@";
            result += NonTradePeriod2Start + "@";
            result += NonTradePeriod2End + "@";
            result += NonTradePeriod3OnOff + "@";
            result += NonTradePeriod3Start + "@";
            result += NonTradePeriod3End + "@";
            result += NonTradePeriod4OnOff + "@";
            result += NonTradePeriod4Start + "@";
            result += NonTradePeriod4End + "@";
            result += NonTradePeriod5OnOff + "@";
            result += NonTradePeriod5Start + "@";
            result += NonTradePeriod5End + "@";

            result += "@";
            result += "@";
            result += "@";
            result += "@";
            result += "@"; // пять пустых полей в резерв

            return result;
        }

        public void LoadFromString(string value)
        {
            try
            {
                string[] values = value.Split('@');

                NonTradePeriod1OnOff = Convert.ToBoolean(values[0]);
                NonTradePeriod1Start.LoadFromString(values[1]);
                NonTradePeriod1End.LoadFromString(values[2]);

                NonTradePeriod2OnOff = Convert.ToBoolean(values[3]);
                NonTradePeriod2Start.LoadFromString(values[4]);
                NonTradePeriod2End.LoadFromString(values[5]);

                NonTradePeriod3OnOff = Convert.ToBoolean(values[6]);
                NonTradePeriod3Start.LoadFromString(values[7]);
                NonTradePeriod3End.LoadFromString(values[8]);

                NonTradePeriod4OnOff = Convert.ToBoolean(values[9]);
                NonTradePeriod4Start.LoadFromString(values[10]);
                NonTradePeriod4End.LoadFromString(values[11]);

                NonTradePeriod5OnOff = Convert.ToBoolean(values[12]);
                NonTradePeriod5Start.LoadFromString(values[13]);
                NonTradePeriod5End.LoadFromString(values[14]);

            }
            catch
            {
                // ignore
            }
        }

        public bool CanTradeThisTime(DateTime curTime)
        {
            if (NonTradePeriod1OnOff == true)
            {
                if (NonTradePeriod1Start < curTime
                 && NonTradePeriod1End > curTime)
                {
                    return false;
                }
            }

            if (NonTradePeriod2OnOff == true)
            {
                if (NonTradePeriod2Start < curTime
                 && NonTradePeriod2End > curTime)
                {
                    return false;
                }
            }

            if (NonTradePeriod3OnOff == true)
            {
                if (NonTradePeriod3Start < curTime
                 && NonTradePeriod3End > curTime)
                {
                    return false;
                }
            }

            if (NonTradePeriod4OnOff == true)
            {
                if (NonTradePeriod4Start < curTime
                 && NonTradePeriod4End > curTime)
                {
                    return false;
                }
            }

            if (NonTradePeriod5OnOff == true)
            {
                if (NonTradePeriod5Start < curTime
                 && NonTradePeriod5End > curTime)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
