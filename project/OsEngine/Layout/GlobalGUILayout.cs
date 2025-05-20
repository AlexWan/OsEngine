/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;
using System.IO;

namespace OsEngine.Layout
{
    public class GlobalGUILayout
    {
        private static bool _isFirstTime = true;

        public static void Listen(System.Windows.Window ui, string name)
        {
            lock (_lockerArrayWithWindows)
            {
                if (_isFirstTime == true)
                {
                    _isFirstTime = false;
                    Load();

                    for (int i = 0; i < UiOpenWindows.Count; i++)
                    {
                        UiOpenWindows[i].UiLocationChangeEvent += UiLocationChangeEvent;
                    }

                    if(ScreenSettingsIsAllRight() == false)
                    {
                        UiOpenWindows = new List<OpenWindow>();
                    }

                    Thread worker = new Thread(SaveWorkerPlace);
                    worker.Start();
                }
            }

            for (int i = 0; i < UiOpenWindows.Count; i++)
            {
                if (UiOpenWindows[i].Name == name)
                {
                    SetLayoutInWindow(ui, UiOpenWindows[i].Layout);
                    UiOpenWindows[i].WindowCreateTime = DateTime.Now;
                    UiOpenWindows[i].IsActivate = false;
                    UiOpenWindows[i].Ui = ui;
                    return;
                }
            }

            OpenWindow window = new OpenWindow();
            window.Name = name;
            window.WindowCreateTime = DateTime.Now;
            window.Layout = new OpenWindowLayout();
            window.Ui = ui;
            window.UiLocationChangeEvent += UiLocationChangeEvent;

            SetLayoutFromWindow(ui, window);

            lock(_lockerArrayWithWindows)
            {
                UiOpenWindows.Add(window);
                _needToSave = true;
            }
      
        }

        private static string _lockerArrayWithWindows = "openUiLocker";

        private static void UiLocationChangeEvent(System.Windows.Window ui, string name)
        {
            lock (_lockerArrayWithWindows)
            {
                for (int i = 0; i < UiOpenWindows.Count; i++)
                {
                    if (UiOpenWindows[i].Name == name)
                    {
                        if (UiOpenWindows[i].IsActivate == false)
                        {
                            if (UiOpenWindows[i].WindowCreateTime.AddSeconds(1) > DateTime.Now)
                            {
                                SetLayoutInWindow(ui, UiOpenWindows[i].Layout);
                            }
                            else
                            {
                                UiOpenWindows[i].IsActivate = true;
                            }

                            return;
                        }
                        
                        if (UiOpenWindows[i].WindowUpdateTime.AddMilliseconds(300) > DateTime.Now)
                        {
                            return;
                        }

                        SetLayoutFromWindow(ui, UiOpenWindows[i]);
                        UiOpenWindows[i].WindowUpdateTime = DateTime.Now;

                        break;
                    }
                }
                _needToSave = true;
            }
        }

        private static void SetLayoutFromWindow(System.Windows.Window ui, OpenWindow windowLayout)
        {

            if (double.IsNaN(ui.ActualHeight) == false)
            {
                windowLayout.Layout.Height = Convert.ToDecimal(ui.ActualHeight);
            }

            if (double.IsNaN(ui.ActualWidth) == false)
            {
                windowLayout.Layout.Widht = Convert.ToDecimal(ui.ActualWidth);
            }

            if (double.IsNaN(ui.Left) == false)
            {
                windowLayout.Layout.Left = Convert.ToDecimal(ui.Left);
            }

            if (double.IsNaN(ui.Top) == false)
            {
                windowLayout.Layout.Top = Convert.ToDecimal(ui.Top);
            }

            if (ui.WindowState == System.Windows.WindowState.Maximized)
            {
                windowLayout.Layout.IsExpand = true;
            }
            else
            {
                windowLayout.Layout.IsExpand = false;
            }			
        }

        private static void SetLayoutInWindow(System.Windows.Window ui, OpenWindowLayout layout)
        {
            if(layout.Height == 0 ||
                layout.Widht == 0 ||
                layout.Left == 0 ||
                layout.Top == 0)
            {
                return;
            }

            if (layout.Left == -32000 ||
               layout.Top == -32000)
            {
                return;
            }

            if (layout.Left < -50 ||
              layout.Top < -50 ||
              layout.Height < 0 ||
              layout.Widht < 0)
            {
                return;
            }

            if (layout.Height < 0 || layout.Widht < 0)
            {
                return;
            }           

            if (layout.IsExpand == true)
            {
                ui.Left = Convert.ToDouble(layout.Left);
                ui.Top = Convert.ToDouble(layout.Top);
                return;
            }

            ui.Height = Convert.ToDouble(layout.Height);
            ui.Width = Convert.ToDouble(layout.Widht);
            ui.Left = Convert.ToDouble(layout.Left);
            ui.Top = Convert.ToDouble(layout.Top);

            if (ui.Top < 0)
            {
                ui.Top = 0;
            }
        }

        public static List<OpenWindow> UiOpenWindows = new List<OpenWindow>();

        private static bool _needToSave;

        public static bool IsClosed;

