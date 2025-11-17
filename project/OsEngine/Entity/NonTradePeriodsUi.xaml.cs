/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Connectors;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;

namespace OsEngine.Entity
{
    public partial class NonTradePeriodsUi : Window
    {
        private NonTradePeriods _periods;

        private void SetCurrentSettingsInForm()
        {
            _repaintNow = true;

            CheckBoxTradeInMonday.IsChecked = _periods.TradeInMonday;
            CheckBoxTradeInMonday.Checked -= CheckBoxTradeInMonday_Checked;
            CheckBoxTradeInMonday.Unchecked -= CheckBoxTradeInMonday_Checked;
            CheckBoxTradeInMonday.Checked += CheckBoxTradeInMonday_Checked;
            CheckBoxTradeInMonday.Unchecked += CheckBoxTradeInMonday_Checked;

            CheckBoxTradeInTuesday.IsChecked = _periods.TradeInTuesday;
            CheckBoxTradeInTuesday.Checked -= CheckBoxTradeInTuesday_Checked;
            CheckBoxTradeInTuesday.Unchecked -= CheckBoxTradeInTuesday_Checked;
            CheckBoxTradeInTuesday.Checked += CheckBoxTradeInTuesday_Checked;
            CheckBoxTradeInTuesday.Unchecked += CheckBoxTradeInTuesday_Checked;

            CheckBoxTradeInWednesday.IsChecked = _periods.TradeInWednesday;
            CheckBoxTradeInWednesday.Checked -= CheckBoxTradeInWednesday_Checked;
            CheckBoxTradeInWednesday.Unchecked -= CheckBoxTradeInWednesday_Checked;
            CheckBoxTradeInWednesday.Checked += CheckBoxTradeInWednesday_Checked;
            CheckBoxTradeInWednesday.Unchecked += CheckBoxTradeInWednesday_Checked;

            CheckBoxTradeInThursday.IsChecked = _periods.TradeInThursday;
            CheckBoxTradeInThursday.Checked -= CheckBoxTradeInThursday_Checked;
            CheckBoxTradeInThursday.Unchecked -= CheckBoxTradeInThursday_Checked;
            CheckBoxTradeInThursday.Checked += CheckBoxTradeInThursday_Checked;
            CheckBoxTradeInThursday.Unchecked += CheckBoxTradeInThursday_Checked;

            CheckBoxTradeInFriday.IsChecked = _periods.TradeInFriday;
            CheckBoxTradeInFriday.Checked -= CheckBoxTradeInFriday_Checked;
            CheckBoxTradeInFriday.Unchecked -= CheckBoxTradeInFriday_Checked;
            CheckBoxTradeInFriday.Checked += CheckBoxTradeInFriday_Checked;
            CheckBoxTradeInFriday.Unchecked += CheckBoxTradeInFriday_Checked;

            CheckBoxTradeInSaturday.IsChecked = _periods.TradeInSaturday;
            CheckBoxTradeInSaturday.Checked -= CheckBoxTradeInSaturday_Checked;
            CheckBoxTradeInSaturday.Unchecked -= CheckBoxTradeInSaturday_Checked;
            CheckBoxTradeInSaturday.Checked += CheckBoxTradeInSaturday_Checked;
            CheckBoxTradeInSaturday.Unchecked += CheckBoxTradeInSaturday_Checked;

            CheckBoxTradeInSunday.IsChecked = _periods.TradeInSunday;
            CheckBoxTradeInSunday.Checked -= CheckBoxTradeInSunday_Checked;
            CheckBoxTradeInSunday.Unchecked -= CheckBoxTradeInSunday_Checked;
            CheckBoxTradeInSunday.Checked += CheckBoxTradeInSunday_Checked;
            CheckBoxTradeInSunday.Unchecked += CheckBoxTradeInSunday_Checked;

            // non trade periods
            CheckPeriods(_periods.NonTradePeriodGeneral,
                CheckBoxNonTradePeriod1OnOff,
                TextBoxNonTradePeriod1Start,
                TextBoxNonTradePeriod1End,
                CheckBoxNonTradePeriod2OnOff,
                TextBoxNonTradePeriod2Start,
                TextBoxNonTradePeriod2End,
                CheckBoxNonTradePeriod3OnOff,
                TextBoxNonTradePeriod3Start,
                TextBoxNonTradePeriod3End,
                CheckBoxNonTradePeriod4OnOff,
                TextBoxNonTradePeriod4Start,
                TextBoxNonTradePeriod4End,
                CheckBoxNonTradePeriod5OnOff,
                TextBoxNonTradePeriod5Start,
                TextBoxNonTradePeriod5End);

            CheckPeriods(_periods.NonTradePeriodMonday,
                CheckBoxNonTradePeriod1OnOff_Monday,
                TextBoxNonTradePeriod1Start_Monday,
                TextBoxNonTradePeriod1End_Monday,
                CheckBoxNonTradePeriod2OnOff_Monday,
                TextBoxNonTradePeriod2Start_Monday,
                TextBoxNonTradePeriod2End_Monday,
                CheckBoxNonTradePeriod3OnOff_Monday,
                TextBoxNonTradePeriod3Start_Monday,
                TextBoxNonTradePeriod3End_Monday,
                CheckBoxNonTradePeriod4OnOff_Monday,
                TextBoxNonTradePeriod4Start_Monday,
                TextBoxNonTradePeriod4End_Monday,
                CheckBoxNonTradePeriod5OnOff_Monday,
                TextBoxNonTradePeriod5Start_Monday,
                TextBoxNonTradePeriod5End_Monday);

            CheckPeriods(_periods.NonTradePeriodTuesday,
                CheckBoxNonTradePeriod1OnOff_Tuesday,
                TextBoxNonTradePeriod1Start_Tuesday,
                TextBoxNonTradePeriod1End_Tuesday,
                CheckBoxNonTradePeriod2OnOff_Tuesday,
                TextBoxNonTradePeriod2Start_Tuesday,
                TextBoxNonTradePeriod2End_Tuesday,
                CheckBoxNonTradePeriod3OnOff_Tuesday,
                TextBoxNonTradePeriod3Start_Tuesday,
                TextBoxNonTradePeriod3End_Tuesday,
                CheckBoxNonTradePeriod4OnOff_Tuesday,
                TextBoxNonTradePeriod4Start_Tuesday,
                TextBoxNonTradePeriod4End_Tuesday,
                CheckBoxNonTradePeriod5OnOff_Tuesday,
                TextBoxNonTradePeriod5Start_Tuesday,
                TextBoxNonTradePeriod5End_Tuesday);

            CheckPeriods(_periods.NonTradePeriodWednesday,
                CheckBoxNonTradePeriod1OnOff_Wednesday,
                TextBoxNonTradePeriod1Start_Wednesday,
                TextBoxNonTradePeriod1End_Wednesday,
                CheckBoxNonTradePeriod2OnOff_Wednesday,
                TextBoxNonTradePeriod2Start_Wednesday,
                TextBoxNonTradePeriod2End_Wednesday,
                CheckBoxNonTradePeriod3OnOff_Wednesday,
                TextBoxNonTradePeriod3Start_Wednesday,
                TextBoxNonTradePeriod3End_Wednesday,
                CheckBoxNonTradePeriod4OnOff_Wednesday,
                TextBoxNonTradePeriod4Start_Wednesday,
                TextBoxNonTradePeriod4End_Wednesday,
                CheckBoxNonTradePeriod5OnOff_Wednesday,
                TextBoxNonTradePeriod5Start_Wednesday,
                TextBoxNonTradePeriod5End_Wednesday);

            CheckPeriods(_periods.NonTradePeriodThursday,
                CheckBoxNonTradePeriod1OnOff_Thursday,
                TextBoxNonTradePeriod1Start_Thursday,
                TextBoxNonTradePeriod1End_Thursday,
                CheckBoxNonTradePeriod2OnOff_Thursday,
                TextBoxNonTradePeriod2Start_Thursday,
                TextBoxNonTradePeriod2End_Thursday,
                CheckBoxNonTradePeriod3OnOff_Thursday,
                TextBoxNonTradePeriod3Start_Thursday,
                TextBoxNonTradePeriod3End_Thursday,
                CheckBoxNonTradePeriod4OnOff_Thursday,
                TextBoxNonTradePeriod4Start_Thursday,
                TextBoxNonTradePeriod4End_Thursday,
                CheckBoxNonTradePeriod5OnOff_Thursday,
                TextBoxNonTradePeriod5Start_Thursday,
                TextBoxNonTradePeriod5End_Thursday);

            CheckPeriods(_periods.NonTradePeriodFriday,
                CheckBoxNonTradePeriod1OnOff_Friday,
                TextBoxNonTradePeriod1Start_Friday,
                TextBoxNonTradePeriod1End_Friday,
                CheckBoxNonTradePeriod2OnOff_Friday,
                TextBoxNonTradePeriod2Start_Friday,
                TextBoxNonTradePeriod2End_Friday,
                CheckBoxNonTradePeriod3OnOff_Friday,
                TextBoxNonTradePeriod3Start_Friday,
                TextBoxNonTradePeriod3End_Friday,
                CheckBoxNonTradePeriod4OnOff_Friday,
                TextBoxNonTradePeriod4Start_Friday,
                TextBoxNonTradePeriod4End_Friday,
                CheckBoxNonTradePeriod5OnOff_Friday,
                TextBoxNonTradePeriod5Start_Friday,
                TextBoxNonTradePeriod5End_Friday);

            CheckPeriods(_periods.NonTradePeriodSaturday,
                CheckBoxNonTradePeriod1OnOff_Saturday,
                TextBoxNonTradePeriod1Start_Saturday,
                TextBoxNonTradePeriod1End_Saturday,
                CheckBoxNonTradePeriod2OnOff_Saturday,
                TextBoxNonTradePeriod2Start_Saturday,
                TextBoxNonTradePeriod2End_Saturday,
                CheckBoxNonTradePeriod3OnOff_Saturday,
                TextBoxNonTradePeriod3Start_Saturday,
                TextBoxNonTradePeriod3End_Saturday,
                CheckBoxNonTradePeriod4OnOff_Saturday,
                TextBoxNonTradePeriod4Start_Saturday,
                TextBoxNonTradePeriod4End_Saturday,
                CheckBoxNonTradePeriod5OnOff_Saturday,
                TextBoxNonTradePeriod5Start_Saturday,
                TextBoxNonTradePeriod5End_Saturday);

            CheckPeriods(_periods.NonTradePeriodSunday,
                CheckBoxNonTradePeriod1OnOff_Sunday,
                TextBoxNonTradePeriod1Start_Sunday,
                TextBoxNonTradePeriod1End_Sunday,
                CheckBoxNonTradePeriod2OnOff_Sunday,
                TextBoxNonTradePeriod2Start_Sunday,
                TextBoxNonTradePeriod2End_Sunday,
                CheckBoxNonTradePeriod3OnOff_Sunday,
                TextBoxNonTradePeriod3Start_Sunday,
                TextBoxNonTradePeriod3End_Sunday,
                CheckBoxNonTradePeriod4OnOff_Sunday,
                TextBoxNonTradePeriod4Start_Sunday,
                TextBoxNonTradePeriod4End_Sunday,
                CheckBoxNonTradePeriod5OnOff_Sunday,
                TextBoxNonTradePeriod5Start_Sunday,
                TextBoxNonTradePeriod5End_Sunday);


            _repaintNow = false;
        }

