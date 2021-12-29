using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Windows;
using OsEngine.Language;

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

            Label1.Content = OsLocalization.Converter.Label1;
            Label2.Content = OsLocalization.Converter.Label2;
            ButtonSetSource.Content = OsLocalization.Converter.Label3;
            ButtonSetExitFile.Content = OsLocalization.Converter.Label3;
            Label4.Header = OsLocalization.Converter.Label4;
            ButtonStart.Content = OsLocalization.Converter.Label5;

            ComboBoxTimeFrameInitial.Items.Add(TimeFrame.Min1.ToString());
            ComboBoxTimeFrameInitial.Items.Add(TimeFrame.Min5.ToString());
            ComboBoxTimeFrameInitial.SelectedItem = TimeFrame.Min1.ToString();
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            decimal devider = 1;

            if(ComboBoxTimeFrameInitial.SelectedItem.ToString() == TimeFrame.Min5.ToString())
            {
                devider = 5;
            }

            List<Candle> candles = _candleConverter.ReadSourceFile();
            List<Candle> mergedCandles = _candleConverter.Merge(candles, 
                Convert.ToInt32(_candleConverter.ResultCandleTimeFrame / (double)devider));

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