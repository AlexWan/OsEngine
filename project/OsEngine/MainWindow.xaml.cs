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
using System.Collections.Generic;
using OsEngine.Entity;
using System.Globalization;
using OsEngine.Logging;
using System.Net.Sockets;
using System.Net;
using System.Text;

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

            ImageAlor2.Visibility = Visibility.Collapsed;
            ImageAlor.Visibility = Visibility.Collapsed;

            this.Closing += MainWindow_Closing;

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

                if (!CheckAlreadyWorkEngine())
                {
                    MessageBox.Show(OsLocalization.MainWindow.Message7);
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

            Thread.CurrentThread.CurrentCulture = OsLocalization.CurCulture;

            Task task = new Task(ThreadAreaGreeting);
            task.Start();

            ChangeText();
            OsLocalization.LocalizationTypeChangeEvent += ChangeText;

            CommandLineInterfaceProcess();

            Task.Run(ClearOptimizerWorkResults);

            this.Activate();
            this.Focus();

            GlobalGUILayout.Listen(this, "mainWindow");

            ImageAlor.MouseEnter += ImageAlor_MouseEnter;
            ImageAlor2.MouseLeave += ImageAlor_MouseLeave;
            ImageAlor2.MouseDown += ImageAlor2_MouseDown;
        }

        private void ImageAlor2_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.alorbroker.ru/open?pr=L0745") { UseShellExecute = true });
        }

        private void ImageAlor_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (OsLocalization.CurLocalization == OsLocalization.OsLocalType.Ru)
            {
                ImageAlor2.Visibility = Visibility.Collapsed;
                ImageAlor.Visibility = Visibility.Visible;
            }
        }

        private void ImageAlor_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (OsLocalization.CurLocalization == OsLocalization.OsLocalType.Ru)
            {
                ImageAlor2.Visibility = Visibility.Visible;
                ImageAlor.Visibility = Visibility.Collapsed;
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            GlobalGUILayout.IsClosed = true;

            if (ProccesIsWorked == true)
            {
                ProccesIsWorked = false;

                if (this.IsVisible == false)
                {
                    _awaitUiBotsInfoLoading = new AwaitObject(OsLocalization.Trader.Label391, 100, 0, true);
                    AwaitUi ui = new AwaitUi(_awaitUiBotsInfoLoading);

                    Thread worker = new Thread(Await7Seconds);
                    worker.Start();

                    ui.ShowDialog();
                }
            }

            Thread.Sleep(500);

            Process.GetCurrentProcess().Kill();
        }

        AwaitObject _awaitUiBotsInfoLoading;

        private void Await7Seconds()
        {
            // Это нужно чтобы потоки сохраняющие данные в файловую систему штатно завершили свою работу
            // This is necessary for threads saving data to the file system to complete their work properly
            Thread.Sleep(7000);
            _awaitUiBotsInfoLoading.Dispose();
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

            if(OsLocalization.CurLocalization == OsLocalization.OsLocalType.Ru)
            {
                this.Height = 415;
                ImageAlor.Visibility = Visibility.Visible;
            }
            else
            {
                this.Height = 315;
                ImageAlor.Visibility = Visibility.Collapsed;
                ImageAlor2.Visibility = Visibility.Collapsed;
            }
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

        private bool CheckAlreadyWorkEngine()
        {
            try
            {
                string myDirectory = Directory.GetCurrentDirectory();

                Process[] ps1 = System.Diagnostics.Process.GetProcesses();

                List<Process> process = new List<Process>();

                for (int i = 0; i < ps1.Length; i++)
                {
                    Process p = ps1[i];

                    try
                    {
                        string mainStr = p.MainWindowHandle.ToString();

                        if (mainStr == "0")
                        {
                            continue;
                        }

                        if (p.MainModule.FileName != ""
                            && p.Modules != null)
                        {
                            process.Add(p);
                        }
                    }
                    catch
                    {

                    }
                }

                int osEngineCount = 0;

                string myProgrammPath = myDirectory + "\\OsEngine.exe";

                for (int i = 0; i < process.Count; i++)
                {
                    Process p = process[i];

                    for (int j = 0; p.Modules != null && j < p.Modules.Count; j++)
                    {
                        if (p.Modules[j].FileName == null)
                        {
                            continue;
                        }

                        if (p.Modules[j].FileName.EndsWith(myProgrammPath))
                        {
                            osEngineCount++;
                        }
                    }
                }

                if (osEngineCount > 0)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string message = OsLocalization.MainWindow.Message5 + e.ExceptionObject;

            message = _startProgram + "  " + message;

            message = System.Reflection.Assembly.GetExecutingAssembly() + "\n" + message;

            _messageToCrashServer = "Crash% " + message;
            Thread worker = new Thread(SendMessageInCrashServer);
            worker.Start();

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

        private StartProgram _startProgram;

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
                _startProgram = StartProgram.IsTester;
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
                _startProgram = StartProgram.IsTester;
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
                _startProgram = StartProgram.IsOsTrader;
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
                _startProgram = StartProgram.IsOsTrader;
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
                _startProgram = StartProgram.IsOsData;
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
                _startProgram = StartProgram.IsOsConverter;
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
                _startProgram = StartProgram.IsOsOptimizer;
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
                _startProgram = StartProgram.IsOsMiner;
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

        string _messageToCrashServer;

        private void SendMessageInCrashServer()
        {
            try
            {
                if(PrimeSettingsMaster.ReportCriticalErrors == false)
                {
                    return;
                }

                TcpClient newClient = new TcpClient();
                newClient.Connect("195.133.196.183", 11000);
                NetworkStream tcpStream = newClient.GetStream();
                byte[] sendBytes = Encoding.UTF8.GetBytes(_messageToCrashServer);
                tcpStream.Write(sendBytes, 0, sendBytes.Length);
                newClient.Close();
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
