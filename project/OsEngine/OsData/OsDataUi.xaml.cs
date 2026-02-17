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

        public OsDataUi()
        {
            InitializeComponent();
            LabelTimeEndValue.Content = "";
            LabelSetNameValue.Content = "";
            LabelTimeStartValue.Content = "";
            Layout.StickyBorders.Listen(this);

            OsDataMaster master = new OsDataMaster();

            _osDataMaster = new OsDataMasterPainter(master, 
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

        private void StartButtonBlinkAnimation()
        {
            try
            {
                DispatcherTimer timer = new DispatcherTimer();
                int blinkCount = 0;
                bool isGreenVisible = true;

                timer.Interval = TimeSpan.FromMilliseconds(300);
                timer.Tick += (s, e) =>
                {
                    try
                    {
                        if (blinkCount >= 20)
                        {
                            timer.Stop();
                            GreenCollectionData.Opacity = 1;
                            WhiteCollectionData.Opacity = 0;
                            return;
                        }

                        if (isGreenVisible)
                        {
                            GreenCollectionData.Opacity = 0;
                            WhiteCollectionData.Opacity = 1;
                        }
                        else
                        {
                            GreenCollectionData.Opacity = 1;
                            WhiteCollectionData.Opacity = 0;
                        }

                        isGreenVisible = !isGreenVisible;
                        blinkCount++;
                    }
                    catch (Exception ex)
                    {
                        ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                        timer.Stop();
                    }
                };

                timer.Start();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void OsDataUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Data.Label27);
            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                e.Cancel = true;
            }

            _osDataMaster.Dispose();
            _osDataMaster = null;
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