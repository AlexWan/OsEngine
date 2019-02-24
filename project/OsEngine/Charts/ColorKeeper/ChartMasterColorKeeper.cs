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
            Load();
        }

        /// <summary>
        ///  загрузить из файла
        /// </summary>
        private void Load()
        {
            try
            {
                Thread.Sleep(500);

                if (!Directory.Exists(@"Engine\Color"))
                {
                    Directory.CreateDirectory(@"Engine\Color");
                }

                if (File.Exists(@"Engine\Color\" + _name + "Color.txt"))
                {
                    using (StreamReader reader = new StreamReader(@"Engine\Color\" + _name + "Color.txt"))
                    {
                        ColorUpBodyCandle = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                        ColorUpBorderCandle = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                        ColorDownBodyCandle = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                        ColorDownBorderCandle = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                        ColorBackSecond = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                        ColorBackChart = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                        ColorBackCursor = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                        ColorText = Color.FromArgb(Convert.ToInt32(reader.ReadLine()));
                        Enum.TryParse(reader.ReadLine(), true, out _pointType);
                        Enum.TryParse(reader.ReadLine(), true, out _colorScheme);
                    }
                }
                else
                {
                    ColorUpBodyCandle = Color.FromArgb(57, 157, 54);
                    ColorUpBorderCandle = Color.FromArgb(57, 157, 54);

                    ColorDownBodyCandle = Color.FromArgb(17, 18, 23);
                    ColorDownBorderCandle = Color.FromArgb(255, 83, 0);

                    ColorBackSecond = Color.FromArgb(17, 18, 23);
                    ColorBackChart = Color.FromArgb(17, 18, 23);
                    ColorBackCursor = Color.FromArgb(255, 83, 0);

                    ColorText = Color.FromArgb(51, 51, 62);

                    _colorScheme = ChartColorScheme.Black;
                }
            }
            catch (Exception error)
            {
                SendNewMessage(error.ToString(),LogMessageType.Error);
            }
        }

        /// <summary>
        /// сохранить в файл
        /// </summary>
        public void Save() 
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\Color\" + _name + "Color.txt"))
                {
                    writer.WriteLine(ColorUpBodyCandle.ToArgb());
                    writer.WriteLine(ColorUpBorderCandle.ToArgb());
                    writer.WriteLine(ColorDownBodyCandle.ToArgb());
                    writer.WriteLine(ColorDownBorderCandle.ToArgb());

                    writer.WriteLine(ColorBackSecond.ToArgb());
                    writer.WriteLine(ColorBackChart.ToArgb());
                    writer.WriteLine(ColorBackCursor.ToArgb());
                    writer.WriteLine(ColorText.ToArgb());

                    writer.WriteLine(_pointType);

                    writer.WriteLine(_colorScheme);
                }

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

            ColorText = Color.FromArgb(149, 159, 176);

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
