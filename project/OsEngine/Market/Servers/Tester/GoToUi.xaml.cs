/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Markup;
using OsEngine.Language;

namespace OsEngine.Market.Servers.Tester
{
    /// <summary>
    /// Логика взаимодействия для GoToUi.xaml
    /// </summary>
    public partial class GoToUi : Window
    {
        public bool IsChange;

        public DateTime TimeStart; 
        
        public DateTime TimeEnd;

        public DateTime TimeGoTo;

        public GoToUi(DateTime timeStart, DateTime timeEnd, DateTime timeNow)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            if ((timeEnd - timeStart).TotalDays <= 0)
            {
                Close();
                return;
            }
            TimeStart = timeStart;
            TimeEnd = timeEnd;
            DateSlider.Maximum = Convert.ToInt32((timeEnd - timeStart).TotalDays);
            TimeGoTo = timeEnd;
            CalendarSelectData.SelectedDate = TimeStart;

            if(timeNow != DateTime.MinValue)
            {
                CalendarSelectData.SelectedDate = timeNow;
            }

            CalendarSelectData.Language = XmlLanguage.GetLanguage(OsLocalization.CurLocalizationCode);

            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            Title = OsLocalization.Charts.Label8;
            LabelGoTo.Content = OsLocalization.Charts.Label9;

            this.Activate();
            this.Focus();
        }

        public void SetLocation(double parentRight, double parentTop)
        {
            this.Left = parentRight - this.Width + 25;
            this.Top = parentTop - 15;
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            IsChange = true;
            Close();
        }

        private void CalendarSelectData_CalendarOpened(object sender, RoutedEventArgs e)
        {
            if(CalendarSelectData.SelectedDate == null)
            {
                return;
            }

            TimeGoTo = (DateTime)CalendarSelectData.SelectedDate;
            SetSlider();
        }

        private void CalendarSelectData_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_sliderIsChanging)
            {
                return;
            }
            if (CalendarSelectData.SelectedDate == null)
            {
                return;
            }

            TimeGoTo = (DateTime)CalendarSelectData.SelectedDate;
            SetSlider();
        }

        bool _sliderIsChanging;

        private void SetSlider()
        {
            _sliderIsChanging = true;

            DateSlider.Value = Convert.ToInt32((TimeGoTo - TimeStart).TotalDays);
            _sliderIsChanging = false;
        }

        private void DateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(_sliderIsChanging)
            {
                return;
            }

            _sliderIsChanging = true;

            int daysFromStart = Convert.ToInt32(DateSlider.Value);

            TimeGoTo = TimeStart.AddDays(daysFromStart);
            CalendarSelectData.SelectedDate = TimeGoTo;

            _sliderIsChanging = false;
        }
    }
}
