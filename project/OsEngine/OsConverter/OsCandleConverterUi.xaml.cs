using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Windows;

namespace OsEngine.OsConverter
{
    /// <summary>
    /// Interaction logic for OsCandleConverterUi.xaml
    /// </summary>
    public partial class OsCandleConverterUi : Window
    {
        private CandleConverter _candleConverter;

        public OsCandleConverterUi()
        {
            InitializeComponent();

            LabelOsa.Content = "V " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            _candleConverter = new CandleConverter(TextBoxSource, TextBoxExit, ComboBoxTimeFrame, HostLog);
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            List<Candle> candles = _candleConverter.ReadSourceFile();
            List<Candle> mergedCandles = _candleConverter.Merge(candles, Convert.ToInt32(_candleConverter.ResultCandleTimeFrame));
            _candleConverter.WriteExitFile(mergedCandles);
        }

        private void ButtonSetSource_Click(object sender, RoutedEventArgs e)
        {
            _candleConverter.SelectSourceFile();

        }

        private void ButtonSetExitFile_Click(object sender, RoutedEventArgs e)
        {
            _candleConverter.CreateExitFile();
        }
    }
}
