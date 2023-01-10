using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

                    SubscribeEvents(ui, name);
                    return;
                }
            }

            OpenWindow window = new OpenWindow();
            window.Name = name;
            window.WindowCreateTime = DateTime.Now;
            window.Layout = new OpenWindowLayout();

            SetLayoutFromWindow(ui, window);

            lock(_lockerArrayWithWindows)
            {
                UiOpenWindows.Add(window);
                if(_isFirstTime == true)
                {
                    _isFirstTime = false;
                    Load();
                }
                _neadToSave = true;
            }

            SubscribeEvents(ui, name);           
        }

        private static string _lockerArrayWithWindows = "openUiLocker";

        private static void SubscribeEvents(System.Windows.Window ui, string name)
        {
            ui.LocationChanged += (o, e) =>
            {
                UiLocationChangeEvent(ui, name);
            };
            ui.SizeChanged += (o, e) =>
            {
                UiLocationChangeEvent(ui, name);
            };
            ui.Closed += (o, e) =>
            {
                UiClosedEvent(ui, name);
            };
        }

        private static void UiClosedEvent(System.Windows.Window ui, string name)
        {
            ui.LocationChanged -= (o, e) =>
            {
                UiLocationChangeEvent(ui, name);
            };
            ui.SizeChanged -= (o, e) =>
            {
                UiLocationChangeEvent(ui, name);
            };
            ui.Closed -= (o, e) =>
            {
                UiClosedEvent(ui, name);
            };
        }

        private static void UiLocationChangeEvent(System.Windows.Window ui, string name)
        {
            lock (_lockerArrayWithWindows)
            {
                for (int i = 0; i < UiOpenWindows.Count; i++)
                {
                    if (UiOpenWindows[i].Name == name)
                    {
                        SetLayoutFromWindow(ui, UiOpenWindows[i]);

                        break;
                    }
                }
                _neadToSave = true;
            }
        }

        private static void SetLayoutFromWindow(System.Windows.Window ui, OpenWindow windowLayout)
        {
            if(windowLayout.WindowCreateTime.AddSeconds(1) > DateTime.Now)
            {
                SetLayoutInWindow(ui, windowLayout.Layout);
                return;
            }

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

            if (layout.Height < 0 ||
                 layout.Widht < 0 ||
                 layout.Left < 0 ||
                 layout.Top < 0)
            {
                return;
            }

            ui.Height = Convert.ToDouble(layout.Height);
            ui.Width = Convert.ToDouble(layout.Widht);
            ui.Left = Convert.ToDouble(layout.Left);
            ui.Top = Convert.ToDouble(layout.Top);
        }

        public static List<OpenWindow> UiOpenWindows = new List<OpenWindow>();

        private static bool _neadToSave;

        public static bool IsClosed;

        private static void SaveWorkerPlace()
        {
            while(true)
            {
                Thread.Sleep(1000);

                if(_neadToSave == false)
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

                        if (UiOpenWindows[i].Layout.Height < 0 ||
                             UiOpenWindows[i].Layout.Widht < 0 ||
                             UiOpenWindows[i].Layout.Left < 0 ||
                             UiOpenWindows[i].Layout.Top < 0)
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
    }


    public class OpenWindow
    {
        public OpenWindowLayout Layout;

        public string Name;

        public DateTime WindowCreateTime;

        public string GetSaveString()
        {
            string res = "";

            res += Name + "#";
            res += Layout.Height + "$" + Layout.Left + "$" + Layout.Top + "$" + Layout.Widht;

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

        }
    }

    public class OpenWindowLayout
    {
        public decimal Top;

        public decimal Left;

        public decimal Widht;

        public decimal Height;
    }
}