        public NonTradePeriodsUi(NonTradePeriods periods)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _periods = periods;

            SetCurrentSettingsInForm();

            // localization

            Title = OsLocalization.Trader.Label575;
            TabItemNonTradePeriods.Header = OsLocalization.Trader.Label462;
            TabItemByDays.Header = OsLocalization.Trader.Label624;

            TabItemMonday.Header = OsLocalization.Trader.Label625;
            TabItemTuesday.Header = OsLocalization.Trader.Label626;
            TabItemWednesday.Header = OsLocalization.Trader.Label627;
            TabItemThursday.Header = OsLocalization.Trader.Label628;
            TabItemFriday.Header = OsLocalization.Trader.Label629;
            TabItemSaturday.Header = OsLocalization.Trader.Label630;
            TabItemSunday.Header = OsLocalization.Trader.Label631;

            ButtonLoadSet.Content = OsLocalization.Market.Label98;
            ButtonSaveSet.Content = OsLocalization.Market.Label99;


            // general non trade periods 
            LocalToPeriods(
            CheckBoxNonTradePeriod1OnOff, CheckBoxNonTradePeriod2OnOff,
            CheckBoxNonTradePeriod3OnOff, CheckBoxNonTradePeriod4OnOff,
            CheckBoxNonTradePeriod5OnOff);

