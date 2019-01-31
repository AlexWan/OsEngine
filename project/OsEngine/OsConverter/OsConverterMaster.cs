/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
        /// конструктор
        /// </summary>
        /// <param name="textBoxSourceFile">контрол для источника тиков</param>
        /// <param name="textBoxExitFile">контрол для исходящего файла</param>
        /// <param name="comboBoxTimeFrame">контрол для таймФрейма создаваемых свечек</param>
        /// <param name="logFormsHost">хост для лога</param>
        public OsConverterMaster(TextBox textBoxSourceFile, TextBox textBoxExitFile, ComboBox comboBoxTimeFrame, WindowsFormsHost logFormsHost)
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
        /// загрузить настройки из файла
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
              SendNewLogMessage(error.ToString(),LogMessageType.Error);
            }
        }

        /// <summary>
        /// сохранить настройки в файл
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
        /// путь к исходному файлу из которого берём данные
        /// </summary>
        private string _sourceFile;

        /// <summary>
        /// путь к исходящему файлу
        /// </summary>
        private string _exitFile;

        /// <summary>
        /// таймФрейм формируемых свечей
        /// </summary>
        public TimeFrame TimeFrame;

        /// <summary>
        /// контрол для пути исходного файла
        /// </summary>
        private TextBox _textBoxSourceFile;

        /// <summary>
        /// контрол для пути исходящего файла
        /// </summary>
        private TextBox _textBoxExitFile;

        /// <summary>
        /// контрол с ТФ
        /// </summary>
        private ComboBox _comboBoxTimeFrame;

        /// <summary>
        /// изменился ТФ
        /// </summary>
        void _comboBoxTimeFrame_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            Enum.TryParse(_comboBoxTimeFrame.SelectedItem.ToString(), out TimeFrame);
            Save();
        }

        /// <summary>
        /// выбрать исходный файл
        /// </summary>
        public void SelectSourceFile()
        {
            FileDialog myDialog = new OpenFileDialog();

            if (string.IsNullOrWhiteSpace(_sourceFile))
            {
                myDialog.FileName = _sourceFile;
            }

            myDialog.ShowDialog();

            if (myDialog.FileName != "") // если хоть что-то выбрано
            {
                _sourceFile = myDialog.FileName;
                Save();
            }
            _textBoxSourceFile.Text = _sourceFile;
        }

        /// <summary>
        /// сохдать новый файл
        /// </summary>
        public void CreateExitFile()
        {
            FileDialog myDialog = new SaveFileDialog();

            if (string.IsNullOrWhiteSpace(_exitFile))
            {
                myDialog.FileName = _exitFile;
            }

            myDialog.ShowDialog();

            if (myDialog.FileName != "") // если хоть что-то выбрано
            {
                _exitFile = myDialog.FileName;
                Save();
            }
            _textBoxExitFile.Text = _exitFile;
        }

        /// <summary>
        /// включить конвертирование
        /// </summary>
        public void StartConvert()
        {
            if (_worker != null &&
                _worker.IsAlive)
            {
                SendNewLogMessage(OsLocalization.Converter.Message1, LogMessageType.System);
                return;
            }

            _worker = new Thread(WorkerSpace);
            _worker.IsBackground = true;
            _worker.Start();
        }

        /// <summary>
        /// поток занимающийся созданием нового файла
        /// </summary>
        private Thread _worker;

        /// <summary>
        /// место работы потока создающего новый файл
        /// </summary>
        private void WorkerSpace()
        {
            if (string.IsNullOrWhiteSpace(_sourceFile))
            {
                SendNewLogMessage(OsLocalization.Converter.Message2, LogMessageType.System);
                return;
            }
            else if (string.IsNullOrWhiteSpace(_exitFile))
            {
                SendNewLogMessage(OsLocalization.Converter.Message3, LogMessageType.System);
                return;
            }

            if (!File.Exists(_exitFile))
            {
                File.Create(_exitFile);
            }

            StreamReader reader = new StreamReader(_sourceFile);

            SendNewLogMessage(OsLocalization.Converter.Message4, LogMessageType.System);

            SendNewLogMessage(OsLocalization.Converter.Message5, LogMessageType.System);

            List<Trade> trades = new List<Trade>();

            int currentWeek = 0;

            bool isNotFirstTime = false;

            try
            {
                while (!reader.EndOfStream)
                {
                    Trade trade = new Trade();
                    trade.SetTradeFromString(reader.ReadLine());

                    int partMonth = 1;

                    if (trade.Time.Day <= 10)
                    {
                        partMonth = 1;
                    }
                    else if (trade.Time.Day > 10 &&
                        trade.Time.Day < 20)
                    {
                        partMonth = 2;
                    }

                    else if (trade.Time.Day >= 20)
                    {
                        partMonth = 3;
                    }

                    if (currentWeek == 0)
                    {

                        SendNewLogMessage(
                            OsLocalization.Converter.Message6 + partMonth +
                            OsLocalization.Converter.Message7 + trade.Time.Month, LogMessageType.System);
                        currentWeek = partMonth;
                    }


                    if (partMonth != currentWeek || reader.EndOfStream)
                    {
                        SendNewLogMessage(OsLocalization.Converter.Message6 + currentWeek +
                                          OsLocalization.Converter.Message7 + trade.Time.Month +
                                          OsLocalization.Converter.Message8, LogMessageType.System);

                        TimeFrameBuilder timeFrameBuilder = new TimeFrameBuilder();
                        timeFrameBuilder.TimeFrame = TimeFrame;

                        CandleSeries series = new CandleSeries(timeFrameBuilder, new Security() { Name = "Unknown" },StartProgram.IsOsConverter);

                        series.IsStarted = true;

                        series.SetNewTicks(trades);

                        List<Candle> candles = series.CandlesAll;

                        if (candles == null)
                        {
                            continue;
                        }

                        StreamWriter writer = new StreamWriter(_exitFile, isNotFirstTime);

                        for (int i = 0; i < candles.Count; i++)
                        {
                            writer.WriteLine(candles[i].StringToSave);
                        }

                        writer.Close();

                        SendNewLogMessage(OsLocalization.Converter.Message9, LogMessageType.System);

                        isNotFirstTime = true;

                        trades = new List<Trade>();
                        series.Clear();

                        currentWeek = partMonth;

                        SendNewLogMessage(OsLocalization.Converter.Message6 + partMonth +
                                          OsLocalization.Converter.Message7 + trade.Time.Month, LogMessageType.System);
                    }
                    else
                    {
                        trades.Add(trade);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(OsLocalization.Converter.Message10, LogMessageType.System);
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
                reader.Close();
                return;
            }

            reader.Close();



            SendNewLogMessage(OsLocalization.Converter.Message9, LogMessageType.System);
        }

// логирование

        /// <summary>
        /// выслать новое сообщение в лог
        /// </summary>
        void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// событие нового сообщения в лог
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}