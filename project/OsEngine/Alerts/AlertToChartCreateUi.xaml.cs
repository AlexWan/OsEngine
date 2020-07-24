/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using OsEngine.Entity;
using OsEngine.Language;
using MessageBox = System.Windows.MessageBox;

namespace OsEngine.Alerts
{

    /// <summary>
    /// Alert creation and editing window
    /// Окно создания и редактирования алерта
    /// </summary>
    public partial class AlertToChartCreateUi
    {
        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="alert">alert for editing, if null will be created new/алерт для редактирования, если null будет создан новый</param>
        /// <param name="keeper">alert storage/хранилище алертов</param>
        public AlertToChartCreateUi(AlertToChart alert, AlertMaster keeper) 
        {
            InitializeComponent();

            _waitOne = false;
            _waitTwo = false;
            NeadToSave = false;
            _candleOneTime = DateTime.MinValue;
            _candleOneValue = 0;
            _candleTwoTime = DateTime.MinValue;
            _candleTwoValue = 0;
            _keeper = keeper;

            ComboBoxType.Items.Add(ChartAlertType.Line);
            ComboBoxType.Items.Add(ChartAlertType.FibonacciChannel);
            ComboBoxType.Items.Add(ChartAlertType.FibonacciSpeedLine);
            ComboBoxType.Items.Add(ChartAlertType.HorisontalLine);

            ComboBoxType.SelectedItem = ChartAlertType.Line;

            ComboBoxType.Text = ChartAlertType.Line.ToString();
            // default fireworks
            // фейерверки по умолчанию
            CheckBoxOnOff.IsChecked = false;
            CheckBoxMusicAlert.IsChecked = false;
            CheckBoxWindow.IsChecked = false;

            ComboBoxFatLine.Text = "2";
            TextBoxLabelAlert.Text = "";

            System.Drawing.Color red = System.Drawing.Color.DarkRed;

            ButtonColorLabel.Background =
                new SolidColorBrush(Color.FromArgb(red.A,red.R,red.G,red.B));

            ButtonColorLine.Background =
                new SolidColorBrush(Color.FromArgb(red.A, red.R, red.G, red.B));

            CheckBoxWindow.IsChecked = false;
            TextBoxAlertMessage.Text = OsLocalization.Alerts.Message2;
            // default trade settings
            // торговые настойки по умолчанию
            TextBoxVolumeReaction.Text = "1";

            ComboBoxSignalType.Items.Add((SignalType.Buy));
            ComboBoxSignalType.Items.Add((SignalType.Sell));
            ComboBoxSignalType.Items.Add((SignalType.CloseAll));
            ComboBoxSignalType.Items.Add((SignalType.CloseOne));
            ComboBoxSignalType.Items.Add((SignalType.None));
            ComboBoxSignalType.SelectedItem = SignalType.None;
            ComboBoxSignalType.SelectionChanged += ComboBoxSignalType_SelectionChanged;

            ComboBoxOrderType.Items.Add((OrderPriceType.Market));
            ComboBoxOrderType.Items.Add((OrderPriceType.Limit));
            ComboBoxOrderType.SelectedItem = OrderPriceType.Market;

            TextBoxSlippage.Text = "0";
            TextBoxClosePosition.Text = "0";

            ComboBoxMusicType.Items.Add(AlertMusic.Bird);
            ComboBoxMusicType.Items.Add(AlertMusic.Duck);
            ComboBoxMusicType.Items.Add(AlertMusic.Wolf);
            ComboBoxMusicType.SelectedItem = AlertMusic.Bird;

            if (alert != null)
            {
                MyAlert = alert;
                LoadFromAlert();
                ComboBoxType.IsEnabled = false;
                NeadToSave = true;
            }
            
            CheckBoxOnOff.Click += CheckBoxOnOff_Click;
            CheckBoxMusicAlert.Click += CheckBoxOnOff_Click;
            CheckBoxWindow.Click += CheckBoxOnOff_Click;

            ComboBoxFatLine.SelectionChanged += ComboBoxFatLine_SelectionChanged;

            TextBoxAlertMessage.TextChanged += TextBoxAlertMessage_TextChanged;
            TextBoxLabelAlert.TextChanged += TextBoxAlertMessage_TextChanged;

            ComboBoxOrderType.SelectionChanged += ComboBoxOrderType_SelectionChanged;
            HideTradeButton();

            ChangeText();
            OsLocalization.LocalizationTypeChangeEvent += ChangeText;


            LabelOsa.MouseDown += LabelOsa_MouseDown;
        }

