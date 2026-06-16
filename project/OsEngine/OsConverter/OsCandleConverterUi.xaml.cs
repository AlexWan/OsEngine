/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Windows;
using OsEngine.Language;
using System.Windows.Threading;

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

            if (InteractiveInstructions.Converter.AllInstructionsInClass == null
             || InteractiveInstructions.Converter.AllInstructionsInClass.Count == 0)
            {
                ButtonCandleConverter.Visibility = Visibility.Hidden;
            }

            Closed += OsCandleConverterUi_Closed;

            StartButtonBlinkAnimation();
        }

        private DispatcherTimer _blinkTimer;
        private int _blinkCount;
        private bool _isGreenVisible = true;

        private void StartButtonBlinkAnimation()
        {
            try
            {
                _blinkTimer = new DispatcherTimer();
                _blinkTimer.Interval = TimeSpan.FromMilliseconds(300);
                _blinkTimer.Tick += _blinkTimer_Tick;
                _blinkTimer.Start();
            }
            catch (Exception ex)
            {
                _candleConverter.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _blinkTimer_Tick(object sender, EventArgs e)
        {
            if (_blinkTimer == null)
            {
                return;
            }

            try
            {
                if (_blinkCount >= 20)
                {
                    _blinkTimer.Stop();
                    PostGreenCandleConverter.Opacity = 1;
                    PostWhiteCandleConverter.Opacity = 0;
                    return;
                }

                if (_isGreenVisible)
                {
                    PostGreenCandleConverter.Opacity = 0;
                    PostWhiteCandleConverter.Opacity = 1;
                }
                else
                {
                    PostGreenCandleConverter.Opacity = 1;
                    PostWhiteCandleConverter.Opacity = 0;
                }

                _isGreenVisible = !_isGreenVisible;
                _blinkCount++;
            }
            catch (Exception ex)
            {
                _candleConverter.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                if (_blinkTimer != null)
                {
                    _blinkTimer.Stop();
                }
            }
        }

        private void OsCandleConverterUi_Closed(object sender, EventArgs e)
        {
            try
            {
                if (_blinkTimer != null)
                {
                    _blinkTimer.Stop();
                    _blinkTimer.Tick -= _blinkTimer_Tick;
                    _blinkTimer = null;
                }

                ButtonStart.Click -= ButtonStart_Click;
                ButtonSetSource.Click -= ButtonSetSource_Click;
                ButtonSetExitFile.Click -= ButtonSetExitFile_Click;
                ButtonCandleConverter.Click -= ButtonCandleConverter_Click;

                _candleConverter = null;

                Closed -= OsCandleConverterUi_Closed;
            }
            catch (Exception ex)
            {
                _candleConverter?.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
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

        #region Posts collection

        private void ButtonCandleConverter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InteractiveInstructions.Converter.Link1.ShowLinkInBrowser();
            }
            catch (Exception ex)
            {
                _candleConverter.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion
    }
}