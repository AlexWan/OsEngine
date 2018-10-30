using System;
using System.Globalization;

namespace OsEngine.Market.Servers.Entity
{
    /// <summary>
    /// параметр для сервера
    /// </summary>
    public interface IServerParameter
    {
        string Name { get; set; }

        string GetStringToSave();

        void LoadFromStr(string str);

        ServerParameterType Type { get; }

        event Action ValueChange;
    }

    /// <summary>
    /// тип параметра для сервера
    /// </summary>
    public enum ServerParameterType
    {
        String,
        Bool,
        Int,
        Decimal,
        Path,
        Password
    }

    /// <summary>
    /// строковый параметр сервера
    /// </summary>
    public class ServerParameterString : IServerParameter
    {
        public string Name
        {
            get { return _name; }
            set { _name = value.Replace("^",""); }
        }

        private string _name;

        public string Value
        {
            get { return _value; }
            set
            {
                if (value == _value)
                {
                    return;
                }
                _value = value.Replace("^", "");
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }

        private string _value;

        public string GetStringToSave()
        {
            return Type + "^" + Name + "^" + Value;
        }

        public void LoadFromStr(string value)
        {
            string[] values = value.Split('^');
            _name = values[1];
            _value = values[2];
        }

        public ServerParameterType Type
        {
            get { return ServerParameterType.String; }
        }

        public event Action ValueChange;
    }

    /// <summary>
    /// децимал параметр сервера
    /// </summary>
    public class ServerParameterDecimal : IServerParameter
    {
        public string Name
        {
            get { return _name; }
            set { _name = value.Replace("^", ""); }
        }

        private string _name;

        public decimal Value
        {
            get { return _value; }
            set
            {
                if (value == _value)
                {
                    return;
                }
                _value = value;
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }

        private decimal _value;

        public string GetStringToSave()
        {
            return  Type + "^" + Name + "^" + Value.ToString(CultureInfo.InvariantCulture);
        }

        public void LoadFromStr(string value)
        {
            string[] values = value.Split('^');
            _name = values[1];
            _value = Convert.ToDecimal(values[2]);
        }

        public ServerParameterType Type
        {
            get { return ServerParameterType.Decimal; }
        }

        public event Action ValueChange;
    }

    /// <summary>
    /// интовый параметр сервера
    /// </summary>
    public class ServerParameterInt : IServerParameter
    {
        public string Name
        {
            get { return _name; }
            set { _name = value.Replace("^", ""); }
        }

        private string _name;

        public int Value
        {
            get { return _value; }
            set
            {
                if (value == _value)
                {
                    return;
                }
                _value = value;
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }

        private int _value;

        public string GetStringToSave()
        {
            return  Type + "^" + Name + "^" + Value;
        }

        public void LoadFromStr(string value)
        {
            string[] values = value.Split('^');
            _name = values[1];
            _value = Convert.ToInt32(values[2]);
        }

        public ServerParameterType Type
        {
            get { return ServerParameterType.Int; }
        }

        public event Action ValueChange;
    }

    /// <summary>
    /// белевый параметр сервера
    /// </summary>
    public class ServerParameterBool : IServerParameter
    {
        public string Name
        {
            get { return _name; }
            set { _name = value.Replace("^", ""); }
        }

        private string _name;

        public bool Value
        {
            get { return _value; }
            set
            {
                if (value == _value)
                {
                    return;
                }
                _value = value;
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }

        private bool _value;

        public string GetStringToSave()
        {
            return  Type + "^" + Name + "^" + Value;
        }

        public void LoadFromStr(string value)
        {
            string[] values = value.Split('^');
            _name = values[1];
            _value = Convert.ToBoolean(values[2]);
        }

        public ServerParameterType Type
        {
            get { return ServerParameterType.Bool; }
        }

        public event Action ValueChange;
    }

    /// <summary>
    /// парольный параметр сервера
    /// </summary>
    public class ServerParameterPassword : IServerParameter
    {
        public string Name
        {
            get { return _name; }
            set { _name = value.Replace("^", ""); }
        }

        private string _name;

        public string Value
        {
            get { return _value; }
            set
            {
                if (value == _value)
                {
                    return;
                }
                _value = value.Replace("^", "");
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }

        private string _value;

        public string GetStringToSave()
        {
            return Type + "^" + Name + "^" + Value;
        }

        public void LoadFromStr(string value)
        {
            string[] values = value.Split('^');
            _name = values[1];
            _value = values[2];
        }

        public ServerParameterType Type
        {
            get { return ServerParameterType.Password; }
        }

        public event Action ValueChange;
    }

    /// <summary>
    /// путь к папке параметр сервера
    /// </summary>
    public class ServerParameterPath : IServerParameter
    {
        public string Name
        {
            get { return _name; }
            set
            {
                _name = value.Replace("^", "");
            }
        }

        private string _name;

        public string Value
        {
            get { return _value; }
            set
            {
                if (value == _value)
                {
                    return;
                }
                _value = value.Replace("^", "");
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }

        private string _value;

        public string GetStringToSave()
        {
            return Type + "^" + Name + "^" + Value;
        }

        public void ShowPathDialog()
        {
            System.Windows.Forms.FolderBrowserDialog myDialog = new System.Windows.Forms.FolderBrowserDialog();
            myDialog.ShowDialog();

            if (myDialog.SelectedPath != "") // если хоть что-то выбрано
            {
                Value = myDialog.SelectedPath;
            }
        }

        public void LoadFromStr(string value)
        {
            string[] values = value.Split('^');
            _name = values[1];
            _value = values[2];
        }

        public ServerParameterType Type
        {
            get { return ServerParameterType.Path; }
        }

        public event Action ValueChange;
    }
}
