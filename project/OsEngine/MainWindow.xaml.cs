/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Microsoft.Win32;
using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Instructions;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Market;
using OsEngine.OsConverter;
using OsEngine.OsData;
using OsEngine.OsOptimizer;
using OsEngine.OsTrader.Gui;
using OsEngine.OsTrader.Gui.BlockInterface;
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
using System.Windows.Threading;
using System.Text.Json;
using OsEngine.UpdateModule;
using OsEngine.MCP;

namespace OsEngine
{
    public partial class MainWindow
    {
        private static MainWindow _window;

        private UpdateResponse _updServerResp;

        private int _commitsCount;

        public static Dispatcher GetDispatcher
        {
            get { return _window.Dispatcher; }
        }

        public static bool DebuggerIsWork;

        public static bool ProccesIsWorked;

        private StartProgram _startProgram = StartProgram.IsMainWindow;

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

            Task updateTask = new Task(GetUpdateInfo);
            updateTask.Start();

            ChangeText();
            OsLocalization.LocalizationTypeChangeEvent += ChangeText;

            if (BlockMaster.IsBlocked == true)
            {
                BlockInterface();
            }
            else
            {
                UnblockInterface();
            }

            StartMcpHost();

            CommandLineInterfaceProcess();

            Task.Run(ClearOptimizerWorkResults);

            this.Activate();
            this.Focus();

            GlobalGUILayout.Listen(this, "mainWindow");

            VideoGrid.MouseDown += VideoGrid_MouseDown;

            if (InteractiveInstructions.MainMenu.AllInstructionsInClass == null
              || InteractiveInstructions.MainMenu.AllInstructionsInClass.Count == 0)
            {
                ButtonPostsMenu.Visibility = Visibility.Hidden;
            }
            else
            {
                ButtonPostsMenu.Click += ButtonPostsMenu_Click;
            }

            ChangeText();

            ReloadFlagButton();

            this.ContentRendered += MainWindow_ContentRendered;

            StartButtonBlinkAnimation();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _mcpMaster?.SendTerminalStopped("shutting_down");

                StopMcpHost();

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

                Thread.Sleep(5000);

                Process.GetCurrentProcess().Kill();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private AwaitObject _awaitUiBotsInfoLoading;

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
            ButtonApi.Content = OsLocalization.MainWindow.OsApiButtonName;
            ButtonCandleConverter.Content = OsLocalization.MainWindow.OsCandleConverter;

            ButtonTesterLight.Content = OsLocalization.MainWindow.OsTesterLiteName;
            ButtonRobotLight.Content = OsLocalization.MainWindow.OsBotStationLiteName;

            ChangeButtonCommits();

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

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string message = OsLocalization.MainWindow.Message5 + " THREAD " + e.ExceptionObject;

            message = _startProgram + "  " + message;

            message = System.Reflection.Assembly.GetExecutingAssembly() + "\n" + message;

            _messageToCrashServer = "Crash% " + message;
            Thread worker = new Thread(SendMessageInCrashServer);
            worker.Start();

