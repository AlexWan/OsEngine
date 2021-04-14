/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Charts;
using OsEngine.Charts.CandleChart;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.OsTrader.Panels.Tab.Internal;
using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;

namespace OsEngine.OsTrader.Panels.Tab
{

    /// <summary>
    /// tab - spread of candlestick data in the form of a candlestick chart /
    /// вкладка - спред свечных данных в виде свечного графика
    /// </summary>
    public class BotTabIndex : IIBotTab
    {
        public BotTabIndex(string name, StartProgram  startProgram)
        {
            TabName = name;
            _startProgram = startProgram;

            Tabs = new List<ConnectorCandles>();
            _valuesToFormula = new List<ValueSave>();
            _chartMaster = new ChartCandleMaster(TabName, _startProgram);

            Load();
        }

        /// <summary>
        /// program that created the robot / 
        /// программа создавшая робота
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// chart
        /// чарт для прорисовки
        /// </summary>
        private ChartCandleMaster _chartMaster;

        /// <summary>
        /// connectors array
        /// Массив для хранения списка интсрументов
        /// </summary>
        public List<ConnectorCandles> Tabs;

 // управление

        /// <summary>
        /// show GUI
        /// вызвать окно управления
        /// </summary>
        public void ShowDialog()
        {
            BotTabIndexUi ui = new BotTabIndexUi(this);
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
        /// show connector GUI
        /// покаазать окно настроек коннектора по индексу
        /// </summary>
        public void ShowIndexConnectorIndexDialog(int index)
        {
            Tabs[index].ShowDialog(false);
            Save();
        }

        /// <summary>
        /// Add new security to the list / 
        /// Добавить новую бумагу в список
        /// </summary>
        public void ShowNewSecurityDialog()
        {
            CreateNewSecurityConnector();
            Tabs[Tabs.Count - 1].ShowDialog(false);
            Save();
        }

        /// <summary>
        /// make a new adapter to connect data / 
        /// сделать новый адаптер для подключения данных
        /// </summary>
        public void CreateNewSecurityConnector()
        {
            ConnectorCandles connector = new ConnectorCandles(TabName + Tabs.Count, _startProgram);
            connector.SaveTradesInCandles = false;
            Tabs.Add(connector);
            Tabs[Tabs.Count - 1].NewCandlesChangeEvent += BotTabIndex_NewCandlesChangeEvent;
        }

        /// <summary>
        /// remove selected security from list / 
        /// удалить выбранную бумагу из списка
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
        /// start drawing this robot / 
        /// начать прорисовку этого робота
        /// </summary> 
        public void StartPaint(Grid grid, WindowsFormsHost host, Rectangle rectangle)
        {
            _chartMaster.StartPaint(grid, host, rectangle);
        }

        /// <summary>
        /// stop drawing this robot / 
        /// остановить прорисовку этого робота
        /// </summary>
        public void StopPaint()
        {
            _chartMaster.StopPaint();
        }

        /// <summary>
        /// bot name /
        /// уникальное имя робота.
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// bot number / 
        /// номер вкладки
        /// </summary>
        public int TabNum { get; set; }

        /// <summary>
        /// clear / 
        /// очистить журнал и графики
        /// </summary>
        public void Clear()
        {
            _valuesToFormula = new List<ValueSave>();
            _chartMaster.Clear();
        }

        /// <summary>
        /// save / 
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
                // ignore
            }
        }

        /// <summary>
        /// load / 
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
                        ConnectorCandles newConnector = new ConnectorCandles(save2[i], _startProgram);
                        newConnector.SaveTradesInCandles = false;

