/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Charts;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.OsTrader.Panels.Tab.Internal;

namespace OsEngine.OsTrader.Panels.Tab
{

    /// <summary>
    /// вкладка прорисовывающая спред двух свечных данных в виде свечного графика
    /// </summary>
    public class BotTabIndex : IIBotTab
    {

        public BotTabIndex(string name, StartProgram  startProgram)
        {
            TabName = name;
            _startProgram = startProgram;

            Tabs = new List<ConnectorCandles>();
            _valuesToFormula = new List<ValueSave>();
            _chartMaster = new ChartMaster(TabName, _startProgram);

            Load();
        }

        /// <summary>
        /// программа создавшая робота
        /// </summary>
        private StartProgram _startProgram;


        /// <summary>
        /// чарт для прорисовки
        /// </summary>
        private ChartMaster _chartMaster;

        /// <summary>
        /// Массив для хранения списка интсрументов
        /// </summary>
        public List<ConnectorCandles> Tabs;

 // управление

        /// <summary>
        /// вызвать окно управления
        /// </summary>
        public void ShowDialog()
        {
            BotTabCandleSpreadUi ui = new BotTabCandleSpreadUi(this);
            ui.ShowDialog();

            if (Tabs.Count != 0)
            {
                _chartMaster.SetNewSecurity("Index on: " + _userFormula, Tabs[0].TimeFrameBuilder, null, Tabs[0].ServerType);
            }
            else
            {
                _chartMaster.Clear();
                
            }
        }

        /// <summary>
        /// покаазать окно настроек коннектора по индексу
        /// </summary>
        public void ShowIndexConnectorIndexDialog(int index)
        {
            Tabs[index].ShowDialog();
            Save();
        }

        /// <summary>
        /// Добавить новую бумагу в список
        /// </summary>
        public void ShowNewSecurityDialog()
        {
            CreateNewSecurityConnector();
            Tabs[Tabs.Count - 1].ShowDialog();
            Save();
        }

        /// <summary>
        /// сделать новый адаптер для подключения данных
        /// </summary>
        public void CreateNewSecurityConnector()
        {
            ConnectorCandles connector = new ConnectorCandles(TabName + Tabs.Count, _startProgram);
            Tabs.Add(connector);
            Tabs[Tabs.Count - 1].NewCandlesChangeEvent += BotTabIndex_NewCandlesChangeEvent;
        }

        /// <summary>
        /// Удалить выбранную бумагу из списка
        /// </summary>
        public void DeleteSecurityTab(int index)
        {
            if (Tabs == null || Tabs.Count <= index)
            {
                return;
            }
            Tabs[index].NewCandlesChangeEvent -= BotTabIndex_NewCandlesChangeEvent;
            Tabs[index].Delete();
            Tabs.RemoveAt(index);

            Save();
        }

        /// <summary>
        /// начать прорисовку этого робота
        /// </summary> 
        public void StartPaint(WindowsFormsHost host, Rectangle rectangle)
        {
            _chartMaster.StartPaint(host,rectangle);
        }

        /// <summary>
        /// остановить прорисовку этого робота
        /// </summary>
        public void StopPaint()
        {
            _chartMaster.StopPaint();
        }

        /// <summary>
        /// уникальное имя робота. Передаётся в конструктор. Участвует в процессе сохранения всех данных связанных с ботом
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// номер вкладки
        /// </summary>
        public int TabNum { get; set; }

        /// <summary>
        /// очистить журнал и графики
        /// </summary>
        public void Clear()
        {
            _valuesToFormula = new List<ValueSave>();
            _chartMaster.Clear();
        }

