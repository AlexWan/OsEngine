/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Threading;
using OsEngine.Language;

namespace OsEngine.OsConverter
{
    public partial class OsConverterUi
    {
        public OsConverterUi()
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            LabelOsa.Content = "V " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            _master = new OsConverterMaster(TextBoxSource, TextBoxExit, ComboBoxTimeFrame, HostLog);

            Label1.Content = OsLocalization.Converter.Label1;
            Label2.Content = OsLocalization.Converter.Label2;
            ButtonSetSource.Content = OsLocalization.Converter.Label3;
            ButtonSetExitFile.Content = OsLocalization.Converter.Label3;
            Label4.Header = OsLocalization.Converter.Label4;
            ButtonStart.Content = OsLocalization.Converter.Label5;

            this.Activate();
            this.Focus();

            if (InteractiveInstructions.Converter.AllInstructionsInClass == null
             || InteractiveInstructions.Converter.AllInstructionsInClass.Count == 0)
            {
                ButtonConverter.Visibility = Visibility.Hidden;
            }

            Closed += OsConverterUi_Closed;

            StartButtonBlinkAnimation();
        }

        private void OsConverterUi_Closed(object sender, EventArgs e)
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
                ButtonConverter.Click -= ButtonConverter_Click;

                _master = null;

                Closed -= OsConverterUi_Closed;
            }
            catch (Exception ex)
            {
                _master?.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private OsConverterMaster _master;

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
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
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
                    PostGreenConverter.Opacity = 1;
                    PostWhiteConverter.Opacity = 0;
                    return;
                }

                if (_isGreenVisible)
                {
                    PostGreenConverter.Opacity = 0;
                    PostWhiteConverter.Opacity = 1;
                }
                else
                {
                    PostGreenConverter.Opacity = 1;
                    PostWhiteConverter.Opacity = 0;
                }

                _isGreenVisible = !_isGreenVisible;
                _blinkCount++;
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                if (_blinkTimer != null)
                {
                    _blinkTimer.Stop();
                }
            }
        }

        private void ButtonSetSource_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _master.SelectSourceFile();
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonSetExitFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _master.CreateExitFile();
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _master.StartConvert();
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #region Posts collection

        private void ButtonConverter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InteractiveInstructions.Converter.Link1.ShowLinkInBrowser();
            }
            catch (Exception ex)
            {
                _master.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion
    }
}