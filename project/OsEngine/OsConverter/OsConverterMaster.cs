/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;

namespace OsEngine.OsConverter
{
    /// <summary>
    /// конвертер тиков в свечи
    /// </summary>
    public class OsConverterMaster
    {

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="textBoxSourceFile">control for tick source/контрол для источника тиков</param>
        /// <param name="textBoxExitFile">control for outgoing file/контрол для исходящего файла</param>
        /// <param name="comboBoxTimeFrame">control for timeframe created candles /контрол для таймФрейма создаваемых свечек</param>
        /// <param name="logFormsHost">log host/хост для лога</param>
        public OsConverterMaster(TextBox textBoxSourceFile, TextBox textBoxExitFile, ComboBox comboBoxTimeFrame,
            WindowsFormsHost logFormsHost)
        {
            _textBoxSourceFile = textBoxSourceFile;
            _textBoxExitFile = textBoxExitFile;
            _comboBoxTimeFrame = comboBoxTimeFrame;
            TimeFrame = TimeFrame.Sec1;

            Load();

            _comboBoxTimeFrame.Items.Add(TimeFrame.Sec1);
            _comboBoxTimeFrame.Items.Add(TimeFrame.Sec2);
            _comboBoxTimeFrame.Items.Add(TimeFrame.Sec5);
            _comboBoxTimeFrame.Items.Add(TimeFrame.Sec10);
            _comboBoxTimeFrame.Items.Add(TimeFrame.Sec15);
            _comboBoxTimeFrame.Items.Add(TimeFrame.Sec20);
            _comboBoxTimeFrame.Items.Add(TimeFrame.Sec30);
            _comboBoxTimeFrame.Items.Add(TimeFrame.Min1);
            _comboBoxTimeFrame.Items.Add(TimeFrame.Min2);
            _comboBoxTimeFrame.Items.Add(TimeFrame.Min5);
            _comboBoxTimeFrame.Items.Add(TimeFrame.Min10);
            _comboBoxTimeFrame.Items.Add(TimeFrame.Min15);
            _comboBoxTimeFrame.Items.Add(TimeFrame.Min20);
            _comboBoxTimeFrame.Items.Add(TimeFrame.Min30);


            _comboBoxTimeFrame.SelectedItem = TimeFrame;

            _comboBoxTimeFrame.SelectionChanged += _comboBoxTimeFrame_SelectionChanged;

            _textBoxSourceFile.Text = _sourceFile;
            _textBoxExitFile.Text = _exitFile;

            Log log = new Log("OsDataMaster", StartProgram.IsOsData);
            log.StartPaint(logFormsHost);
            log.Listen(this);
        }

        /// <summary>
        /// load settings from file/загрузить настройки из файла
        /// </summary>
        public void Load()
        {
            if (!File.Exists("Engine\\Converter.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader("Engine\\Converter.txt"))
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
                using (StreamWriter writer = new StreamWriter("Engine\\Converter.txt", false))
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

        /// <summary>
        /// changed tf/изменился ТФ
        /// </summary>
        void _comboBoxTimeFrame_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            Enum.TryParse(_comboBoxTimeFrame.SelectedItem.ToString(), out TimeFrame);
            Save();
        }

        /// <summary>
        /// select source file/выбрать исходный файл
        /// </summary>
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
                if (!myDialog.FileName.Contains(".txt"))
                {
                    myDialog.FileName += ".txt";
                }

                _exitFile = myDialog.FileName;
                Save();
            }

            _textBoxExitFile.Text = _exitFile;
        }

        /// <summary>
        /// enable conversion/включить конвертирование
        /// </summary>
        public void StartConvert()
        {
            if (_worker != null &&
                _worker.IsCompleted)
            {
                SendNewLogMessage(OsLocalization.Converter.Message1, LogMessageType.System);
                return;
            }

            _worker = new Task(WorkerSpaceStreaming); // Use the streaming worker
            _worker.Start();
        }


        private void WorkerSpaceStreaming()
        {
            using (StreamReader reader = new StreamReader(_sourceFile))
            using (StreamWriter writer = new StreamWriter(_exitFile, false))
            {
                SendNewLogMessage(OsLocalization.Converter.Message4, LogMessageType.System);
                SendNewLogMessage(OsLocalization.Converter.Message5, LogMessageType.System);

                List<Trade> trades = new List<Trade>();
                DateTime currentDay = DateTime.MinValue;

                while (!reader.EndOfStream)
                {
                    Trade trade = new Trade();
                    trade.SetTradeFromString(reader.ReadLine());

                    if (currentDay == DateTime.MinValue)
                    {
                        currentDay = trade.Time.Date;
                    }

                    if (trade.Time.Date != currentDay || reader.EndOfStream)
                    {
                        ProcessTradesAndWriteCandles(trades, writer);
                        trades = new List<Trade>(); // Clear trades for the next day
                        currentDay = trade.Time.Date;
                    }

                    trades.Add(trade);
                }

                // Process any remaining trades
                if (trades.Count > 0)
                {
                    ProcessTradesAndWriteCandles(trades, writer);
                }

                SendNewLogMessage(OsLocalization.Converter.Message9, LogMessageType.System);

                MessageBox.Show("Conversion completed!", "Notification", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// Helper function to process trades and write candles to the output file
        /// </summary>
        private void ProcessTradesAndWriteCandles(List<Trade> trades, StreamWriter writer)
        {
            SendNewLogMessage(OsLocalization.Converter.Message8, LogMessageType.System);

            TimeFrameBuilder timeFrameBuilder = new TimeFrameBuilder(StartProgram.IsOsData);
            timeFrameBuilder.TimeFrame = TimeFrame;

            CandleSeries series = new CandleSeries(timeFrameBuilder, new Security() { Name = "Unknown" },
                StartProgram.IsOsConverter);
            series.IsStarted = true;

            series.SetNewTicks(trades);

            List<Candle> candles = series.CandlesAll;

            if (candles != null)
            {
                for (int i = 0; i < candles.Count; i++)
                {
                    writer.WriteLine(candles[i].StringToSave);
                }
            }

            SendNewLogMessage(OsLocalization.Converter.Message9, LogMessageType.System);

            series.Clear(); // Clear the candle series
        }


        /// <summary>
        /// stream creating new file/поток занимающийся созданием нового файла
        /// </summary>
        private Task _worker;

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