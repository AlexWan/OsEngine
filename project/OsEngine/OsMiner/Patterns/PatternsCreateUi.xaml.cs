using System.Windows;


namespace OsEngine.OsMiner.Patterns
{
    /// <summary>
    /// Логика взаимодействия для PatternsCreateChangeUi.xaml
    /// </summary>
    public partial class PatternsCreateUi : Window
    {
        public PatternsCreateUi(int patternNum)
        {
            InitializeComponent();

            TextBoxPatternName.Text = "Паттерн " + patternNum;
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            IsAccepted = true;
            NamePattern = TextBoxPatternName.Text;
            Close();
        }

        public bool IsAccepted;

        public string NamePattern;
    }
}