        /// <summary>
        /// сохранить настройки в файл
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + TabName + @"SpreadSet.txt", false))
                {
                    string save = "";
                    for (int i = 0; i < Tabs.Count; i++)
                    {
                        save += Tabs[i].UniqName + "#";
                    }
                    writer.WriteLine(save);

                    writer.WriteLine(_userFormula);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// загрузить настройки из файла
        /// </summary>
        public void Load()
        {
            if (!File.Exists(@"Engine\" + TabName + @"SpreadSet.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + TabName + @"SpreadSet.txt"))
                {
                    string[] save2 = reader.ReadLine().Split('#');
                    for (int i = 0; i < save2.Length - 1; i++)
                    {
                        Tabs.Add(new ConnectorCandles(save2[i], _startProgram));
                        Tabs[Tabs.Count - 1].NewCandlesChangeEvent += BotTabIndex_NewCandlesChangeEvent;
                    }
                    UserFormula = reader.ReadLine();

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// удалить робота и все дочерние структуры
        /// </summary>
        public void Delete()
        {
            _chartMaster.Delete();

            if (File.Exists(@"Engine\" + TabName + @"SpreadSet.txt"))
            {
                File.Delete(@"Engine\" + TabName + @"SpreadSet.txt");
            }
        }

        /// <summary>
        /// подключена ли вкладка на скачивание данных
        /// </summary>
        public bool IsConnected
        {
            get
            {
                if (Tabs == null)
                {
                    return false;
                }
                for (int i = 0; i < Tabs.Count; i++)
                {
                    if (Tabs[i].IsConnected == false)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// из коннектора пришли новые данные
        /// </summary>
        private void BotTabIndex_NewCandlesChangeEvent(List<Candle> candles)
        {
            for (int i = 0; i < Tabs.Count; i++)
            {
                List<Candle> myCandles = Tabs[i].Candles(true);
                if (myCandles == null || myCandles.Count < 10)
                {
                    return;
                }
            }

            DateTime time = Tabs[0].Candles(true)[Tabs[0].Candles(true).Count - 1].TimeStart;

            for (int i = 0; i < Tabs.Count; i++)
            {
                List<Candle> myCandles = Tabs[i].Candles(true);
                if (myCandles[myCandles.Count - 1].TimeStart != time)
                {
                    return;
                }
            }
            // цикл для сбоа всех свечей в один массив

            if (string.IsNullOrWhiteSpace(ConvertedFormula))
            {
                return;
            }

            string nameArray = Calculate(ConvertedFormula);

            if (_valuesToFormula != null && !string.IsNullOrWhiteSpace(nameArray))
            {
                ValueSave val = _valuesToFormula.Find(v => v.Name == nameArray);

                if (val != null)
                {
                    Candles = val.ValueCandles;

                    if (Candles[Candles.Count - 1].TimeStart == Candles[Candles.Count - 2].TimeStart)
                    {
                        Candles.RemoveAt(Candles.Count - 1);
                    }

                    _chartMaster.SetCandles(Candles);

                    if (SpreadChangeEvent != null)
                    {
                        SpreadChangeEvent(Candles);
                    }
                }
            }
        }

        /// <summary>
        /// свечи спреда
        /// </summary>
        public List<Candle> Candles;

        /// <summary>
        /// спред изменился
        /// </summary>
        public event Action<List<Candle>> SpreadChangeEvent;

// расчёт индекса

        /// <summary>
        /// формула пользователя
        /// </summary>
        public string UserFormula
        {
            get { return _userFormula; }
            set
            {
                if (_userFormula == value)
                {
                    return;
                }
                _userFormula = value;
                Save();

                _valuesToFormula = new List<ValueSave>();
                Candles = new List<Candle>();
                _chartMaster.Clear();

                ConvertedFormula = ConvertFormula(_userFormula);

                string nameArray = Calculate(ConvertedFormula);

                if (_valuesToFormula != null && !string.IsNullOrWhiteSpace(nameArray))
                {
                    ValueSave val = _valuesToFormula.Find(v => v.Name == nameArray);

                    if (val != null)
                    {
                        Candles = val.ValueCandles;

                        _chartMaster.SetCandles(Candles);

                        if (SpreadChangeEvent != null)
                        {
                            SpreadChangeEvent(Candles);
                        }
                    }
                }
            }
        }
        private string _userFormula;

        /// <summary>
        /// формула приведённая к формату программы
        /// </summary>
        public string ConvertedFormula;

        /// <summary>
        /// массив объектов для хранения промежуточных массивов свечей
        /// </summary>
        private List<ValueSave> _valuesToFormula;

        /// <summary>
        /// проверить формулу на ошибки и привести к виду программы
        /// </summary>
        public string ConvertFormula(string formula)
        {
            if (string.IsNullOrEmpty(formula))
            {
                return "";
            }

            // удаляем пробелы

            formula = formula.Replace(" ", "");

            // проверяем формулу на валиндность

            for (int i = 0; i < formula.Length; i++)
            {
                if (formula[i] != '/' && formula[i] !=  '*' && formula[i] !=  '+' && formula[i] !=  '-'
                    && formula[i] != '(' && formula[i] != ')' && formula[i] != 'A' && formula[i] != '1' && formula[i] != '0'
                    && formula[i] !=  '2' && formula[i] !=  '3' && formula[i] !=  '4' && formula[i] !=  '5'
                    && formula[i] !=  '6' && formula[i] !=  '7' && formula[i] !=  '8' && formula[i] !=  '9')
                { // непонятные символы
                    SendNewLogMessage("Не удалось форматировать строку, т.к. в ней запрещённые символы",LogMessageType.Error);
                    return "";
                }
            }

            for (int i = 1; i < formula.Length; i++)
            {
                if ((formula[i] == '/' || formula[i] == '*' || formula[i] == '+' || formula[i] == '-') &&
                    (formula[i - 1] == '/' || formula[i - 1] == '*' || formula[i - 1] == '+' || formula[i - 1] == '-'))
                { // два знака подряд
                    SendNewLogMessage("Не удалось форматировать строку, т.к. в ней запрещённые символы",
                        LogMessageType.Error);
                    return "";
                }
            }

            return formula;
        }

        /// <summary>
        /// пересчитать массив
        /// </summary>
        /// <param name="formula">формула</param>
        /// <returns>название массива в котором лежит финальное значение</returns>
        public string Calculate(string formula)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(formula))
                {
                    return "";
                }

                string inside = "";
                string s = formula;
                int startindex = -1;
                int finishindex;

                // 1 разбиваем на скобки
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i] == '(')
                    {
                        startindex = i;
                        inside = "";
                    }
                    else if (s[i] == ')' && startindex != -1)
                    {
                        finishindex = i;

                        string partOne = "";
                        string partTwo = "";

                        for (int j = 0; j < startindex; j++)
                        {
                            partOne += s[j];
                        }
                        for (int j = finishindex + 1; j < s.Length; j++)
                        {
                            partTwo += s[j];
                        }

                        return Calculate(partOne + Calculate(inside) + partTwo);
                    }
                    else if (startindex != -1)
                    {
                        inside += s[i];
                    }
                }

                // 2 разбить на два значения

                bool haveDevide = false;
                int znakCount = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i] == '/' || s[i] == '*')
                    {
                        haveDevide = true;
                    }
                    if (s[i] == '/' || s[i] == '*' || s[i] == '-' || s[i] == '+')
                    {
                        znakCount++;
                    }
                }

                if (znakCount > 1 && haveDevide)
                {
                    int indexStart = 0;
                    int indexEnd = s.Length;

                    bool devadeFound = false;

                    for (int i = 0; i < s.Length; i++)
                    {
                        if (devadeFound == false &&
                            (s[i] != '/' && s[i] != '*' && s[i] != '-' && s[i] != '+'))
                        {
                            continue;
                        }
                        else if (devadeFound == false &&
                                 (s[i] == '-' || s[i] == '+'))
                        {
                            indexStart = i + 1;
                        }
                        else if (devadeFound == false &&
                                 (s[i] == '*' || s[i] == '/'))
                        {
                            devadeFound = true;
                        }
                        else if (devadeFound == true &&
                                 (s[i] != '/' && s[i] != '*' && s[i] != '-' && s[i] != '+'))
                        {

                        }
                        else if (devadeFound == true &&
                                 (s[i] == '/' || s[i] == '*' || s[i] == '-' || s[i] == '+'))
                        {
                            indexEnd = i;
                            break;
                        }
                    }

                    string partOne = "";
                    string partTwo = "";
                    string value = "";

                    for (int i = 0; i < s.Length; i++)
                    {
                        if (i < indexStart)
                        {
                            partOne += s[i];
                        }
                        else if (i >= indexStart && i < indexEnd)
                        {
                            value += s[i];
                        }
                        else if (i >= indexEnd)
                        {
                            partTwo += s[i];
                        }
                    }

                    string result = partOne + Calculate(value) + partTwo;

                    return Calculate(result);
                }
                else if (znakCount > 1 && haveDevide == false)
                {
                    int indexStart = 0;
                    int indexEnd = s.Length;

                    bool devadeFound = false;

                    for (int i = 0; i < s.Length; i++)
                    {
                        if (indexStart == 0 &&
                            (s[i] == '/' || s[i] == '*' || s[i] == '-' || s[i] == '+'))
                        {
                            indexStart = i;
                            continue;
                        }
                        if (indexStart == 0)
                        {
                            continue;
                        }

                        if (devadeFound == false &&
                            (s[i] == '/' || s[i] == '*' || s[i] == '-' || s[i] == '+'))
                        {
                            devadeFound = true;
                        }
                        else if (devadeFound == true &&
                                 (s[i] == '/' || s[i] == '*' || s[i] == '-' || s[i] == '+'))
                        {
                            indexEnd = i;
                            break;
                        }
                    }

                    string partOne = "";
                    string partTwo = "";
                    string value = "";

                    for (int i = 0; i < s.Length; i++)
                    {
                        if (i <= indexStart)
                        {
                            partOne += s[i];
                        }
                        else if (i > indexStart && i < indexEnd)
                        {
                            value += s[i];
                        }
                        else if (i >= indexEnd)
                        {
                            partTwo += s[i];
                        }
                    }

                    string result = partOne + Calculate(value) + partTwo;

                    return Calculate(result);
                }


                // поиск переменных

                string valueOne = "";
                string valueTwo = "";
                string znak = "";

                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i] == '/' || s[i] == '*' || s[i] == '-' || s[i] == '+')
                    {
                        znak += s[i];
                    }
                    else if (znak == "")
                    {
                        valueOne += s[i];
                    }
                    else if (znak != "")
                    {
                        valueTwo += s[i];
                    }
                }

                // подсчёт 

                return Concate(valueOne, valueTwo, znak);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return "";
        }

        /// <summary>
        /// посчитать значения
        /// </summary>
        /// <param name="valOne">значение один</param>
        /// <param name="valTwo">значение два</param>
        /// <param name="sign">знак</param>
        private string Concate(string valOne, string valTwo, string sign)
        {
            if(string.IsNullOrWhiteSpace(valOne))
            {
                return valTwo;
            }
            if (string.IsNullOrWhiteSpace(valTwo))
            {
                return valOne;
            }

            if (valOne[0] != 'A' && valTwo[0] != 'A' &&
                valOne[0] != 'B' && valTwo[0] != 'B')
            {// оба значения цифры
                decimal one = Convert.ToDecimal(valOne);
                decimal two = Convert.ToDecimal(valTwo);

                return ConcateDecimals(one, two, sign);
            }
            else if ((valOne[0] == 'A' || valOne[0] == 'B')
                    && (valTwo[0] == 'A' || valTwo[0] == 'B'))
            {// оба значение массивы
                return ConcateCandles(valOne, valTwo, sign);
            }
            else if ((valOne[0] == 'A' || valOne[0] == 'B')
                     && valTwo[0] != 'A' && valTwo[0] != 'B')
            {// первое значение массив
                return ConcateCandleAndDecimal(valOne, valTwo, sign);
            }
            else if (valOne[0] != 'A' && valOne[0] != 'B'
                     && (valTwo[0] == 'A' || valTwo[0] == 'B'))
            {// второе значение массив
                return ConcateDecimalAndCandle(valOne, valTwo, sign);
            }
            return "";
        }

        /// <summary>
        /// сложить цифры
        /// </summary>
        private string ConcateDecimals(decimal valOne, decimal valTwo, string sign)
        {
            if (sign == "+")
            {
                return (valOne+valTwo).ToString();
            }
            else if (sign == "-")
            {
                return (valOne-valTwo).ToString();
            }
            else if (sign == "*")
            {
                return (valOne*valTwo).ToString();
            }
            else if (sign == "/")
            {
                if (valTwo == 0)
                {
                    return "0";
                }
                return (valOne/valTwo).ToString();
            }
            return "";
        }

        /// <summary>
        /// посчитать массивы свечей
        /// </summary>
        private string ConcateCandles(string valOne,string valTwo,string sign)
        {
            // берём первое значение

            List<Candle> candlesOne = null;

            if (valOne[0] == 'A')
            {
                int iOne = Convert.ToInt32(valOne.Split('A')[1]);
                if (iOne >= Tabs.Count)
                {
                    return "";
                }
                candlesOne = Tabs[iOne].Candles(true);
            }
            if (candlesOne == null)
            {
                ValueSave value = _valuesToFormula.Find(v => v.Name == valOne);
                if (value == null)
                {
                    return "";
                }

                candlesOne = value.ValueCandles;
            }

            // берём второе значение

            List<Candle> candlesTwo = null;

            if (valTwo[0] == 'A')
            {
                int iOne = Convert.ToInt32(valTwo.Split('A')[1]);

                if (iOne >= Tabs.Count)
                {
                    return "";
                }
                candlesTwo = Tabs[iOne].Candles(true);
            }
            if (candlesTwo == null)
            {
                ValueSave value = _valuesToFormula.Find(v => v.Name == valTwo);
                if (value == null)
                {
                    return "";
                }
                candlesTwo = value.ValueCandles;
            }

            // берём исходящее значение

            string znak = "";

            if (sign == "+")
            {
                znak = "plus";
            }
            else if (sign == "-")
            {
                znak = "minus";
            }
            else if (sign == "*")
            {
              znak = "multiply";
            }
            else if (sign == "/")
            {
                znak = "devide";
            }

            ValueSave exitVal = _valuesToFormula.Find(val => val.Name == "B" + valOne + znak + valTwo);

            if (exitVal == null)
            {
                exitVal = new ValueSave();
                exitVal.Name = "B" +  valOne + znak + valTwo;
                exitVal.ValueCandles = new List<Candle>();
                _valuesToFormula.Add(exitVal);
            }

            List<Candle> exitCandles = exitVal.ValueCandles;

            if (exitCandles.Count != 0 &&
                candlesOne[candlesOne.Count-1].TimeStart == candlesTwo[candlesTwo.Count-1].TimeStart &&
                candlesOne[candlesOne.Count-1].TimeStart == exitCandles[exitCandles.Count-1].TimeStart)
            { // надо обновить только последнюю свечу
                exitCandles[exitCandles.Count - 1] = (GetCandle(exitCandles[exitCandles.Count - 1], candlesOne[candlesOne.Count - 1], candlesTwo[candlesTwo.Count - 1], sign));
            }
            else if (exitCandles.Count != 0 &&
                candlesOne[candlesOne.Count-1].TimeStart == candlesTwo[candlesTwo.Count-1].TimeStart &&
                candlesOne[candlesOne.Count-2].TimeStart == exitCandles[exitCandles.Count-1].TimeStart)
            { // нужно добавить одну свечу
                exitCandles.Add(GetCandle(null, candlesOne[candlesOne.Count - 1], candlesTwo[candlesTwo.Count-1], sign));
            }
            else
            { // обновить нужно всё

                int indexStartFirst = 0;
                int indexStartSecond = 0;

                for (int i1 = candlesOne.Count - 1, i2 = candlesTwo.Count - 1; exitCandles.Count != 0 && i1 > -1 && i2 > -1; i2--, i1--)
                {
                    if (candlesOne[i1].TimeStart <= exitCandles[exitCandles.Count - 1].TimeStart &&
                        indexStartFirst == 0)
                    {
                        indexStartFirst = i1+1;
                    }
                    if (candlesTwo[i2].TimeStart <= exitCandles[exitCandles.Count - 1].TimeStart &&
                        indexStartSecond == 0)
                    {
                        indexStartSecond = i2+1;
                    }
                    if (indexStartSecond != 0 &&
                        indexStartFirst != 0)
                    {
                        break;
                    }
                }

                for (int i1 = indexStartFirst, i2 = indexStartSecond; i1 < candlesOne.Count && i2 < candlesTwo.Count; i2++, i1++)
                {
                    if (candlesOne[i1].TimeStart == candlesTwo[i2].TimeStart)
                    {
                        exitCandles.Add(GetCandle(null, candlesOne[i1], candlesTwo[i2], sign));
                    }
                    else if (candlesOne[i1].TimeStart > candlesTwo[i2].TimeStart)
                    {
                        i1--;
                    }
                    else if (candlesOne[i1].TimeStart < candlesTwo[i2].TimeStart)
                    {
                        i2--;
                    }
                }
                exitVal.ValueCandles = exitCandles;
            }

            return exitVal.Name;
        }

        /// <summary>
        /// посчитать массив свечей и цифру
        /// </summary>
        private string ConcateCandleAndDecimal(string valOne, string valTwo, string sign)
        {
            // берём первое значение

            List<Candle> candlesOne = null;

            if (valOne[0] == 'A')
            {
                int iOne = Convert.ToInt32(valOne.Split('A')[1]);

                if (iOne >= Tabs.Count)
                {
                    return "";
                }
                candlesOne = Tabs[iOne].Candles(true);
                if(candlesOne == null)
                {
                    return valOne;
                }
            }
            if (candlesOne == null)
            {
                ValueSave value = _valuesToFormula.Find(v => v.Name == valOne);
                if (value == null)
                {
                    return "";
                }

                candlesOne = value.ValueCandles;
            }

            // берём второе значение

            decimal valueTwo = Convert.ToDecimal(valTwo);

            // берём исходящее значение

            string znak = "";

            if (sign == "+")
            {
                znak = "plus";
            }
            else if (sign == "-")
            {
                znak = "minus";
            }
            else if (sign == "*")
            {
                znak = "multiply";
            }
            else if (sign == "/")
            {
                znak = "devide";
            }

            ValueSave exitVal = _valuesToFormula.Find(val => val.Name == "B" + valOne + znak + valTwo);

            if (exitVal == null)
            {
                exitVal = new ValueSave();
                exitVal.Name = "B" + valOne + znak + valTwo;
                exitVal.ValueCandles = new List<Candle>();
                _valuesToFormula.Add(exitVal);
            }

            List<Candle> exitCandles = exitVal.ValueCandles;

            if (exitCandles.Count != 0 &&
                candlesOne[candlesOne.Count - 1].TimeStart == exitCandles[exitCandles.Count - 1].TimeStart)
            { // надо обновить только последнюю свечу
                exitCandles[exitCandles.Count - 1] = (GetCandle(exitCandles[exitCandles.Count - 1], candlesOne[candlesOne.Count - 1], valueTwo, sign));
            }
            else if (exitCandles.Count != 0 &&
                candlesOne[candlesOne.Count - 2].TimeStart == exitCandles[exitCandles.Count - 1].TimeStart)
            { // нужно добавить одну свечу
                exitCandles.Add(GetCandle(null, candlesOne[candlesOne.Count - 1], valueTwo, sign));
            }
            else
            { // обновить нужно всё

                int indexStartFirst = 0;

                for (int i1 = candlesOne.Count - 1; exitCandles.Count != 0 && i1 > -1; i1--)
                {
                    if (candlesOne[i1].TimeStart <= exitCandles[exitCandles.Count - 1].TimeStart &&
                        indexStartFirst == 0)
                    {
                        indexStartFirst = i1+1;
                        break;
                    }
                }

                for (int i1 = indexStartFirst; i1 < candlesOne.Count; i1++)
                {
                    exitCandles.Add(GetCandle(null, candlesOne[i1], valueTwo, sign));
                }
                exitVal.ValueCandles = exitCandles;
            }

            return exitVal.Name;
        }

        /// <summary>
        /// посчитать цифру и массив
        /// </summary>
        private string ConcateDecimalAndCandle(string valOne,string valTwo,string sign)
        {
            // берём первое значение

            decimal valueOne = Convert.ToDecimal(valOne);

            // берём второе значение

            List<Candle> candlesTwo = null;

            if (valTwo[0] == 'A')
            {
                int iOne = Convert.ToInt32(valTwo.Split('A')[1]);
                candlesTwo = Tabs[iOne].Candles(true);
            }
            if (candlesTwo == null)
            {
                ValueSave value = _valuesToFormula.Find(v => v.Name == valTwo);
                if (value == null)
                {
                    return "";
                }
                candlesTwo = value.ValueCandles;
            }

            // берём исходящее значение

            string znak = "";

            if (sign == "+")
            {
                znak = "plus";
            }
            else if (sign == "-")
            {
                znak = "minus";
            }
            else if (sign == "*")
            {
              znak = "multiply";
            }
            else if (sign == "/")
            {
                znak = "devide";
            }

            ValueSave exitVal = _valuesToFormula.Find(val => val.Name == "B" + valOne + znak + valTwo);

            if (exitVal == null)
            {
                exitVal = new ValueSave();
                exitVal.Name = "B" +  valOne + znak + valTwo;
                exitVal.ValueCandles = new List<Candle>();
                _valuesToFormula.Add(exitVal);
            }

            List<Candle> exitCandles = exitVal.ValueCandles;

            if (exitCandles.Count != 0 &&
                candlesTwo[candlesTwo.Count - 1].TimeStart == exitCandles[exitCandles.Count - 1].TimeStart)
            { // надо обновить только последнюю свечу
                exitCandles[exitCandles.Count - 1] = (GetCandle(exitCandles[exitCandles.Count - 1], valueOne, candlesTwo[candlesTwo.Count - 1], sign));
            }
            else if (exitCandles.Count != 0 &&
                candlesTwo[candlesTwo.Count - 2].TimeStart == exitCandles[exitCandles.Count - 1].TimeStart)
            { // нужно добавить одну свечу
                exitCandles.Add(GetCandle(null, valueOne, candlesTwo[candlesTwo.Count - 1], sign));
            }
            else
            { // обновить нужно всё
                int indexStartSecond = 0;

                for (int i2 = candlesTwo.Count - 1; exitCandles.Count != 0 && i2 > -1; i2--)
                {
                    if (candlesTwo[i2].TimeStart <= exitCandles[exitCandles.Count - 1].TimeStart &&
                        indexStartSecond == 0)
                    {
                        indexStartSecond = i2+1;
                        break;
                    }
                }

                for (int i2 = indexStartSecond; i2 < candlesTwo.Count; i2++)
                {
                     exitCandles.Add(GetCandle(null, valueOne, candlesTwo[i2], sign));
                }
                exitVal.ValueCandles = exitCandles;
            }

            return exitVal.Name;
        }

        private Candle GetCandle(Candle oldCandle, Candle candleOne, decimal valueTwo, string sign)
        {
            if (oldCandle == null)
            {
                oldCandle = new Candle();
                oldCandle.TimeStart = candleOne.TimeStart;
            }

            if (sign == "+")
            {
                oldCandle.High = Math.Round(candleOne.High + valueTwo, 5);
                oldCandle.Low = Math.Round(candleOne.Low + valueTwo, 5);
                oldCandle.Open = Math.Round(candleOne.Open + valueTwo, 5);
                oldCandle.Close = Math.Round(candleOne.Close + valueTwo, 5);
            }
            else if (sign == "-")
            {
                oldCandle.High = Math.Round(candleOne.High - valueTwo, 5);
                oldCandle.Low = Math.Round(candleOne.Low - valueTwo, 5);
                oldCandle.Open = Math.Round(candleOne.Open - valueTwo, 5);
                oldCandle.Close = Math.Round(candleOne.Close - valueTwo, 5);
            }
            else if (sign == "*")
            {
                oldCandle.High = Math.Round(candleOne.High * valueTwo, 5);
                oldCandle.Low = Math.Round(candleOne.Low * valueTwo, 5);
                oldCandle.Open = Math.Round(candleOne.Open * valueTwo, 5);
                oldCandle.Close = Math.Round(candleOne.Close * valueTwo, 5);
            }
            else if (sign == "/")
            {
                oldCandle.High = Math.Round(candleOne.High / valueTwo, 5);
                oldCandle.Low = Math.Round(candleOne.Low / valueTwo, 5);
                oldCandle.Open = Math.Round(candleOne.Open / valueTwo, 5);
                oldCandle.Close = Math.Round(candleOne.Close / valueTwo, 5);
            }

            return oldCandle;
        }

        private Candle GetCandle(Candle oldCandle, decimal valOne, Candle candleTwo, string sign)
        {
            if (oldCandle == null)
            {
                oldCandle = new Candle();
                oldCandle.TimeStart = candleTwo.TimeStart;
            }

            if (sign == "+")
            {
                oldCandle.High = Math.Round(valOne + candleTwo.High, 5);
                oldCandle.Low = Math.Round(valOne + candleTwo.Low, 5);
                oldCandle.Open = Math.Round(valOne + candleTwo.Open, 5);
                oldCandle.Close = Math.Round(valOne + candleTwo.Close, 5);
            }
            else if (sign == "-")
            {
                oldCandle.High = Math.Round(valOne - candleTwo.High, 5);
                oldCandle.Low = Math.Round(valOne - candleTwo.Low, 5);
                oldCandle.Open = Math.Round(valOne - candleTwo.Open, 5);
                oldCandle.Close = Math.Round(valOne - candleTwo.Close, 5);
            }
            else if (sign == "*")
            {
                oldCandle.High = Math.Round(valOne * candleTwo.High, 5);
                oldCandle.Low = Math.Round(valOne * candleTwo.Low, 5);
                oldCandle.Open = Math.Round(valOne * candleTwo.Open, 5);
                oldCandle.Close = Math.Round(valOne * candleTwo.Close, 5);
            }
            else if (sign == "/")
            {
                oldCandle.High = Math.Round(valOne / candleTwo.High, 5);
                oldCandle.Low = Math.Round(valOne / candleTwo.Low, 5);
                oldCandle.Open = Math.Round(valOne / candleTwo.Open, 5);
                oldCandle.Close = Math.Round(valOne / candleTwo.Close, 5);
            }

            return oldCandle;
        }

        private Candle GetCandle(Candle oldCandle, Candle candleOne, Candle candleTwo, string sign)
        {
            if (oldCandle == null)
            {
                oldCandle = new Candle();
                oldCandle.TimeStart = candleOne.TimeStart;
            }

            if (sign == "+")
            {
                oldCandle.High = Math.Round(candleOne.High + candleTwo.High, 5);
                oldCandle.Low = Math.Round(candleOne.Low + candleTwo.Low, 5);
                oldCandle.Open = Math.Round(candleOne.Open + candleTwo.Open, 5);
                oldCandle.Close = Math.Round(candleOne.Close + candleTwo.Close, 5);
            }
            else if (sign == "-")
            {
                oldCandle.High = Math.Round(candleOne.High - candleTwo.High, 5);
                oldCandle.Low = Math.Round(candleOne.Low - candleTwo.Low, 5);
                oldCandle.Open = Math.Round(candleOne.Open - candleTwo.Open, 5);
                oldCandle.Close = Math.Round(candleOne.Close - candleTwo.Close, 5);
            }
            else if (sign == "*")
            {
                oldCandle.High = Math.Round(candleOne.High * candleTwo.High, 5);
                oldCandle.Low = Math.Round(candleOne.Low * candleTwo.Low, 5);
                oldCandle.Open = Math.Round(candleOne.Open * candleTwo.Open, 5);
                oldCandle.Close = Math.Round(candleOne.Close * candleTwo.Close, 5);
            }
            else if (sign == "/")
            {
                oldCandle.High = Math.Round(candleOne.High / candleTwo.High, 5);
                oldCandle.Low = Math.Round(candleOne.Low / candleTwo.Low, 5);
                oldCandle.Open = Math.Round(candleOne.Open / candleTwo.Open, 5);
                oldCandle.Close = Math.Round(candleOne.Close / candleTwo.Close, 5);
            }

            return oldCandle;
        }

