/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using OsEngine.Alerts;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.OsConverter;
using OsEngine.OsData;
using OsEngine.OsMiner;
using OsEngine.OsOptimizer;
using OsEngine.OsTrader.Gui;
using OsEngine.PrimeSettings;
using OsEngine.Layout;

namespace OsEngine
{

    /// <summary>
    /// Application start screen
    /// Стартовое окно приложения
    /// </summary>
    public partial class MainWindow
    {

        private static MainWindow _window;

        public static Dispatcher GetDispatcher
        {
            get { return _window.Dispatcher; }
        }

        /// <summary>
        ///  is application running
        /// работает ли приложение или закрывается
        /// </summary>
        public static bool ProccesIsWorked;

        public MainWindow()
        {
            Process ps = Process.GetCurrentProcess();
            ps.PriorityClass = ProcessPriorityClass.RealTime;

            InitializeComponent();
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            this.Closed += MainWindow_Closed;

            try
            {
                int winVersion = Environment.OSVersion.Version.Major;
                if (winVersion < 6)
                {
                    MessageBox.Show(OsLocalization.MainWindow.Message1);
                    Close();
                }
                if (!CheckDotNetVersion())
                {
                    Close();
                }

                if (!CheckWorkWithDirectory())
                {
                    MessageBox.Show(OsLocalization.MainWindow.Message2);
                    Close();
                }

                if(!CheckOutSomeLibrariesNearby())
                {
                    MessageBox.Show(OsLocalization.MainWindow.Message6);
                    Close();
                }
            }
            catch (Exception)
            {
                MessageBox.Show(OsLocalization.MainWindow.Message3);
                Close();
            }

            AlertMessageManager.TextBoxFromStaThread = new TextBox();

            ProccesIsWorked = true;
            _window = this;

            ServerMaster.ActivateLogging();

            Task task = new Task(ThreadAreaGreeting);
            task.Start();

            ChangeText();
            OsLocalization.LocalizationTypeChangeEvent += ChangeText;

            CommandLineInterfaceProcess();

            Task.Run(ClearOptimizerWorkResults);

            this.Activate();
            this.Focus();

            GlobalGUILayout.Listen(this, "mainWindow");

            
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            ProccesIsWorked = false;
            GlobalGUILayout.IsClosed = true;
            Process.GetCurrentProcess().Kill();
        }

        private void ChangeText()
        {
            Title = OsLocalization.MainWindow.Title;
            BlockDataLabel.Content = OsLocalization.MainWindow.BlockDataLabel;
            BlockTestingLabel.Content = OsLocalization.MainWindow.BlockTestingLabel;
            BlockTradingLabel.Content = OsLocalization.MainWindow.BlockTradingLabel;
            ButtonData.Content = OsLocalization.MainWindow.OsDataName;
            ButtonConverter.Content = OsLocalization.MainWindow.OsConverter;
            ButtonTester.Content = OsLocalization.MainWindow.OsTesterName;
            ButtonOptimizer.Content = OsLocalization.MainWindow.OsOptimizerName;
            ButtonMiner.Content = OsLocalization.MainWindow.OsMinerName;

            ButtonRobot.Content = OsLocalization.MainWindow.OsBotStationName;
            ButtonCandleConverter.Content = OsLocalization.MainWindow.OsCandleConverter;

            ButtonTesterLight.Content = OsLocalization.MainWindow.OsTesterLightName;
            ButtonRobotLight.Content = OsLocalization.MainWindow.OsBotStationLightName;
        }

        /// <summary>
        /// check the version of dotnet
        /// проверить версию дотНет
        /// </summary>
        private bool CheckDotNetVersion()
        {
            using (RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full\\"))
            {
                if (ndpKey == null)
                {
                    return false;
                }
                int releaseKey = Convert.ToInt32(ndpKey.GetValue("Release"));

                if (releaseKey >= 393295)
                {
                    //"4.6 or later";
                    return true;
                }
                if ((releaseKey >= 379893))
                {
                    //"4.5.2 or later";
                    return true;
                }
                if ((releaseKey >= 378675))
                {
                    //"4.5.1 or later";
                    return true;
                }
                if ((releaseKey >= 378389))
                {
                    MessageBox.Show(OsLocalization.MainWindow.Message4);
                    return false;
                }

                MessageBox.Show(OsLocalization.MainWindow.Message4);

                return false;
            }
        }

        private bool CheckOutSomeLibrariesNearby()
        {
            // проверяем чтобы пользователь не запустился с рабочего стола, но не ярлыком, а экзешником

            if(File.Exists("QuikSharp.dll") == false)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// check the permission of the program to create files in the directory
        /// проверяем разрешение программы создавать файлы в директории
        /// </summary>
        private bool CheckWorkWithDirectory()
        {
            try
            {

                if (!Directory.Exists("Engine"))
                {
                    Directory.CreateDirectory("Engine");
                }

                if (File.Exists("Engine\\checkFile.txt"))
                {
                    File.Delete("Engine\\checkFile.txt");
                }

                File.Create("Engine\\checkFile.txt");

                if (File.Exists("Engine\\checkFile.txt") == false)
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }


            return true;
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string message = OsLocalization.MainWindow.Message5 + e.ExceptionObject;

            if (PrimeSettingsMaster.RebootTradeUiLigth == true &&
                RobotUiLight.IsRobotUiLightStart)
            {
                Reboot(message);
            }
            else
            {
                MessageBox.Show(message);
            }
        }

        private void Reboot(string message)
        {

            if (!CheckAccess())
            {
                Dispatcher.Invoke(() =>
                {
                    Reboot(message);
                });
                return;
            }

            App.app.Shutdown();
            Process process = new Process();
            process.StartInfo.FileName = Directory.GetCurrentDirectory() + "\\OsEngine.exe";
            process.StartInfo.Arguments = " -error " + message;
            process.Start();

            Process.GetCurrentProcess().Kill();
        }

        private void ButtonTesterCandleOne_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Hide();
                TesterUi candleOneUi = new TesterUi();
                candleOneUi.ShowDialog();
                Close();
                ProccesIsWorked = false;
                Thread.Sleep(5000);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
            Process.GetCurrentProcess().Kill();
        }

        private void ButtonTesterLight_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Hide();
                TesterUiLight candleOneUi = new TesterUiLight();
                candleOneUi.ShowDialog();
                Close();
                ProccesIsWorked = false;
                Thread.Sleep(5000);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
            Process.GetCurrentProcess().Kill();
        }

