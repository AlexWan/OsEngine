/*
 *Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Drawing;
using System.IO;
using System.Threading;
using OsEngine.Logging;

namespace OsEngine.Charts.ColorKeeper
{
    /// <summary>
    /// Хранилище цветов для чарта
    /// </summary>
    public class ChartMasterColorKeeper
    {
        
        /// <summary>
        /// имя
        /// </summary>
        private readonly string _name;

        public ChartColorScheme ColorScheme
        {
            get { return _colorScheme; }
        }

        private ChartColorScheme _colorScheme;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="name">имя панели, которой принадлежит</param>
        public ChartMasterColorKeeper(string name) 
        {
            _name = name;
            _pointType = PointType.Cross;
            SetThemeScheme(Themes.ThemeManager.CurrentTheme);
        }

        /// <summary>
        /// установить схему цветов чарта из темы приложения
        /// </summary>
        public void SetThemeScheme(string themeId)
        {
            if (themeId == "Tiffany")
            {
                ColorUpBodyCandle = Color.FromArgb(255, 14, 159, 110);
                ColorUpBorderCandle = Color.FromArgb(255, 11, 133, 96);
                ColorDownBodyCandle = Color.FromArgb(255, 214, 60, 74);
                ColorDownBorderCandle = Color.FromArgb(255, 183, 47, 60);
                ColorBackSecond = Color.FromArgb(255, 201, 231, 226);
                ColorBackChart = Color.FromArgb(255, 215, 239, 236);
                ColorBackCursor = Color.FromArgb(255, 10, 186, 181);
                ColorText = Color.FromArgb(255, 78, 110, 105);

                _colorScheme = ChartColorScheme.White;
            }
            else if (themeId == "Gray")
            {
                ColorUpBodyCandle = Color.FromArgb(255, 31, 138, 46);
                ColorUpBorderCandle = Color.FromArgb(255, 23, 115, 39);
                ColorDownBodyCandle = Color.FromArgb(255, 198, 40, 40);
                ColorDownBorderCandle = Color.FromArgb(255, 169, 31, 31);
                ColorBackSecond = Color.FromArgb(255, 255, 255, 255);
                ColorBackChart = Color.FromArgb(255, 238, 241, 244);
                ColorBackCursor = Color.FromArgb(255, 47, 109, 179);
                ColorText = Color.FromArgb(255, 89, 97, 107);

                _colorScheme = ChartColorScheme.White;
            }
            else if (themeId == "Midnight")
            {
                ColorUpBodyCandle = Color.FromArgb(255, 63, 163, 77);
                ColorUpBorderCandle = Color.FromArgb(255, 46, 128, 64);
                ColorDownBodyCandle = Color.FromArgb(255, 168, 32, 32);
                ColorDownBorderCandle = Color.FromArgb(255, 192, 40, 40);
                ColorBackSecond = Color.FromArgb(255, 10, 15, 24);
                ColorBackChart = Color.FromArgb(255, 14, 20, 32);
                ColorBackCursor = Color.FromArgb(255, 74, 123, 200);
                ColorText = Color.FromArgb(255, 138, 147, 166);

                _colorScheme = ChartColorScheme.Dark;
            }
            else
            { // DarkOrange — схема по умолчанию
                ColorUpBodyCandle = Color.FromArgb(57, 157, 54);
                ColorUpBorderCandle = Color.FromArgb(57, 157, 54);

                ColorDownBodyCandle = Color.FromArgb(17, 18, 23);
                ColorDownBorderCandle = Color.FromArgb(255, 83, 0);

                ColorBackSecond = Color.FromArgb(17, 18, 23);
                ColorBackChart = Color.FromArgb(17, 18, 23);
                ColorBackCursor = Color.FromArgb(255, 83, 0);

                ColorText = Color.FromArgb(255, 147, 147, 147);

                _colorScheme = ChartColorScheme.Black;
            }

            if (NeedToRePaintFormEvent != null)
            {
                NeedToRePaintFormEvent();
            }
        }

        /// <summary>
        /// уведомить о необходимости перерисовки.
        /// Раньше сохранял цвета в per-panel файл Engine\Color\ — отключено,
        /// цвета чарта определяются темой приложения (ThemeManager)
        /// </summary>
        public void Save() 
        {
            try
            {
                if (NeedToRePaintFormEvent != null)
                {
                    NeedToRePaintFormEvent();
                }
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(),LogMessageType.Error);
            }
        }

        /// <summary>
        /// удалить файл с настройками
        /// </summary>
        public void Delete()
        {
            try
            {
                if (File.Exists(@"Engine\Color\" + _name + "Color.txt"))
                {
                    File.Delete(@"Engine\Color\" + _name + "Color.txt");
                }
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(),LogMessageType.Error);
            }
        }

        /// <summary>
        /// загрузить чёрную схему
        /// </summary>
        public void SetBlackScheme()
        {
            ColorUpBodyCandle = Color.FromArgb(57, 157, 54);
            ColorUpBorderCandle = Color.FromArgb(57, 157, 54);

            ColorDownBodyCandle = Color.FromArgb(17, 18, 23);
            ColorDownBorderCandle = Color.FromArgb(255, 83, 0);

            ColorBackSecond = Color.FromArgb(17, 18, 23);
            ColorBackChart = Color.FromArgb(17, 18, 23);
            ColorBackCursor = Color.FromArgb(255, 83, 0);
            ColorText = Color.FromArgb(255, 147, 147, 147);

            _colorScheme = ChartColorScheme.Black;

            Save();

            if (NeedToRePaintFormEvent != null)
            {
                NeedToRePaintFormEvent();
            }
        }

        /// <summary>
        /// загрузить белую схему
        /// </summary>
        public void SetWhiteScheme()
        {
            ColorUpBodyCandle = Color.Azure;
            ColorUpBorderCandle = Color.Azure;

            ColorDownBodyCandle = Color.Black;
            ColorDownBorderCandle = Color.Black;

            ColorBackSecond = Color.Black;

            ColorBackChart = Color.FromArgb(255, 147, 147, 147);
            //ColorBackCursor = Color.DarkOrange;
            ColorBackCursor = Color.FromArgb(255, 255, 107, 0);

            ColorText = Color.Black;

            _colorScheme = ChartColorScheme.White;

            Save();

            if (NeedToRePaintFormEvent != null)
            {
                NeedToRePaintFormEvent();
            }
        }

 // цвета

        public Color ColorUpBodyCandle;

        public Color ColorDownBodyCandle;

        public Color ColorUpBorderCandle;

        public Color ColorDownBorderCandle;

        public Color ColorBackSecond;

        public Color ColorBackChart;

        public Color ColorBackCursor;

        public Color ColorText;

 // спецификация прорисовки позиций на графике

        /// <summary>
        /// размер для точки обозначающей позицию
        /// </summary>
        public ChartPositionTradeSize PointsSize;

        /// <summary>
        /// тип точки
        /// </summary>
        public PointType PointType
        {
            get { return _pointType; }
            set
            {
                _pointType = value;
                Save();
            }
        }

        private PointType _pointType;

        /// <summary>
        /// событие изменения цвета в хранилище
        /// </summary>
        public event Action NeedToRePaintFormEvent;

        /// <summary>
        /// выслать наверх сообщение об ошибке
        /// </summary>
        private void SendNewMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, LogMessageType.Error);
            }
            else if (type == LogMessageType.Error)
            { // если никто на нас не подписан и происходит ошибка
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string,LogMessageType> LogMessageEvent;

    }

    /// <summary>
    /// тип прорисовки позиций на графике
    /// </summary>
    public enum PointType
    {
        /// <summary>
        /// перекрестие
        /// </summary>
        Cross,

        /// <summary>
        /// в дебаггере перекрестие, без него картинка треугольника
        /// </summary>
        Auto,

        /// <summary>
        /// круг
        /// </summary>
        Circle,

        /// <summary>
        /// треугольник
        /// </summary>
        TriAngle,

        /// <summary>
        /// ромб
        /// </summary>
        Romb

    }

    /// <summary>
    /// схема раскраски чарта
    /// </summary>
    public enum ChartColorScheme
    {
        /// <summary>
        /// чёрная
        /// </summary>
        Black,
        /// <summary>
        /// белая
        /// </summary>
        White,
        /// <summary>
        /// тёмная
        /// </summary>
        Dark,
    }

    /// <summary>
    /// Размер точки данных на чарте для трейда. 1 - самая маленькая
    /// </summary>
    public enum ChartPositionTradeSize
    {
        Size1,

        Size2,

        Size3,

        Size4
    }
}