            LocalToPeriods(
            CheckBoxNonTradePeriod1OnOff_Monday, CheckBoxNonTradePeriod2OnOff_Monday,
            CheckBoxNonTradePeriod3OnOff_Monday, CheckBoxNonTradePeriod4OnOff_Monday,
            CheckBoxNonTradePeriod5OnOff_Monday);

            LocalToPeriods(
            CheckBoxNonTradePeriod1OnOff_Tuesday, CheckBoxNonTradePeriod2OnOff_Tuesday,
            CheckBoxNonTradePeriod3OnOff_Tuesday, CheckBoxNonTradePeriod4OnOff_Tuesday,
            CheckBoxNonTradePeriod5OnOff_Tuesday);

            LocalToPeriods(
            CheckBoxNonTradePeriod1OnOff_Wednesday, CheckBoxNonTradePeriod2OnOff_Wednesday,
            CheckBoxNonTradePeriod3OnOff_Wednesday, CheckBoxNonTradePeriod4OnOff_Wednesday,
            CheckBoxNonTradePeriod5OnOff_Wednesday);

            LocalToPeriods(
            CheckBoxNonTradePeriod1OnOff_Thursday, CheckBoxNonTradePeriod2OnOff_Thursday,
            CheckBoxNonTradePeriod3OnOff_Thursday, CheckBoxNonTradePeriod4OnOff_Thursday,
            CheckBoxNonTradePeriod5OnOff_Thursday);

