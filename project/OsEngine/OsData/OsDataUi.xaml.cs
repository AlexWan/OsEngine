/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using System.Windows;
using OsEngine.Language;
using System.Windows.Threading;
using System;
using OsEngine.Market;
using OsEngine.Instructions;

namespace OsEngine.OsData
{
    public partial class OsDataUi
    {
        private OsDataMasterPainter _osDataMaster;

        /// <summary>
        /// Underlying OsData master for MCP API integration.
        /// </summary>
        public OsDataMaster Master { get; private set; }

        public OsDataUi()
        {
            InitializeComponent();
            LabelTimeEndValue.Content = "";
            LabelSetNameValue.Content = "";
            LabelTimeStartValue.Content = "";
            Layout.StickyBorders.Listen(this);
            Layout.StartupLocation.Start_FitHeightToWorkArea(this);

            Master = new OsDataMaster();

            _osDataMaster = new OsDataMasterPainter(Master,
                ChartHostPanel, HostLog, HostSource,
                HostSet, LabelSetNameValue, LabelTimeStartValue,
                LabelTimeEndValue, ProgressBarLoadProgress, TextBoxSearchSource);

            LabelOsa.Content = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Closing += OsDataUi_Closing;
            Label4.Content = OsLocalization.Data.Label4;
            Label24.Content = OsLocalization.Data.Label24;
            Label26.Header = OsLocalization.Data.Label26;
            NewDataSetButton.Content = OsLocalization.Data.Label30;
            LabelSetName.Content = OsLocalization.Data.Label31;
            LabelStartTimeStr.Content = OsLocalization.Data.Label18;
            LabelTimeEndStr.Content = OsLocalization.Data.Label19;
            TextBoxSearchSource.Text = OsLocalization.Market.Label64;

            this.Activate();
            this.Focus();

            _osDataMaster.StartPaintActiveSet();

            if (InteractiveInstructions.Data.AllInstructionsInClass == null
             || InteractiveInstructions.Data.AllInstructionsInClass.Count == 0)
            {
                ButtonPostsData.Visibility = Visibility.Hidden;
            }
            else
            {
                ButtonPostsData.Click += ButtonPostsData_Click;
            }

            StartButtonBlinkAnimation();
        }

        private void OsDataUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                bool isProgrammaticClose = false;

                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    isProgrammaticClose = mainWindow.IsProgrammaticClose;
                }

                if (!isProgrammaticClose)
                {
                    AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Data.Label27);
                    ui.ShowDialog();

                    if (ui.UserAcceptAction == false)
                    {
                        e.Cancel = true;
                        return;
                    }
                }

                if (_osDataMaster != null)
                {
                    _osDataMaster.StopPaintActiveSet();
                    _osDataMaster.Dispose();
                    _osDataMaster = null;
                }

                if (ChartHostPanel != null)
                {
                    ChartHostPanel.Child = null;
                }
                if (HostSource != null)
                {
                    HostSource.Child = null;
                }
                if (HostSet != null)
                {
                    HostSet.Child = null;
                }
                if (HostLog != null)
                {
                    HostLog.Child = null;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                e.Cancel = false;
            }
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
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
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
                    _blinkTimer.Tick -= _blinkTimer_Tick;
                    _blinkTimer = null;
                    GreenCollectionData.Opacity = 1;
                    WhiteCollectionData.Opacity = 0;
                    return;
                }

                if (_isGreenVisible)
                {
                    GreenCollectionData.Opacity = 0;
                    WhiteCollectionData.Opacity = 1;
                }
                else
                {
                    GreenCollectionData.Opacity = 1;
                    WhiteCollectionData.Opacity = 0;
                }

                _isGreenVisible = !_isGreenVisible;
                _blinkCount++;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                if (_blinkTimer != null)
                {
                    _blinkTimer.Stop();
                }
            }
        }

        private void NewDataSetButton_Click(object sender, RoutedEventArgs e)
        {
            _osDataMaster.CreateNewSetDialog();
        }

        #region Posts collection

        private InstructionsUi _instructionsUi;

        private void ButtonPostsData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_instructionsUi == null)
                {
                    _instructionsUi = new InstructionsUi(
                        InteractiveInstructions.Data.AllInstructionsInClass, InteractiveInstructions.Data.AllInstructionsInClassDescription);
                    _instructionsUi.Show();
                    _instructionsUi.Closed += _instructionsUi_Closed;
                }
                else
                {
                    if (_instructionsUi.WindowState == WindowState.Minimized)
                    {
                        _instructionsUi.WindowState = WindowState.Normal;
                    }
                    _instructionsUi.Activate();
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _instructionsUi_Closed(object sender, EventArgs e)
        {
            try
            {
                _instructionsUi.Closed -= _instructionsUi_Closed;
                _instructionsUi = null;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion
    }
}