/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;

namespace OsEngine.OsConverter
{
    /// <summary>
    /// Логика взаимодействия для OsConverterUi.xaml
    /// </summary>
    public partial class OsConverterUi
    {
        public OsConverterUi()
        {
            InitializeComponent();

            _master = new OsConverterMaster(TextBoxSource, TextBoxExit, ComboBoxTimeFrame, HostLog);
        }

        private OsConverterMaster _master;

        private void ButtonSetSource_Click(object sender, RoutedEventArgs e)
        {
            _master.SelectSourceFile();
        }

        private void ButtonSetExitFile_Click(object sender, RoutedEventArgs e)
        {
            _master.CreateExitFile();
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            _master.StartConvert();
        }
    }
}
