/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using OsEngine.Market;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;


namespace OsEngine.OsData
{
    public partial class SetUpdatingUi : Window
    {
        private OsDataSet _set;

        public SetUpdatingUi(OsDataSet set)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _set = set;

            if (_set.Updater != null)
            {
                ComboBoxRegime.Text = _set.Updater.Regime;
                GetHours(_set.Updater.HourUpdate);
                HourRBut.IsChecked = _set.Updater.Period == "Hour";
            }
            else
            {
                GetHours(0);
            }

            Title = OsLocalization.Data.Label93;
            RegimeLabel.Content = OsLocalization.Data.Label20;
            PeriodLabel.Content = OsLocalization.Data.Label94;
            DayRBut.Content = OsLocalization.Data.Label95;
            HourRBut.Content = OsLocalization.Data.Label96;
            UpdTimeLabel.Content = OsLocalization.Data.Label97;

            Closed += SetUpdatingUi_Closed;

            if (InteractiveInstructions.Data.AllInstructionsInClass == null
             || InteractiveInstructions.Data.AllInstructionsInClass.Count == 0)
            {
                ButtonDataSetUpdating.Visibility = Visibility.Visible;
            }

            StartButtonBlinkAnimation();
        }

        private void SetUpdatingUi_Closed(object sender, EventArgs e)
        {
            try
            {
                if (_blinkTimer != null)
                {
                    _blinkTimer.Stop();
                    _blinkTimer.Tick -= _blinkTimer_Tick;
                    _blinkTimer = null;
                }

                ComboBoxRegime.SelectionChanged -= RegimeChanged;
                DayRBut.Checked -= DayRBut_Checked;
                HourRBut.Checked -= HourRBut_Checked;
                ButtonDataSetUpdating.Click -= ButtonDataSetUpdating_Click;

                _set = null;

                Closed -= SetUpdatingUi_Closed;
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
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
                    PostGreenDataSetUpdating.Opacity = 1;
                    PostWhiteDataSetUpdating.Opacity = 0;
                    return;
                }

                if (_isGreenVisible)
                {
                    PostGreenDataSetUpdating.Opacity = 0;
                    PostWhiteDataSetUpdating.Opacity = 1;
                }
                else
                {
                    PostGreenDataSetUpdating.Opacity = 1;
                    PostWhiteDataSetUpdating.Opacity = 0;
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

        private void HourRBut_Checked(object sender, RoutedEventArgs e) // ежечасно
        {
            try
            {
                DayRBut.Background = Brushes.Transparent;
                HourRBut.Background = Brushes.White;

                if (_set != null && _set.Updater != null)
                {
                    _set.Updater.UpdatePeriod = new TimeSpan(1, 0, 0);
                    _set.Updater.TimeNextUpdate = DateTime.Now.Date.AddHours(DateTime.Now.Hour + 1);

                    _set.Updater.Period = "Hour";

                    _set.Updater.SaveUpdateSettings("Data\\" + _set.SetName + @"\\UpdateSettings.txt");
                }
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void DayRBut_Checked(object sender, RoutedEventArgs e) // ежедневно
        {
            try
            {
                if (UpdTimeLabel == null || TimeBox == null)
                    return;

                HourRBut.Background = Brushes.Transparent;
                DayRBut.Background = Brushes.White;

                if (_set != null && _set.Updater != null)
                {
                    SetDailyPeriod();

                    _set.Updater.SaveUpdateSettings("Data\\" + _set.SetName + @"\\UpdateSettings.txt");
                }
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void GetHours(int selIndex)
        {
            List<string> hours = [];

            for (int i = 0; i < 24; i++)
            {
                hours.Add(i.ToString("00"));
            }

            TimeBox.ItemsSource = hours;
            TimeBox.SelectedIndex = selIndex;
        }

        private void RegimeChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_set == null) return;

                if (!(ComboBoxRegime.SelectedItem is ComboBoxItem selectedItem))
                    return;

                string selectedValue = selectedItem.Content.ToString();

                if (selectedValue == "On") // переключились на On
                {
                    if (_set.Updater != null)
                    {
                        _set.Updater.Regime = "On";
                        return;
                    }

                    _set.Updater = new SetUpdater();

                    if (DayRBut.IsChecked == true)
                    {
                        SetDailyPeriod();
                    }
                    else if (HourRBut.IsChecked == true)
                    {
                        _set.Updater.UpdatePeriod = new TimeSpan(1, 0, 0);
                        _set.Updater.TimeNextUpdate = DateTime.Now.Date.AddHours(DateTime.Now.Hour + 1);

                        _set.Updater.Period = "Hour";
                    }

                    _set.Updater.Regime = "On";
                }
                else  // Переключились на Off
                {
                    if (_set.Updater != null)
                    {
                        _set.Updater.Regime = "Off";
                    }
                }

                _set.Updater.SaveUpdateSettings("Data\\" + _set.SetName + @"\\UpdateSettings.txt");
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
                ComboBoxRegime.SelectedIndex = 0;
                _set.Updater = null;
            }
        }

        private void SetDailyPeriod()
        {
            int updHour = Convert.ToInt32(TimeBox.Text);

            _set.Updater.UpdatePeriod = new TimeSpan(1, 0, 0, 0, 0);
            _set.Updater.HourUpdate = updHour;

            if (DateTime.Now.Hour > updHour)
            {
                _set.Updater.TimeNextUpdate = DateTime.Now.Date.AddDays(1).AddHours(updHour);
            }
            else
            {
                _set.Updater.TimeNextUpdate = DateTime.Now.Date.AddHours(updHour);
            }

            _set.Updater.Period = "Day";
        }

        #region Posts collection

        private void ButtonDataSetUpdating_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InteractiveInstructions.Data.Link6.ShowLinkInBrowser();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion
    }
}