        private void ChangeText()
        {
            Title = OsLocalization.Alerts.TitleAlertToChartCreateUi;
            CheckBoxOnOff.Content = OsLocalization.Alerts.Label1;
            ButtonSendFirst.Content = OsLocalization.Alerts.Label2;
            LabelTrade.Content = OsLocalization.Alerts.Label3;
            LabelReactionType.Content = OsLocalization.Alerts.Label4;
            LabelOrderType.Content = OsLocalization.Alerts.Label5;
            LabelVolume.Content = OsLocalization.Alerts.Label6;
            LabelSlippage.Content = OsLocalization.Alerts.Label7;
            LabelNumClosedPos.Content = OsLocalization.Alerts.Label8;
            LabelFireworks.Content = OsLocalization.Alerts.Label9;


            CheckBoxMusicAlert.Content = OsLocalization.Alerts.Label10;
            LabelLineWidth.Content = OsLocalization.Alerts.Label11;
            LabelToopTipText.Content = OsLocalization.Alerts.Label12;
            LabelToolTipColor.Content = OsLocalization.Alerts.Label13;
            LabelLineColor.Content = OsLocalization.Alerts.Label14;
            ButtonColorLabel.Content = OsLocalization.Alerts.Label15;
            ButtonColorLine.Content = OsLocalization.Alerts.Label15;
            CheckBoxWindow.Content = OsLocalization.Alerts.Label16;
            ButtonSave.Content = OsLocalization.Alerts.Label17;

        }

        void LabelOsa_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("http://o-s-a.net");
        }


        /// <summary>
        /// hide unnecessary controls for trading
        /// спрятать ненужные контролы для торговли
        /// </summary>
        private void HideTradeButton()
        {
            SignalType type;

            Enum.TryParse(ComboBoxSignalType.SelectedItem.ToString(), true, out type);

            if (type == SignalType.None)
            {
                TextBoxSlippage.IsEnabled = false;
                TextBoxClosePosition.IsEnabled = false;
                TextBoxVolumeReaction.IsEnabled = false;
                ComboBoxOrderType.IsEnabled = false;
                return;
            }
            else if (type == SignalType.CloseAll)
            {
                TextBoxSlippage.IsEnabled = true;
                TextBoxClosePosition.IsEnabled = false;
                TextBoxVolumeReaction.IsEnabled = true;
                ComboBoxOrderType.IsEnabled = true;
            }
            else if (type == SignalType.CloseOne)
            {
                TextBoxSlippage.IsEnabled = true;
                TextBoxClosePosition.IsEnabled = true;
                TextBoxVolumeReaction.IsEnabled = true;
                ComboBoxOrderType.IsEnabled = true;
            }
            else if (type == SignalType.Buy)
            {
                TextBoxSlippage.IsEnabled = true;
                TextBoxClosePosition.IsEnabled = false;
                TextBoxVolumeReaction.IsEnabled = true;
                ComboBoxOrderType.IsEnabled = true;
            }
            else if (type == SignalType.Sell)
            {
                TextBoxSlippage.IsEnabled = true;
                TextBoxClosePosition.IsEnabled = false;
                TextBoxVolumeReaction.IsEnabled = true;
                ComboBoxOrderType.IsEnabled = true;
            }

            OrderPriceType orderType;

            Enum.TryParse(ComboBoxOrderType.SelectedItem.ToString(), true, out orderType);

            if (orderType == OrderPriceType.Limit)
            {
                TextBoxSlippage.IsEnabled = true;
            }
            else
            {
                TextBoxSlippage.IsEnabled = false;
            }
        }

        /// <summary>
        /// alert storage
        /// хранилище Алертов
        /// </summary>
        private readonly AlertMaster _keeper;

        /// <summary>
        /// current alert
        /// текущий Алерт
        /// </summary>
        public AlertToChart MyAlert;

        /// <summary>
        /// whether you need to save Alert after closing window
        /// нужно ли сохранять Алерт после закрытия окна
        /// </summary>
        public bool NeadToSave;

