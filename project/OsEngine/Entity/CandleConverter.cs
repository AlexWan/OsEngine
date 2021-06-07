/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels.Tab;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;


namespace OsEngine.Entity
{
    /// <summary>
    /// candle converter
    /// конвертер для свечек
    /// </summary>
    public class CandleConverter
    {
        /// <summary>
        /// the vaults of already converted candlesticks
        /// хранилища уже конвертированных свечек
        /// </summary>
        private static List<ValueSave> _valuesToFormula = new List<ValueSave>();
        /// <summary>
        /// the path to the source file from which we take the data/путь к исходному файлу из которого берём данные
        /// </summary>
        private string _sourceFile;

        /// <summary>
        /// outgoing file path/путь к исходящему файлу
        /// </summary>
        private string _exitFile;

        /// <summary>
        /// timeframe molded candles/таймФрейм формируемых свечей
        /// </summary>
        public TimeFrame TimeFrame;

        /// <summary>
        /// control for source file path/контрол для пути исходного файла
        /// </summary>
        private TextBox _textBoxSourceFile;

        /// <summary>
        /// control for outgoing file path/контрол для пути исходящего файла
        /// </summary>
        private TextBox _textBoxExitFile;

        /// <summary>
        /// control with tf/контрол с ТФ
        /// </summary>
        private ComboBox _comboBoxTimeFrame;

        private TimeFrameBuilder _timeFrameBuilder;

        public double ResultCandleTimeFrame;

        public CandleConverter(TextBox textBoxSourceFile, TextBox textBoxExitFile, ComboBox comboBoxTimeFrame, WindowsFormsHost logFormsHost)
        {
            _textBoxSourceFile = textBoxSourceFile;
            _textBoxExitFile = textBoxExitFile;
            _comboBoxTimeFrame = comboBoxTimeFrame;
            TimeFrame = TimeFrame.Sec1;

            Load();
            _comboBoxTimeFrame.Items.Add(TimeFrame.Min5);
            _comboBoxTimeFrame.Items.Add(TimeFrame.Min10);
            _comboBoxTimeFrame.Items.Add(TimeFrame.Min15);
            _comboBoxTimeFrame.Items.Add(TimeFrame.Min30);


            _comboBoxTimeFrame.SelectedItem = TimeFrame;

            if(_comboBoxTimeFrame.SelectedItem == null)
            {
                _comboBoxTimeFrame.SelectedItem = TimeFrame.Min5;
            }

            Enum.TryParse(_comboBoxTimeFrame.SelectedItem.ToString(), out TimeFrame);
            _timeFrameBuilder = new TimeFrameBuilder();

            _timeFrameBuilder.TimeFrame = TimeFrame;
            TimeSpan timeSpan = _timeFrameBuilder.TimeFrameTimeSpan;
            ResultCandleTimeFrame = timeSpan.TotalMinutes;

            _comboBoxTimeFrame.SelectionChanged += _comboBoxTimeFrame_SelectionChanged1;

            _textBoxSourceFile.Text = _sourceFile;
            _textBoxExitFile.Text = _exitFile;


            Log log = new Log("OsDataMaster", StartProgram.IsOsData);
            log.StartPaint(logFormsHost);
            log.Listen(this);



        }

        private void _comboBoxTimeFrame_SelectionChanged1(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            Enum.TryParse(_comboBoxTimeFrame.SelectedItem.ToString(), out TimeFrame);
            _timeFrameBuilder.TimeFrame = TimeFrame;
            TimeSpan timeSpan = _timeFrameBuilder.TimeFrameTimeSpan;
            ResultCandleTimeFrame = timeSpan.TotalMinutes;
            Save();
        }

