using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
