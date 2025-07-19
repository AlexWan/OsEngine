/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.AutoFollow
{

    public enum CopyTraderType
    {
        None,
        Portfolio,
        Robot
    }

    public class CopyTrader
    {
        public int Number;

        public string Name;

        public CopyTraderType Type;

        public bool IsOn;

        public string State;

        public string GetStringToSave()
        {
            string result = Number + "%";
            result += Name + "%";
            result += Type + "%";
            result += IsOn + "%";

            return result;
        }

        public void LoadFromString(string saveStr)
        {
            Number = Convert.ToInt32(saveStr.Split('%')[0]);
            Name = saveStr.Split('%')[1];
            Enum.TryParse(saveStr.Split('%')[2], out Type);
            IsOn = Convert.ToBoolean(saveStr.Split('%')[3]);
        }

        public void ClearDelete()
        {
            if(DeleteEvent != null)
            {
                DeleteEvent();
            }
        }

        public event Action DeleteEvent;
    }

}