            LocalToPeriods(
            CheckBoxNonTradePeriod1OnOff_Friday, CheckBoxNonTradePeriod2OnOff_Friday,
            CheckBoxNonTradePeriod3OnOff_Friday, CheckBoxNonTradePeriod4OnOff_Friday,
            CheckBoxNonTradePeriod5OnOff_Friday);

            LocalToPeriods(
            CheckBoxNonTradePeriod1OnOff_Saturday, CheckBoxNonTradePeriod2OnOff_Saturday,
            CheckBoxNonTradePeriod3OnOff_Saturday, CheckBoxNonTradePeriod4OnOff_Saturday,
            CheckBoxNonTradePeriod5OnOff_Saturday);

            LocalToPeriods(
            CheckBoxNonTradePeriod1OnOff_Sunday, CheckBoxNonTradePeriod2OnOff_Sunday,
            CheckBoxNonTradePeriod3OnOff_Sunday, CheckBoxNonTradePeriod4OnOff_Sunday,
            CheckBoxNonTradePeriod5OnOff_Sunday);

            // trade days 
            CheckBoxTradeInMonday.Content = OsLocalization.Trader.Label474;
            CheckBoxTradeInTuesday.Content = OsLocalization.Trader.Label475;
            CheckBoxTradeInWednesday.Content = OsLocalization.Trader.Label476;
            CheckBoxTradeInThursday.Content = OsLocalization.Trader.Label477;
            CheckBoxTradeInFriday.Content = OsLocalization.Trader.Label478;
            CheckBoxTradeInSaturday.Content = OsLocalization.Trader.Label479;
            CheckBoxTradeInSunday.Content = OsLocalization.Trader.Label480;

            this.Closed += NonTradePeriodsUi_Closed;
        }

