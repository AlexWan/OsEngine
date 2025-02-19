/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using OsEngine.Entity;
using OsEngine.Properties;
using System.Windows.Forms;

namespace OsEngine.Alerts
{
    public class AlertToPrice: IIAlert
    {
        public AlertToPrice(string name)
        {
            TypeAlert = AlertType.PriceAlert;
            SignalType = SignalType.None;
            MusicType = AlertMusic.Duck;

            Name = name;
            Load();
        }

        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @"Alert.txt", false))
                {
                    writer.WriteLine(Message);
                    writer.WriteLine(IsOn);
                    writer.WriteLine(MessageIsOn);
                    writer.WriteLine(MusicType);

                    writer.WriteLine(SignalType);
                    writer.WriteLine(VolumeReaction);
                    writer.WriteLine(Slippage);
                    writer.WriteLine(NumberClosePosition);
                    writer.WriteLine(OrderPriceType);

                    writer.WriteLine(TypeActivation);
                    writer.WriteLine(PriceActivation);
                    writer.WriteLine(SlippageType);
                    writer.Close();
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }

        public void Load()
        {
            if (!File.Exists(@"Engine\" + Name + @"Alert.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + Name + @"Alert.txt"))
                {
                    Message = reader.ReadLine();
                    IsOn = Convert.ToBoolean(reader.ReadLine());
                    MessageIsOn = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out MusicType);

                    Enum.TryParse(reader.ReadLine(), true, out SignalType);
                    VolumeReaction = reader.ReadLine().ToDecimal();
                    Slippage = reader.ReadLine().ToDecimal();
                    NumberClosePosition = Convert.ToInt32(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), true, out OrderPriceType);

                    Enum.TryParse(reader.ReadLine(), true, out TypeActivation);
                    PriceActivation = reader.ReadLine().ToDecimal();
                    Enum.TryParse(reader.ReadLine(), true, out SlippageType);
                    
                    reader.Close();
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }

        public void Delete()
        {
            if (File.Exists(@"Engine\" + Name + @"Alert.txt"))
            {
                File.Delete(@"Engine\" + Name + @"Alert.txt");
            }
        }

        public void ShowDialog()
        {
            AlertToPriceCreateUi ui = new AlertToPriceCreateUi(this);
            ui.ShowDialog();
        }

        public bool IsOn { get; set; }

        public string Name { get; set; }

        public AlertType TypeAlert { get; set; }

        public AlertSignal CheckSignal(List<Candle> candles, Security sec)
        {
            if (IsOn == false || candles == null)
            {
                return null;
            }

            // 3 run along allert lines and check triggering
            // 3 бежим по линиям аллерта и проверяем срабатывание

            if (TypeActivation == PriceAlertTypeActivation.PriceLowerOrEqual &&
                candles[candles.Count - 1].Close <= PriceActivation ||
                TypeActivation == PriceAlertTypeActivation.PriceHigherOrEqual &&
                candles[candles.Count - 1].Close >= PriceActivation)
            {
                IsOn = false;
                if (MessageIsOn)
                {
                    UnmanagedMemoryStream stream = Resources.Bird;

                    if (MusicType == AlertMusic.Duck)
                    {
                        stream = Resources.Duck;
                    }
                    if (MusicType == AlertMusic.Wolf)
                    {
                        stream = Resources.wolf01;
                    }

                    AlertMessageManager.ThrowAlert(stream, Name, Message);
                }
                if (SignalType != SignalType.None)
                {
                    decimal realSlippage = 0;

                    if (SlippageType == AlertSlippageType.Absolute)
                    {
                        realSlippage = Slippage;
                    }
                    else if (SlippageType == AlertSlippageType.PriceStep)
                    {
                        realSlippage = Slippage * sec.PriceStep;
                    }
                    else if (SlippageType == AlertSlippageType.Persent)
                    {
                        realSlippage = (candles[candles.Count - 1].Close / 100) * Slippage;
                    }

                    return new AlertSignal
                    {
                        SignalType = SignalType,
                        Volume = VolumeReaction,
                        NumberClosingPosition = NumberClosePosition,
                        PriceType = OrderPriceType,
                        Slippage = realSlippage
                    };
                }
                if (SignalType == SignalType.None)
                {
                    return new AlertSignal();
                }
            }

            return null;
        }
        // custom settings
        // индивидуальные настройки

        public PriceAlertTypeActivation TypeActivation;

        public decimal PriceActivation;

        public SignalType SignalType;

        /// <summary>
        /// execution volume
        /// объём для исполнения
        /// </summary>
        public decimal VolumeReaction;

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slippage;

        public AlertSlippageType SlippageType;

        /// <summary>
        /// position number that will be closed
        /// номер позиции которая будет закрыта
        /// </summary>
        public int NumberClosePosition;

        /// <summary>
        /// order type 
        /// тип ордера 
        /// </summary>
        public OrderPriceType OrderPriceType;

        /// <summary>
        /// whether ejecting is enabled Message windows
        /// влкючено ли выбрасывание Окна сообщения
        /// </summary>
        public bool MessageIsOn;

        /// <summary>
        /// text of message thrown out when alert triggered
        /// текст сообщения, выбрасываемый при срабатывании Алерта
        /// </summary>
        public string Message;

        /// <summary>
        /// path to music file
        /// путь к файлу с музыкой
        /// </summary>
        public AlertMusic MusicType;

    }

    /// <summary>
    /// Price condition for activation of Alert
    /// условие активации Алерта по цене
    /// </summary>
    public enum PriceAlertTypeActivation
    {
        /// <summary>
        /// price higher or equal to value
        /// цена выше или равна значению
        /// </summary>
        PriceHigherOrEqual,

        /// <summary>
        /// Price lower or equal to
        /// цена ниже или равна значению
        /// </summary>
        PriceLowerOrEqual
    }
}
