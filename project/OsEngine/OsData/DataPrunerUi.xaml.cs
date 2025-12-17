/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using System;
using System.Collections.Generic;
using System.Windows;


namespace OsEngine.OsData
{
    public partial class DataPrunerUi : Window
    {
        private OsDataSet _set;

        private OsDataSetPainter _setPainter;

        public DataPrunerUi(OsDataSet set, OsDataSetPainter setPainter)
        {
             InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _set = set;
            _setPainter = setPainter;

            Title = OsLocalization.Data.Label78;
            ButtonDelete1.Content = OsLocalization.Data.Label41;
            ButtonDelete2.Content = OsLocalization.Data.Label41;
            ButtonDelete3.Content = OsLocalization.Data.Label41;
            ButtonDelete4.Content = OsLocalization.Data.Label41;
            LabelTf.Content = OsLocalization.Data.Label37;
            LabelDelByTime.Content = OsLocalization.Data.Label62;
            LabelStartTime.Content = OsLocalization.Data.Label63;
            LabelEndTime.Content = OsLocalization.Data.Label64;
            LabelObjNums.Content = OsLocalization.Data.Label65;
            LabelObjsNum.Content = OsLocalization.Data.Label66;
            LabelDelByVol.Content = OsLocalization.Data.Label67;
            LabelMinVol.Content = OsLocalization.Data.Label69;
            LabelMaxVol.Content = OsLocalization.Data.Label68;
            LabelDelByFract.Content = OsLocalization.Data.Label70;
            LabelMaxDigit.Content = OsLocalization.Data.Label71;
            LabelMinDigit.Content = OsLocalization.Data.Label72;
            DatePickerTimeStart.SelectedDate = _set.BaseSettings.TimeStart;
            DatePickerTimeEnd.SelectedDate = _set.BaseSettings.TimeEnd;

            GetTfFromSet();

            Activate();
            Focus();

            Closed += DataPrunerUi_Closed;
        }

