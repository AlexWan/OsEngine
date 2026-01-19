/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using OsEngine.Market;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            if (_periods.NonTradePeriodGeneral.NonTradePeriod1Start.TimeSpan >= _periods.NonTradePeriodGeneral.NonTradePeriod1End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodGeneral.NonTradePeriod1OnOff, OsLocalization.Trader.Label462, numberPeriod: 1, $"{_periods.NonTradePeriodGeneral.NonTradePeriod1Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodGeneral.NonTradePeriod1End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodGeneral.NonTradePeriod2Start.TimeSpan >= _periods.NonTradePeriodGeneral.NonTradePeriod2End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodGeneral.NonTradePeriod2OnOff, OsLocalization.Trader.Label462, numberPeriod: 2, $"{_periods.NonTradePeriodGeneral.NonTradePeriod2Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodGeneral.NonTradePeriod2End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodGeneral.NonTradePeriod3Start.TimeSpan >= _periods.NonTradePeriodGeneral.NonTradePeriod3End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodGeneral.NonTradePeriod3OnOff, OsLocalization.Trader.Label462, numberPeriod: 3, $"{_periods.NonTradePeriodGeneral.NonTradePeriod3Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodGeneral.NonTradePeriod3End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodGeneral.NonTradePeriod4Start.TimeSpan >= _periods.NonTradePeriodGeneral.NonTradePeriod4End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodGeneral.NonTradePeriod4OnOff, OsLocalization.Trader.Label462, numberPeriod: 4, $"{_periods.NonTradePeriodGeneral.NonTradePeriod4Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodGeneral.NonTradePeriod4End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodGeneral.NonTradePeriod5Start.TimeSpan >= _periods.NonTradePeriodGeneral.NonTradePeriod5End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodGeneral.NonTradePeriod4OnOff, OsLocalization.Trader.Label462, numberPeriod: 5, $"{_periods.NonTradePeriodGeneral.NonTradePeriod5Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodGeneral.NonTradePeriod5End.TimeSpan.ToString()}");
            }

            if (_periods.NonTradePeriodMonday.NonTradePeriod1Start.TimeSpan >= _periods.NonTradePeriodMonday.NonTradePeriod1End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodMonday.NonTradePeriod1OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label625, numberPeriod: 1, $"{_periods.NonTradePeriodMonday.NonTradePeriod1Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodMonday.NonTradePeriod1End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodMonday.NonTradePeriod2Start.TimeSpan >= _periods.NonTradePeriodMonday.NonTradePeriod2End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodMonday.NonTradePeriod2OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label625, numberPeriod: 2, $"{_periods.NonTradePeriodMonday.NonTradePeriod2Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodMonday.NonTradePeriod2End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodMonday.NonTradePeriod3Start.TimeSpan >= _periods.NonTradePeriodMonday.NonTradePeriod3End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodMonday.NonTradePeriod3OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label625, numberPeriod: 3, $"{_periods.NonTradePeriodMonday.NonTradePeriod3Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodMonday.NonTradePeriod3End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodMonday.NonTradePeriod4Start.TimeSpan >= _periods.NonTradePeriodMonday.NonTradePeriod4End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodMonday.NonTradePeriod4OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label625, numberPeriod: 4, $"{_periods.NonTradePeriodMonday.NonTradePeriod4Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodMonday.NonTradePeriod4End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodMonday.NonTradePeriod5Start.TimeSpan >= _periods.NonTradePeriodMonday.NonTradePeriod5End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodMonday.NonTradePeriod5OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label625, numberPeriod: 5, $"{_periods.NonTradePeriodMonday.NonTradePeriod5Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodMonday.NonTradePeriod5End.TimeSpan.ToString()}");
            }

            if (_periods.NonTradePeriodTuesday.NonTradePeriod1Start.TimeSpan >= _periods.NonTradePeriodTuesday.NonTradePeriod1End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodTuesday.NonTradePeriod1OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label626, numberPeriod: 1, $"{_periods.NonTradePeriodTuesday.NonTradePeriod1Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodTuesday.NonTradePeriod1End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodTuesday.NonTradePeriod2Start.TimeSpan >= _periods.NonTradePeriodTuesday.NonTradePeriod2End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodTuesday.NonTradePeriod2OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label626, numberPeriod: 2, $"{_periods.NonTradePeriodTuesday.NonTradePeriod2Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodTuesday.NonTradePeriod2End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodTuesday.NonTradePeriod3Start.TimeSpan >= _periods.NonTradePeriodTuesday.NonTradePeriod3End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodTuesday.NonTradePeriod3OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label626, numberPeriod: 3, $"{_periods.NonTradePeriodTuesday.NonTradePeriod3Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodTuesday.NonTradePeriod3End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodTuesday.NonTradePeriod4Start.TimeSpan >= _periods.NonTradePeriodTuesday.NonTradePeriod4End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodTuesday.NonTradePeriod4OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label626, numberPeriod: 4, $"{_periods.NonTradePeriodTuesday.NonTradePeriod4Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodTuesday.NonTradePeriod4End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodTuesday.NonTradePeriod5Start.TimeSpan >= _periods.NonTradePeriodTuesday.NonTradePeriod5End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodTuesday.NonTradePeriod5OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label626, numberPeriod: 5, $"{_periods.NonTradePeriodTuesday.NonTradePeriod5Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodTuesday.NonTradePeriod5End.TimeSpan.ToString()}");
            }

            if (_periods.NonTradePeriodWednesday.NonTradePeriod1Start.TimeSpan >= _periods.NonTradePeriodWednesday.NonTradePeriod1End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodWednesday.NonTradePeriod1OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label627, numberPeriod: 1, $"{_periods.NonTradePeriodWednesday.NonTradePeriod1Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodWednesday.NonTradePeriod1Start.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodWednesday.NonTradePeriod2Start.TimeSpan >= _periods.NonTradePeriodWednesday.NonTradePeriod2End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodWednesday.NonTradePeriod2OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label627, numberPeriod: 2, $"{_periods.NonTradePeriodWednesday.NonTradePeriod2Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodWednesday.NonTradePeriod2Start.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodWednesday.NonTradePeriod3Start.TimeSpan >= _periods.NonTradePeriodWednesday.NonTradePeriod3End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodWednesday.NonTradePeriod3OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label627, numberPeriod: 3, $"{_periods.NonTradePeriodWednesday.NonTradePeriod3Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodWednesday.NonTradePeriod3Start.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodWednesday.NonTradePeriod4Start.TimeSpan >= _periods.NonTradePeriodWednesday.NonTradePeriod4End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodWednesday.NonTradePeriod4OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label627, numberPeriod: 4, $"{_periods.NonTradePeriodWednesday.NonTradePeriod4Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodWednesday.NonTradePeriod4Start.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodWednesday.NonTradePeriod5Start.TimeSpan >= _periods.NonTradePeriodWednesday.NonTradePeriod5End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodWednesday.NonTradePeriod5OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label627, numberPeriod: 5, $"{_periods.NonTradePeriodWednesday.NonTradePeriod5Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodWednesday.NonTradePeriod5Start.TimeSpan.ToString()}");
            }

            if (_periods.NonTradePeriodThursday.NonTradePeriod1Start.TimeSpan >= _periods.NonTradePeriodThursday.NonTradePeriod1End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodThursday.NonTradePeriod1OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label628, numberPeriod: 1, $"{_periods.NonTradePeriodThursday.NonTradePeriod1Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodThursday.NonTradePeriod1End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodThursday.NonTradePeriod2Start.TimeSpan >= _periods.NonTradePeriodThursday.NonTradePeriod2End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodThursday.NonTradePeriod2OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label628, numberPeriod: 2, $"{_periods.NonTradePeriodThursday.NonTradePeriod2Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodThursday.NonTradePeriod2End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodThursday.NonTradePeriod3Start.TimeSpan >= _periods.NonTradePeriodThursday.NonTradePeriod3End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodThursday.NonTradePeriod3OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label628, numberPeriod: 3, $"{_periods.NonTradePeriodThursday.NonTradePeriod3Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodThursday.NonTradePeriod3End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodThursday.NonTradePeriod4Start.TimeSpan >= _periods.NonTradePeriodThursday.NonTradePeriod4End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodThursday.NonTradePeriod4OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label628, numberPeriod: 4, $"{_periods.NonTradePeriodThursday.NonTradePeriod4Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodThursday.NonTradePeriod4End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodThursday.NonTradePeriod5Start.TimeSpan >= _periods.NonTradePeriodThursday.NonTradePeriod5End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodThursday.NonTradePeriod5OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label628, numberPeriod: 5, $"{_periods.NonTradePeriodThursday.NonTradePeriod5Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodThursday.NonTradePeriod5End.TimeSpan.ToString()}");
            }

            if (_periods.NonTradePeriodFriday.NonTradePeriod1Start.TimeSpan >= _periods.NonTradePeriodFriday.NonTradePeriod1End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodFriday.NonTradePeriod1OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label629, numberPeriod: 1, $"{_periods.NonTradePeriodFriday.NonTradePeriod1Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodFriday.NonTradePeriod1End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodFriday.NonTradePeriod2Start.TimeSpan >= _periods.NonTradePeriodFriday.NonTradePeriod2End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodFriday.NonTradePeriod2OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label629, numberPeriod: 2, $"{_periods.NonTradePeriodFriday.NonTradePeriod2Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodFriday.NonTradePeriod2End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodFriday.NonTradePeriod3Start.TimeSpan >= _periods.NonTradePeriodFriday.NonTradePeriod3End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodFriday.NonTradePeriod3OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label629, numberPeriod: 3, $"{_periods.NonTradePeriodFriday.NonTradePeriod3Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodFriday.NonTradePeriod3End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodFriday.NonTradePeriod4Start.TimeSpan >= _periods.NonTradePeriodFriday.NonTradePeriod4End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodFriday.NonTradePeriod4OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label629, numberPeriod: 4, $"{_periods.NonTradePeriodFriday.NonTradePeriod4Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodFriday.NonTradePeriod4End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodFriday.NonTradePeriod5Start.TimeSpan >= _periods.NonTradePeriodFriday.NonTradePeriod5End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodFriday.NonTradePeriod5OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label629, numberPeriod: 5, $"{_periods.NonTradePeriodFriday.NonTradePeriod5Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodFriday.NonTradePeriod5End.TimeSpan.ToString()}");
            }

            if (_periods.NonTradePeriodSaturday.NonTradePeriod1Start.TimeSpan >= _periods.NonTradePeriodSaturday.NonTradePeriod1End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodSaturday.NonTradePeriod1OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label630, numberPeriod: 1, $"{_periods.NonTradePeriodSaturday.NonTradePeriod1Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodSaturday.NonTradePeriod1End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodSaturday.NonTradePeriod2Start.TimeSpan >= _periods.NonTradePeriodSaturday.NonTradePeriod2End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodSaturday.NonTradePeriod2OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label630, numberPeriod: 2, $"{_periods.NonTradePeriodSaturday.NonTradePeriod2Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodSaturday.NonTradePeriod2End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodSaturday.NonTradePeriod3Start.TimeSpan >= _periods.NonTradePeriodSaturday.NonTradePeriod3End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodSaturday.NonTradePeriod3OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label630, numberPeriod: 3, $"{_periods.NonTradePeriodSaturday.NonTradePeriod3Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodSaturday.NonTradePeriod3End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodSaturday.NonTradePeriod4Start.TimeSpan >= _periods.NonTradePeriodSaturday.NonTradePeriod4End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodSaturday.NonTradePeriod4OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label630, numberPeriod: 4, $"{_periods.NonTradePeriodSaturday.NonTradePeriod4Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodSaturday.NonTradePeriod4End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodSaturday.NonTradePeriod5Start.TimeSpan >= _periods.NonTradePeriodSaturday.NonTradePeriod5End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodSaturday.NonTradePeriod5OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label630, numberPeriod: 5, $"{_periods.NonTradePeriodSaturday.NonTradePeriod5Start.TimeSpan.ToString()} >= {_periods.NonTradePeriodSaturday.NonTradePeriod5End.TimeSpan.ToString()}");
            }

            if (_periods.NonTradePeriodSunday.NonTradePeriod1Start.TimeSpan >= _periods.NonTradePeriodSunday.NonTradePeriod1End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodSunday.NonTradePeriod1OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label631, numberPeriod: 1, $"{_periods.NonTradePeriodSunday.NonTradePeriod1Start.TimeSpan.ToString()} > {_periods.NonTradePeriodSunday.NonTradePeriod1End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodSunday.NonTradePeriod2Start.TimeSpan >= _periods.NonTradePeriodSunday.NonTradePeriod2End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodSunday.NonTradePeriod2OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label631, numberPeriod: 2, $"{_periods.NonTradePeriodSunday.NonTradePeriod2Start.TimeSpan.ToString()} > {_periods.NonTradePeriodSunday.NonTradePeriod2End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodSunday.NonTradePeriod3Start.TimeSpan >= _periods.NonTradePeriodSunday.NonTradePeriod3End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodSunday.NonTradePeriod3OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label631, numberPeriod: 3, $"{_periods.NonTradePeriodSunday.NonTradePeriod3Start.TimeSpan.ToString()} > {_periods.NonTradePeriodSunday.NonTradePeriod3End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodSunday.NonTradePeriod4Start.TimeSpan >= _periods.NonTradePeriodSunday.NonTradePeriod4End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodSunday.NonTradePeriod4OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label631, numberPeriod: 4, $"{_periods.NonTradePeriodSunday.NonTradePeriod4Start.TimeSpan.ToString()} > {_periods.NonTradePeriodSunday.NonTradePeriod4End.TimeSpan.ToString()}");
            }
            if (_periods.NonTradePeriodSunday.NonTradePeriod5Start.TimeSpan >= _periods.NonTradePeriodSunday.NonTradePeriod5End.TimeSpan)
            {
                OffPeriod(ref _periods.NonTradePeriodSunday.NonTradePeriod5OnOff, OsLocalization.Trader.Label624 + ". " + OsLocalization.Trader.Label631, numberPeriod: 5, $"{_periods.NonTradePeriodSunday.NonTradePeriod5Start.TimeSpan.ToString()} > {_periods.NonTradePeriodSunday.NonTradePeriod5End.TimeSpan.ToString()}");
            }

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
            // -- Period 1 -- \\ 

            NonTradeChartElement nonTradeChartPeriod1 = new NonTradeChartElement();
            nonTradeChartPeriod1.NonTradePeriodCheckBox = checkBoxNonTradePeriod1OnOff;
            nonTradeChartPeriod1.NonTradePeriodStartTextBox = nonTradePeriod1Start;
            nonTradeChartPeriod1.NonTradePeriodEndTextBox = nonTradePeriod1End;

            checkBoxNonTradePeriod1OnOff.IsChecked = period.NonTradePeriod1OnOff;
            checkBoxNonTradePeriod1OnOff.Checked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod1OnOff_Checked(sender, e, period, nonTradeChartPeriod1);
            };
            checkBoxNonTradePeriod1OnOff.Unchecked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod1OnOff_Checked(sender, e, period, nonTradeChartPeriod1);
            };

            nonTradePeriod1Start.Text = period.NonTradePeriod1Start.ToString();
            nonTradePeriod1Start.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod1Start_TextChanged(sender, e, period, nonTradeChartPeriod1);
            };

            nonTradePeriod1End.Text = period.NonTradePeriod1End.ToString();
            nonTradePeriod1End.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod1End_TextChanged(sender, e, period, nonTradeChartPeriod1);
            };

            CheckNonTradePeriods(nonTradeChartPeriod1, period.NonTradePeriod1Start, period.NonTradePeriod1End);

            // ---- \\

            // -- Period 2 -- \\

            NonTradeChartElement nonTradeChartPeriod2 = new NonTradeChartElement();
            nonTradeChartPeriod2.NonTradePeriodCheckBox = checkBoxNonTradePeriod2OnOff;
            nonTradeChartPeriod2.NonTradePeriodStartTextBox = nonTradePeriod2Start;
            nonTradeChartPeriod2.NonTradePeriodEndTextBox = nonTradePeriod2End;

            checkBoxNonTradePeriod2OnOff.IsChecked = period.NonTradePeriod2OnOff;
            checkBoxNonTradePeriod2OnOff.Checked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod2OnOff_Checked(sender, e, period, nonTradeChartPeriod2);
            };
            checkBoxNonTradePeriod2OnOff.Unchecked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod2OnOff_Checked(sender, e, period, nonTradeChartPeriod2);
            };

            nonTradePeriod2Start.Text = period.NonTradePeriod2Start.ToString();
            nonTradePeriod2Start.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod2Start_TextChanged(sender, e, period, nonTradeChartPeriod2);
            };

            nonTradePeriod2End.Text = period.NonTradePeriod2End.ToString();
            nonTradePeriod2End.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod2End_TextChanged(sender, e, period, nonTradeChartPeriod2);
            };

            CheckNonTradePeriods(nonTradeChartPeriod2, period.NonTradePeriod2Start, period.NonTradePeriod2End);

            // ---- \\

            // -- Period 3 -- \\

            NonTradeChartElement nonTradeChartPeriod3 = new NonTradeChartElement();
            nonTradeChartPeriod3.NonTradePeriodCheckBox = checkBoxNonTradePeriod3OnOff;
            nonTradeChartPeriod3.NonTradePeriodStartTextBox = nonTradePeriod3Start;
            nonTradeChartPeriod3.NonTradePeriodEndTextBox = nonTradePeriod3End;

            checkBoxNonTradePeriod3OnOff.IsChecked = period.NonTradePeriod3OnOff;
            checkBoxNonTradePeriod3OnOff.Checked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod3OnOff_Checked(sender, e, period, nonTradeChartPeriod3);
            };
            checkBoxNonTradePeriod3OnOff.Unchecked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod3OnOff_Checked(sender, e, period, nonTradeChartPeriod3);
            };

            nonTradePeriod3Start.Text = period.NonTradePeriod3Start.ToString();
            nonTradePeriod3Start.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod3Start_TextChanged(sender, e, period, nonTradeChartPeriod3);
            };

            nonTradePeriod3End.Text = period.NonTradePeriod3End.ToString();
            nonTradePeriod3End.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod3End_TextChanged(sender, e, period, nonTradeChartPeriod3);
            };

            CheckNonTradePeriods(nonTradeChartPeriod3, period.NonTradePeriod3Start, period.NonTradePeriod3End);

            // ---- \\

            // -- Period 4 -- \\

            NonTradeChartElement nonTradeChartPeriod4 = new NonTradeChartElement();
            nonTradeChartPeriod4.NonTradePeriodCheckBox = checkBoxNonTradePeriod4OnOff;
            nonTradeChartPeriod4.NonTradePeriodStartTextBox = nonTradePeriod4Start;
            nonTradeChartPeriod4.NonTradePeriodEndTextBox = nonTradePeriod4End;

            checkBoxNonTradePeriod4OnOff.IsChecked = period.NonTradePeriod4OnOff;
            checkBoxNonTradePeriod4OnOff.Checked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod4OnOff_Checked(sender, e, period, nonTradeChartPeriod4);
            };
            checkBoxNonTradePeriod4OnOff.Unchecked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod4OnOff_Checked(sender, e, period, nonTradeChartPeriod4);
            };

            nonTradePeriod4Start.Text = period.NonTradePeriod4Start.ToString();
            nonTradePeriod4Start.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod4Start_TextChanged(sender, e, period, nonTradeChartPeriod4);
            };

            nonTradePeriod4End.Text = period.NonTradePeriod4End.ToString();
            nonTradePeriod4End.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod4End_TextChanged(sender, e, period, nonTradeChartPeriod4);
            };

            CheckNonTradePeriods(nonTradeChartPeriod4, period.NonTradePeriod4Start, period.NonTradePeriod4End);

            // ---- \\

            // -- Period 5 -- \\

            NonTradeChartElement nonTradeChartPeriod5 = new NonTradeChartElement();
            nonTradeChartPeriod5.NonTradePeriodCheckBox = checkBoxNonTradePeriod5OnOff;
            nonTradeChartPeriod5.NonTradePeriodStartTextBox = nonTradePeriod5Start;
            nonTradeChartPeriod5.NonTradePeriodEndTextBox = nonTradePeriod5End;

            checkBoxNonTradePeriod5OnOff.IsChecked = period.NonTradePeriod5OnOff;
            checkBoxNonTradePeriod5OnOff.Checked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod5OnOff_Checked(sender, e, period, nonTradeChartPeriod5);
            };
            checkBoxNonTradePeriod5OnOff.Unchecked += (object sender, RoutedEventArgs e) =>
            {
                CheckBoxNonTradePeriod5OnOff_Checked(sender, e, period, nonTradeChartPeriod5);
            };

            nonTradePeriod5Start.Text = period.NonTradePeriod5Start.ToString();
            nonTradePeriod5Start.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod5Start_TextChanged(sender, e, period, nonTradeChartPeriod5);
            };

            nonTradePeriod5End.Text = period.NonTradePeriod5End.ToString();
            nonTradePeriod5End.TextChanged += (object sender, TextChangedEventArgs e) =>
            {
                TextBoxNonTradePeriod5End_TextChanged(sender, e, period, nonTradeChartPeriod5);
            };

            CheckNonTradePeriods(nonTradeChartPeriod5, period.NonTradePeriod5Start, period.NonTradePeriod5End);

            // ---- \\
        }

        #endregion

        #region Non trade periods

        private bool _repaintNow = false;

        private void CheckBoxNonTradePeriod1OnOff_Checked(object sender, RoutedEventArgs e, NonTradePeriodInDay period, NonTradeChartElement chartElement)
        {
            try
            {
                if (_repaintNow == true)
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

        private void TextBoxNonTradePeriod1Start_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period, NonTradeChartElement chartElement)
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

                CheckNonTradePeriods(chartElement, period.NonTradePeriod1Start, period.NonTradePeriod1End);

            }
            catch
            {
                if (chartElement != null && chartElement.NonTradePeriodStartTextBox != null && chartElement.NonTradePeriodEndTextBox != null)
                {
                    chartElement.NonTradePeriodStartTextBox.Background = Brushes.Red;
                    chartElement.NonTradePeriodEndTextBox.Background = Brushes.Red;
                }
            }
        }

        private void TextBoxNonTradePeriod1End_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period, NonTradeChartElement chartElement)
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

                CheckNonTradePeriods(chartElement, period.NonTradePeriod1Start, period.NonTradePeriod1End);
            }
            catch
            {
                if (chartElement != null && chartElement.NonTradePeriodStartTextBox != null && chartElement.NonTradePeriodEndTextBox != null)
                {
                    chartElement.NonTradePeriodStartTextBox.Background = Brushes.Red;
                    chartElement.NonTradePeriodEndTextBox.Background = Brushes.Red;
                }
            }
        }

        private void CheckBoxNonTradePeriod2OnOff_Checked(object sender, RoutedEventArgs e, NonTradePeriodInDay period, NonTradeChartElement chartElement)
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

        private void TextBoxNonTradePeriod2Start_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period, NonTradeChartElement chartElement)
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

                CheckNonTradePeriods(chartElement, period.NonTradePeriod2Start, period.NonTradePeriod2End);
            }
            catch
            {
                if (chartElement != null && chartElement.NonTradePeriodStartTextBox != null && chartElement.NonTradePeriodEndTextBox != null)
                {
                    chartElement.NonTradePeriodStartTextBox.Background = Brushes.Red;
                    chartElement.NonTradePeriodEndTextBox.Background = Brushes.Red;
                }
            }
        }

        private void TextBoxNonTradePeriod2End_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period, NonTradeChartElement chartElement)
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

                CheckNonTradePeriods(chartElement, period.NonTradePeriod2Start, period.NonTradePeriod2End);
            }
            catch
            {
                if (chartElement != null && chartElement.NonTradePeriodStartTextBox != null && chartElement.NonTradePeriodEndTextBox != null)
                {
                    chartElement.NonTradePeriodStartTextBox.Background = Brushes.Red;
                    chartElement.NonTradePeriodEndTextBox.Background = Brushes.Red;
                }
            }
        }

        private void CheckBoxNonTradePeriod3OnOff_Checked(object sender, RoutedEventArgs e, NonTradePeriodInDay period, NonTradeChartElement chartElement)
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

        private void TextBoxNonTradePeriod3Start_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period, NonTradeChartElement chartElement)
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

                CheckNonTradePeriods(chartElement, period.NonTradePeriod3Start, period.NonTradePeriod3End);
            }
            catch
            {
                if (chartElement != null && chartElement.NonTradePeriodStartTextBox != null && chartElement.NonTradePeriodEndTextBox != null)
                {
                    chartElement.NonTradePeriodStartTextBox.Background = Brushes.Red;
                    chartElement.NonTradePeriodEndTextBox.Background = Brushes.Red;
                }
            }
        }

        private void TextBoxNonTradePeriod3End_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period, NonTradeChartElement chartElement)
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

                CheckNonTradePeriods(chartElement, period.NonTradePeriod3Start, period.NonTradePeriod3End);
            }
            catch
            {
                if (chartElement != null && chartElement.NonTradePeriodStartTextBox != null && chartElement.NonTradePeriodEndTextBox != null)
                {
                    chartElement.NonTradePeriodStartTextBox.Background = Brushes.Red;
                    chartElement.NonTradePeriodEndTextBox.Background = Brushes.Red;
                }
            }
        }

        private void CheckBoxNonTradePeriod4OnOff_Checked(object sender, RoutedEventArgs e, NonTradePeriodInDay period, NonTradeChartElement chartElement)
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

        private void TextBoxNonTradePeriod4Start_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period, NonTradeChartElement chartElement)
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

                CheckNonTradePeriods(chartElement, period.NonTradePeriod4Start, period.NonTradePeriod4End);
            }
            catch
            {
                if (chartElement != null && chartElement.NonTradePeriodStartTextBox != null && chartElement.NonTradePeriodEndTextBox != null)
                {
                    chartElement.NonTradePeriodStartTextBox.Background = Brushes.Red;
                    chartElement.NonTradePeriodEndTextBox.Background = Brushes.Red;
                }
            }
        }

        private void TextBoxNonTradePeriod4End_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period, NonTradeChartElement chartElement)
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

                CheckNonTradePeriods(chartElement, period.NonTradePeriod4Start, period.NonTradePeriod4End);
            }
            catch
            {
                if (chartElement != null && chartElement.NonTradePeriodStartTextBox != null && chartElement.NonTradePeriodEndTextBox != null)
                {
                    chartElement.NonTradePeriodStartTextBox.Background = Brushes.Red;
                    chartElement.NonTradePeriodEndTextBox.Background = Brushes.Red;
                }
            }
        }

        private void CheckBoxNonTradePeriod5OnOff_Checked(object sender, RoutedEventArgs e, NonTradePeriodInDay period, NonTradeChartElement chartElement)
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

        private void TextBoxNonTradePeriod5Start_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period, NonTradeChartElement chartElement)
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

                CheckNonTradePeriods(chartElement, period.NonTradePeriod5Start, period.NonTradePeriod5End);
            }
            catch
            {
                if (chartElement != null && chartElement.NonTradePeriodStartTextBox != null && chartElement.NonTradePeriodEndTextBox != null)
                {
                    chartElement.NonTradePeriodStartTextBox.Background = Brushes.Red;
                    chartElement.NonTradePeriodEndTextBox.Background = Brushes.Red;
                }
            }
        }

        private void TextBoxNonTradePeriod5End_TextChanged(object sender, TextChangedEventArgs e, NonTradePeriodInDay period, NonTradeChartElement chartElement)
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

                CheckNonTradePeriods(chartElement, period.NonTradePeriod5Start, period.NonTradePeriod5End);
            }
            catch
            {
                if (chartElement != null && chartElement.NonTradePeriodStartTextBox != null && chartElement.NonTradePeriodEndTextBox != null)
                {
                    chartElement.NonTradePeriodStartTextBox.Background = Brushes.Red;
                    chartElement.NonTradePeriodEndTextBox.Background = Brushes.Red;
                }
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
                        for (int i = 0; i < array.Count; i++)
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
                        while (reader.EndOfStream == false)
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

        #region Helpers

        private void OffPeriod(ref bool nonTradePeriodInDayOnOff, string nameTab, int numberPeriod, string errorTime)
        {
            try
            {
                nonTradePeriodInDayOnOff = false;
                _periods.Save();
                ServerMaster.SendNewLogMessage($"{OsLocalization.Trader.Label666}. {this.Name}. {nameTab}. {OsLocalization.Trader.Label559} {numberPeriod} {OsLocalization.Trader.Label667}. {OsLocalization.Trader.Label668}: {errorTime}.", Logging.LogMessageType.Error);
            }
            catch
            {
                // ignore
            }
        }

        private void CheckNonTradePeriods(NonTradeChartElement chartElement, TimeOfDay nonTradePeriodStart, TimeOfDay nonTradePeriodEnd)
        {
            try
            {
                TimeSpan startDayTime = new TimeSpan(0, 0, 0, 0);

                if (nonTradePeriodStart.TimeSpan < startDayTime ||
                    nonTradePeriodEnd.TimeSpan < startDayTime ||
                    nonTradePeriodStart.TimeSpan >= nonTradePeriodEnd.TimeSpan)
                {
                    chartElement.NonTradePeriodStartTextBox.Background = Brushes.Red;
                    chartElement.NonTradePeriodEndTextBox.Background = Brushes.Red;
                }
                else
                {
                    chartElement.NonTradePeriodStartTextBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF111217"));
                    chartElement.NonTradePeriodEndTextBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF111217"));
                }
            }
            catch
            {
                // ignore
            }
        }

        private class NonTradeChartElement
        {
            public CheckBox NonTradePeriodCheckBox;
            public TextBox NonTradePeriodStartTextBox;
            public TextBox NonTradePeriodEndTextBox;
        }

        #endregion
    }
}