        private void LocalToPeriods(
            CheckBox checkBoxNonTradePeriod1OnOff,
            CheckBox checkBoxNonTradePeriod2OnOff,
            CheckBox checkBoxNonTradePeriod3OnOff,
             CheckBox checkBoxNonTradePeriod4OnOff,
              CheckBox checkBoxNonTradePeriod5OnOff)
        {
            checkBoxNonTradePeriod1OnOff.Content = OsLocalization.Trader.Label473 + " 1";
            checkBoxNonTradePeriod2OnOff.Content = OsLocalization.Trader.Label473 + " 2";
            checkBoxNonTradePeriod3OnOff.Content = OsLocalization.Trader.Label473 + " 3";
            checkBoxNonTradePeriod4OnOff.Content = OsLocalization.Trader.Label473 + " 4";
            checkBoxNonTradePeriod5OnOff.Content = OsLocalization.Trader.Label473 + " 5";
        }

        private void NonTradePeriodsUi_Closed(object sender, EventArgs e)
        {
            _periods = null;

        }

        #region Trade days 

        private void CheckBoxTradeInMonday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                _periods.TradeInMonday = CheckBoxTradeInMonday.IsChecked.Value;
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInTuesday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                _periods.TradeInTuesday = CheckBoxTradeInTuesday.IsChecked.Value;
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInWednesday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                _periods.TradeInWednesday = CheckBoxTradeInWednesday.IsChecked.Value;
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInThursday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                _periods.TradeInThursday = CheckBoxTradeInThursday.IsChecked.Value;
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInFriday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                _periods.TradeInFriday = CheckBoxTradeInFriday.IsChecked.Value;
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInSaturday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                _periods.TradeInSaturday = CheckBoxTradeInSaturday.IsChecked.Value;
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInSunday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                _periods.TradeInSunday = CheckBoxTradeInSunday.IsChecked.Value;
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Non trade periods

