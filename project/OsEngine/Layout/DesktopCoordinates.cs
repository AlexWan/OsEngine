using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;

namespace OsEngine.Layout
{
    public class DesktopCoordinates
    {

        public static double YmousePos(System.Windows.Window ui)
        {
            var point = System.Windows.Forms.Control.MousePosition;
            Point newPoint = new Point(point.X, point.Y);

            var transform = PresentationSource.FromVisual(ui).CompositionTarget.TransformFromDevice;
            var mouse = transform.Transform(newPoint);

            return mouse.Y;
        }

        public static double XmousePos(System.Windows.Window ui)
        {
            var point = System.Windows.Forms.Control.MousePosition;
            Point newPoint = new Point(point.X, point.Y);

            var transform = PresentationSource.FromVisual(ui).CompositionTarget.TransformFromDevice;
            var mouse = transform.Transform(newPoint);

            return mouse.X;
        }

        public static double YmousePosOldVersion()
        {
            var point = System.Windows.Forms.Control.MousePosition;
            return point.Y;
        }

        public static double XmousePosOldVersion()
        {
            var point = System.Windows.Forms.Control.MousePosition;
            return point.X;
        }

        public static double CurrentScreenWidth()
        {
            int width = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Size.Width;
            return width;
        }

        public static double CurrentScreenHeight()
        {
            int height = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Size.Height;
            return height;
        }

    }
    public struct POINT
    {
        public int X;
        public int Y;
    }
}
