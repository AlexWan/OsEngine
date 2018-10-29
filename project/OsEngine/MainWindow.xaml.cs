/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using OsEngine.Alerts;
using OsEngine.Market;
using OsEngine.OsConverter;
using OsEngine.OsData;
using OsEngine.OsMiner;
using OsEngine.OsOptimizer;
using OsEngine.OsTrader.Gui;

namespace OsEngine
{

    /// <summary>
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
        /// работает ли приложение или закрывается
        /// </summary>
        public static bool ProccesIsWorked;


        public MainWindow()
        {
            Process ps = Process.GetCurrentProcess();
            ps.PriorityClass = ProcessPriorityClass.RealTime;

            Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
            InitializeComponent();
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
                int winVersion = Environment.OSVersion.Version.Major;
                if (winVersion < 6)
                {
                    MessageBox.Show(
                        "Ваша оперативная система не соответствуют рабочим параметрам робота. Нужно использовать минимум Windows 7");
                    Close();
                }
                if (!CheckDotNetVersion())
                {
                    Close();
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Ошибка при попытке проверить версию Windows. Программа закрыта. Опишите систему в которой вы пытаетесь запустить программу и напишите разработчику");
                Close();
            }

            // передаём в менеджер сообщений объект центарльного потока
            AlertMessageManager.TextBoxFromStaThread = new TextBox();

            ProccesIsWorked = true;
            _window = this;

            ServerMaster.ActivateLogging();
        }

        /// <summary>
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
                    MessageBox.Show("Ваша версия .Net 4.5 or later. Робот не будет работать в Вашей системе. С.м. в инструкции главу: Требования к Windows и .Net");
                    return false;
                }

                MessageBox.Show("Ваша версия старше .Net 4.5. Робот не будет работать в Вашей системе. С.м. в инструкции главу: Требования к Windows и .Net");

                return false;
            }
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string message = "ВСЁ ПРОПАЛО!!! Произошло не обработанное исключение. Сейчас нужно сделать следущее: \r ";
            message += "1) Сохранить изображение этого окна, нажав PrintScrin на клавиатуре и вставить изображение в Paint  \r ";
            message += "2) Написать подробности произошедшего инцидента. При каких обстоятельствах программа упала  \r ";
            message += "3) Выслать изображение этого окна и описание ситуации на адрес: alexey@o-s-a.net \r ";
            message += "4) Если ситуация повториться, вероятно будет нужно очистить папку Engine и QuikTrades что рядом с роботом  \r ";
            message += "5) Возможно придётся удалить процесс Os.Engine из диспетчера задач руками.  \r ";
            message += "6) Ошибка:  " + e.ExceptionObject;

            MessageBox.Show(message);
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
    }
}