        private void ButtonRobotCandleOne_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Hide();
                RobotUi candleOneUi = new RobotUi();
                candleOneUi.ShowDialog();
                Close();
                ProccesIsWorked = false;
                Thread.Sleep(5000);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
            Process.GetCurrentProcess().Kill();
        }

        private void ButtonRobotLight_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Hide();
                RobotUiLight candleOneUi = new RobotUiLight();
                candleOneUi.ShowDialog();
                Close();
                ProccesIsWorked = false;
                Thread.Sleep(5000);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
            Process.GetCurrentProcess().Kill();
        }

        private void ButtonData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Hide();
                OsDataUi ui = new OsDataUi();
                ui.ShowDialog();
                Close();
                ProccesIsWorked = false;
                Thread.Sleep(5000);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
            Process.GetCurrentProcess().Kill();
        }

        private void ButtonConverter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Hide();
                OsConverterUi ui = new OsConverterUi();
                ui.ShowDialog();
                Close();
                ProccesIsWorked = false;
                Thread.Sleep(10000);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
            Process.GetCurrentProcess().Kill();
        }

        private void ButtonOptimizer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Hide();
                OptimizerUi ui = new OptimizerUi();
                ui.ShowDialog();
                Close();
                ProccesIsWorked = false;
                Thread.Sleep(10000);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
            Process.GetCurrentProcess().Kill();
        }

        private void ButtonMiner_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Hide();
                OsMinerUi ui = new OsMinerUi();
                ui.ShowDialog();
                Close();
                ProccesIsWorked = false;
                Thread.Sleep(10000);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
            Process.GetCurrentProcess().Kill();
        }

        private async void ThreadAreaGreeting()
        {
            await Task.Delay(1000);
            double angle = 5;

            for (int i = 0; i < 7; i++)
            {
                RotatePic(angle);
                await Task.Delay(50);
                angle += 10;
            }

            for (int i = 0; i < 7; i++)
            {
                RotatePic(angle);
                await Task.Delay(100);
                angle += 10;
            }

            await Task.Delay(100);
            RotatePic(angle);

        }

        private void RotatePic(double angle)
        {
            if (ImageGear.Dispatcher.CheckAccess() == false)
            {
                ImageGear.Dispatcher.Invoke(new Action<double>(RotatePic), angle);
                return;
            }

            ImageGear.RenderTransform = new RotateTransform(angle, 12, 12);

        }

        private void ButtonSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsUi == null)
            {
                _settingsUi = new PrimeSettingsMasterUi();
                _settingsUi.Show();
                _settingsUi.Closing += delegate { _settingsUi = null; };
            }
            else
            {
                _settingsUi.Activate();
            }
        }

        private PrimeSettingsMasterUi _settingsUi;

        private void CandleConverter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Hide();
                OsCandleConverterUi ui = new OsCandleConverterUi();
                ui.ShowDialog();
                Close();
                ProccesIsWorked = false;
                Thread.Sleep(10000);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
            Process.GetCurrentProcess().Kill();
        }

        private void CommandLineInterfaceProcess()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.Exists(args, a => a.Equals("-robots")))
            {
                ButtonRobotCandleOne_Click(this, default);
            }
            else if (Array.Exists(args, a => a.Equals("-tester")))
            {
                ButtonTesterCandleOne_Click(this, default);
            }
            else if (Array.Exists(args, a => a.Equals("-robotslight")))
            {
                ButtonRobotLight_Click(this, default);
            }
            else if (Array.Exists(args, a => a.Equals("-error")) && PrimeSettingsMaster.RebootTradeUiLigth)
            {

                CriticalErrorHandler.ErrorInStartUp = true;

                Array.ForEach(args, (a) => { CriticalErrorHandler.ErrorMessage += a; });

                new Task(() =>
                {
                    string messageError = String.Empty;

                    for (int i = 0; i < args.Length; i++)
                    {
                        messageError += args[i];
                    }

                    MessageBox.Show(messageError);

                }).Start();

                ButtonRobotLight_Click(this, default);
            }
        }

        private void ClearOptimizerWorkResults()
        {
            try
            {
                if (Directory.Exists("Engine") == false)
                {
                    return;
                }

                string[] files = Directory.GetFiles("Engine");

                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        if (files[i].Contains(" OpT "))
                        {
                            File.Delete(files[i]);
                        }
                    }
                    catch
                    {
                        // ignore
                    }

                }
            }
            catch
            {
                // ignore
            }
        }
    }

    public static class CriticalErrorHandler
    {
        public static string ErrorMessage = String.Empty;

        public static bool ErrorInStartUp = false;
    }

}
