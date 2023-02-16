/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;

namespace OsEngine.Layout
{
    public class StickyBorders
    {
        public static void Listen(System.Windows.Window ui)
        {
            SubscribeEvents(ui);

            lock(_windowsArrayLoker)
            {
                if(ui.Uid == "")
                {
                    ui.Uid = (DateTime.Now - DateTime.MinValue).TotalMilliseconds.ToString();
                }

                MoveWindow window = new MoveWindow();
                window.Ui = ui;
                window.UpdatePosition();

                _windows.Add(window);
            }
        }

        private static List<MoveWindow> _windows = new List<MoveWindow>();

        private static string _windowsArrayLoker = "lockerWinArray";

        private static void SubscribeEvents(System.Windows.Window ui)
        {
            ui.LocationChanged += Ui_LocationChanged;
            ui.SizeChanged += Ui_SizeChanged;
            ui.Closed += Ui_Closed;
        }

        private static void Ui_Closed(object sender, EventArgs e)
        {
            try
            {
                System.Windows.Window ui = (System.Windows.Window)sender;

                ui.LocationChanged -= Ui_LocationChanged;
                ui.SizeChanged -= Ui_SizeChanged;
                ui.Closed -= Ui_Closed;

                lock (_windowsArrayLoker)
                {
                    for (int i = 0; i < _windows.Count; i++)
                    {
                        if (_windows[i].Ui.Uid == ui.Uid)
                        {
                            _windows.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                System.Windows.MessageBox.Show(error.ToString());
            }
        }

        private static void Ui_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            try
            {
                System.Windows.Window ui = (System.Windows.Window)sender;

                MoveWindow window = null;

                lock (_windowsArrayLoker)
                {
                    for (int i = 0; i < _windows.Count; i++)
                    {
                        if (_windows[i].Ui.Uid == ui.Uid)
                        {
                            window = _windows[i];
                            break;
                        }
                    }
                }

                if (window == null)
                {
                    return;
                }

                window.UpdatePosition();

                if (window.IsReady() == false)
                {
                    return;
                }

                lock (_windowsArrayLoker)
                {
                    for (int i = 0; i < _windows.Count; i++)
                    {
                        _windows[i].UnFreezeX();
                        _windows[i].UnFreezeY();
                    }
                }
            }
            catch(Exception error)
            {
                System.Windows.MessageBox.Show(error.ToString());
            }
        }

        private static void Ui_LocationChanged(object sender, EventArgs e)
        {
            try
            {
                System.Windows.Window ui = (System.Windows.Window)sender;

                MoveWindow window = null;

                lock (_windowsArrayLoker)
                {
                    for (int i = 0; i < _windows.Count; i++)
                    {
                        if (_windows[i].Ui.Uid == ui.Uid)
                        {
                            window = _windows[i];
                            break;
                        }
                    }
                }

                if (window == null)
                {
                    return;
                }

                if(window.IsReady() == false)
                {
                    return;
                }

                window.CheckFreezeStateX();
                window.CheckFreezeStateY();

                TryToGlueWindowOnMove(window);

            }
            catch (Exception error)
            {
                System.Windows.MessageBox.Show(error.ToString());
            }
        }

        // логика слепления окон

        private static void TryToGlueWindowOnMove(MoveWindow windowActive)
        {

            windowActive.UpdatePosition();
            windowActive.UpdateDirection();

            if(windowActive.Direction == MoveWindowDirection.None)
            {
                return;
            }

            if (windowActive.Direction == MoveWindowDirection.Left 
                || windowActive.Direction == MoveWindowDirection.LeftDown
                || windowActive.Direction == MoveWindowDirection.LeftUp)
            {// проверяем ЛЕВУЮ границу окна

                lock (_windowsArrayLoker)
                {
                    for (int i = 0; i < _windows.Count; i++)
                    {
                        if (_windows[i].Ui.Uid == windowActive.Ui.Uid)
                        {
                            continue;
                        }

                        double distanceP = _windows[i].GetPersentDistance(windowActive.Left, BorderType.Right);

                        if(distanceP > 0 && distanceP < 0.5)
                        {
                            windowActive.FreezeX(_windows[i].Right + 1);
                            break;
                        }

                        distanceP = _windows[i].GetPersentDistance(windowActive.Left, BorderType.Left);

                        if (distanceP > 0 && distanceP < 0.5)
                        {
                            windowActive.FreezeX(_windows[i].Left);
                            break;
                        }
                    }
                }
            }

            if (windowActive.Direction == MoveWindowDirection.Right
               || windowActive.Direction == MoveWindowDirection.RightDown
               || windowActive.Direction == MoveWindowDirection.RightUp)
            {// проверяем ПРАВУЮ границу окна
                lock (_windowsArrayLoker)
                {
                    for (int i = 0; i < _windows.Count; i++)
                    {
                        if (_windows[i].Ui.Uid == windowActive.Ui.Uid)
                        {
                            continue;
                        }

                        double distanceP = _windows[i].GetPersentDistance(windowActive.Right, BorderType.Left);

                        if (distanceP > 0 && distanceP < 0.5)
                        {
                            windowActive.FreezeX(_windows[i].Left - 1 - windowActive.Ui.Width);
                            break;
                        }

                        distanceP = _windows[i].GetPersentDistance(windowActive.Right, BorderType.Right);

                        if (distanceP > 0 && distanceP < 0.5)
                        {
                            windowActive.FreezeX(_windows[i].Right - windowActive.Ui.Width);
                            break;
                        }
                    }
                }
            }

            if (windowActive.Direction == MoveWindowDirection.Up
               || windowActive.Direction == MoveWindowDirection.LeftUp
               || windowActive.Direction == MoveWindowDirection.RightUp)
            {// проверяем ВЕРХНЮЮ границу окна
                lock (_windowsArrayLoker)
                {
                    for (int i = 0; i < _windows.Count; i++)
                    {
                        if (_windows[i].Ui.Uid == windowActive.Ui.Uid)
                        {
                            continue;
                        }

                        double distanceP = _windows[i].GetPersentDistance(windowActive.Up, BorderType.Bottom);

                        if (distanceP > 0 && distanceP < 1)
                        {
                            windowActive.FreezeY(_windows[i].Bottom + 1);
                            break;
                        }

                        distanceP = _windows[i].GetPersentDistance(windowActive.Up, BorderType.Up);

                        if (distanceP > 0 && distanceP < 1)
                        {
                            windowActive.FreezeY(_windows[i].Up);
                            break;
                        }
                    }
                }
            }

            if (windowActive.Direction == MoveWindowDirection.Down
               || windowActive.Direction == MoveWindowDirection.LeftDown
               || windowActive.Direction == MoveWindowDirection.RightDown)
            {// проверяем НИЖНЮЮ границу окна
                lock (_windowsArrayLoker)
                {
                    for (int i = 0; i < _windows.Count; i++)
                    {
                        if (_windows[i].Ui.Uid == windowActive.Ui.Uid)
                        {
                            continue;
                        }

                        double distanceP = _windows[i].GetPersentDistance(windowActive.Bottom, BorderType.Up);

                        if (distanceP > 0 && distanceP < 1)
                        {
                            windowActive.FreezeY(_windows[i].Up - 1 - windowActive.Ui.Height);
                            break;
                        }

                        distanceP = _windows[i].GetPersentDistance(windowActive.Bottom, BorderType.Bottom);

                        if (distanceP > 0 && distanceP < 1)
                        {
                            windowActive.FreezeY(_windows[i].Bottom - windowActive.Ui.Height);
                            break;
                        }
                    }
                }
            }

        }

    }

    /// <summary>
    /// двигающееся окно
    /// </summary>
    public class MoveWindow
    {
        public bool IsReady()
        {
            if (Ui.IsVisible == false)
            {
                return false;
            }
            if (Ui.IsInitialized == false)
            {
                return false;
            }

            if (Ui.Width == 0)
            {
                return false;
            }

            if (Ui.Height == 0)
            {
                return false;
            }

            return true;
        }

        public void UpdatePosition()
        {
            _rightPrev = Right;
            _bottomPrev = Bottom;
            _leftPrev = Left;
            _upPrev = Up;

            if(_height != 0 && _width != 0 &&
                (_height != Ui.Height
                || _width != Ui.Width))
            {
                _rightPrev = 0;
                _bottomPrev = 0;
                _leftPrev = 0;
                _upPrev = 0;
                Direction = MoveWindowDirection.None;
            }

            _height = Ui.Height;
            _width = Ui.Width;

            Right = Ui.Left + Ui.Width;
            Bottom = Ui.Top + Ui.Height;
            Left = Ui.Left;
            Up = Ui.Top;
        }

        public void UpdateDirection()
        {
            if (_rightPrev == 0
                            && _bottomPrev == 0
                            && _leftPrev == 0
                            && _upPrev == 0)
            {
                // первый вход
                return;
            }

            if (_rightPrev == Right
                && _bottomPrev == Bottom
                && _leftPrev == Left
                && _upPrev == Up)
            {
                // ничего не поменялось
                return;
            }

            // устанавливаем направление движения

            if (Right > _rightPrev)
            {// какое-то движение вправо
                if (Up < _upPrev)
                {// движемся вправо и вверх
                    Direction = MoveWindowDirection.RightUp;
                }
                else if (Up > _upPrev)
                {// движемся вправо и вниз
                    Direction = MoveWindowDirection.RightDown;
                }
                else
                {// движемя просто вправо
                    Direction = MoveWindowDirection.Right;
                }
            }
            else if (Left < _leftPrev)
            {// какое-то движение влево
                if (Up < _upPrev)
                {// движемся влево и вверх
                    Direction = MoveWindowDirection.LeftUp;
                }
                else if (Up > _upPrev)
                {// движемся влево и вниз
                    Direction = MoveWindowDirection.LeftDown;
                }
                else
                {// движемя просто влево
                    Direction = MoveWindowDirection.Left;
                }
            }
            else if (Up < _upPrev)
            {// ввижемся просто вверх
                Direction = MoveWindowDirection.Up;
            }
            else if (Up > _upPrev)
            {// ввижемся просто вниз
                Direction = MoveWindowDirection.Down;
            }
            else
            {
                Direction = MoveWindowDirection.None;
            }
        }

        public System.Windows.Window Ui;

        public double Left;
        public double Right;
        public double Bottom;
        public double Up;

        public double GetPersentDistance(double border, BorderType borderTypeToGetDistance)
        {
            double result = 100;

            if(borderTypeToGetDistance == BorderType.Up &&
                Up != 0)
            {
                result = Math.Abs(Up - border) / (border / 100);
            }
            if (borderTypeToGetDistance == BorderType.Bottom &&
                Bottom != 0)
            {
                result = Math.Abs(Bottom - border) / (border / 100);
            }
            if (borderTypeToGetDistance == BorderType.Left &&
                Left != 0)
            {
                double dist = Math.Abs(Left - border);
                result = dist / (border / 100);
            }
            if (borderTypeToGetDistance == BorderType.Right &&
                Right != 0)
            {
                result = Math.Abs(Right - border) / (border / 100);
            }

            return result;
        }

        private double _rightPrev;
        private double _bottomPrev;
        private double _leftPrev;
        private double _upPrev;

        private double _height;
        private double _width;

        public MoveWindowDirection Direction;

        // заморозка расположения окна по горизонтали

        private double _leftXFreez;
        private double _mousePositionStartXFreez;
        private bool _xIsFreezing;
        private DateTime _lastTimeFreezeUnFreezeX;

        public void FreezeX(double leftX)
        {
            if(_xIsFreezing == true)
            {
                return;
            }
            if(_lastTimeFreezeUnFreezeX.AddSeconds(1)> DateTime.Now)
            {
                return;
            }

            _leftXFreez = leftX;
            _xIsFreezing = true;

            _mousePositionStartXFreez = DesktopCoordinates.XmousePosOldVersion();
            _lastTimeFreezeUnFreezeX = DateTime.Now;
        }

        public void CheckFreezeStateX()
        {
            if(_xIsFreezing == false)
            {
                return;
            }

            // 1 пробуем разморозить

            double xMouseCurrent = DesktopCoordinates.XmousePosOldVersion();

            double xMouseMove = Math.Abs(_mousePositionStartXFreez - xMouseCurrent);

            double xMouseMovePercent = xMouseMove / (_mousePositionStartXFreez / 100);

            if(xMouseMovePercent > 0.5)
            {
                UnFreezeX();
                return;
            }

            // 2 если не разморозили, выставляем X назад

            Ui.Left = _leftXFreez;
        }

        public void UnFreezeX()
        {
            _xIsFreezing = false;
            _leftXFreez = 0;
            _mousePositionStartXFreez = 0;
            _lastTimeFreezeUnFreezeX = DateTime.Now;
        }

        // заморозка расположения окна по вертикали

        private double _upYFreez;
        private double _mousePositionStartYFreez;
        private bool _yIsFreezing;
        private DateTime _lastTimeFreezeUnFreezeY;

        public void FreezeY(double upY)
        {
            if (_yIsFreezing == true)
            {
                return;
            }
            if (_lastTimeFreezeUnFreezeY.AddSeconds(1) > DateTime.Now)
            {
                return;
            }

            _upYFreez = upY;
            _yIsFreezing = true;

            _mousePositionStartYFreez = DesktopCoordinates.YmousePosOldVersion();
            _lastTimeFreezeUnFreezeY = DateTime.Now;
        }

        public void UnFreezeY()
        {
            _yIsFreezing = false;
            _upYFreez = 0;
            _mousePositionStartYFreez = 0;
            _lastTimeFreezeUnFreezeY = DateTime.Now;
        }

        public void CheckFreezeStateY()
        {
            if (_yIsFreezing == false)
            {
                return;
            }

            // 1 пробуем разморозить

            double xMouseCurrent = DesktopCoordinates.YmousePosOldVersion();

            double yMouseMove = Math.Abs(_mousePositionStartYFreez - xMouseCurrent);

            double yMouseMovePercent = yMouseMove / (_mousePositionStartYFreez / 100);

            if (yMouseMovePercent > 1)
            {
                UnFreezeY();
                return;
            }

            // 2 если не разморозили, выставляем X назад

            Ui.Top = _upYFreez;
        }

    }

    public enum BorderType
    {
        Up,
        Bottom,
        Left,
        Right
    }

    public enum MoveWindowDirection
    {
        None,
        Left,
        Right,
        Down,
        Up,
        LeftUp,
        LeftDown,
        RightUp,
        RightDown
    }
}