                        Tabs.Add(newConnector);
                        Tabs[Tabs.Count - 1].NewCandlesChangeEvent += BotTabIndex_NewCandlesChangeEvent;
                    }
                    UserFormula = reader.ReadLine();

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// remove tab and all child structures
        /// удалить робота и все дочерние структуры
        /// </summary>
        public void Delete()
        {
            _chartMaster.Delete();

            if (File.Exists(@"Engine\" + TabName + @"SpreadSet.txt"))
            {
                File.Delete(@"Engine\" + TabName + @"SpreadSet.txt");
            }

            for(int i = 0; Tabs != null && i < Tabs.Count; i++)
            {
                Tabs[i].Delete();
            }
        }

        /// <summary>
        /// whether the tab is connected to download data / 
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

        public DateTime LastTimeCandleUpdate { get; set; }

        /// <summary>
        /// new data came from the connector / 
        /// из коннектора пришли новые данные
        /// </summary>
        private void BotTabIndex_NewCandlesChangeEvent(List<Candle> candles)
        {
            LastTimeCandleUpdate = DateTime.Now;

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
            //loop to collect all the candles in one array
            // цикл для сбора всех свечей в один массив

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

                    if (Candles.Count > 1 && 
                        Candles[Candles.Count - 1].TimeStart == Candles[Candles.Count - 2].TimeStart)
                    {
                        try
                        {
                            Candles.RemoveAt(Candles.Count - 1);
                        }
                        catch
                        {
                            // ignore
                        }
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
        /// candles
        /// свечи спреда
        /// </summary>
        public List<Candle> Candles;

        /// <summary>
        /// spread change event
        /// спред изменился
        /// </summary>
        public event Action<List<Candle>> SpreadChangeEvent;

// index calculation / расчёт индекса

        /// <summary>
        /// formula /
        /// формула
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
        /// formula reduced to program format / 
        /// формула приведённая к формату программы
        /// </summary>
        public string ConvertedFormula;

        /// <summary>
        /// array of objects for storing intermediate arrays of candles / 
        /// массив объектов для хранения промежуточных массивов свечей
        /// </summary>
        private List<ValueSave> _valuesToFormula;

        /// <summary>
        /// check the formula for errors and lead to the appearance of the program / 
        /// проверить формулу на ошибки и привести к виду программы
        /// </summary>
        public string ConvertFormula(string formula)
        {
            if (string.IsNullOrEmpty(formula))
            {
                return "";
            }

            // delete spaces / удаляем пробелы

            formula = formula.Replace(" ", "");

            // check the formula for validity / проверяем формулу на валиндность

            for (int i = 0; i < formula.Length; i++)
            {
                if (formula[i] != '/' && formula[i] !=  '*' && formula[i] !=  '+' && formula[i] !=  '-'
                    && formula[i] != '(' && formula[i] != ')' && formula[i] != 'A' && formula[i] != '1' && formula[i] != '0'
                    && formula[i] !=  '2' && formula[i] !=  '3' && formula[i] !=  '4' && formula[i] !=  '5'
                    && formula[i] !=  '6' && formula[i] !=  '7' && formula[i] !=  '8' && formula[i] !=  '9')
                { // incomprehensible characters / непонятные символы
                    SendNewLogMessage(OsLocalization.Trader.Label76,LogMessageType.Error);
                    return "";
                }
            }

            for (int i = 1; i < formula.Length; i++)
            {
                if ((formula[i] == '/' || formula[i] == '*' || formula[i] == '+' || formula[i] == '-') &&
                    (formula[i - 1] == '/' || formula[i - 1] == '*' || formula[i - 1] == '+' || formula[i - 1] == '-'))
                { // two signs in a row / два знака подряд
                    SendNewLogMessage(OsLocalization.Trader.Label76,
                        LogMessageType.Error);
                    return "";
                }
            }

            return formula;
        }

        /// <summary>
        ///  recalculate an arra / 
        /// пересчитать массив
        /// </summary>
        /// <param name="formula">formula / формула</param>
        /// <returns>the name of the array in which the final value lies / название массива в котором лежит финальное значение</returns>
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

                // 1 break into brackets
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

                // 2 split into two values
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

                // search for variables
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

                return Concate(valueOne, valueTwo, znak);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return "";
        }

        /// <summary>
        /// calculate values / 
        /// посчитать значения
        /// </summary>
        /// <param name="valOne">value one / значение один</param>
        /// <param name="valTwo">value two / значение два</param>
        /// <param name="sign">sign / знак</param>
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
            {
                // both digit values
                // оба значения цифры
                decimal one = Convert.ToDecimal(valOne);
                decimal two = Convert.ToDecimal(valTwo);

                return ConcateDecimals(one, two, sign);
            }
            else if ((valOne[0] == 'A' || valOne[0] == 'B')
                    && (valTwo[0] == 'A' || valTwo[0] == 'B'))
            {
                // both value arrays
                // оба значение массивы
                return ConcateCandles(valOne, valTwo, sign);
            }
            else if ((valOne[0] == 'A' || valOne[0] == 'B')
                     && valTwo[0] != 'A' && valTwo[0] != 'B')
            {
                // first value array
                // первое значение массив
                return ConcateCandleAndDecimal(valOne, valTwo, sign);
            }
            else if (valOne[0] != 'A' && valOne[0] != 'B'
                     && (valTwo[0] == 'A' || valTwo[0] == 'B'))
            {
                // second value array
                // второе значение массив
                return ConcateDecimalAndCandle(valOne, valTwo, sign);
            }
            return "";
        }

        /// <summary>
        /// add numbers // 
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
        /// count arrays of candles / 
        /// посчитать массивы свечей
        /// </summary>
        private string ConcateCandles(string valOne,string valTwo,string sign)
        {
            // take the first value
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

            // take the second value
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

            // take outgoing value
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
            {
                // need to update only the last candle
                // надо обновить только последнюю свечу
                exitCandles[exitCandles.Count - 1] = (GetCandle(exitCandles[exitCandles.Count - 1], candlesOne[candlesOne.Count - 1], candlesTwo[candlesTwo.Count - 1], sign));
            }
            else if (exitCandles.Count != 0 &&
                candlesOne[candlesOne.Count-1].TimeStart == candlesTwo[candlesTwo.Count-1].TimeStart &&
                candlesOne[candlesOne.Count-2].TimeStart == exitCandles[exitCandles.Count-1].TimeStart)
            {
                // need to add one candle
                // нужно добавить одну свечу
                exitCandles.Add(GetCandle(null, candlesOne[candlesOne.Count - 1], candlesTwo[candlesTwo.Count-1], sign));
            }
            else
            {
                // need to update everything
                // обновить нужно всё

                int indexStartFirst = 0;
                int indexStartSecond = 0;

                exitCandles = new List<Candle>();

                for (int i = 1; i < candlesOne.Count; i++)
                {
                    for (int i2 = 0; i2 < candlesTwo.Count; i2++)
                    {
                        if (candlesTwo[i2].TimeStart >= candlesOne[i].TimeStart)
                        {
                            indexStartFirst = i;
                            indexStartSecond = i2;
                            break;
                        }

                    }

                    if (indexStartSecond != 0)
                    {
                        break;
                    }

                }

                for (int i1 = indexStartFirst, i2 = indexStartSecond; i1 < candlesOne.Count && i2 < candlesTwo.Count; i2++, i1++)
                {
                    if (candlesOne[i1] == null)
                    {
                        candlesOne.RemoveAt(i1);
                        i2--; i1--;
                        continue;
                    }
                    if (candlesTwo[i2] == null)
                    {
                        candlesTwo.RemoveAt(i2);
                        i2--; i1--;
                        continue;
                    }
                    Candle candleOne = candlesOne[i1];
                    Candle candleTwo = candlesTwo[i2];

                    try
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
                    catch (Exception e)
                    {

                    }

                }
                exitVal.ValueCandles = exitCandles;
            }

            return exitVal.Name;
        }

        /// <summary>
        /// count an array of candles and a number / 
        /// посчитать массив свечей и цифру
        /// </summary>
        private string ConcateCandleAndDecimal(string valOne, string valTwo, string sign)
        {
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

            decimal valueTwo = Convert.ToDecimal(valTwo);

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

            int lastOper = -1;

            if (exitCandles.Count != 0 &&
                candlesOne[candlesOne.Count - 1].TimeStart == exitCandles[exitCandles.Count - 1].TimeStart)
            {
                // need to update only the last candle
                // надо обновить только последнюю свечу
                lastOper = 1;
                exitCandles[exitCandles.Count - 1] = (GetCandle(exitCandles[exitCandles.Count - 1], candlesOne[candlesOne.Count - 1], valueTwo, sign));
            }
            else if (exitCandles.Count != 0 &&
                candlesOne[candlesOne.Count - 2].TimeStart == exitCandles[exitCandles.Count - 1].TimeStart)
            {
                lastOper = 2;
                // need to add one candle
                // нужно добавить одну свечу

                exitCandles.Add(GetCandle(null, candlesOne[candlesOne.Count - 1], valueTwo, sign));
            }
            else
            {
                lastOper = 3;
                // need to update everything
                // обновить нужно всё

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

            for (int i = 0; i < exitVal.ValueCandles.Count; i++)
            {
                if (exitVal.ValueCandles[i] == null)
                {

                }
            }

            return exitVal.Name;
        }

        /// <summary>
        /// count number and array / 
        /// посчитать цифру и массив
        /// </summary>
        private string ConcateDecimalAndCandle(string valOne,string valTwo,string sign)
        {
            // take the first value
            // берём первое значение

            decimal valueOne = Convert.ToDecimal(valOne);

            // take the second value
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

            // take outgoing value
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
            {
                // need to update only the last candle
                // надо обновить только последнюю свечу
                exitCandles[exitCandles.Count - 1] = (GetCandle(exitCandles[exitCandles.Count - 1], valueOne, candlesTwo[candlesTwo.Count - 1], sign));
            }
            else if (exitCandles.Count != 0 &&
                candlesTwo[candlesTwo.Count - 2].TimeStart == exitCandles[exitCandles.Count - 1].TimeStart)
            {
                // need to add one candle
                // нужно добавить одну свечу
                exitCandles.Add(GetCandle(null, valueOne, candlesTwo[candlesTwo.Count - 1], sign));
            }
            else
            {
                // need to update everything
                // обновить нужно всё
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
                oldCandle.High = Math.Round(candleOne.High + valueTwo, 8);
                oldCandle.Low = Math.Round(candleOne.Low + valueTwo, 8);
                oldCandle.Open = Math.Round(candleOne.Open + valueTwo, 8);
                oldCandle.Close = Math.Round(candleOne.Close + valueTwo, 8);
            }
            else if (sign == "-")
            {
                oldCandle.High = Math.Round(candleOne.High - valueTwo, 8);
                oldCandle.Low = Math.Round(candleOne.Low - valueTwo, 8);
                oldCandle.Open = Math.Round(candleOne.Open - valueTwo, 8);
                oldCandle.Close = Math.Round(candleOne.Close - valueTwo, 8);
            }
            else if (sign == "*")
            {
                oldCandle.High = Math.Round(candleOne.High * valueTwo, 8);
                oldCandle.Low = Math.Round(candleOne.Low * valueTwo, 8);
                oldCandle.Open = Math.Round(candleOne.Open * valueTwo, 8);
                oldCandle.Close = Math.Round(candleOne.Close * valueTwo, 8);
            }
            else if (sign == "/")
            {
                oldCandle.High = Math.Round(candleOne.High / valueTwo, 8);
                oldCandle.Low = Math.Round(candleOne.Low / valueTwo, 8);
                oldCandle.Open = Math.Round(candleOne.Open / valueTwo, 8);
                oldCandle.Close = Math.Round(candleOne.Close / valueTwo, 8);
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
                oldCandle.High = Math.Round(valOne + candleTwo.High, 8);
                oldCandle.Low = Math.Round(valOne + candleTwo.Low, 8);
                oldCandle.Open = Math.Round(valOne + candleTwo.Open, 8);
                oldCandle.Close = Math.Round(valOne + candleTwo.Close, 8);
            }
            else if (sign == "-")
            {
                oldCandle.High = Math.Round(valOne - candleTwo.High, 8);
                oldCandle.Low = Math.Round(valOne - candleTwo.Low, 8);
                oldCandle.Open = Math.Round(valOne - candleTwo.Open, 8);
                oldCandle.Close = Math.Round(valOne - candleTwo.Close, 8);
            }
            else if (sign == "*")
            {
                oldCandle.High = Math.Round(valOne * candleTwo.High, 8);
                oldCandle.Low = Math.Round(valOne * candleTwo.Low, 8);
                oldCandle.Open = Math.Round(valOne * candleTwo.Open, 8);
                oldCandle.Close = Math.Round(valOne * candleTwo.Close, 8);
            }
            else if (sign == "/")
            {
                oldCandle.High = Math.Round(valOne / candleTwo.High, 8);
                oldCandle.Low = Math.Round(valOne / candleTwo.Low, 8);
                oldCandle.Open = Math.Round(valOne / candleTwo.Open, 8);
                oldCandle.Close = Math.Round(valOne / candleTwo.Close, 8);
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
                oldCandle.High = Math.Round(candleOne.High + candleTwo.High, 8);
                oldCandle.Low = Math.Round(candleOne.Low + candleTwo.Low, 8);
                oldCandle.Open = Math.Round(candleOne.Open + candleTwo.Open, 8);
                oldCandle.Close = Math.Round(candleOne.Close + candleTwo.Close, 8);
            }
            else if (sign == "-")
            {
                oldCandle.High = Math.Round(candleOne.High - candleTwo.High, 8);
                oldCandle.Low = Math.Round(candleOne.Low - candleTwo.Low, 8);
                oldCandle.Open = Math.Round(candleOne.Open - candleTwo.Open, 8);
                oldCandle.Close = Math.Round(candleOne.Close - candleTwo.Close, 8);
            }
            else if (sign == "*")
            {
                oldCandle.High = Math.Round(candleOne.High * candleTwo.High, 8);
                oldCandle.Low = Math.Round(candleOne.Low * candleTwo.Low, 8);
                oldCandle.Open = Math.Round(candleOne.Open * candleTwo.Open, 8);
                oldCandle.Close = Math.Round(candleOne.Close * candleTwo.Close, 8);
            }
            else if (sign == "/")
            {
                oldCandle.High = Math.Round(candleOne.High / candleTwo.High, 8);
                oldCandle.Low = Math.Round(candleOne.Low / candleTwo.Low, 8);
                oldCandle.Open = Math.Round(candleOne.Open / candleTwo.Open, 8);
                oldCandle.Close = Math.Round(candleOne.Close / candleTwo.Close, 8);
            }

            return oldCandle;
        }

        // Индикаторы

        /// <summary>
        /// create indicator / 
        /// создать индикатор
        /// </summary>
        /// <param name="indicator"> indicator /  индикатор</param>
        /// <param name="nameArea">name of the area where it will be placed / название области на которую он будет помещён"Prime"</param>
        /// <returns></returns>
        public IIndicator CreateCandleIndicator(IIndicator indicator, string nameArea)
        {
            return _chartMaster.CreateIndicator(indicator, nameArea);
        }

        /// <summary>
        /// remove indicator from candlestick chart / 
        /// удалить индикатор со свечного графика
        /// </summary>
        /// <param name="indicator">индикатор</param>
        public void DeleteCandleIndicator(IIndicator indicator)
        {
            _chartMaster.DeleteIndicator(indicator);
        }

        /// <summary>
        /// indicators available on the index / 
        /// индикаторы доступные у индекса
        /// </summary>
        public List<IIndicator> Indicators
        {
            get { return _chartMaster.Indicators; }
        } 

// log / логирование

        /// <summary>
        /// send log message / 
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

        /// <summary>
        /// get chart information
        /// получить информацию о чарте
        /// </summary>
        public string GetChartLabel()
        {
            return _chartMaster.GetChartLabel();
        }
    }

    /// <summary>
    /// object to store intermediate data by index / 
    /// объект для хранения промежуточных данных по индексу
    /// </summary>
    public class ValueSave
    {
        /// <summary>
        /// name / 
        /// имя
        /// </summary>
        public string Name;

        /// <summary>
        /// candles / 
        /// свечи
        /// </summary>
        public  List<Candle> ValueCandles;
    }
}
