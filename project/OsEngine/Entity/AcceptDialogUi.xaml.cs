using System.Windows;


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
                ButtonCancel.Content = "Ещё подумать";
            }
            else if (_numMessage == 1)
            {
                ButtonCancel.Content = "Дальше зарабатывать";
            }
            else if (_numMessage == 2)
            {
                ButtonCancel.Content = "Нет!";
            }
            else if (_numMessage == 3)
            {
                ButtonCancel.Content = "Остановится!";
            }
            else if (_numMessage == 4)
            {
                ButtonCancel.Content = "Не нужно так";
            }


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