// Индикаторы

        /// <summary>
        /// создать индикатор на свечном графике. Начать его прорисовку на графике. Прогрузить его и подписать на обновление.
        /// </summary>
        /// <param name="indicator">индикатор</param>
        /// <param name="nameArea">название области на которую он будет помещён. Главная по умолчанию "Prime"</param>
        /// <returns></returns>
        public IIndicatorCandle CreateCandleIndicator(IIndicatorCandle indicator, string nameArea)
        {
            return _chartMaster.CreateIndicator(indicator, nameArea);
        }

        /// <summary>
        /// удалить индикатор со свечного графика. Удалить область индикатора
        /// </summary>
        /// <param name="indicator">индикатор</param>
        public void DeleteCandleIndicator(IIndicatorCandle indicator)
        {
            _chartMaster.DeleteIndicator(indicator);
        }

        /// <summary>
        /// индикаторы доступные у индекса
        /// </summary>
        public List<IIndicatorCandle> Indicators
        {
            get { return _chartMaster.Indicators; }
        } 

// логирование

        /// <summary>
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

    }

    /// <summary>
    /// объект для хранения промежуточных данных по индексу
    /// </summary>
    public class ValueSave
    {
        /// <summary>
        /// имя
        /// </summary>
        public string Name;

        /// <summary>
        /// свечи
        /// </summary>
        public  List<Candle> ValueCandles;
    }
}