        private void CheckPeriods(NonTradePeriodInDay period,

        CheckBox checkBoxNonTradePeriod1OnOff,
        TextBox nonTradePeriod1Start,
        TextBox nonTradePeriod1End,

        CheckBox checkBoxNonTradePeriod2OnOff,
        TextBox nonTradePeriod2Start,
        TextBox nonTradePeriod2End,

        CheckBox checkBoxNonTradePeriod3OnOff,
        TextBox nonTradePeriod3Start,
        TextBox nonTradePeriod3End,

        CheckBox checkBoxNonTradePeriod4OnOff,
        TextBox nonTradePeriod4Start,
        TextBox nonTradePeriod4End,

        CheckBox checkBoxNonTradePeriod5OnOff,
        TextBox nonTradePeriod5Start,
        TextBox nonTradePeriod5End)
        {

            checkBoxNonTradePeriod1OnOff.IsChecked = period.NonTradePeriod1OnOff;

            checkBoxNonTradePeriod1OnOff.Checked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod1OnOff_Checked(sender, e, period);
            };
            checkBoxNonTradePeriod1OnOff.Unchecked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod1OnOff_Checked(sender, e, period);
            };

            checkBoxNonTradePeriod2OnOff.IsChecked = period.NonTradePeriod2OnOff;
            checkBoxNonTradePeriod2OnOff.Checked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod2OnOff_Checked(sender, e, period);
            };
            checkBoxNonTradePeriod2OnOff.Unchecked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod2OnOff_Checked(sender, e, period);
            };

            checkBoxNonTradePeriod3OnOff.IsChecked = period.NonTradePeriod3OnOff;
            checkBoxNonTradePeriod3OnOff.Checked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod3OnOff_Checked(sender, e, period);
            };
            checkBoxNonTradePeriod3OnOff.Unchecked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod3OnOff_Checked(sender, e, period);
            };

            checkBoxNonTradePeriod4OnOff.IsChecked = period.NonTradePeriod4OnOff;
            checkBoxNonTradePeriod4OnOff.Checked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod4OnOff_Checked(sender, e, period);
            };
            checkBoxNonTradePeriod4OnOff.Unchecked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod4OnOff_Checked(sender, e, period);
            };

            checkBoxNonTradePeriod5OnOff.IsChecked = period.NonTradePeriod5OnOff;
            checkBoxNonTradePeriod5OnOff.Checked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod5OnOff_Checked(sender, e, period);
            };
            checkBoxNonTradePeriod5OnOff.Unchecked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod5OnOff_Checked(sender, e, period);
            };


            nonTradePeriod1Start.Text = period.NonTradePeriod1Start.ToString();
            nonTradePeriod1Start.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod1Start_TextChanged(sender, e, period);
            };
            nonTradePeriod2Start.Text = period.NonTradePeriod2Start.ToString();
            nonTradePeriod2Start.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod2Start_TextChanged(sender, e, period);
            };
            nonTradePeriod3Start.Text = period.NonTradePeriod3Start.ToString();
            nonTradePeriod3Start.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod3Start_TextChanged(sender, e, period);
            };
            nonTradePeriod4Start.Text = period.NonTradePeriod4Start.ToString();
            nonTradePeriod4Start.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod4Start_TextChanged(sender, e, period);
            };
            nonTradePeriod5Start.Text = period.NonTradePeriod5Start.ToString();
            nonTradePeriod5Start.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod5Start_TextChanged(sender, e, period);
            };


            nonTradePeriod1End.Text = period.NonTradePeriod1End.ToString();
            nonTradePeriod1End.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod1End_TextChanged(sender, e, period);
            };
            nonTradePeriod2End.Text = period.NonTradePeriod2End.ToString();
            nonTradePeriod2End.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod2End_TextChanged(sender, e, period);
            };
            nonTradePeriod3End.Text = period.NonTradePeriod3End.ToString();
            nonTradePeriod3End.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod3End_TextChanged(sender, e, period);
            };
            nonTradePeriod4End.Text = period.NonTradePeriod4End.ToString();
            nonTradePeriod4End.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod4End_TextChanged(sender, e, period);
            };
            nonTradePeriod5End.Text = period.NonTradePeriod5End.ToString();
            nonTradePeriod5End.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod5End_TextChanged(sender, e, period);
            };
        }

        #endregion

        #region Non trade periods

        private bool _repaintNow = false;

        private void CheckBoxNonTradePeriod1OnOff_Checked(object sender, RoutedEventArgs e, NonTradePeriodInDay period)
        {
            try
            {
                if(_repaintNow == true)
                {
                    return;
                }
                CheckBox box = (CheckBox)sender;

                period.NonTradePeriod1OnOff = box.IsChecked.Value;
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod1Start_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period)
        {
            try
            {
                if (_repaintNow == true)
                {
                    return;
                }
                TextBox box = (TextBox)sender;

                if (string.IsNullOrEmpty(box.Text))
                {
                    return;
                }

                period.NonTradePeriod1Start.LoadFromString(box.Text);
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod1End_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period)
        {
            try
            {
                if (_repaintNow == true)
                {
                    return;
                }
                TextBox box = (TextBox)sender;

                if (string.IsNullOrEmpty(box.Text))
                {
                    return;
                }

                period.NonTradePeriod1End.LoadFromString(box.Text);
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }



        private void CheckBoxNonTradePeriod2OnOff_Checked(object sender, RoutedEventArgs e, NonTradePeriodInDay period)
        {
            try
            {
                if (_repaintNow == true)
                {
                    return;
                }
                CheckBox box = (CheckBox)sender;

                period.NonTradePeriod2OnOff = box.IsChecked.Value;
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod2Start_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period)
        {
            try
            {
                if (_repaintNow == true)
                {
                    return;
                }
                TextBox box = (TextBox)sender;

                if (string.IsNullOrEmpty(box.Text))
                {
                    return;
                }

                period.NonTradePeriod2Start.LoadFromString(box.Text);
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod2End_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period)
        {
            try
            {
                if (_repaintNow == true)
                {
                    return;
                }
                TextBox box = (TextBox)sender;

                if (string.IsNullOrEmpty(box.Text))
                {
                    return;
                }

                period.NonTradePeriod2End.LoadFromString(box.Text);
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }



        private void CheckBoxNonTradePeriod3OnOff_Checked(object sender, RoutedEventArgs e, NonTradePeriodInDay period)
        {
            try
            {
                if (_repaintNow == true)
                {
                    return;
                }
                CheckBox box = (CheckBox)sender;

                period.NonTradePeriod3OnOff = box.IsChecked.Value;
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod3Start_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period)
        {
            try
            {
                if (_repaintNow == true)
                {
                    return;
                }
                TextBox box = (TextBox)sender;

                if (string.IsNullOrEmpty(box.Text))
                {
                    return;
                }

                period.NonTradePeriod3Start.LoadFromString(box.Text);
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod3End_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period)
        {
            try
            {
                if (_repaintNow == true)
                {
                    return;
                }
                TextBox box = (TextBox)sender;

                if (string.IsNullOrEmpty(box.Text))
                {
                    return;
                }

                period.NonTradePeriod3End.LoadFromString(box.Text);
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }



        private void CheckBoxNonTradePeriod4OnOff_Checked(object sender, RoutedEventArgs e, NonTradePeriodInDay period)
        {
            try
            {
                if (_repaintNow == true)
                {
                    return;
                }

                CheckBox box = (CheckBox)sender;

                period.NonTradePeriod4OnOff = box.IsChecked.Value;
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod4Start_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period)
        {
            try
            {
                if (_repaintNow == true)
                {
                    return;
                }
                TextBox box = (TextBox)sender;

                if (string.IsNullOrEmpty(box.Text))
                {
                    return;
                }

                period.NonTradePeriod4Start.LoadFromString(box.Text);
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod4End_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period)
        {
            try
            {
                if (_repaintNow == true)
                {
                    return;
                }
                TextBox box = (TextBox)sender;

                if (string.IsNullOrEmpty(box.Text))
                {
                    return;
                }

                period.NonTradePeriod4End.LoadFromString(box.Text);
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }



        private void CheckBoxNonTradePeriod5OnOff_Checked(object sender, RoutedEventArgs e, NonTradePeriodInDay period)
        {
            try
            {
                if (_repaintNow == true)
                {
                    return;
                }
                CheckBox box = (CheckBox)sender;

                period.NonTradePeriod5OnOff = box.IsChecked.Value;
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod5Start_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period)
        {
            try
            {
                if (_repaintNow == true)
                {
                    return;
                }
                TextBox box = (TextBox)sender;

                if (string.IsNullOrEmpty(box.Text))
                {
                    return;
                }

                period.NonTradePeriod5Start.LoadFromString(box.Text);
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod5End_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period)
        {
            try
            {
                if (_repaintNow == true)
                {
                    return;
                }
                TextBox box = (TextBox)sender;

                if (string.IsNullOrEmpty(box.Text))
                {
                    return;
                }

                period.NonTradePeriod5End.LoadFromString(box.Text);
                _periods.Save();
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Save or Load settings

        private void ButtonSaveSet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
                saveFileDialog.InitialDirectory = System.Windows.Forms.Application.StartupPath;
                saveFileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";

                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.ShowDialog();

                if (string.IsNullOrEmpty(saveFileDialog.FileName))
                {
                    return;
                }

                string filePath = saveFileDialog.FileName;

                if (File.Exists(filePath) == false)
                {
                    using (FileStream stream = File.Create(filePath))
                    {
                        // do nothin
                    }
                }

                try
                {
                    List<string> array = _periods.GetFullSaveArray();

                    using (StreamWriter writer = new StreamWriter(filePath))
                    {
                        for(int i = 0;i < array.Count;i++)
                        {
                            writer.WriteLine(array[i]);
                        }
                    }
                }
                catch (Exception error)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(error.ToString());
                    ui.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(ex.ToString());
                ui.ShowDialog();
            }
        }

        private void ButtonLoadSet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
                openFileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.InitialDirectory = System.Windows.Forms.Application.StartupPath;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.ShowDialog();

                if (string.IsNullOrEmpty(openFileDialog.FileName))
                {
                    return;
                }

                string filePath = openFileDialog.FileName;

                if (File.Exists(filePath) == false)
                {
                    return;
                }

                try
                {
                    List<string> array = new List<string>();

                    using (StreamReader reader = new StreamReader(filePath))
                    {
                        while(reader.EndOfStream == false)
                        {
                            array.Add(reader.ReadLine());
                        }
                    }

                    _periods.LoadFromSaveArray(array);
                }
                catch (Exception error)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(error.ToString());
                    ui.ShowDialog();
                }

                SetCurrentSettingsInForm();

                _periods.Save();
            }
            catch (Exception ex)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(ex.ToString());
                ui.ShowDialog();
            }
        }

        #endregion
    }
}
