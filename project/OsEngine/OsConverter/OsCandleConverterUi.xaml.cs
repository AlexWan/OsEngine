/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Windows;
using OsEngine.Language;

namespace OsEngine.OsConverter
{
    public partial class OsCandleConverterUi : Window
    {
        private CandleConverter _candleConverter;

        public OsCandleConverterUi()
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
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

            this.Activate();
            this.Focus();
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                decimal divider = 1;

                if (ComboBoxTimeFrameInitial.SelectedItem.ToString() == TimeFrame.Min5.ToString())
                {
                    divider = 5;
                }

                List<Candle> candles = _candleConverter.ReadSourceFile();
                List<Candle> mergedCandles = _candleConverter.Merge(candles,
                    Convert.ToInt32(_candleConverter.ResultCandleTimeFrame / (double)divider));

                _candleConverter.WriteExitFile(mergedCandles);
                _candleConverter.SendNewLogMessage("The operation is complete", Logging.LogMessageType.System);
            }
            catch (Exception ex)
            {
                _candleConverter.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonSetSource_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _candleConverter.SelectSourceFile();
            }
            catch (Exception ex)
            {
                _candleConverter.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonSetExitFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _candleConverter.CreateExitFile();
            }
            catch (Exception ex)
            {
                _candleConverter.SendNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
            }
        }
    }
}