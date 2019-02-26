/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using OsEngine.Language;

namespace OsEngine.OsConverter
{
    /// <summary>
    /// Interaction Logic for OsConverterUi.xaml/Логика взаимодействия для OsConverterUi.xaml
    /// </summary>
    public partial class OsConverterUi
    {
        public OsConverterUi()
        {
            InitializeComponent();

            LabelOsa.Content = "V " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            _master = new OsConverterMaster(TextBoxSource, TextBoxExit, ComboBoxTimeFrame, HostLog);

            Label1.Content = OsLocalization.Converter.Label1;
            Label2.Content = OsLocalization.Converter.Label2;
            ButtonSetSource.Content = OsLocalization.Converter.Label3;
            ButtonSetExitFile.Content = OsLocalization.Converter.Label3;
            Label4.Header = OsLocalization.Converter.Label4;
            ButtonStart.Content = OsLocalization.Converter.Label5;

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
