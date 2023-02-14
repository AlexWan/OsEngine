using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace OsEngine.Layout
{
    public class StartupLocation
    {
        /// <summary>
        /// запустить окно так чтобы текущее положение мыши было в его углу
        /// </summary>
        public static void Start_MouseInCorner(System.Windows.Window ui)
        {
            ui.Activated += Ui_Start_MouseInCorner_ContentActivated;
            ui.Closed += Ui_Start_MouseInCorner_ContentActivatedClosed;
        }

        private static void Ui_Start_MouseInCorner_ContentActivatedClosed(object sender, EventArgs e)
        {
            System.Windows.Window ui = (System.Windows.Window)sender;

            ui.Activated -= Ui_Start_MouseInCorner_ContentActivated;
            ui.Closed -= Ui_Start_MouseInCorner_ContentActivatedClosed;
        }

        private static void Ui_Start_MouseInCorner_ContentActivated(object sender, EventArgs e)
        {
            System.Windows.Window ui = (System.Windows.Window)sender;

            double xPosByWin32 = DesktopCoordinates.XmousePos(ui);
            double yPosByWin32 = DesktopCoordinates.YmousePos(ui);

            ui.Left = xPosByWin32 - ui.ActualWidth;
            ui.Top = yPosByWin32;

            ui.Activated -= Ui_Start_MouseInCorner_ContentActivated;
            ui.Closed -= Ui_Start_MouseInCorner_ContentActivatedClosed;
        }

        /// <summary>
        /// запустить окно так чтобы текущее положение мыши было в его центре
        /// </summary>
        public static void Start_MouseInCentre(System.Windows.Window ui)
        {
            ui.Activated += Ui_Start_MouseInCentre_ContentActivated;
            ui.Closed += Ui_Start_MouseInCentre_ContentActivatedClosed;
        }

        private static void Ui_Start_MouseInCentre_ContentActivatedClosed(object sender, EventArgs e)
        {
            System.Windows.Window ui = (System.Windows.Window)sender;
            ui.Activated -= Ui_Start_MouseInCentre_ContentActivated;
            ui.Closed -= Ui_Start_MouseInCentre_ContentActivatedClosed;
        }

        private static void Ui_Start_MouseInCentre_ContentActivated(object sender, EventArgs e)
        {
            // рассчитываем стандартное размещение

            System.Windows.Window ui = (System.Windows.Window)sender;

            double xPosByWin32 = DesktopCoordinates.XmousePos(ui);
            double yPosByWin32 = DesktopCoordinates.YmousePos(ui);

            double leftPos = xPosByWin32 - ui.Width/2;
            double topPos = yPosByWin32 - ui.Height / 2;

            // проверка разворачивания окна за экраном слева и сверху

            if (leftPos < 0)
            {
                leftPos = 0;
            }

            if(topPos < 0)
            {
                topPos = 0;
            }

            // проверка разворачивания окна за экраном вправо и вниз

            double xPosMouseOld = DesktopCoordinates.XmousePosOldVersion();
            double yPosMouseOld = DesktopCoordinates.YmousePosOldVersion();

            double screenWidth = DesktopCoordinates.CurrentScreenWidth();
            double screenHeight = DesktopCoordinates.CurrentScreenHeight();

            if(xPosMouseOld + ui.Width / 2 > screenWidth)
            {
                leftPos = xPosByWin32 - ui.Width;
            }

            if (yPosMouseOld + ui.Height / 2 > screenHeight)
            {
                topPos = yPosByWin32 - ui.Height;
            }

            // устанавливаем окончательные значения

            ui.Left = leftPos;
            ui.Top = topPos;

            ui.Activated -= Ui_Start_MouseInCentre_ContentActivated;
            ui.Closed -= Ui_Start_MouseInCentre_ContentActivatedClosed;
        }

    }
}
