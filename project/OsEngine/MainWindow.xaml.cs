﻿/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OsEngine.Alerts;
using OsEngine.OsConverter;
using OsEngine.OsData;
using OsEngine.OsTrader.Gui;

namespace OsEngine
{

    /// <summary>
    /// Стартовое окно приложения
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow() // конструктор окна
        {
            
            Process ps = Process.GetCurrentProcess();
            ps.PriorityClass = ProcessPriorityClass.RealTime;

            Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
            InitializeComponent();
            LabelOsa.Content = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;


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

            // передаём в менеджер сообщений объект центрального потока
            AlertMessageManager.TextBoxFromStaThread = new TextBox();

            ProccesIsWorked = true;
        }

        /// <summary>
        /// проверить версию дотНет
        /// </summary>
        private bool CheckDotNetVersion()
        {
            using (RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full\\"))
            {
                if(ndpKey == null)
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
                    MessageBox.Show("Ваша версия .Net 4.5.1 or later. Робот не будет работать в Вашей системе. С.м. в инструкции главу: Требования к Windows и .Net");
                    return false;
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





        /// <summary>
        /// работает ли приложение или закрывается
        /// </summary>
        public static bool ProccesIsWorked;


    }
}
