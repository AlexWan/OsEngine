/*
 *Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/


using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Properties;

namespace OsEngine.Alerts
{
    /// <summary>
    /// Alert
    /// Алерт
    /// </summary>
    public class AlertToChart:IIAlert
    {

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="name">alert name/имя алерта</param>
        /// <param name="gridView">control for calling main thread/контрол для вызова основного потока</param>
        public AlertToChart(string name, WindowsFormsHost gridView)
        {
            _lastAlarm = DateTime.MinValue;
            Name = name;
            _gridView = gridView;
            VolumeReaction = 1;
            Load();
            TypeAlert = AlertType.ChartAlert;
        }

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="gridView">control for calling main thread/контрол для вызова основного потока</param>
        public AlertToChart(WindowsFormsHost gridView)
        {
            _gridView = gridView;
            _lastAlarm = DateTime.MinValue;
            VolumeReaction = 1;
            TypeAlert = AlertType.ChartAlert;
        }

        /// <summary>
        /// download from file
        /// загрузить из файла
        /// </summary>
        public void Load()
        {
            if (!File.Exists(@"Engine\" + Name + @"Alert.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + Name + @"Alert.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), true, out Type);

                    string [] savesLine = reader.ReadLine().Split('%');

                    Lines = new ChartAlertLine[savesLine.Length-1];

                    for (int i = 0; i < savesLine.Length - 1; i++)
                    {
                        Lines[i] = new ChartAlertLine();
                        Lines[i].SetFromSaveString(savesLine[i]);
                    }

                    Label = reader.ReadLine();
                    Message = reader.ReadLine();
                    BorderWidth = Convert.ToInt32(reader.ReadLine());
                    IsOn = Convert.ToBoolean(reader.ReadLine());
                    IsMusicOn = Convert.ToBoolean(reader.ReadLine());
                    IsMessageOn = Convert.ToBoolean(reader.ReadLine());

                    ColorLine = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    ColorLabel = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                    Enum.TryParse(reader.ReadLine(), out Music);

                    Enum.TryParse(reader.ReadLine(), true, out SignalType);
                    VolumeReaction = reader.ReadLine().ToDecimal();
                    Slippage = Convert.ToDecimal(reader.ReadLine());
                    NumberClosePosition = Convert.ToInt32(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(),true,out OrderPriceType);

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// save to file
        /// сохранить в файл
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @"Alert.txt", false))
                {
                    writer.WriteLine(Type);

                    string saveLineString = "";

                    for (int i = 0; i < Lines.Length; i++)
                    {
                        saveLineString += Lines[i].GetStringToSave() + "%";
                    }

                    writer.WriteLine(saveLineString);

                    writer.WriteLine(Label);
                    writer.WriteLine(Message);
                    writer.WriteLine(BorderWidth);
                    writer.WriteLine(IsOn);
                    writer.WriteLine(IsMusicOn);
                    writer.WriteLine(IsMessageOn);
                    writer.WriteLine(ColorLine.ToArgb());
                    writer.WriteLine(ColorLabel.ToArgb());
                    writer.WriteLine(Music);
                    writer.WriteLine(SignalType);
                    writer.WriteLine(VolumeReaction);
                    writer.WriteLine(Slippage);
                    writer.WriteLine(NumberClosePosition);
                    writer.WriteLine(OrderPriceType);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// delete save file
        /// удалить файл сохранений
        /// </summary>
        public void Delete()
        {
            if (File.Exists(@"Engine\" + Name + @"Alert.txt"))
            {
                File.Delete(@"Engine\" + Name + @"Alert.txt");
            }
        }

        /// <summary>
        /// Alert field
        /// поле для записи Алертов
        /// </summary>
        private readonly WindowsFormsHost _gridView;

        /// <summary>
        /// Alert name
        /// Имя Алерта
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// is Alert enabled
        /// включен ли Алерт
        /// </summary>
        public bool IsOn { get; set; }

        public AlertType TypeAlert { get; set; }

        /// <summary>
        /// message text emitted when alert triggered
        /// текст сообщения, выбрасываемый при срабатывании Алерта
        /// </summary>
        public string Message;

        /// <summary>
        /// line width
        /// ширина линии
        /// </summary>
        public int BorderWidth;

        /// <summary>
        /// Is music on?
        /// включена ли Музыка
        /// </summary>
        public bool IsMusicOn;

        /// <summary>
        /// whether message window discard enabled
        /// влкючено ли выбрасывание Окна сообщения
        /// </summary>
        public bool IsMessageOn;

        /// <summary>
        /// line color
        /// цвет линии
        /// </summary>
        public Color ColorLine;

        /// <summary>
        /// signature colour
        /// цвет подписи
        /// </summary>
        public Color ColorLabel;

        /// <summary>
        /// path to music file
        /// путь к файлу с музыкой
        /// </summary>
        public AlertMusic Music;

        /// <summary>
        /// signal type
        /// тип сигнала
        /// </summary>
        public SignalType SignalType;

        /// <summary>
        /// volume for execution
        /// объём для исполнения
        /// </summary>
        public decimal VolumeReaction;

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slippage;

        /// <summary>
        /// position number that will be closed
        /// номер позиции которая будет закрыта
        /// </summary>
        public int NumberClosePosition;

        /// <summary>
        /// order type
        /// тип ордера 
        /// </summary>
        public OrderPriceType OrderPriceType;

        /// <summary>
        /// signature
        /// подпись
        /// </summary>
        public string Label;

        /// <summary>
        /// alert type
        /// Тип Алерта
        /// </summary>
        public ChartAlertType Type;

        /// <summary>
        /// Line set
        /// Набор линий
        /// </summary>
        public ChartAlertLine[] Lines;

        /// <summary>
        /// recent alert call
        /// последнее время вызыва аллерта
        /// </summary>
        private DateTime _lastAlarm;

        /// <summary>
        /// check alert for triggering
        /// проверить алерт на срабатывание
        /// </summary>
        public AlertSignal CheckSignal(List<Candle> candles)
        {
            if (IsOn == false || candles == null)
            {
                return null;
            }

            if (_lastAlarm != DateTime.MinValue &&
                _lastAlarm == candles[candles.Count - 1].TimeStart)
            {
                // alert already triggered at this moment
                // алерт уже сработал в эту минуту
                return null;
            }
            // 1 need to find out if time lines are in current range
            // 1 надо выяснить. входят ли линии по времени в текущий диапазон
            if (Lines[0].TimeFirstPoint > candles[candles.Count - 1].TimeStart &&
                Lines[0].TimeSecondPoint > candles[candles.Count - 1].TimeStart ||
                Lines[0].TimeFirstPoint < candles[0].TimeStart &&
                Lines[0].TimeSecondPoint < candles[0].TimeStart)
            {
                return null;
            }
            // 2 find out which points of array allert built from
            // 2 узнаём какие в массиве точки из которых собран аллерт

            int numberCandleFirst = -1;
            int numberCandleSecond = -1;

            for (int i = 0; i < candles.Count; i++)
            {
                if (candles[i].TimeStart == Lines[0].TimeFirstPoint)
                {
                    numberCandleFirst = i;
                }

                if (candles[i].TimeStart == Lines[0].TimeSecondPoint)
                {
                    numberCandleSecond = i;
                }
            }

            if (numberCandleSecond == -1 ||
                numberCandleFirst == -1)
            {
                return null;
            }
            // 3 running along allert lines and checking for triggering
            // 3 бежим по линиям аллерта и проверяем срабатывание

            bool isAlarm = false;

            for (int i = 0; i < Lines.Length; i++)
            {
                // 1 see how long our line goes by candle
                // а узнаём, сколько наша линия проходит за свечку

                decimal stepCorner = (Lines[i].ValueFirstPoint - Lines[i].ValueSecondPoint) / (numberCandleFirst - numberCandleSecond);
                // 2 now build an array of line values parallel to candlestick array
                // б теперь строим массив значений линии параллельный свечному массиву

                decimal[] lineDecimals = new decimal[candles.Count];
                decimal point = Lines[i].ValueFirstPoint;

                for (int i2 = numberCandleFirst; i2 < lineDecimals.Length; i2++)
                {
                    // running ahead of array.
                    // бежим вперёд по массиву
                    lineDecimals[i2] = point;
                    point += stepCorner;
                }
                for (int i2 = numberCandleFirst; i2 > -1; i2--)
                {
                    // running backwards through array.
                    // бежим назад по массиву
                    lineDecimals[i2] = point;
                    point -= stepCorner;
                }

                decimal redLineUp = candles[candles.Count - 1].High;
                if (candles[candles.Count - 2].Close > redLineUp)
                {
                    redLineUp = candles[candles.Count - 2].Close;
                }

                decimal redLineDown = candles[candles.Count - 1].Low;
                if (candles[candles.Count - 2].Close < redLineDown)
                {
                    redLineDown = candles[candles.Count - 2].Close;
                }

                decimal lastPoint = lineDecimals[lineDecimals.Length - 1];


                if ((redLineUp > lastPoint &&
                     redLineDown < lastPoint) ||
                    (candles[candles.Count - 1].Close < lastPoint && candles[candles.Count - 1].High > lastPoint) ||
                    (candles[candles.Count - 1].Close > lastPoint && candles[candles.Count - 1].High < lastPoint) ||
                    (candles[candles.Count - 1].Close > lastPoint && candles[candles.Count - 2].Close < lastPoint)||
                    (candles[candles.Count - 1].Close < lastPoint && candles[candles.Count - 2].Close > lastPoint))
                {
                    // if the closing price is in zone of triggering of allert
                    // если цена закрытия вошла в зону срабатывания аллерта
                    _lastAlarm = candles[candles.Count - 1].TimeStart;
                    isAlarm = true;
                    SignalAlarm();
                }
            }

            if (isAlarm)
            {
                return new AlertSignal { SignalType = SignalType, Volume = VolumeReaction,NumberClosingPosition =  NumberClosePosition,PriceType = OrderPriceType,Slipage =  Slippage};
            }

            return null;
        }

        /// <summary>
        /// start alert
        /// запустить оповещение
        /// </summary>
        private void SignalAlarm()
        {
            if (IsMusicOn)
            {
                UnmanagedMemoryStream stream = Resources.Bird;

                if (Music == AlertMusic.Duck)
                {
                    stream = Resources.Duck;
                }
                if (Music == AlertMusic.Wolf)
                {
                    stream = Resources.wolf01;
                }
                AlertMessageManager.ThrowAlert(stream, Name, Message);
            }

            if (IsMessageOn)
            {
                SetMessage();
            }

            IsOn = false;
            Save();
        }

        /// <summary>
        /// throw message in form of window
        /// выбросить сообщение в виде окошка
        /// </summary>
        private void SetMessage()
        {
            if (!_gridView.Dispatcher.CheckAccess())
            {
                _gridView.Dispatcher.Invoke((SetMessage));
                return;
            }

            if (!string.IsNullOrWhiteSpace(Message))
            {
                AlertMessageSimpleUi ui = new AlertMessageSimpleUi(Message);
                ui.Show();
            }
            else
            {
                AlertMessageSimpleUi ui = new AlertMessageSimpleUi(OsLocalization.Alerts.Message2 + Label);
                ui.Show();
            }
        }

    }

    /// <summary>
    /// alert type
    /// Тип Алерта
    /// </summary>
    public enum ChartAlertType
    {
        /// <summary>
        /// Line
        /// Линия
        /// </summary>
        Line,
        /// <summary>
        /// Fibonacci Channel
        /// Канал Фибоначи
        /// </summary>
        FibonacciChannel,
        /// <summary>
        ///  Fibonacci Speed Line
        /// Скоростная линия Фибоначчи
        /// </summary>
        FibonacciSpeedLine,
        /// <summary>
        /// Horizontal line
        /// Горизонтальная линия
        /// </summary>
        HorisontalLine,
    }

    /// <summary>
    /// alert line
    /// Линия Алерта
    /// </summary>
    public class ChartAlertLine
    {
        /// <summary>
        /// time of first point
        /// время первой точки
        /// </summary>
        public DateTime TimeFirstPoint;

        /// <summary>
        /// time of first point
        /// значение первой точки
        /// </summary>
        public decimal ValueFirstPoint;

        /// <summary>
        /// time of second point
        /// время второй точки
        /// </summary>
        public DateTime TimeSecondPoint;

        /// <summary>
        /// second point value
        /// значение второй точки
        /// </summary>
        public decimal ValueSecondPoint;

        /// <summary>
        /// line value on last candlestick of array
        /// значение линии на последней свече массива
        /// </summary>
        public decimal LastPoint;

        /// <summary>
        /// take string to save
        /// взять строку для сохранение
        /// </summary>
        public string GetStringToSave()
        {
            if (!Thread.CurrentThread.CurrentCulture.Equals(new CultureInfo("ru-RU")))
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
            }

            string result = "";

            result += TimeFirstPoint + "@";
            result += ValueFirstPoint + "@";

            result += TimeSecondPoint + "@";
            result += ValueSecondPoint + "@";
            result += LastPoint + "@";

            return result;
        }

        /// <summary>
        /// set line from save line
        /// установить линию со cтроки сохранения
        /// </summary>
        public void SetFromSaveString(string saveString)
        {
            string[] saveStrings = saveString.Split('@');

            TimeFirstPoint = Convert.ToDateTime(saveStrings[0]);
            ValueFirstPoint = Convert.ToDecimal(saveStrings[1]);

            TimeSecondPoint = Convert.ToDateTime(saveStrings[2]);
            ValueSecondPoint = Convert.ToDecimal(saveStrings[3]);
            LastPoint = Convert.ToDecimal(saveStrings[4]);
        }

    }
}
