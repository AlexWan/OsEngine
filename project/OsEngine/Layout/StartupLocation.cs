/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using OsEngine.Logging;
using OsEngine.Market;

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
            try
            {
                System.Windows.Window ui = (System.Windows.Window)sender;

                ui.Activated -= Ui_Start_MouseInCorner_ContentActivated;
                ui.Closed -= Ui_Start_MouseInCorner_ContentActivatedClosed;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private static void Ui_Start_MouseInCorner_ContentActivated(object sender, EventArgs e)
        {
            try
            {
                System.Windows.Window ui = (System.Windows.Window)sender;

                double xPosByWin32 = DesktopCoordinates.XmousePos(ui);
                double yPosByWin32 = DesktopCoordinates.YmousePos(ui);

                ui.Left = xPosByWin32 - ui.ActualWidth;
                ui.Top = yPosByWin32;

                ui.Activated -= Ui_Start_MouseInCorner_ContentActivated;
                ui.Closed -= Ui_Start_MouseInCorner_ContentActivatedClosed;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
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
            try
            {
                System.Windows.Window ui = (System.Windows.Window)sender;

                ui.Activated -= Ui_Start_MouseInCentre_ContentActivated;
                ui.Closed -= Ui_Start_MouseInCentre_ContentActivatedClosed;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private static void Ui_Start_MouseInCentre_ContentActivated(object sender, EventArgs e)
        {
            try
            {
                // рассчитываем стандартное размещение

                System.Windows.Window ui = (System.Windows.Window)sender;

                double xPosByWin32 = DesktopCoordinates.XmousePos(ui);
                double yPosByWin32 = DesktopCoordinates.YmousePos(ui);

                double leftPos = xPosByWin32 - ui.Width / 2;
                double topPos = yPosByWin32 - ui.Height / 2;

                System.Drawing.Rectangle actualArea = new System.Drawing.Rectangle();
                actualArea = System.Windows.Forms.Screen.GetWorkingArea(System.Windows.Forms.Control.MousePosition);

                // проверка разворачивания окна за экраном слева и сверху

                if (leftPos < actualArea.X)
                {
                    leftPos = actualArea.X;
                }

                if (topPos < actualArea.Y)
                {
                    topPos = actualArea.Y;
                }

                // проверка разворачивания окна за экраном вправо и вниз

                if (leftPos + ui.Width > actualArea.X + actualArea.Width)
                {
                    leftPos = actualArea.X + actualArea.Width - ui.Width;
                }

                if (topPos + ui.Height > actualArea.Y + actualArea.Height)
                {
                    topPos = actualArea.Y + actualArea.Height - ui.Height;
                }

                // устанавливаем окончательные значения

                ui.Left = leftPos;
                ui.Top = topPos;

                ui.Activated -= Ui_Start_MouseInCentre_ContentActivated;
                ui.Closed -= Ui_Start_MouseInCentre_ContentActivatedClosed;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// ужать высоту окна под рабочую область экрана,
        /// если она больше (проблема крупных масштабов экрана 125%+)
        /// </summary>
        public static void Start_FitHeightToWorkArea(System.Windows.Window ui)
        {
            ui.Activated += Ui_Start_FitHeightToWorkArea_ContentActivated;
            ui.Closed += Ui_Start_FitHeightToWorkArea_ContentActivatedClosed;
        }

        private static void Ui_Start_FitHeightToWorkArea_ContentActivatedClosed(object sender, EventArgs e)
        {
            try
            {
                System.Windows.Window ui = (System.Windows.Window)sender;

                ui.Activated -= Ui_Start_FitHeightToWorkArea_ContentActivated;
                ui.Closed -= Ui_Start_FitHeightToWorkArea_ContentActivatedClosed;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private static void Ui_Start_FitHeightToWorkArea_ContentActivated(object sender, EventArgs e)
        {
            try
            {
                System.Windows.Window ui = (System.Windows.Window)sender;

                System.Drawing.Rectangle workArea = System.Windows.Forms.Screen.FromHandle(
                    new System.Windows.Interop.WindowInteropHelper(ui).Handle).WorkingArea;

                // запас под панель задач и рамку окна
                double maxHeight = workArea.Height - 40;

                if (ui.Height > maxHeight)
                {
                    if (ui.MinHeight > maxHeight)
                    {
                        ui.MinHeight = maxHeight;
                    }

                    ui.Height = maxHeight;
                }

                // окно не должно открываться за пределами видимой области
                if (ui.Top < workArea.Top)
                {
                    ui.Top = workArea.Top;
                }

                if (ui.Top + ui.Height > workArea.Bottom)
                {
                    ui.Top = workArea.Bottom - ui.Height;
                }

                ui.Activated -= Ui_Start_FitHeightToWorkArea_ContentActivated;
                ui.Closed -= Ui_Start_FitHeightToWorkArea_ContentActivatedClosed;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

    }
}
