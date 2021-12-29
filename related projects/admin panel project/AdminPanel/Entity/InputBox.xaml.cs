using System.Windows;

namespace AdminPanel.Entity
{
    /// <summary>
    /// Логика взаимодействия для InputBox.xaml
    /// </summary>
    public partial class InputBox : Window
    {
        public InputBox()
        {
            InitializeComponent();
        }

        public string Code { get; set; }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Code = TbCode.Text;
            Close();
        }
    }
}