        /// <summary>
        /// upload Alert's settings to form
        /// загрузить настройки Алерта на форму
        /// </summary>
        private void LoadFromAlert()
        {
            ComboBoxSignalType.SelectedItem = MyAlert.SignalType;
            ComboBoxType.SelectedItem = MyAlert.Type;
            CheckBoxOnOff.IsChecked = MyAlert.IsOn;
            CheckBoxMusicAlert.IsChecked = MyAlert.IsMusicOn;
            ComboBoxMusicType.SelectedItem = MyAlert.Music;

            ComboBoxFatLine.Text = MyAlert.BorderWidth.ToString();
            TextBoxLabelAlert.Text = MyAlert.Label;

            System.Drawing.Color labelColor = MyAlert.ColorLabel;
            System.Drawing.Color labelLine = MyAlert.ColorLine;

            ButtonColorLabel.Background =
                new SolidColorBrush(Color.FromArgb(labelColor.A, labelColor.R, labelColor.G, labelColor.B));

            ButtonColorLine.Background =
                new SolidColorBrush(Color.FromArgb(labelLine.A, labelLine.R, labelLine.G, labelLine.B));


            CheckBoxWindow.IsChecked = MyAlert.IsMessageOn;
            TextBoxAlertMessage.Text = MyAlert.Message;
            TextBoxVolumeReaction.Text = MyAlert.VolumeReaction.ToString();

            TextBoxSlippage.Text = MyAlert.Slippage.ToString(new CultureInfo("ru-RU"));
            TextBoxClosePosition.Text = MyAlert.NumberClosePosition.ToString();
            TextBoxVolumeReaction.Text = MyAlert.VolumeReaction.ToString();
            ComboBoxOrderType.SelectedItem = MyAlert.OrderPriceType;
            ComboBoxSignalType.SelectedItem = MyAlert.SignalType;
        }
        //Interception of control changes
        //перехват изменения контролов