        public List<Candle> ReadSourceFile()
        {
            List<Candle> candles = new List<Candle>();

            if (_sourceFile == null)
            {
                SendNewLogMessage("There is no candles data file specified", LogMessageType.Error);
                return candles;
            }


            using (var fileStream = File.OpenRead(_sourceFile))
            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, 128))
            {
                String line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    Candle candle = new Candle();
                    candle.SetCandleFromString(line);
                    candles.Add(candle);
                }

            }
            return candles;
        }

        public void WriteExitFile(List<Candle> candles)
        {
            using (StreamWriter outputFile = new StreamWriter(_exitFile))
            {
                foreach (Candle candle in candles)
                    outputFile.WriteLine(candle.StringToSave);
            }
        }

        public void SelectSourceFile()
        {
            FileDialog myDialog = new OpenFileDialog();

            if (string.IsNullOrWhiteSpace(_sourceFile))
            {
                myDialog.FileName = _sourceFile;
            }

            myDialog.ShowDialog();

            if (myDialog.FileName != "") // if anything is selected/если хоть что-то выбрано
            {
                _sourceFile = myDialog.FileName;
                Save();
            }
            _textBoxSourceFile.Text = _sourceFile;
        }

        /// <summary>
        /// create new file/сохдать новый файл
        /// </summary>
        public void CreateExitFile()
        {
            FileDialog myDialog = new SaveFileDialog();

            if (string.IsNullOrWhiteSpace(_exitFile))
            {
                myDialog.FileName = _exitFile;
            }

            myDialog.ShowDialog();

            if (myDialog.FileName != "") //  if anything is selected/если хоть что-то выбрано
            {
                _exitFile = myDialog.FileName;
                Save();
            }
            _textBoxExitFile.Text = _exitFile;
        }

        /// <summary>
        /// dump candles
        /// слить свечи 
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        /// <param name="countMerge">Number of folds for the initial TF/количество складывания для начального ТФ</param>
        /// <returns></returns>
        public List<Candle> Merge(List<Candle> candles, int countMerge)
        {
            if (countMerge <= 1)
            {
                return candles;
            }

            if (candles == null ||
                candles.Count == 0 ||
                candles.Count < countMerge)
            {
                return candles;
            }


            ValueSave saveVal = _valuesToFormula.Find(val => val.Name == candles[0].StringToSave + countMerge);

            List<Candle> mergeCandles = null;

            if (saveVal != null)
            {
                mergeCandles = saveVal.ValueCandles;
            }
            else
            {
                mergeCandles = new List<Candle>();
                saveVal = new ValueSave();
                saveVal.ValueCandles = mergeCandles;
                saveVal.Name = candles[0].StringToSave + countMerge;
                _valuesToFormula.Add(saveVal);
            }
            // we know the initial index.        
            // узнаём начальный индекс

            int firstIndex = 0;

            if (mergeCandles.Count != 0)
            {
                mergeCandles.RemoveAt(mergeCandles.Count - 1);
            }

            if (mergeCandles.Count != 0)
            {
                for (int i = candles.Count - 1; i > -1; i--)
                {
                    if (mergeCandles[mergeCandles.Count - 1].TimeStart == candles[i].TimeStart)
                    {
                        firstIndex = i + countMerge;

                        if (candles[i].TimeStart.Hour == 10 && candles[i].TimeStart.Minute == 1)
                        {
                            firstIndex -= 1;
                        }
                        break;
                    }
                }
            }
            // " Gathering
            // собираем

            for (int i = firstIndex; i < candles.Count;)
            {
                int countReal = countMerge;

                if (countReal + i > candles.Count)
                {
                    countReal = candles.Count - i;
                }
                else if (i + countMerge < candles.Count &&
                    candles[i].TimeStart.Day != candles[i + countMerge].TimeStart.Day)
                {
                    countReal = 0;

                    for (int i2 = i; i2 < candles.Count; i2++)
                    {
                        if (candles[i].TimeStart.Day == candles[i2].TimeStart.Day)
                        {
                            countReal += 1;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                if (countReal == 0)
                {
                    break;
                }

                if (candles[i].TimeStart.Hour == 10 && candles[i].TimeStart.Minute == 1 &&
                    countReal == countMerge)
                {
                    countReal -= 1;
                }

                mergeCandles.Add(Concate(candles, i, countReal));
                i += countReal;

            }

            return mergeCandles;
        }

        /// <summary>
        /// candle connection
        /// соединить свечи
        /// </summary>
        /// <param name="candles">original candles/изначальные свечи</param>
        /// <param name="index">start index/индекс начала</param>
        /// <param name="count">candle count for connection/количество свечек для соединения</param>
        /// <returns></returns>
        private Candle Concate(List<Candle> candles, int index, int count)
        {
            Candle candle = new Candle();

            candle.Open = candles[index].Open;
            candle.High = Decimal.MinValue;
            candle.Low = Decimal.MaxValue;
            candle.TimeStart = candles[index].TimeStart;

            for (int i = index; i < candles.Count && i < index + count; i++)
            {
                if (candles[i].Trades != null)
                {
                    candle.Trades.AddRange(candles[i].Trades);
                }

                candle.Volume += candles[i].Volume;

                if (candles[i].High > candle.High)
                {
                    candle.High = candles[i].High;
                }

                if (candles[i].Low < candle.Low)
                {
                    candle.Low = candles[i].Low;
                }

                candle.Close = candles[i].Close;
            }

            return candle;
        }

        /// <summary>
        /// clean up old data
        /// очистить старые данные
        /// </summary>
        public void Clear()
        {
            _valuesToFormula = new List<ValueSave>();
        }

        public void Load()
        {
            if (!File.Exists("Engine\\CandleConverter.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader("Engine\\CandleConverter.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), out TimeFrame);
                    _sourceFile = reader.ReadLine();
                    _exitFile = reader.ReadLine();

                    reader.Close();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// save settings to file/сохранить настройки в файл
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter("Engine\\CandleConverter.txt", false))
                {
                    writer.WriteLine(TimeFrame);
                    writer.WriteLine(_sourceFile);
                    writer.WriteLine(_exitFile);

                    writer.Close();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }


        // logging/логирование

        /// <summary>
        /// send new message to log/выслать новое сообщение в лог
        /// </summary>
        void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// new message event to log/событие нового сообщения в лог
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
