using OsEngine.Market;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OsEngine.Robots.AutoTestBots
{
    /// <summary>
    /// Логика взаимодействия для TestBotConnectionParams.xaml
    /// </summary>
    public partial class TestBotConnectionParams : Window
    {

        TestBotConnection testBotConnection;
        public TestBotConnectionParams(TestBotConnection testBot)
        {
            InitializeComponent();

            TextBoxCountToReLoadServer.Text = "5";
            TextBoxSecondToReloadServer.Text = "120";
            TextBoxCountTabsToConnectServer.Text = "10";
            testBotConnection = testBot;

            for (int i = 0; i < ServerMaster.ServersTypes.Count; i++)
            {
                ComboBoxConnectorEnum.Items.Add(ServerMaster.ServersTypes[i].ToString());
            }
            ComboBoxConnectorEnum.SelectedItem = ServerMaster.ServersTypes[0].ToString();

            testBotConnection.CreateServer((string)ComboBoxConnectorEnum.SelectedItem);

            ComboBoxConnectorEnum.SelectionChanged += ComboBoxConnectorEnum_SelectionChanged;
            ButtonServerSettingsShow.Click += ButtonServerSettingsShow_Click;
            ButtonStartTest.Click += ButtonStartTest_Click;
            ButtonStopTest.Click += ButtonStopTest_Click;
            Closed += TestBotConnectionParams_Closed;
        }



        #region Events
        private void ButtonStopTest_Click(object sender, RoutedEventArgs e)
        {
            if (testBotConnection.TestingIsStart == true)
            {
                testBotConnection.TestingIsNeedStop = true;
            }
        }

        private void TestBotConnectionParams_Closed(object sender, System.EventArgs e)
        {
            testBotConnection.testBotConnectionParams = null;
        }

        private void ButtonStartTest_Click(object sender, RoutedEventArgs e)
        {
            string server = ComboBoxConnectorEnum.SelectedItem.ToString();

            bool ValidateISuccess = ValidateTextBox();



            if (ValidateISuccess)
            {
                int CountToReLoadServer = Convert.ToInt32(TextBoxCountToReLoadServer.Text);
                int SecondToReloadServer = Convert.ToInt32(TextBoxSecondToReloadServer.Text);
                int CountTabsToConnectServer = Convert.ToInt32(TextBoxCountTabsToConnectServer.Text);

                new Thread(() =>
                {
                    testBotConnection.StartTestingConnector(server, CountToReLoadServer,
                        SecondToReloadServer, CountTabsToConnectServer);
                }).Start();
            }
            else
            {
                new Task(() =>
                {
                    MessageBox.Show("Validate Error");
                }).Start();
            }

        }

        private void ButtonServerSettingsShow_Click(object sender, RoutedEventArgs e)
        {
            var servers = ServerMaster.GetServers();

            var server = servers.Find(ser =>
            ser.ServerType.ToString().Equals((string)ComboBoxConnectorEnum.SelectedItem));

            server.ShowDialog();
        }

        private void ComboBoxConnectorEnum_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            testBotConnection.CreateServer((string)ComboBoxConnectorEnum.SelectedItem);
        }

        #endregion

        #region Drawing

        public void DrawingRamLabelRam(LabelRAM ram)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() =>
                {
                    DrawingRamLabelRam(ram);
                });
                return;
            }
            if (ram == LabelRAM.Start)
            {
                DrawingStartRam.Content = (GC.GetTotalMemory(false) / (1024)) / 100;
            }
            else
            {
                DrawingEndRam.Content = (GC.GetTotalMemory(false) / (1024)) / 100;
            }

        }

        public void DrawingRectagle(bool IsConnect)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() =>
                {
                    DrawingRectagle(IsConnect);
                });
                return;
            }

            if (IsConnect)
            {
                var converter = new System.Windows.Media.BrushConverter();
                var brush = (Brush)converter.ConvertFromString("#008000");
                RectangleStatusConnect.Fill = brush;
            }
            else
            {
                var converter = new System.Windows.Media.BrushConverter();
                var brush = (Brush)converter.ConvertFromString("#FF0000");
                RectangleStatusConnect.Fill = brush;
            }

        }

        public void DrawingLabeleStatusTest(string value)
        {

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() =>
                {
                    DrawingLabeleStatusTest(value);
                });
                return;
            }

            LabelStateTesting.Content = value;
        }

        public void DrawingProgressBar(int value)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() =>
                {
                    DrawingProgressBar(value);
                });
                return;
            }

            ProgressBarTest.Value = value;

        }

        #endregion

        private bool ValidateTextBox()
        {
            int number;
            if (Int32.TryParse(TextBoxCountToReLoadServer.Text, out number) &&
                Int32.TryParse(TextBoxSecondToReloadServer.Text, out number) &&
                Int32.TryParse(TextBoxCountTabsToConnectServer.Text, out number))
            {
                return true;
            }
            else
            {
                return false;
            }

        }
    }
}
