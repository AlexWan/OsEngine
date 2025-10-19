/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Microsoft.Win32;
using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Market;
using OsEngine.OsConverter;
using OsEngine.OsData;
using OsEngine.OsOptimizer;
using OsEngine.OsTrader.Gui;
using OsEngine.OsTrader.Gui.BlockInterface;
using OsEngine.OsTrader.ServerAvailability;
using OsEngine.OsTrader.SystemAnalyze;
using OsEngine.PrimeSettings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

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

        public static bool DebuggerIsWork;

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
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

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

                if (!CheckOutSomeLibrariesNearby())
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

            if (Debugger.IsAttached)
            {
                DebuggerIsWork = true;
            }

            AlertMessageManager.TextBoxFromStaThread = new TextBox();

            ProccesIsWorked = true;
            _window = this;

            ServerMaster.Activate();
            SystemUsageAnalyzeMaster.Activate();

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

            VideoGrid.MouseDown += VideoGrid_MouseDown;

            if (BlockMaster.IsBlocked == true)
            {
                BlockInterface();
            }
            else
            {
                UnblockInterface();
            }

            ChangeText();

            ReloadFlagButton();

            this.ContentRendered += MainWindow_ContentRendered;
        }
        private void GifT_MediaEnded(object sender, RoutedEventArgs e)
        {
            GifT.Pause(); // останавливаем на последнем кадре
        }
        #region Block and Unblock interface

        private void BlockInterface()
        {
            ImageData.Visibility = Visibility.Hidden;
            ImageTests.Visibility = Visibility.Hidden;
            ImageTrading.Visibility = Visibility.Hidden;
            ImageFlag_Ru.Visibility = Visibility.Hidden;
            ImageFlag_Eng.Visibility = Visibility.Hidden;

            ImagePadlock.Visibility = Visibility.Visible;
            ImagePadlock.MouseEnter += ImagePadlock_MouseEnter;
            ImagePadlock.MouseLeave += ImagePadlock_MouseLeave;
            ImagePadlock.MouseDown += ImagePadlock_MouseDown;
            ButtonSettings.IsEnabled = false;
            ButtonRobot.IsEnabled = false;
            ButtonTester.IsEnabled = false;
            ButtonData.IsEnabled = false;
            ButtonCandleConverter.IsEnabled = false;
            ButtonConverter.IsEnabled = false;
            ButtonOptimizer.IsEnabled = false;
            ButtonTesterLight.IsEnabled = false;
            ButtonRobotLight.IsEnabled = false;
            ButtonLocal_Ru.IsEnabled = false;
            ButtonLocal_Eng.IsEnabled = false;
        }

        private void ImagePadlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RobotsUiLightUnblock ui = new RobotsUiLightUnblock();

            ui.ShowDialog();

            if (ui.IsUnBlocked == true)
            {
                UnblockInterface();
            }
        }

        private void ImagePadlock_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ImagePadlock.Cursor = System.Windows.Input.Cursors.Arrow;
        }

        private void ImagePadlock_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ImagePadlock.Cursor = System.Windows.Input.Cursors.Hand;
        }

        private void UnblockInterface()
        {
            ImageData.Visibility = Visibility.Visible;
            ImageTests.Visibility = Visibility.Visible;
            ImageTrading.Visibility = Visibility.Visible;
            ImageFlag_Ru.Visibility = Visibility.Visible;
            ImageFlag_Eng.Visibility = Visibility.Visible;

            ImagePadlock.Visibility = Visibility.Hidden;
            ButtonSettings.IsEnabled = true;
            ButtonRobot.IsEnabled = true;
            ButtonTester.IsEnabled = true;
            ButtonData.IsEnabled = true;
            ButtonCandleConverter.IsEnabled = true;
            ButtonConverter.IsEnabled = true;
            ButtonOptimizer.IsEnabled = true;
            ButtonTesterLight.IsEnabled = true;
            ButtonRobotLight.IsEnabled = true;
            ButtonLocal_Ru.IsEnabled = true;
            ButtonLocal_Eng.IsEnabled = true;
        }

        #endregion

        private void VideoGrid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://www.tbank.ru/invest") { UseShellExecute = true });
            }
            catch
            {
                // ignore
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

        private void MainWindow_ContentRendered(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                Thread.Sleep(1000);
                try
                {
                    ChangeText();
                }
                catch
                {
                    // ignore
                }
            });
        }

        private void ChangeText()
        {

            if (ImageGear.Dispatcher.CheckAccess() == false)
            {
                ImageGear.Dispatcher.Invoke(new Action(ChangeText));
                return;
            }

            Title = OsLocalization.MainWindow.Title;
            BlockDataLabel.Content = OsLocalization.MainWindow.BlockDataLabel;
            BlockTestingLabel.Content = OsLocalization.MainWindow.BlockTestingLabel;
            BlockTradingLabel.Content = OsLocalization.MainWindow.BlockTradingLabel;
            ButtonData.Content = OsLocalization.MainWindow.OsDataName;
            ButtonConverter.Content = OsLocalization.MainWindow.OsConverter;
            ButtonTester.Content = OsLocalization.MainWindow.OsTesterName;
            ButtonOptimizer.Content = OsLocalization.MainWindow.OsOptimizerName;

            ButtonRobot.Content = OsLocalization.MainWindow.OsBotStationName;
            ButtonCandleConverter.Content = OsLocalization.MainWindow.OsCandleConverter;

            ButtonTesterLight.Content = OsLocalization.MainWindow.OsTesterLightName;
            ButtonRobotLight.Content = OsLocalization.MainWindow.OsBotStationLightName;

            if (OsLocalization.CurLocalization == OsLocalization.OsLocalType.Ru)
            {
                VideoGrid.Visibility = Visibility.Visible;
                this.Height = 430;
                GifT.Play();
            }
            else
            {
                this.Height = 315;
                VideoGrid.Visibility = Visibility.Collapsed;
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

            if (File.Exists("QuikSharp.dll") == false)
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

                string myProgramPath = myDirectory + "\\OsEngine.exe";

                for (int i = 0; i < process.Count; i++)
                {
                    Process p = process[i];

                    for (int j = 0; p.Modules != null && j < p.Modules.Count; j++)
                    {
                        if (p.Modules[j].FileName == null)
                        {
                            continue;
                        }

                        if (p.Modules[j].FileName.EndsWith(myProgramPath))
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
            string message = OsLocalization.MainWindow.Message5 + " THREAD " + e.ExceptionObject;

            message = _startProgram + "  " + message;

            message = System.Reflection.Assembly.GetExecutingAssembly() + "\n" + message;

            _messageToCrashServer = "Crash% " + message;
            Thread worker = new Thread(SendMessageInCrashServer);
            worker.Start();

            if (PrimeSettingsMaster.RebootTradeUiLight == true &&
                RobotUiLight.IsRobotUiLightStart)
            {
                Reboot(message);
            }
            else
            {
                MessageBox.Show(message);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception != null
                && e.Exception.ToString().Contains("(995):")== true)
            { // игнорируем прерывания потока за делом по кансел токену
                return;
            }

            string message = OsLocalization.MainWindow.Message5 + " TASK " + e.Exception.ToString();

            message = _startProgram + "  " + message;

            message = System.Reflection.Assembly.GetExecutingAssembly() + "\n" + message;

            _messageToCrashServer = "Crash% " + message;
            Thread worker = new Thread(SendMessageInCrashServer);
            worker.Start();

            if (PrimeSettingsMaster.RebootTradeUiLight == true &&
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
            else if (Array.Exists(args, a => a.Equals("-error")) && PrimeSettingsMaster.RebootTradeUiLight)
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
                if (PrimeSettingsMaster.ReportCriticalErrors == false)
                {
                    return;
                }

                TcpClient newClient = new TcpClient();
                newClient.Connect("45.173.152.144", 11000);
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

        private void ButtonLocal_Click(object sender, RoutedEventArgs e)
        {
            OsLocalization.OsLocalType newType;

            if (ButtonLocal_Ru.Background.ToString() == "#FFFF5500")
            {
                return;
            }

            if (Enum.TryParse("Ru", out newType))
            {
                OsLocalization.CurLocalization = newType;
                Thread.CurrentThread.CurrentCulture = OsLocalization.CurCulture;

                ButtonLocal_Ru.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#ff5500");
                ButtonLocal_Eng.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF111217");
                GifT.Position = TimeSpan.Zero;
                GifT.Play();
            }
        }

        private void ButtonLocal_Eng_Click(object sender, RoutedEventArgs e)
        {
            OsLocalization.OsLocalType newType;

            if (Enum.TryParse("Eng", out newType))
            {
                OsLocalization.CurLocalization = newType;
                Thread.CurrentThread.CurrentCulture = OsLocalization.CurCulture;

                ButtonLocal_Eng.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#ff5500");
                ButtonLocal_Ru.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF111217");
            }
        }

        private void ReloadFlagButton()
        {
            if (OsLocalization.CurLocalization == OsLocalization.OsLocalType.Ru)
            {
                ButtonLocal_Ru.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#ff5500");
            }
            else
            {
                ButtonLocal_Eng.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#ff5500");
            }
        }
    }


    public static class CriticalErrorHandler
    {
        public static string ErrorMessage = String.Empty;

        public static bool ErrorInStartUp = false;
    }

}