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
    /// </summary>
    public class CandleConverter
    {
        /// <summary>
        /// the path to the source file from which we take the data
        /// </summary>
        private string _sourceFile;

        /// <summary>
        /// outgoing file path
        /// </summary>
        private string _exitFile;

        /// <summary>
        /// timeframe molded candles
        /// </summary>
        public TimeFrame TimeFrame;

        /// <summary>
        /// control for source file path
        /// </summary>
        private TextBox _textBoxSourceFile;

        /// <summary>
        /// control for outgoing file path
        /// </summary>
        private TextBox _textBoxExitFile;

        /// <summary>
        /// control with tf
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

            if (_comboBoxTimeFrame.SelectedItem == null)
            {
                _comboBoxTimeFrame.SelectedItem = TimeFrame.Min5;
            }

            Enum.TryParse(_comboBoxTimeFrame.SelectedItem.ToString(), out TimeFrame);
            _timeFrameBuilder = new TimeFrameBuilder(StartProgram.IsOsData);

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
        /// create new file
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
        /// </summary>
        /// <param name="candles">candles</param>
        /// <param name="frameSpan">timeframe of outgoing candles</param>
        /// <returns></returns>
        public static List<Candle> Merge(List<Candle> candles, TimeSpan frameSpan)
        {
            if (candles == null ||
                candles.Count == 0)
            {
                return candles;
            }

            if (frameSpan.TotalMinutes <= 1)
            {
                return candles;
            }

            // агрегация по времени: свеча попадает в то окно,
            // к которому относится её TimeStart. Та же формула границ,
            // что у Simple-реализации серий свечей в движке
            List<Candle> mergeCandles = new List<Candle>();

            Candle current = null;
            DateTime currentWindowStart = DateTime.MinValue;

            for (int i = 0; i < candles.Count; i++)
            {
                DateTime windowStart = GetWindowStart(candles[i].TimeStart, frameSpan);

                if (current == null ||
                    windowStart != currentWindowStart)
                {
                    current = new Candle();
                    current.TimeStart = windowStart;
                    current.Open = candles[i].Open;
                    current.High = candles[i].High;
                    current.Low = candles[i].Low;
                    current.Close = candles[i].Close;
                    current.Volume = candles[i].Volume;

                    if (candles[i].Trades != null)
                    {
                        current.Trades.AddRange(candles[i].Trades);
                    }

                    mergeCandles.Add(current);
                    currentWindowStart = windowStart;
                }
                else
                {
                    if (candles[i].High > current.High)
                    {
                        current.High = candles[i].High;
                    }

                    if (candles[i].Low < current.Low)
                    {
                        current.Low = candles[i].Low;
                    }

                    current.Close = candles[i].Close;
                    current.Volume += candles[i].Volume;

                    if (candles[i].Trades != null)
                    {
                        current.Trades.AddRange(candles[i].Trades);
                    }
                }
            }

            return mergeCandles;
        }

        /// <summary>
        /// start of the time window in which the candle falls
        /// </summary>
        private static DateTime GetWindowStart(DateTime time, TimeSpan frameSpan)
        {
            DateTime start = new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0);

            int frameMinutes = (int)frameSpan.TotalMinutes;

            start = start.AddMinutes(-(start.Minute % frameMinutes));

            return start;
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
        /// save settings to file
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

        // logging

        /// <summary>
        /// send new message to log
        /// </summary>
        public void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// new message event to log
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}