        private static void SaveWorkerPlace()
        {
            while(true)
            {
                Thread.Sleep(1000);

                if(_needToSave == false)
                {
                    continue;
                }

                if(IsClosed)
                {
                    return;
                }

                Save();
            }
        }

        private static void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\LayoutGui.txt", false))
                {
                    for(int i = 0;i < UiOpenWindows.Count;i++)
                    {
                        if (UiOpenWindows[i].Layout.Height == 0 ||
                            UiOpenWindows[i].Layout.Widht == 0 ||
                            UiOpenWindows[i].Layout.Left == 0 ||
                            UiOpenWindows[i].Layout.Top == 0)
                        {
                            continue;
                        }

                        if (UiOpenWindows[i].Layout.Left == -32000 ||
                            UiOpenWindows[i].Layout.Top == -32000)
                        {//свернутое значение окна пропускаем при сохранение
                            continue;
                        }


                        if (UiOpenWindows[i].Layout.Height < 0 || UiOpenWindows[i].Layout.Widht < 0)
                        {
                            continue;
                        }
                        
                        writer.WriteLine(UiOpenWindows[i].GetSaveString());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private static void Load()
        {
            if (!File.Exists(@"Engine\LayoutGui.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\LayoutGui.txt"))
                {
                    while(reader.EndOfStream == false)
                    {
                        string res = reader.ReadLine();

                        if(string.IsNullOrEmpty(res))
                        {
                            return;
                        }

                        OpenWindow window = new OpenWindow();
                        window.LoadFromString(res);
                        UiOpenWindows.Add(window);
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        // Проверка размера экрана

        private static bool ScreenSettingsIsAllRight()
        {
            int widthCur = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Size.Width;
            int heightCur = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Size.Height;
            int monitorCountCur = System.Windows.Forms.Screen.AllScreens.Length;

            if (!File.Exists(@"Engine\ScreenResolution.txt"))
            {
                SaveResolution(widthCur, heightCur, monitorCountCur);
                return true;
            }

            int widthOld = 0;
            int heightOld = 0;
            int monitorCountOld = 0;

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\ScreenResolution.txt"))
                {
                    widthOld = Convert.ToInt32(reader.ReadLine());
                    heightOld = Convert.ToInt32(reader.ReadLine());
                    monitorCountOld = Convert.ToInt32(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

            if(widthCur != widthOld ||
                heightCur != heightOld ||
                monitorCountCur != monitorCountOld)
            {
                SaveResolution(widthCur, heightCur, monitorCountCur);
                return false;
            }

            SaveResolution(widthCur, heightCur, monitorCountCur);
            return true;
        }

        private static void SaveResolution(int widthCur, int heightCur, int monitorCountCur)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\ScreenResolution.txt", false))
                {
                    writer.WriteLine(widthCur.ToString());
                    writer.WriteLine(heightCur.ToString());
                    writer.WriteLine(monitorCountCur.ToString());

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }
    }


    public class OpenWindow
    {
        public System.Windows.Window Ui
        {
            get
            {
                return _ui;
            }
            set
            {
                if(_ui != null)
                {
                    _ui.LocationChanged -= _ui_LocationChanged;
                    _ui.SizeChanged -= _ui_SizeChanged;
                    _ui.Closed -= _ui_Closed;
                }

                _ui = value;

                if(_ui != null)
                {
                    _ui.LocationChanged += _ui_LocationChanged;
                    _ui.SizeChanged += _ui_SizeChanged;
                    _ui.Closed += _ui_Closed;
                }
            }
        }

        private void _ui_Closed(object sender, EventArgs e)
        {
            _ui.LocationChanged -= _ui_LocationChanged;
            _ui.SizeChanged -= _ui_SizeChanged;
            _ui.Closed -= _ui_Closed;
            _ui = null;
        }

        private void _ui_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            UiLocationChangeEvent(_ui, Name);
        }

        private void _ui_LocationChanged(object sender, EventArgs e)
        {
            UiLocationChangeEvent(_ui, Name);
        }

        private System.Windows.Window _ui;

        public OpenWindowLayout Layout;

        public string Name;

        public DateTime WindowCreateTime;

        public bool IsActivate = false;

        public DateTime WindowUpdateTime;

        public string GetSaveString()
        {
            string res = "";

            res += Name + "#";
            res += Layout.Height + "$" + Layout.Left + "$" + Layout.Top + "$" + Layout.Widht + "$" + Layout.IsExpand;

            return res;
        }

        public void LoadFromString(string str)
        {
            string[] save = str.Split('#');
            Name = save[0];

            Layout = new OpenWindowLayout();

            string[] strLayout = save[1].Split('$');

            Layout.Height = strLayout[0].ToDecimal();
            Layout.Left = strLayout[1].ToDecimal();
            Layout.Top = strLayout[2].ToDecimal();
            Layout.Widht = strLayout[3].ToDecimal();
            Layout.IsExpand = Convert.ToBoolean(strLayout[4]);
        }

        public event Action<System.Windows.Window, string> UiLocationChangeEvent;
    }

    public class OpenWindowLayout
    {
        public decimal Top;

        public decimal Left;

        public decimal Widht;

        public decimal Height;
		
        public bool IsExpand;    // является ли окно развернутым		
    }
}