            if (PrimeSettingsMaster.RebootTradeUiLight == true &&
                RobotUiLite.IsRobotUiLightStart)
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
                && e.Exception.ToString().Contains("(995):") == true)
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
                RobotUiLite.IsRobotUiLightStart)
            {
                Reboot(message);
            }
            else
            {
                MessageBox.Show(message);
            }
        }

        #region MCP Host

        private McpMaster _mcpMaster;

        private McpApiUi _mcpApiUi;

        private int _mcpPort = McpSettings.Port;

        private string _mcpApiKey = McpSettings.ApiKey;

        private Window _activeModeWindow;

        private bool _isProgrammaticClose;

        /// <summary>
        /// Indicates that the terminal is being closed programmatically (e.g. via MCP terminal_stop).
        /// Mode windows can use this flag to skip user confirmation dialogs.
        /// </summary>
        public bool IsProgrammaticClose => _isProgrammaticClose;

        private void StartMcpHost()
        {
            try
            {
                int port = McpSettings.Port;
                string apiKey = McpSettings.ApiKey;

                ParseMcpCommandLineArgs(ref port, ref apiKey);

                _mcpPort = port;
                _mcpApiKey = apiKey;

                _mcpMaster = new McpMaster(
                    port,
                    apiKey,
                    RestartMcpHost,
                    GetTerminalStatusForMcp,
                    LaunchTerminalProgram,
                    StopTerminalProgram,
                    KillTerminalProgram,
                    OpenModeForMcp);

                if (McpSettings.IsEnabled)
                {
                    _mcpMaster.Start();
                }
            }
            catch (Exception ex)
            {
                // Log as System to avoid a MessageBox during startup if no log subscribers exist yet.
                ServerMaster.SendNewLogMessage($"MCP host start failed: {ex}", Logging.LogMessageType.System);
            }
        }

        public void RestartMcpHost()
        {
            try
            {
                StopMcpHost();
                StartMcpHost();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void LaunchTerminalProgram(string mode)
        {
            try
            {
                StopMcpHost();

                string arguments = GetLaunchArguments(mode);
                arguments += $" -mcpPort {_mcpPort}";
                arguments += $" -mcpApiKey {_mcpApiKey}";

                string exePath = Process.GetCurrentProcess().MainModule.FileName;

                Process process = new Process();
                process.StartInfo.FileName = exePath;
                process.StartInfo.Arguments = arguments;
                process.Start();

                Process.GetCurrentProcess().Kill();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private string GetLaunchArguments(string mode)
        {
            switch (mode?.ToLowerInvariant())
            {
                case "tester":
                    return "-tester";
                case "testerlight":
                    return "-testerlight";
                case "robots":
                    return "-robots";
                case "robotslight":
                    return "-robotslight";
                case "data":
                    return "-data";
                case "optimizer":
                    return "-optimizer";
                case "converter":
                    return "-converter";
                default:
                    return string.Empty;
            }
        }

        private void StopTerminalProgram()
        {
            try
            {
                if (Dispatcher.CheckAccess())
                {
                    StopTerminalProgramInternal();
                }
                else
                {
                    Dispatcher.Invoke(StopTerminalProgramInternal);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void StopTerminalProgramInternal()
        {
            _isProgrammaticClose = true;
            ProccesIsWorked = false;

            try
            {
                _activeModeWindow?.Close();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }

            Close();
        }

        private void KillTerminalProgram()
        {
            try
            {
                Process.GetCurrentProcess().Kill();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void OpenModeForMcp(string mode)
        {
            try
            {
                if (Dispatcher.CheckAccess())
                {
                    OpenMode(mode);
                }
                else
                {
                    Dispatcher.Invoke(() => OpenMode(mode));
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void OpenMode(string mode)
        {
            try
            {
                if (_startProgram != StartProgram.IsMainWindow)
                {
                    ServerMaster.SendNewLogMessage("terminal_open_mode is available only from MainWindow", Logging.LogMessageType.Error);
                    return;
                }

                switch (mode?.ToLowerInvariant())
                {
                    case "tester":
                        ButtonTesterCandleOne_Click(null, null);
                        break;
                    case "testerlight":
                        ButtonTesterLight_Click(null, null);
                        break;
                    case "robots":
                        ButtonRobotCandleOne_Click(null, null);
                        break;
                    case "robotslight":
                        ButtonRobotLight_Click(null, null);
                        break;
                    case "data":
                        ButtonData_Click(null, null);
                        break;
                    case "optimizer":
                        ButtonOptimizer_Click(null, null);
                        break;
                    case "converter":
                        ButtonConverter_Click(null, null);
                        break;
                    default:
                        ServerMaster.SendNewLogMessage($"Unknown mode for terminal_open_mode: {mode}", Logging.LogMessageType.Error);
                        break;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void StopMcpHost()
        {
            try
            {
                _mcpMaster?.Stop();
                _mcpMaster = null;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private McpTerminalStatus GetTerminalStatusForMcp()
        {
            var status = new McpTerminalStatus
            {
                Mode = _startProgram.ToString(),
                Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                ProcessStarted = Process.GetCurrentProcess().StartTime,
                IsMainWindowVisible = false
            };

            try
            {
                if (Dispatcher.CheckAccess())
                {
                    status.IsMainWindowVisible = IsVisible;
                }
                else
                {
                    Dispatcher.Invoke(() => { status.IsMainWindowVisible = IsVisible; });
                }
            }
            catch
            {
                // ignore
            }

            return status;
        }

        private void ParseMcpCommandLineArgs(ref int port, ref string apiKey)
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].Equals("-mcpPort", StringComparison.OrdinalIgnoreCase)
                        && i + 1 < args.Length)
                    {
                        if (int.TryParse(args[i + 1], out int parsedPort))
                        {
                            port = parsedPort;
                        }
                    }
                    else if (args[i].Equals("-mcpApiKey", StringComparison.OrdinalIgnoreCase)
                        && i + 1 < args.Length)
                    {
                        apiKey = args[i + 1];
                    }
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

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
            ButtonApi.IsEnabled = false;
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
            ButtonNewCommits.IsEnabled = false;
        }

        private void ImagePadlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                RobotsUiLightUnblock ui = new RobotsUiLightUnblock();

                ui.ShowDialog();

                if (ui.IsUnBlocked == true)
                {
                    UnblockInterface();
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
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
            ButtonApi.IsEnabled = true;
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
            ButtonNewCommits.IsEnabled = true;
        }

        #endregion

        #region Check system on start program

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

        #endregion

        #region Open program buttons

        private void ButtonTesterCandleOne_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _startProgram = StartProgram.IsTester;
                ServerMaster.TesterStarted();
                Hide();
                _mcpMaster?.SendTerminalModeChanged(_startProgram);
                TesterUi candleOneUi = new TesterUi();
                _activeModeWindow = candleOneUi;
                candleOneUi.ShowDialog();
                _activeModeWindow = null;
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
                ServerMaster.TesterStarted();
                Hide();
                _mcpMaster?.SendTerminalModeChanged(_startProgram);
                TesterUiLite candleOneUi = new TesterUiLite();
                _activeModeWindow = candleOneUi;
                candleOneUi.ShowDialog();
                _activeModeWindow = null;
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
                ServerMaster.RealStarted();
                Hide();
                _mcpMaster?.SendTerminalModeChanged(_startProgram);
                RobotUi candleOneUi = new RobotUi();
                _activeModeWindow = candleOneUi;
                candleOneUi.ShowDialog();
                _activeModeWindow = null;
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
                ServerMaster.RealStarted();
                Hide();
                _mcpMaster?.SendTerminalModeChanged(_startProgram);
                RobotUiLite candleOneUi = new RobotUiLite();
                _activeModeWindow = candleOneUi;
                candleOneUi.ShowDialog();
                _activeModeWindow = null;
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
                _mcpMaster?.SendTerminalModeChanged(_startProgram);
                OsDataUi ui = new OsDataUi();
                _activeModeWindow = ui;
                _mcpMaster?.SetOsDataMaster(ui.Master);
                ui.ShowDialog();
                _activeModeWindow = null;
                _mcpMaster?.SetOsDataMaster(null);
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
                _mcpMaster?.SendTerminalModeChanged(_startProgram);
                OsConverterUi ui = new OsConverterUi();
                _activeModeWindow = ui;
                ui.ShowDialog();
                _activeModeWindow = null;
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
                ServerMaster.OptimizerStarted();
                Hide();
                _mcpMaster?.SendTerminalModeChanged(_startProgram);
                OptimizerUi ui = new OptimizerUi();
                _activeModeWindow = ui;
                ui.ShowDialog();
                _activeModeWindow = null;
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

        private void ButtonSettings_Click(object sender, RoutedEventArgs e)
        {
            try
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
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private PrimeSettingsMasterUi _settingsUi;

        private void ButtonApi_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_mcpApiUi == null)
                {
                    _mcpApiUi = new McpApiUi(_mcpMaster, RestartMcpHost);
                    _mcpApiUi.Show();
                    _mcpApiUi.Closing += delegate { _mcpApiUi = null; };
                }
                else
                {
                    _mcpApiUi.Activate();
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

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
            try
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
                else if (Array.Exists(args, a => a.Equals("-testerlight")))
                {
                    ButtonTesterLight_Click(this, default);
                }
                else if (Array.Exists(args, a => a.Equals("-data")))
                {
                    ButtonData_Click(this, default);
                }
                else if (Array.Exists(args, a => a.Equals("-optimizer")))
                {
                    ButtonOptimizer_Click(this, default);
                }
                else if (Array.Exists(args, a => a.Equals("-converter")))
                {
                    ButtonConverter_Click(this, default);
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
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
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

        #endregion

        #region Crash server

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
                newClient.Connect("45.137.152.144", 11000);
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

        #endregion

        #region Pictures and visual 

        private async void ThreadAreaGreeting()
        {
            try
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
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
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

        private void StartButtonBlinkAnimation()
        {
            try
            {
                DispatcherTimer timer = new DispatcherTimer();
                int blinkCount = 0;
                bool isGreenVisible = true;

                timer.Interval = TimeSpan.FromMilliseconds(300);
                timer.Tick += (s, e) =>
                {
                    try
                    {
                        if (blinkCount >= 20)
                        {
                            timer.Stop();
                            GreenCollectionMenu.Opacity = 1;
                            WhiteCollectionMenu.Opacity = 0;
                            return;
                        }

                        if (isGreenVisible)
                        {
                            GreenCollectionMenu.Opacity = 0;
                            WhiteCollectionMenu.Opacity = 1;
                        }
                        else
                        {
                            GreenCollectionMenu.Opacity = 1;
                            WhiteCollectionMenu.Opacity = 0;
                        }

                        isGreenVisible = !isGreenVisible;
                        blinkCount++;
                    }
                    catch (Exception ex)
                    {
                        ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                        timer.Stop();
                    }
                };

                timer.Start();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void GifT_MediaEnded(object sender, RoutedEventArgs e)
        {
            GifT.Pause(); // останавливаем на последнем кадре
        }

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

        #endregion

        #region Posts collection

        private InstructionsUi _instructionsUi;

        private void ButtonPostsMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_instructionsUi == null)
                {
                    _instructionsUi = new InstructionsUi(
                        InteractiveInstructions.MainMenu.AllInstructionsInClass, InteractiveInstructions.MainMenu.AllInstructionsInClassDescription);
                    _instructionsUi.Show();
                    _instructionsUi.Closed += _instructionsUi_Closed;
                }
                else
                {
                    if (_instructionsUi.WindowState == WindowState.Minimized)
                    {
                        _instructionsUi.WindowState = WindowState.Normal;
                    }
                    _instructionsUi.Activate();
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _instructionsUi_Closed(object sender, EventArgs e)
        {
            try
            {
                _instructionsUi.Closed -= _instructionsUi_Closed;
                _instructionsUi = null;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Updater

        private async void GetUpdateInfo()
        {
            try
            {
                if (Debugger.IsAttached && Process.GetProcessesByName("devenv").Length > 0)
                {
                    return;
                }

                DateTime insideVersionDate = File.GetLastWriteTimeUtc(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OsEngine.exe"));

                if (!File.Exists(@"Engine\Updater\LastUpdatesInfo.txt")) // модуль запустили первый раз, значит сборка актуальная
                {
                    Directory.CreateDirectory(@"Engine\Updater");

                    Directory.CreateDirectory(@"Engine\Log");

                    //записываем дату сборки, далее ориентир будет по ней
                    File.WriteAllText(@"Engine\Updater\LastUpdatesInfo.txt", insideVersionDate.ToString("G"));
                }
                else
                {
                    // взять из файла время последнего обновления
                    string time = File.ReadAllText(@"Engine\Updater\LastUpdatesInfo.txt");

                    insideVersionDate = DateTime.Parse(time);
                }

                // передать серверу дату 

                string ip = "185.186.143.9";
                int port = 49152;

                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync(ip, port);

                    if (client.Connected)
                    {
                        string request = $"{{\"LastUpdateDate\":\"{insideVersionDate:yyyy-MM-ddTHH:mm:ss}\"}}";

                        byte[] data = Encoding.UTF8.GetBytes(request);

                        using (NetworkStream stream = client.GetStream())
                        {

                            stream.Write(data, 0, data.Length);

                            using (MemoryStream ms = new MemoryStream())
                            {
                                byte[] buffer = new byte[8192];
                                int bytesRead;

                                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    ms.Write(buffer, 0, bytesRead);
                                }

                                string response = Encoding.UTF8.GetString(ms.ToArray());

                                if (!string.IsNullOrEmpty(response))
                                {
                                    UpdateResponse firstResponse = JsonSerializer.Deserialize<UpdateResponse>(response);

                                    if (firstResponse != null)
                                    {
                                        if (firstResponse.Success)
                                        {
                                            _updServerResp = firstResponse;

                                            if (!File.Exists(@"Engine\Updater\FilesVersionsTime.txt"))
                                            {
                                                WriteFilesVersionsTime(insideVersionDate); // при первом запуске проекта с обновлятором записываем время версии файлов в Debug
                                            }

                                            _commitsCount = firstResponse.MissedCommitsCount;

                                            Application.Current.Dispatcher.Invoke(() =>
                                            {
                                                ChangeButtonCommits();
                                            });
                                        }
                                        else
                                        {
                                            _updServerResp = firstResponse;
                                            throw new Exception("Сервер ответил с ошибкой");
                                        }
                                    }
                                    else
                                    {
                                        throw new Exception("Нет ответа от сервера");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                _commitsCount = -1;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ChangeButtonCommits();
                });
            }
        }

        private void WriteFilesVersionsTime(DateTime fileTime)
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                for (int i = 0; i < _updServerResp.Files.Count; i++)
                {
                    var fileInfo = _updServerResp.Files[i];

                    DateTime correctTime = fileInfo.LastUpdate > fileTime ? fileTime : fileInfo.LastUpdate;

                    sb.AppendLine(fileInfo.Name + "#" + correctTime + "#" + fileInfo.Size);

                }

                File.WriteAllText(@"Engine\Updater\FilesVersionsTime.txt", sb.ToString());

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void ChangeButtonCommits()
        {
            try
            {
                string buttCont = OsLocalization.MainWindow.NewCommits;
                SolidColorBrush foreground = new((Color)ColorConverter.ConvertFromString("#FFEEEFFF"));
                SolidColorBrush background = new((Color)ColorConverter.ConvertFromString("#FF111217"));

                if (_commitsCount > 0)
                {
                    buttCont = OsLocalization.MainWindow.NewCommits.Replace("0", _commitsCount.ToString());
                    background = new SolidColorBrush(Color.FromRgb(255, 85, 0));
                    foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF0F0F0"));
                }
                else if (_commitsCount < 0)
                {
                    buttCont = OsLocalization.MainWindow.NewCommits.Replace("0", " ?");
                    foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE30F26"));
                }

                ButtonNewCommits.Content = buttCont;
                ButtonNewCommits.Foreground = foreground;
                ButtonNewCommits.Background = background;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void ButtonNewCommits_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Hide();
                UpdateModuleUi ui = new UpdateModuleUi(_updServerResp);
                ui.ShowDialog();

                if(ui.IsUpdated == true)
                {
                    Close();
                    ProccesIsWorked = false;
                    Thread.Sleep(5000);
                    Process.GetCurrentProcess().Kill();
                }
                else
                {
                    Show();
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
           
        }

        private void ButtCommits_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_commitsCount > 0 || _commitsCount < 0)
                ButtonNewCommits.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1A1A1A"));
        }

        private void ButtCommits_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_commitsCount > 0 || _commitsCount < 0)
                ButtonNewCommits.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF0F0F0"));
        }

        #endregion
    }


    public static class CriticalErrorHandler
    {
        public static string ErrorMessage = String.Empty;

        public static bool ErrorInStartUp = false;
    }

}