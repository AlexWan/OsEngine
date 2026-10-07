using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace OsEngine
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App
    {
        public static App app;

        protected override void OnStartup(StartupEventArgs e)
        {
            app = this;

            // завершением управляем сами (Kill/Shutdown); авто-завершение по последнему окну
            // ломало закрытие MainWindow (Application.DoShutdown по закрывающемуся окну)
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            Themes.ThemeManager.Apply(Themes.ThemeManager.Load());

            base.OnActivated(e);
        }

        void IconMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1)
                sender.ForWindowFromTemplate(w => SystemCommands.CloseWindow(w));
        }

        void IconMouseUp(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                var point = element.PointToScreen(new Point(element.ActualWidth / 2, element.ActualHeight));
                sender.ForWindowFromTemplate(w => SystemCommands.ShowSystemMenu(w, point));
            }
        }

        void WindowLoaded(object sender, RoutedEventArgs e)
        {
            Window w = (Window)sender;
            w.StateChanged += WindowStateChanged;

            // защита от отрицательных размеров окна: при minimize/restore/maximize WPF WindowChrome
            // может построить отрицательный normal/restore Rect (ArgumentException "Ширина и высота
            // не должны быть отрицательными"); клампим размеры в WndProc до неотрицательных
            HwndSource source = (HwndSource)PresentationSource.FromVisual(w);

            if (source != null)
            {
                source.AddHook(_windowSizeGuardHook);
                w.Closed += (s, args) =>
                {
                    try { source.RemoveHook(_windowSizeGuardHook); }
                    catch
                    {
                        // ignore
                    }
                };
            }
        }

        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const int WM_GETMINMAXINFO = 0x0024;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        private static readonly HwndSourceHook _windowSizeGuardHook = WindowSizeGuardHook;

        private static IntPtr WindowSizeGuardHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            try
            {
                if (msg == WM_WINDOWPOSCHANGING)
                {
                    WINDOWPOS pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);

                    if (pos.cx < 0
                        || pos.cy < 0)
                    {
                        if (pos.cx < 0) pos.cx = 0;
                        if (pos.cy < 0) pos.cy = 0;

                        Marshal.StructureToPtr(pos, lParam, false);
                    }
                }
                else if (msg == WM_GETMINMAXINFO)
                {
                    MINMAXINFO info = Marshal.PtrToStructure<MINMAXINFO>(lParam);

                    bool changed = false;

                    if (info.ptMaxSize.X < 0) { info.ptMaxSize.X = 0; changed = true; }
                    if (info.ptMaxSize.Y < 0) { info.ptMaxSize.Y = 0; changed = true; }
                    if (info.ptMinTrackSize.X < 0) { info.ptMinTrackSize.X = 0; changed = true; }
                    if (info.ptMinTrackSize.Y < 0) { info.ptMinTrackSize.Y = 0; changed = true; }
                    if (info.ptMaxTrackSize.X < 0) { info.ptMaxTrackSize.X = 0; changed = true; }
                    if (info.ptMaxTrackSize.Y < 0) { info.ptMaxTrackSize.Y = 0; changed = true; }

                    // ptMaxPosition НЕ трогаем: отрицательные координаты допустимы на левых мониторах

                    if (changed)
                    {
                        Marshal.StructureToPtr(info, lParam, false);
                    }
                }
            }
            catch
            {
                // защитный хук не должен ломать обработку сообщений
            }

            return IntPtr.Zero;
        }

        void WindowStateChanged(object sender, EventArgs e)
        {
            var w = ((Window)sender);
            var handle = w.GetWindowHandle();
            var containerBorder = (Border)w.Template.FindName("PART_Container", w);

            if (w.WindowState == WindowState.Maximized)
            {
                // Make sure window doesn't overlap with the taskbar.
                var screen = System.Windows.Forms.Screen.FromHandle(handle);

                containerBorder.Padding = new Thickness(
                    SystemParameters.WorkArea.Left + 7,
                    SystemParameters.WorkArea.Top + 7,
                    (SystemParameters.PrimaryScreenWidth - SystemParameters.WorkArea.Right) + 7,
                    (SystemParameters.PrimaryScreenHeight - SystemParameters.WorkArea.Bottom) + 5);

            }
            else
            {
                containerBorder.Padding = new Thickness(0, 0, 0, 0);
            }
        }

        void CloseButtonClick(object sender, RoutedEventArgs e)
        {
            sender.ForWindowFromTemplate(w => SystemCommands.CloseWindow(w));
        }

        void MinButtonClick(object sender, RoutedEventArgs e)
        {

            sender.ForWindowFromTemplate(w => SystemCommands.MinimizeWindow(w));
        }

        void MaxButtonClick(object sender, RoutedEventArgs e)
        {
            sender.ForWindowFromTemplate(w =>
            {
                if (w.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(w);
                else SystemCommands.MaximizeWindow(w);
            });
        }
    }

    internal static class LocalExtensions
    {
        public static void ForWindowFromTemplate(this object templateFrameworkElement, Action<Window> action)
        {
            Window window = ((FrameworkElement)templateFrameworkElement).TemplatedParent as Window;
            if (window != null) action(window);
        }

        public static IntPtr GetWindowHandle(this Window window)
        {
            WindowInteropHelper helper = new WindowInteropHelper(window);
            return helper.Handle;
        }
    }
}
