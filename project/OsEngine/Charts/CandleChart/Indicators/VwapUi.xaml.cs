using System;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Interaction Logic for VwapUi.xaml
    /// Логика взаимодействия для VwapUi.xaml
    /// </summary>
    public partial class VwapUi : Window
    {
        private Vwap _indicator;

        /// <summary>
        /// have the indicator settings changed
        /// изменились ли настройки индикатора
        /// </summary>
        public bool IsChange;

        public VwapUi(Vwap indicator)
        {
            InitializeComponent();
            _indicator = indicator;

            UseDate.IsChecked = _indicator.UseDate;

            ((DateTimePicker)DatePickerStart.Child).Value = _indicator.DatePickerStart;
            ((DateTimePicker)DatePickerEnd.Child).Value = _indicator.DatePickerEnd;

            ToEndTicks.IsChecked = _indicator.ToEndTicks;

            ((DateTimePicker)TimePickerStart.Child).Value = _indicator.TimePickerStart;
            ((DateTimePicker)TimePickerEnd.Child).Value = _indicator.TimePickerEnd;

            Deviations2.IsChecked = _indicator.DateDev2;
            Deviations3.IsChecked = _indicator.DateDev3;
            Deviations4.IsChecked = _indicator.DateDev4;



            UseDay.IsChecked = _indicator.UseDay;

            DayDeviations2.IsChecked = _indicator.DayDev2;
            DayDeviations3.IsChecked = _indicator.DayDev3;
            DayDeviations4.IsChecked = _indicator.DayDev4;


            UseWeekly.IsChecked = _indicator.UseWeekly;

            WeekDeviations2.IsChecked = _indicator.WeekDev2;
            WeekDeviations3.IsChecked = _indicator.WeekDev3;
            WeekDeviations4.IsChecked = _indicator.WeekDev4;


            HostColorUp.Child = new System.Windows.Forms.TextBox();
            HostColorUp.Child.BackColor = _indicator.ColorDate;

            HostColorDown.Child = new System.Windows.Forms.TextBox();
            HostColorDown.Child.BackColor = _indicator.ColorDateDev;

            HostColorDayUp.Child = new System.Windows.Forms.TextBox();
            HostColorDayUp.Child.BackColor = _indicator.ColorDay;

            HostColorDayDown.Child = new System.Windows.Forms.TextBox();
            HostColorDayDown.Child.BackColor = _indicator.ColorDayDev;

            HostColorWeekUp.Child = new System.Windows.Forms.TextBox();
            HostColorWeekUp.Child.BackColor = _indicator.ColorWeek;

            HostColorWeekDown.Child = new System.Windows.Forms.TextBox();
            HostColorWeekDown.Child.BackColor = _indicator.ColorWeekDev;
        }

        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            if (UseDate.IsChecked.HasValue)
            {
                _indicator.UseDate = UseDate.IsChecked.Value;
                _indicator.DatePickerStart = ((DateTimePicker)DatePickerStart.Child).Value;
                _indicator.TimePickerStart = ((DateTimePicker)TimePickerStart.Child).Value;

                if (ToEndTicks.IsChecked.HasValue)
                {
                    _indicator.ToEndTicks = ToEndTicks.IsChecked.Value;
                }
                _indicator.DatePickerEnd = ((DateTimePicker)DatePickerEnd.Child).Value;
                _indicator.TimePickerEnd = ((DateTimePicker)TimePickerEnd.Child).Value;


                if (Deviations2.IsChecked != null) _indicator.DateDev2 = Deviations2.IsChecked.Value;
                if (Deviations3.IsChecked != null) _indicator.DateDev3 = Deviations3.IsChecked.Value;
                if (Deviations4.IsChecked != null) _indicator.DateDev4 = Deviations4.IsChecked.Value;
            }

            if (UseDay.IsChecked.HasValue)
            {
                _indicator.UseDay = UseDay.IsChecked.Value;

                if (DayDeviations2.IsChecked != null) _indicator.DayDev2 = DayDeviations2.IsChecked.Value;
                if (DayDeviations3.IsChecked != null) _indicator.DayDev3 = DayDeviations3.IsChecked.Value;
                if (DayDeviations4.IsChecked != null) _indicator.DayDev4 = DayDeviations4.IsChecked.Value;

            }

            if (UseWeekly.IsChecked.HasValue)
            {
                _indicator.UseWeekly = UseWeekly.IsChecked.Value;

                if (WeekDeviations2.IsChecked != null) _indicator.WeekDev2 = WeekDeviations2.IsChecked.Value;
                if (WeekDeviations3.IsChecked != null) _indicator.WeekDev3 = WeekDeviations3.IsChecked.Value;
                if (WeekDeviations4.IsChecked != null) _indicator.WeekDev4 = WeekDeviations4.IsChecked.Value;

            }

            _indicator.PaintOn = true;

            _indicator.ColorDate = HostColorUp.Child.BackColor;
            _indicator.ColorDateDev = HostColorDown.Child.BackColor;

            _indicator.ColorDay = HostColorDayUp.Child.BackColor;
            _indicator.ColorDayDev = HostColorDayDown.Child.BackColor;

            _indicator.ColorWeek = HostColorWeekUp.Child.BackColor;
            _indicator.ColorWeekDev = HostColorWeekDown.Child.BackColor;

            _indicator.Save();

            IsChange = true;
            Close();
        }

        private void ButtonColorUp_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorUp.Child.BackColor;
            dialog.ShowDialog();
            HostColorUp.Child.BackColor = dialog.Color;
        }

        private void ButtonColorDown_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorDown.Child.BackColor;
            dialog.ShowDialog();
            HostColorDown.Child.BackColor = dialog.Color;
        }

        private void ButtonColorDayUp_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorDayUp.Child.BackColor;
            dialog.ShowDialog();
            HostColorDayUp.Child.BackColor = dialog.Color;
        }

        private void ButtonColorDayDown_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorDayDown.Child.BackColor;
            dialog.ShowDialog();
            HostColorDayDown.Child.BackColor = dialog.Color;
        }

        private void ButtonColorWeekUp_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorWeekUp.Child.BackColor;
            dialog.ShowDialog();
            HostColorWeekUp.Child.BackColor = dialog.Color;
        }

        private void ButtonColorWeekDown_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorWeekDown.Child.BackColor;
            dialog.ShowDialog();
            HostColorWeekDown.Child.BackColor = dialog.Color;
        }
    }
}
