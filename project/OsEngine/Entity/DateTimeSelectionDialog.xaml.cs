/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using OsEngine.Language;

namespace OsEngine.Entity
{
    /// <summary>
    /// Логика взаимодействия для DateTimeSelectionDialog.xaml
    /// </summary>
    public partial class DateTimeSelectionDialog : Window
    {
        public DateTimeSelectionDialog(DateTime initTime)
        {
            InitializeComponent();
            Time = initTime;

            DateTimePicker.SelectedDate = Time;
            TextBoxHour.Text = initTime.Hour.ToString();
            TextBoxMinute.Text = initTime.Minute.ToString();
            TextBoxSecond.Text = initTime.Second.ToString();

            Title = OsLocalization.Entity.TimeChangeDialogLabel1;
            ButtonSave.Content = OsLocalization.Entity.TimeChangeDialogLabel2;
            LabelHour.Content = OsLocalization.Entity.TimeChangeDialogLabel3;
            LabelMinute.Content = OsLocalization.Entity.TimeChangeDialogLabel4;
            LabelSecond.Content = OsLocalization.Entity.TimeChangeDialogLabel5;

            this.Activate();
            this.Focus();
        }

        public DateTime Time;

        public bool IsSaved;

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int hour = Convert.ToInt32(TextBoxHour.Text);
                int min = Convert.ToInt32(TextBoxMinute.Text);
                int sec = Convert.ToInt32(TextBoxSecond.Text);

                Time = new DateTime(DateTimePicker.SelectedDate.Value.Year, DateTimePicker.SelectedDate.Value.Month, DateTimePicker.SelectedDate.Value.Day, hour, min, sec);
                IsSaved = true;
            }
            catch
            {
                return;
            }

            Close();
        }
    }
}
