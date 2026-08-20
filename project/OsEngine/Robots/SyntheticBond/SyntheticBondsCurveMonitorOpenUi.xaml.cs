using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace OsEngine.Robots.SyntheticBond
{
    public partial class SyntheticBondsCurveMonitorOpenUi : Window
    {
        private SyntheticBondsCurveMonitor _robot;
        private DispatcherTimer _timer;

        public SyntheticBondsCurveMonitorOpenUi(SyntheticBondsCurveMonitor robot, string baseName)
        {
            InitializeComponent();

            _robot = robot;

            Title = OsLocalization.ConvertToLocString("Eng:Open synthetic bond_Ru:Открытие синтетической облигации_");
            LabelBase.Content = OsLocalization.ConvertToLocString("Eng:Base_Ru:База_");
            LabelVolume.Content = OsLocalization.ConvertToLocString("Eng:Volume_Ru:Объём_");
            ButtonOpen.Content = OsLocalization.ConvertToLocString("Eng:Open. Long base + short fut_Ru:Открыть. Лонг база + шорт фьюч_");
            ButtonCancel.Content = OsLocalization.ConvertToLocString("Eng:Cancel_Ru:Отмена_");

            ComboBoxVolumeType.Items.Add("Deposit percent");
            ComboBoxVolumeType.Items.Add("Contract currency");
            ComboBoxVolumeType.SelectedItem = _robot.GetVolumeTypeDefault();
            TextBoxVolume.Text = _robot.GetVolumeValueDefault().ToString();

            List<string> bondNames = _robot.GetConfiguredBondNames();

            for (int i = 0; i < bondNames.Count; i++)
            {
                ComboBoxBase.Items.Add(bondNames[i]);
            }

            ComboBoxBase.SelectedItem = baseName;

            if (ComboBoxBase.SelectedItem == null
                && ComboBoxBase.Items.Count > 0)
            {
                ComboBoxBase.SelectedIndex = 0;
            }

            ComboBoxBase.SelectionChanged += ComboBoxBase_SelectionChanged;
            ButtonOpen.Click += ButtonOpen_Click;
            ButtonCancel.Click += ButtonCancel_Click;
            ButtonInfo.Click += ButtonInfo_Click;
            Closing += SyntheticBondsCurveMonitorOpenUi_Closing;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += _timer_Tick;
            _timer.Start();

            StartupLocation.Start_MouseInCorner(this);

            RefreshAll();
        }

        private void SyntheticBondsCurveMonitorOpenUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _timer.Stop();
                _timer.Tick -= _timer_Tick;

                ComboBoxBase.SelectionChanged -= ComboBoxBase_SelectionChanged;
                ButtonOpen.Click -= ButtonOpen_Click;
                ButtonCancel.Click -= ButtonCancel_Click;
                ButtonInfo.Click -= ButtonInfo_Click;
                Closing -= SyntheticBondsCurveMonitorOpenUi_Closing;

                _robot = null;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ComboBoxBase_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                RefreshAll();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _timer_Tick(object sender, EventArgs e)
        {
            try
            {
                RefreshAll();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private int GetSelectedSeriesIndex()
        {
            if (RadioButtonSeries1.IsChecked == true) return 0;
            if (RadioButtonSeries2.IsChecked == true) return 1;
            if (RadioButtonSeries3.IsChecked == true) return 2;

            return 0;
        }

        private void RefreshAll()
        {
            string baseName = ComboBoxBase.SelectedItem as string;

            if (string.IsNullOrEmpty(baseName))
            {
                return;
            }

            List<SeriesInfo> series = _robot.GetSeriesQuotes(baseName);

            UpdateSeriesRadio(RadioButtonSeries1, series, 0);
            UpdateSeriesRadio(RadioButtonSeries2, series, 1);
            UpdateSeriesRadio(RadioButtonSeries3, series, 2);

            int seriesIndex = GetSelectedSeriesIndex();

            if (series.Count <= seriesIndex)
            {
                LabelFutBid.Content = "-";
                LabelBaseAsk.Content = "-";
                LabelContango.Content = "-";
                LabelYield.Content = "-";
                LabelExpiration.Content = "-";
                return;
            }

            decimal mult = _robot.GetMultByBondName(baseName);
            (decimal futBid, decimal baseAsk) = _robot.GetPairPrices(baseName, seriesIndex);

            decimal contangoPercent = 0;
            decimal yieldPercent = 0;

            if (baseAsk != 0)
            {
                contangoPercent = (futBid / mult - baseAsk) / (baseAsk / 100);
            }

            int days = series[seriesIndex].DaysToExpiration;

            if (days > 0)
            {
                yieldPercent = contangoPercent * 365 / days;
            }

            LabelFutBid.Content = OsLocalization.ConvertToLocString("Eng:Fut bid _Ru:Фьюч бид _") + futBid;
            LabelBaseAsk.Content = OsLocalization.ConvertToLocString("Eng:Base ask _Ru:База аск _") + baseAsk;
            LabelContango.Content = OsLocalization.ConvertToLocString("Eng:Contango _Ru:Контанго _") + Math.Round(contangoPercent, 3) + "%";
            LabelYield.Content = OsLocalization.ConvertToLocString("Eng:Yield _Ru:Доходность _")
                + Math.Round(yieldPercent, 1) + OsLocalization.ConvertToLocString("Eng:% ann_Ru:% год._");
            LabelExpiration.Content = OsLocalization.ConvertToLocString("Eng:Expiration _Ru:Экспирация _")
                + series[seriesIndex].Expiration.ToString("dd.MM.yyyy")
                + " (" + days + OsLocalization.ConvertToLocString("Eng: d_Ru: дн._") + ")";
        }

        private void UpdateSeriesRadio(System.Windows.Controls.RadioButton radio, List<SeriesInfo> series, int index)
        {
            if (series.Count > index)
            {
                radio.IsEnabled = true;
                radio.Content = series[index].Name
                    + "   " + Math.Round(series[index].YieldPercent, 1) + OsLocalization.ConvertToLocString("Eng:% ann_Ru:% год._")
                    + "   " + OsLocalization.ConvertToLocString("Eng:exp. _Ru:эксп. _")
                    + series[index].Expiration.ToString("dd.MM.yy")
                    + " (" + series[index].DaysToExpiration + OsLocalization.ConvertToLocString("Eng: d_Ru: дн._") + ")";
            }
            else
            {
                radio.IsEnabled = false;
                radio.IsChecked = false;
                radio.Content = "-";

                if (GetSelectedSeriesIndex() == index)
                {
                    RadioButtonSeries1.IsChecked = true;
                }
            }
        }

        private void ButtonOpen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseName = ComboBoxBase.SelectedItem as string;

                if (string.IsNullOrEmpty(baseName))
                {
                    return;
                }

                decimal volume = TextBoxVolume.Text.ToDecimal();

                if (volume <= 0)
                {
                    return;
                }

                string volumeType = ComboBoxVolumeType.SelectedItem as string;
                int seriesIndex = GetSelectedSeriesIndex();

                (decimal futVolume, decimal baseVolume, decimal baseDisplayLots) = _robot.GetPairVolumes(baseName, seriesIndex, volumeType, volume);

                if (futVolume <= 0
                    || baseVolume <= 0)
                {
                    CustomMessageBoxUi uiError = new CustomMessageBoxUi(OsLocalization.ConvertToLocString(
                        "Eng:Can not calculate volumes. Not enough money for one futures contract or no quotes_" +
                        "Ru:Не удалось рассчитать объёмы. Не хватает денег на один фьючерсный контракт или нет котировок_"));
                    uiError.ShowDialog();
                    return;
                }

                List<SeriesInfo> series = _robot.GetSeriesQuotes(baseName);
                string futName = series[seriesIndex].Name;

                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.ConvertToLocString(
                    "Eng:Opening synthetic bond_Ru:Открываем синтетическую облигацию_") + "\n\n"
                    + OsLocalization.ConvertToLocString("Eng:LONG_Ru:ЛОНГ_") + " " + baseName + "  -  " + baseDisplayLots + "\n"
                    + OsLocalization.ConvertToLocString("Eng:SHORT_Ru:ШОРТ_") + " " + futName + "  -  " + futVolume + "\n\n"
                    + OsLocalization.ConvertToLocString("Eng:Continue_Ru:Продолжить_") + "?");

                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                _robot.OpenPairManually(baseName, seriesIndex, volumeType, volume);

                Close();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.ConvertToLocString(
                    "Eng:Synthetic bond opening window (cash-and-carry arbitrage).\n\n" +
                    "What happens on open. The stock (base) is bought and the futures on it is sold at the same time. The position is delta-neutral, profit is locked as contango (futures premium over the stock).\n\n" +
                    "Window elements.\n" +
                    "- Base - the stock from the monitor settings.\n" +
                    "- Futures series - nearest, next or third one. Annualized yield and expiration date are shown nearby.\n" +
                    "- Fut bid / Base ask - current leg prices.\n" +
                    "- Contango - futures to stock spread in %.\n" +
                    "- Yield - contango annualized to expiration.\n" +
                    "- Volume - set for the futures leg. Deposit percent (% of portfolio) or Contract currency (money amount). Contracts are rounded down to whole. The stock leg is placed in lots, contracts x mult / stock lot (in the tester lot = 1, so in shares).\n\n" +
                    "Minimum money is 1 contract x (base price x mult) + futures margin. If there is not enough money for one contract, the position will not open (error in the log).\n\n" +
                    "Risk. Variation margin on the futures short requires free money if the market goes up_" +
                    "Ru:Окно открытия синтетической облигации (кэш-энд-керри арбитраж).\n\n" +
                    "Что происходит при открытии. Покупается акция (база) и одновременно продаётся фьючерс на неё же. Позиция дельта-нейтральна, доход фиксируется в виде контанго (премии фьючерса над акцией).\n\n" +
                    "Элементы окна.\n" +
                    "- База - акция из настроек монитора.\n" +
                    "- Серия фьючерса - ближайшая, следующая или третья. Рядом показаны доходность в % годовых и дата экспирации.\n" +
                    "- Фьюч бид / База аск - текущие цены ног.\n" +
                    "- Контанго - спред фьючерса к акции в %.\n" +
                    "- Доходность - контанго в % годовых до экспирации.\n" +
                    "- Объём - задаётся для фьючерсной ноги. Deposit percent (% от портфеля) или Contract currency (сумма в рублях). Контракты округляются до целых. Нога акций выставляется в лотах, контракты x мульт / лот акции (в тестере лот = 1, то есть в штуках).\n\n" +
                    "Минимум денег это 1 контракт x (цена базы x мульт) + ГО фьючерса. Если денег не хватает на один контракт - позиция не откроется (ошибка в логе).\n\n" +
                    "Риск. Вариационная маржа по шорту фьючерса требует свободных денег при росте рынка_"));
                ui.ShowDialog();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Close();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
    }
}
