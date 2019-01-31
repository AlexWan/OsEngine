using System.Windows;
using OsEngine.Language;


namespace OsEngine.Entity
{
    /// <summary>
    /// Логика взаимодействия для AcceptDialogUi.xaml
    /// </summary>
    public partial class AcceptDialogUi
    {

        /// <summary>
        /// номер надписи которая будет выведена на кнопку
        /// </summary>
        private static int _numMessage;

        /// <summary>
        /// Пользователь одобрил проводитмое действие
        /// </summary>
        public bool UserAcceptActioin;

        /// <summary>
        /// конструктор окна
        /// </summary>
        /// <param name="text">текст который будет выведен в качестве основного сообщения пользователю. То что он должен будет одобрить</param>
        public AcceptDialogUi(string text)
        {
            InitializeComponent();
            LabelText.Content = text;

            _numMessage++;

            if (_numMessage > 4)
            {
                _numMessage = 0;
            }

            if (_numMessage == 0)
            {
                ButtonCancel.Content = OsLocalization.Entity.ButtonCancel1;
            }
            else if (_numMessage == 1)
            {
                ButtonCancel.Content = OsLocalization.Entity.ButtonCancel2;
            }
            else if (_numMessage == 2)
            {
                ButtonCancel.Content = OsLocalization.Entity.ButtonCancel3;
            }
            else if (_numMessage == 3)
            {
                ButtonCancel.Content = OsLocalization.Entity.ButtonCancel4;
            }
            else if (_numMessage == 4)
            {
                ButtonCancel.Content = OsLocalization.Entity.ButtonCancel5;
            }

            Title = OsLocalization.Entity.TitleAcceptDialog;
            ButtonAccept.Content = OsLocalization.Entity.ButtonAccept;

        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            UserAcceptActioin = false;
            Close();
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            UserAcceptActioin = true;
            Close();
        }
    }
}