        private void DataPrunerUi_Closed(object sender, EventArgs e)
        {
            try
            {
                _set = null;
                _setPainter = null;
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonDelete1_Click(object sender, RoutedEventArgs e) // по времени
        {
            try
            {
                if (DatePickerTimeStart.SelectedDate == null || DatePickerTimeEnd.SelectedDate == null)
                {
                    ServerMaster.Log?.ProcessMessage(OsLocalization.Data.Label74, Logging.LogMessageType.Error);
                     return;
                }

                if (DatePickerTimeStart.SelectedDate > DatePickerTimeEnd.SelectedDate)
                {
                    ServerMaster.Log?.ProcessMessage(OsLocalization.Data.Label75, Logging.LogMessageType.Error);
                    return;
                }

                DateTime? start = DatePickerTimeStart.SelectedDate;

                DateTime? end = DatePickerTimeEnd.SelectedDate;

                List<SecurityToLoad> wrongSecurities = [];

                for (int i = 0; i < _set.SecuritiesLoad.Count; i++)
                {
                    for (int j = 0; j < _set.SecuritiesLoad[i].SecLoaders.Count; j++)
                    {
                        SecurityTfLoader tf = _set.SecuritiesLoad[i].SecLoaders[j];

                        if (tf.TimeStartInReal.Date > start || tf.TimeEndInReal.Date < end)
                        {
                            wrongSecurities.Add(_set.SecuritiesLoad[i]);
                            break;
                        }
                    }
                }

                if (wrongSecurities.Count > 0)
                    DeleteWrongSecurities(wrongSecurities);

            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonDelete2_Click(object sender, RoutedEventArgs e) // по объектам
        {
            try
            {
                if (!Int32.TryParse(TextBoxObjs.Text, out int needObjects))
                {
                    ServerMaster.Log?.ProcessMessage(OsLocalization.Data.Label74, Logging.LogMessageType.Error);
                    return;
                }

                List<SecurityToLoad> wrongSecurities = [];

                for (int i = 0; i < _set.SecuritiesLoad.Count; i++)
                {
                    for (int j = 0; j < _set.SecuritiesLoad[i].SecLoaders.Count; j++)
                    {
                        SecurityTfLoader tf = _set.SecuritiesLoad[i].SecLoaders[j];

                        if (tf.TimeFrame.ToString() == ComboBoxTf.Text && tf.Objects() < needObjects)
                        {
                            wrongSecurities.Add(_set.SecuritiesLoad[i]);
                            break;
                        }
                    }
                }

                if (wrongSecurities.Count > 0)
                    DeleteWrongSecurities(wrongSecurities);

            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void GetTfFromSet()
        {
            List<string> timeFrames = [];

            for (int j = 0; j < _set.SecuritiesLoad[0].SecLoaders.Count; j++)
            {
                timeFrames.Add(_set.SecuritiesLoad[0].SecLoaders[j].TimeFrame.ToString());
            }

            if (timeFrames.Count > 0)
            {
                ComboBoxTf.ItemsSource = timeFrames;
                ComboBoxTf.SelectedItem = timeFrames[0];
            }
        }

        private void ButtonDelete3_Click(object sender, RoutedEventArgs e) // по волатильности
        {
            try
            {
                if (string.IsNullOrEmpty( TextBoxMaxVol.Text) || string.IsNullOrEmpty(TextBoxMinVol.Text))
                {
                    ServerMaster.Log?.ProcessMessage(OsLocalization.Data.Label77, Logging.LogMessageType.Error);
                    return;
                }

                decimal maxVol = TextBoxMaxVol.Text.ToDecimal();
                decimal minVol = TextBoxMinVol.Text.ToDecimal();
                

                List<SecurityToLoad> wrongSecurities = [];

                for (int i = 0; i < _set.SecuritiesLoad.Count; i++)
                {
                    for (int j = 0; j < _set.SecuritiesLoad[i].SecLoaders.Count; j++)
                    {
                        SecurityTfLoader tf = _set.SecuritiesLoad[i].SecLoaders[j];

                        List<Candle> candles = GetDailyCandlesForMonth(tf);

                        if (candles == null || candles.Count == 0)
                            continue;

                        decimal averVolatility = CalculateAverDayVolatility(candles);

                        if (averVolatility > maxVol || averVolatility < minVol)
                        {
                            wrongSecurities.Add(_set.SecuritiesLoad[i]);
                            break;
                        }
                    }
                }

                if (wrongSecurities.Count > 0)
                    DeleteWrongSecurities(wrongSecurities);

            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private List<Candle> GetDailyCandlesForMonth(SecurityTfLoader loader)
        {
            List<Candle> candles = null;

            if (loader.TimeFrame == TimeFrame.Tick)
            {
                candles = loader.GetExtCandlesFromTrades();
            }
            else
            {
                candles = loader.GetCandlesAllHistory();
            }

            if (candles == null ||
                candles.Count == 0)
            {
                return null;
            }

            DateTime lastDay = candles[^1].TimeStart.Date;

            DateTime oneMonthBack = candles[^1].TimeStart.Date.AddMonths(-1);

            List<Candle> dailyCandles = [];

            while (lastDay >= candles[0].TimeStart.Date && lastDay > oneMonthBack)
            {
                List<Candle> oneDayCandles = candles.FindAll(p => p.TimeStart.Date == lastDay);

                if (oneDayCandles.Count == 0)
                {
                    lastDay = lastDay.AddDays(-1);
                    continue;
                }
    
                decimal high = decimal.MinValue;
                decimal low = decimal.MaxValue;

                for (int i = 0; i < oneDayCandles.Count; i++)
                {
                    if (oneDayCandles[i].High > high)
                    {
                        high = oneDayCandles[i].High;
                    }
                    if (oneDayCandles[i].Low < low)
                    {
                        low = oneDayCandles[i].Low;
                    }
                }

                decimal open = oneDayCandles[0].Open;
                decimal close = oneDayCandles[^1].Close;

                Candle dailyCandle = new();

                dailyCandle.High = high;
                dailyCandle.Low = low;
                dailyCandle.Open = open;
                dailyCandle.Close = close;

                dailyCandles.Add(dailyCandle);

                lastDay = lastDay.AddDays(-1);
            }

            return dailyCandles;
        }

        public decimal CalculateAverDayVolatility(List<Candle> candles)
        {
            decimal volSum = 0;

            for (int i = 0; i < candles.Count; i++)
            {
                decimal move = candles[i].High - candles[i].Low;

                decimal movePercent = move / (candles[i].Low / 100);

                volSum += movePercent;
            }

            return volSum / candles.Count;
        }

        private void ButtonDelete4_Click(object sender, RoutedEventArgs e) // по значимой части
        {
            try
            {
                if (!Int32.TryParse(TextBoxMaxDigits.Text, out int maxDigit) || !Int32.TryParse(TextBoxMinDigits.Text, out int minDigit))
                {
                    ServerMaster.Log?.ProcessMessage(OsLocalization.Data.Label74, Logging.LogMessageType.Error);
                    return;
                }

                List<SecurityToLoad> wrongSecurities = [];

                for (int i = 0; i < _set.SecuritiesLoad.Count; i++)
                {
                    for (int j = 0; j < _set.SecuritiesLoad[i].SecLoaders.Count; j++)
                    {
                        SecurityTfLoader tf = _set.SecuritiesLoad[i].SecLoaders[j];

                        List<Candle> candles = GetDailyCandlesForMonth(tf);

                        if (candles == null || candles.Count == 0)
                            continue;

                        int digits = GetSignificantDigitsCount(candles);

                        if (digits > maxDigit || digits < minDigit)
                        {
                            wrongSecurities.Add(_set.SecuritiesLoad[i]);
                            break;
                        }
                    }
                }

                if (wrongSecurities.Count > 0)
                    DeleteWrongSecurities(wrongSecurities);
   
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private int GetSignificantDigitsCount(List<Candle> candles)
        {
            List<int> digits = new List<int>();

            for (int i = 0; i < candles.Count; i++)
            {
                if (candles[i].Close == 0)
                {
                    digits.Add(1);
                    continue;
                }

                decimal absPrice = Math.Abs(candles[i].Close);

                int significantDigits = 0;

                decimal temp = absPrice;

                while (temp != Math.Floor(temp))
                {
                    temp *= 10;
                }

                long integerValue = (long)temp;

                while (integerValue % 10 == 0 && integerValue != 0)
                {
                    integerValue /= 10;
                }

                significantDigits = CountDigits(integerValue);

                digits.Add(significantDigits);
            }

            int maxDigits = int.MinValue;

            for (int j = 0; j < digits.Count; j++)
            {
                if (digits[j] > maxDigits)
                    maxDigits = digits[j];
            }

            return maxDigits;
        }

        private int CountDigits(long number)
        {
            if (number == 0) return 1;

            int count = 0;

            while (number > 0)
            {
                count++;
                number /= 10;
            }

            return count;
        }

        private void DeleteWrongSecurities(List<SecurityToLoad> wrongSecurities)
        {
            string secList = string.Join(", ", wrongSecurities.ConvertAll(security => security.SecName));

            string attentionMsg = $"{OsLocalization.Data.Label76.Split('.')[0]}:\n{secList}\n{OsLocalization.Data.Label76.Split('.')[1]}";

            AcceptDialogUi ui = new AcceptDialogUi(attentionMsg);
            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return;
            }

            for (int i = 0; i < wrongSecurities.Count; i++)
            {
                int index = _set.SecuritiesLoad.IndexOf(wrongSecurities[i]);

                if (index != -1)
                {
                    _set.SecuritiesLoad[index].Delete();
                    _set.SecuritiesLoad.RemoveAt(index);
                }
            }

            _set.Save();
            _setPainter.RePaintInterface();

            Close();
        }
    }
}