        /// <summary>
        /// user switched order type
        /// пользователь переключил тип ордера
        /// </summary>
        void ComboBoxOrderType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            HideTradeButton();
        }

        /// <summary>
        /// user changed signal type
        /// ползователь изменил тип сигнала
        /// </summary>
        void ComboBoxSignalType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            HideTradeButton();
        }

        /// <summary>
        /// click on tick on / off
        /// клик на галочке вкл/выкл
        /// </summary>
        void CheckBoxOnOff_Click(object sender, RoutedEventArgs e)
        {
            SaveAlert();
        }

        /// <summary>
        /// alert signature text
        /// тексе подписи алерта
        /// </summary>
        void TextBoxAlertMessage_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            SaveAlert();
        }

        /// <summary>
        /// line thickness changed
        /// толщина линии изменена
        /// </summary>
        void ComboBoxFatLine_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            SaveAlert();
        }
        // slider work
        //работа со слайдером

        /// <summary>
        /// slider's position has changed.
        /// изменилось положение слайдера
        /// </summary>
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ComboBoxType.Text == ChartAlertType.Line.ToString() ||
                _waitOne|| 
                _waitTwo|| 
                _arrayCandles == null)
            {
                 return;
            }

            SetReadyLineAlert(_arrayCandles);

        }

        /// <summary>
        /// latest incoming data
        ///  последние входящие данные 
        /// </summary>
        private List<Candle> _arrayCandles;
        // waiting for incoming data
        // ожидание входящих данных

        /// <summary>
        /// waiting for click on chart to draw horizontal line
        /// ждём клика по чарту чтобы прорисовать горизонтальную линию
        /// </summary>
        private bool _waitHorisont;

        /// <summary>
        /// waiting for click on chart to draw horizontal line
        /// ждём клика по чарту чтобы прорисовать горизонтальную линию
        /// </summary>
        private bool _waitOne;

        /// <summary>
        /// waiting for click on chart to draw horizontal line
        /// ждём клика по чарту чтобы прорисовать горизонтальную линию
        /// </summary>
        private bool _waitTwo;
        // points
        //точки

        /// <summary>
        /// first point index
        /// индекс первой точки
        /// </summary>
        private int _candleOneNumber;

        /// <summary>
        /// time of first point
        /// время первой точки
        /// </summary>
        private DateTime _candleOneTime;

        /// <summary>
        /// first point value
        /// значение первой точки
        /// </summary>
        private decimal _candleOneValue;

        /// <summary>
        /// second point index
        /// индекс второй точки
        /// </summary>
        private int _candleTwoNumber;

        /// <summary>
        /// second point time
        /// время второй точки
        /// </summary>
        private DateTime _candleTwoTime;

        /// <summary>
        /// second point value
        /// значение второй точки
        /// </summary>
        private decimal _candleTwoValue;
        // creating alerts
        // создание алертов

        /// <summary>
        /// load new data point from chart
        /// загрузить с чарта новую точку с данными
        /// </summary>
        /// <param name="arrayCandles">array of candles/массив свечек</param>
        /// <param name="numberCandle">candle index/индекс свечи</param>
        /// <param name="valueY">igrick value/значение игрик</param>
        public void SetFormChart(List<Candle> arrayCandles, int numberCandle, decimal valueY)
        {
            _arrayCandles = arrayCandles;

            if (_waitOne == false && _waitTwo == false && _waitHorisont == false)
            {
                return;
            }

            if (arrayCandles == null ||
                numberCandle < 0 ||
                numberCandle > arrayCandles.Count ||
                valueY <= 0 ||
                arrayCandles.Count < 10)
            {
                return;
            }
            // find candle, which stuck
            // находим свечку, в которую ткнули

            Candle candle = arrayCandles[numberCandle];

            if (_waitOne)
            {
                if (candle.TimeStart == _candleTwoTime)
                {
                    MessageBox.Show("Линия не может быть построена из двух точек на одной оси Х");
                    return;
                }
                _candleOneTime = candle.TimeStart;
                _candleOneValue = valueY;
                _candleOneNumber = numberCandle;
                _waitOne = false;
                _waitTwo = true;
                return;
            }
            else if (_waitTwo)
            {
                if (candle.TimeStart == _candleOneTime)
                {
                    MessageBox.Show("Линия не может быть построена из двух точек на одной оси Х");
                    return;
                }

                _candleTwoTime = candle.TimeStart;
                _candleTwoValue = valueY;
                _candleTwoNumber = numberCandle;
                _waitTwo = false;
            }

            if (_waitHorisont)
            {
                _candleOneTime = candle.TimeStart;
                _candleOneValue = valueY;
                _candleOneNumber = numberCandle;

                if (numberCandle != 0)
                {
                    _candleTwoTime = arrayCandles[0].TimeStart;
                    _candleTwoValue = valueY;
                    _candleTwoNumber = 0;
                }
                else
                {
                    _candleTwoTime = arrayCandles[3].TimeStart;
                    _candleTwoValue = valueY;
                    _candleTwoNumber = 3;
                }
                _waitHorisont = false;
            }

            if (_candleOneTime != DateTime.MinValue && _candleTwoTime != DateTime.MinValue)
            {
                // swap points if they are not in correct order
                //меняем местами точки, если они установлены не в правильном порядке
                if (_candleOneNumber > _candleTwoNumber)
                {
                    int glassNumber = _candleTwoNumber;
                    DateTime glassTime = _candleTwoTime;
                    decimal glassPoint = _candleTwoValue;

                    _candleTwoNumber = _candleOneNumber;
                    _candleTwoTime = _candleOneTime;
                    _candleTwoValue = _candleOneValue;

                    _candleOneNumber = glassNumber;
                    _candleOneTime = glassTime;
                    _candleOneValue = glassPoint;
                }

                SetReadyLineAlert(arrayCandles);
            }
        }

        /// <summary>
        /// save alert
        /// сохранить Алерт
        /// </summary>
        private void SaveAlert() 
        {

            if (MyAlert == null)
            {
                return;
            }

            SetSettingsForomWindow();

            _keeper.Delete(MyAlert);

            _keeper.SetNewAlert(MyAlert);
        }

        /// <summary>
        /// upload to Alert new settings from form
        /// загрузить в Алерт новые настройки, с формы
        /// </summary>
        private void SetSettingsForomWindow()
        {
            try
            {
                TextBoxVolumeReaction.Text.ToDecimal();
            }
            catch (Exception)
            {
                MessageBox.Show(OsLocalization.Alerts.Message3);
            }

            if (MyAlert == null)
            {
                return;
            }

            
            MyAlert.IsMusicOn = CheckBoxMusicAlert.IsChecked.Value;
            MyAlert.IsOn = CheckBoxOnOff.IsChecked.Value;
            MyAlert.IsMessageOn = CheckBoxWindow.IsChecked.Value;

            MyAlert.Label = TextBoxLabelAlert.Text;
          //  MyAlert.ColorLabel = HostColorLabel.Child.BackColor;

           // MyAlert.ColorLine = HostColorLine.Child.BackColor;
            MyAlert.BorderWidth = Convert.ToInt32(ComboBoxFatLine.SelectedItem);

            MyAlert.Message = TextBoxAlertMessage.Text;

            Enum.TryParse(ComboBoxType.Text, true, out MyAlert.Type);

            Enum.TryParse(ComboBoxSignalType.Text, true, out MyAlert.SignalType);
            MyAlert.VolumeReaction = TextBoxVolumeReaction.Text.ToDecimal();

            MyAlert.Slippage = Convert.ToDecimal(TextBoxSlippage.Text);
            MyAlert.NumberClosePosition = Convert.ToInt32(TextBoxClosePosition.Text);
            Enum.TryParse(ComboBoxOrderType.Text, true, out MyAlert.OrderPriceType);

            Enum.TryParse(ComboBoxMusicType.Text,out MyAlert.Music);


            System.Windows.Media.Color  labelColor = ((SolidColorBrush)ButtonColorLabel.Background).Color;
            MyAlert.ColorLabel = System.Drawing.Color.FromArgb(labelColor.A, labelColor.R, labelColor.G, labelColor.B);

            System.Windows.Media.Color lineColor = ((SolidColorBrush)ButtonColorLine.Background).Color;
            MyAlert.ColorLine = System.Drawing.Color.FromArgb(lineColor.A, lineColor.R, lineColor.G, lineColor.B);


        }

        /// <summary>
        /// convert previously specified values to alert
        /// преобразуем указанные ранее значения в Алерт
        /// </summary>
        /// <param name="candles">массив свечек</param>
        private void SetReadyLineAlert(List<Candle> candles)
        {
            if (candles == null ||
                 (candles[candles.Count - 1].TimeStart - candles[0].TimeStart).TotalHours < 1)
            {
                return;
            }
            // create new alert
            // создаём новый алерт
            AlertToChart alert = new AlertToChart(_keeper.HostAllert);
            alert.Name = null;
            alert.Lines = GetAlertLines(candles);

            if (MyAlert != null)
            {
                _keeper.Delete(MyAlert);
            }

            MyAlert = alert;

            SaveAlert();
        }

        /// <summary>
        /// take Lines for Alert
        /// взять Линии для Алерта
        /// </summary>
        /// <param name="candles">array of candles/массив свечек</param>
        /// <returns>alert lines/линии алерта</returns>
        private ChartAlertLine[] GetAlertLines(List<Candle> candles)
        {
            if (ComboBoxType.Text == ChartAlertType.Line.ToString() ||
                ComboBoxType.Text == ChartAlertType.HorisontalLine.ToString())
            {
                decimal valueOne = _candleOneValue;
                decimal valueTwo = _candleTwoValue;
                int numberOne = _candleOneNumber;
                int numberTwo = _candleTwoNumber;

                ChartAlertLine[] lines = { AlertLineCreate(valueOne, valueTwo, numberOne, numberTwo, candles) };
                return lines;
            }

            if (ComboBoxType.Text == ChartAlertType.FibonacciSpeedLine.ToString())
            {
                return GetSpeedAlertLines(candles);
            }


            if (ComboBoxType.Text == ChartAlertType.FibonacciChannel.ToString())
            {
               return GetChanalLines(candles);
            }

            return null;
        }

        /// <summary>
        /// take Line for Alert type SpeedLine
        /// взять Линии для Алерта типа SpeedLine
        /// </summary>
        /// <param name="candles">candles/свечки</param>
        /// <returns>lines/линии</returns>
        private ChartAlertLine[] GetSpeedAlertLines(List<Candle> candles)
        {

            //1) Specify first point/Указываем первую точку
            //2) Specify second point/Указываем вторую точку
            //3) Rectangle built between points/Между точками строиться прямоугольник
            //4) Vertical catheter is overlaid with three dots. At same time, on ascending movement, countdown from bottom to top: proportion 0.382______ 0.487_______ 0.618. We draw three lines through them.
            //4) По вертикальному катету накладываем три точки. При этом на восходящем движении отсчет идет снизу вверх: пропорция 0.382______ 0.487_______ 0.618. Проводим через них три линии.

            decimal valueOneClick = _candleOneValue;

            decimal valueTwoClick;

            decimal devider = Convert.ToDecimal(Slider.Value - 100);

            valueTwoClick = _candleTwoValue + _candleTwoValue * Convert.ToDecimal(devider / 1000);
            // 1 if points are pressed on one straight line
            // 1 если нажаты точки на одной прямой
            if (valueTwoClick == valueOneClick)
            {
                decimal valueOne = _candleOneValue;
                decimal valueTwo = _candleTwoValue;
                int numberOne = _candleOneNumber;
                int numberTwo = _candleTwoNumber;

                ChartAlertLine[] lines = { AlertLineCreate(valueOne, valueTwo, numberOne, numberTwo, candles) };
                return lines;
            }
            // 2 find the height of cathetus
            // 2 находим высоту катета

            decimal highKatet;

            bool isUpSpeedLine;

            if (valueTwoClick > valueOneClick)
            {
                isUpSpeedLine = true;
                highKatet = valueTwoClick - valueOneClick;
            }
            else
            {
                isUpSpeedLine = false;
                highKatet = valueOneClick - valueTwoClick;
            }
            // declare points for three lines
            // объявляем точки для трёх линий

            decimal firstValueToAllLine = _candleOneValue;
            int firstNumberToAllLine = _candleOneNumber;

            int secondNumberToAllLine = _candleTwoNumber;

            decimal oneLineValue;
            decimal twoLineValue;
            decimal threeLineValue;

            if (isUpSpeedLine)
            {
                oneLineValue = valueOneClick + highKatet * 0.382m;
                twoLineValue = valueOneClick + highKatet * 0.487m;
                threeLineValue = valueOneClick + highKatet * 0.618m;
            }
            else
            {
                oneLineValue = valueOneClick - highKatet * 0.382m;
                twoLineValue = valueOneClick - highKatet * 0.487m;
                threeLineValue = valueOneClick - highKatet * 0.618m;
            }

            ChartAlertLine oneLine = AlertLineCreate(firstValueToAllLine, oneLineValue, firstNumberToAllLine,
                secondNumberToAllLine, candles);

            ChartAlertLine twoLine = AlertLineCreate(firstValueToAllLine, twoLineValue, firstNumberToAllLine,
            secondNumberToAllLine, candles);

            ChartAlertLine treeLine = AlertLineCreate(firstValueToAllLine, threeLineValue, firstNumberToAllLine,
            secondNumberToAllLine, candles);

            ChartAlertLine[] alertLines = {oneLine, twoLine, treeLine};

            return alertLines;

        }

        /// <summary>
        /// take alert's lines, like chanal
        /// взять Линии для Алерта типа Chanal
        /// </summary>
        /// <param name="candles">candles/свечи</param>
        /// <returns>lines/линии</returns>
        private ChartAlertLine[] GetChanalLines(List<Candle> candles)
        {
            if (Slider.Value == 100)
            {
                decimal valueOne = _candleOneValue;
                decimal valueTwo = _candleTwoValue;
                int numberOne = _candleOneNumber;
                int numberTwo = _candleTwoNumber;

                ChartAlertLine[] lines = { AlertLineCreate(valueOne, valueTwo, numberOne, numberTwo, candles) };
                return lines;
            }
            // 1 we take points.
            // 1 берём точки

            decimal onePoint = _candleOneValue;
            int oneNumber = _candleOneNumber; //first point of line // первая точка линии

            decimal twoPoint = _candleTwoValue;
            int twoPNumber = _candleTwoNumber; //second point of line // вторая точка линии

            decimal l023value1;
            decimal l023value2;

            decimal l038value1;
            decimal l038value2;

            decimal l050value1;
            decimal l050value2;

            decimal l061value1;
            decimal l061value2;

            decimal l076value1;
            decimal l076value2;

            decimal l0100value1;
            decimal l0100value2;

            decimal l0161value1;
            decimal l0161value2;

            decimal l0261value1;
            decimal l0261value2;

            decimal l0423value1;
            decimal l0423value2;

            decimal sliderValue = Convert.ToDecimal(Slider.Value);
            decimal devider;

            if (Slider.Value > 100)
            {
                devider = (sliderValue - 100) / 1000; // 100 / 1000 = 0,1

                l023value1 = onePoint + devider * onePoint * 0.23m;
                l023value2 = twoPoint + devider * twoPoint * 0.23m;

                l038value1 = onePoint + devider * onePoint * 0.38m;
                l038value2 = twoPoint + devider * twoPoint * 0.38m;

                l050value1 = onePoint + devider * onePoint * 0.50m;
                l050value2 = twoPoint + devider * twoPoint * 0.50m;

                l061value1 = onePoint + devider * onePoint * 0.61m;
                l061value2 = twoPoint + devider * twoPoint * 0.61m;

                l076value1 = onePoint + devider * onePoint * 0.76m;
                l076value2 = twoPoint + devider * twoPoint * 0.76m;

                l0100value1 = onePoint + devider * onePoint * 1m;
                l0100value2 = twoPoint + devider * twoPoint * 1m;

                l0161value1 = onePoint + devider * onePoint * 1.61m;
                l0161value2 = twoPoint + devider * twoPoint * 1.61m;

                l0261value1 = onePoint + devider * onePoint * 2.61m;
                l0261value2 = twoPoint + devider * twoPoint * 2.61m;

                l0423value1 = onePoint + devider * onePoint * 4.23m;
                l0423value2 = twoPoint + devider * twoPoint * 4.423m;
            }
            else
            {
                devider = (100 - sliderValue) / 1000; // 100 / 1000 = 0,1

                l023value1 = onePoint - devider * onePoint * 0.23m;
                l023value2 = twoPoint - devider * twoPoint * 0.23m;

                l038value1 = onePoint - devider * onePoint * 0.38m;
                l038value2 = twoPoint - devider * twoPoint * 0.38m;

                l050value1 = onePoint - devider * onePoint * 0.50m;
                l050value2 = twoPoint - devider * twoPoint * 0.50m;

                l061value1 = onePoint - devider * onePoint * 0.61m;
                l061value2 = twoPoint - devider * twoPoint * 0.61m;

                l076value1 = onePoint - devider * onePoint * 0.76m;
                l076value2 = twoPoint - devider * twoPoint * 0.76m;

                l0100value1 = onePoint - devider * onePoint * 1m;
                l0100value2 = twoPoint - devider * twoPoint * 1m;

                l0161value1 = onePoint - devider * onePoint * 1.61m;
                l0161value2 = twoPoint - devider * twoPoint * 1.61m;

                l0261value1 = onePoint - devider * onePoint * 2.61m;
                l0261value2 = twoPoint - devider * twoPoint * 2.61m;

                l0423value1 = onePoint - devider * onePoint * 4.23m;
                l0423value2 = twoPoint - devider * twoPoint * 4.23m;
            }

            // 2 alerts


            ChartAlertLine oneLine = AlertLineCreate(onePoint, twoPoint, oneNumber, twoPNumber, candles);

            ChartAlertLine l023Line = AlertLineCreate(l023value1, l023value2, oneNumber, twoPNumber, candles);

            ChartAlertLine l038Line = AlertLineCreate(l038value1, l038value2, oneNumber, twoPNumber, candles);

            ChartAlertLine l050Line = AlertLineCreate(l050value1, l050value2, oneNumber, twoPNumber, candles);
            ChartAlertLine l061Line = AlertLineCreate(l061value1, l061value2, oneNumber, twoPNumber, candles);
            ChartAlertLine l076Line = AlertLineCreate(l076value1, l076value2, oneNumber, twoPNumber, candles);
            ChartAlertLine l100Line = AlertLineCreate(l0100value1, l0100value2, oneNumber, twoPNumber, candles);
            ChartAlertLine l161Line = AlertLineCreate(l0161value1, l0161value2, oneNumber, twoPNumber, candles);
            ChartAlertLine l261Line = AlertLineCreate(l0261value1, l0261value2, oneNumber, twoPNumber, candles);
            ChartAlertLine l423Line = AlertLineCreate(l0423value1, l0423value2, oneNumber, twoPNumber, candles);

            ChartAlertLine[] alertLines = new ChartAlertLine[10];

            alertLines[0] =  oneLine;
            alertLines[1] = l023Line;
            alertLines[2] = l038Line;
            alertLines[3] = l050Line;
            alertLines[4] = l061Line;
            alertLines[5] = l076Line;
            alertLines[6] = l100Line;
            alertLines[7] = l161Line;
            alertLines[8] = l261Line;
            alertLines[9] = l423Line;

            return alertLines;

        }

        /// <summary>
        /// Create line of points
        /// Создать линию из точек
        /// </summary>
        /// <param name="valueOne">first point value/значение первой точки</param>
        /// <param name="valueTwo">second point value/значение второй точки</param>
        /// <param name="numberOne">first point number/номер первой точки</param>
        /// <param name="numberTwo">second point number/номер второй точки</param>
        /// <param name="candles">candles/свечи</param>
        /// <returns>alerts/алерт</returns>
        private ChartAlertLine AlertLineCreate(decimal valueOne, decimal valueTwo, int numberOne, int numberTwo, List<Candle> candles)
        {
            // 2 calculate indicator movement per candlestick on this TF
            // 2 рассчитываем движение индикатора за свечу на данном ТФ

            decimal stepCorner; // how long our line goes by candle //сколько наша линия проходит за свечку

            stepCorner = (valueTwo - valueOne) / (numberTwo - numberOne + 1);
            // 3 now build an array of line values parallel to candlestick array
            // 3 теперь строим массив значений линии параллельный свечному массиву

            decimal[] lineDecimals = new decimal[candles.Count];
            decimal point = valueOne;
            lineDecimals[numberOne] = point;

            for (int i = numberOne + 1; i < lineDecimals.Length; i++)
            {
                // running ahead of array.
                // бежим вперёд по массиву
                lineDecimals[i] = point;
                point += stepCorner;
            }

            point = valueOne;
            for (int i = numberOne - 1; i > -1; i--)
            {
                // running backwards through array.
                // бежим назад по массиву
                lineDecimals[i] = point;
                point -= stepCorner;
            }
            // 4 find nearest one from the beginning of hour and the next one after it.
            // 4 находим ближайший от начала час  и следующий за ним

            int firstHourCandle = -1;

            int secondHourCandle = -1;

            int nowHour = candles[candles.Count - 1].TimeStart.Hour;

            for (int i = candles.Count - 1; i > -1; i--)
            {
                if (nowHour != candles[i].TimeStart.Hour)
                {
                    nowHour = candles[i].TimeStart.Hour;

                    if (firstHourCandle == -1)
                    {
                        firstHourCandle = i + 1;
                    }
                    else
                    {
                        secondHourCandle = i + 1;
                        break;
                    }
                }
            }
            // 6 calculate real position of alert. In hours.
            // 6 рассчитываем реальное положение алерта. В часах.

            if (secondHourCandle <0 ||
                firstHourCandle <0)
            {
                return null;
            }

            DateTime startTime = candles[secondHourCandle].TimeStart;
            DateTime endTime = candles[firstHourCandle].TimeStart;

            decimal startPoint = lineDecimals[secondHourCandle];
            decimal endPoint = lineDecimals[firstHourCandle];
            // 5 Then calculate line value of first and second hour
            // 5 далее рассчитываем значение линии на первом и втором часе

            ChartAlertLine line = new ChartAlertLine();
            line.TimeFirstPoint = startTime;
            line.ValueFirstPoint = startPoint;

            line.TimeSecondPoint = endTime;
            line.ValueSecondPoint = endPoint;

            return line;
        }

        /// <summary>
        /// save button
        /// кнопка сохранить
        /// </summary>
        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            SaveAlert();
            NeadToSave = true;
            Close();
        }

        /// <summary>
        /// signature color button
        /// кнопка цвет подписи
        /// </summary>
        private void ButtonColorLabel_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog ui = new ColorDialog();

            System.Windows.Media.Color labelColor = ((SolidColorBrush)ButtonColorLabel.Background).Color;
            ui.Color = System.Drawing.Color.FromArgb(labelColor.A, labelColor.R, labelColor.G, labelColor.B);

            ui.ShowDialog();

            System.Drawing.Color newColor = ui.Color;
            ButtonColorLabel.Background =
                new SolidColorBrush(Color.FromArgb(newColor.A, newColor.R, newColor.G, newColor.B));

            SaveAlert();
        }

        /// <summary>
        /// line color button
        /// кнопка цвет линии
        /// </summary>
        private void ButtonColorLine_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog ui = new ColorDialog();

            System.Windows.Media.Color lineColor = ((SolidColorBrush)ButtonColorLine.Background).Color;
            ui.Color = System.Drawing.Color.FromArgb(lineColor.A, lineColor.R, lineColor.G, lineColor.B);
            ui.ShowDialog();

            System.Drawing.Color newColor = ui.Color;
            ButtonColorLine.Background =
                new SolidColorBrush(Color.FromArgb(newColor.A, newColor.R, newColor.G, newColor.B));

            SaveAlert();
        }

        /// <summary>
        /// button to specify a slant line
        /// кнопка указать наклонную линию
        /// </summary>
        private void ButtonSendFirst_Click(object sender, RoutedEventArgs e)
        {
            ChartAlertType type;

            Enum.TryParse(ComboBoxType.Text, out type);

            if (type == ChartAlertType.FibonacciChannel)
            {
                if (_waitOne == false)
                {
                    _waitOne = true;
                    _waitTwo = false;
                }
                else if (_waitOne)
                {
                    _waitOne = false;
                    _waitTwo = false;
                }
                Slider.Value = 100;
            }
            else if (type == ChartAlertType.Line)
            {
                if (_waitOne == false)
                {
                    _waitOne = true;
                    _waitTwo = false;
                }
                else if (_waitOne)
                {
                    _waitOne = false;
                    _waitTwo = false;
                }
            }
            else if (type == ChartAlertType.FibonacciSpeedLine)
            {
                if (_waitOne == false)
                {
                    _waitOne = true;
                    _waitTwo = false;
                }
                else if (_waitOne)
                {
                    _waitOne = false;
                    _waitTwo = false;
                }
                Slider.Value = 100;
            }
            else if (type == ChartAlertType.HorisontalLine)
            {
                _waitHorisont = true;
                _waitOne = false;
                _waitTwo = false;
            }

          
        }

    }
}
