/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Candles.Series;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

/*



*/

namespace OsEngine.Robots.FuturesDevStageBots
{

    [Bot("FuturesDevStageHMA")]
    public class FuturesDevStageHMA : BotPanel
    {
        BotTabSimple _base1;
        BotTabScreener _futs1;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _icebergCount;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _bollingerLength;
        private StrategyParameterDecimal _bollingerDeviation;

        private StrategyParameterString _contangoFilterRegime;
        private StrategyParameterInt _contangoStageToTradeLong;
        private StrategyParameterInt _contangoStageToTradeShort;
        private StrategyParameterDecimal _contangoCoefficient1;
        private StrategyParameterInt _contangoIndicatorLength;
        private StrategyParameterDecimal _contangoIndicatorDeviation;

        // Trade periods
        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;
        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        // Auto connection securities

        private StrategyParameterString _portfolioNum;

        public FuturesDevStageHMA(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // non trade periods
            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 05 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1OnOff = true;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2Start = new TimeOfDay() { Hour = 13, Minute = 54 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2End = new TimeOfDay() { Hour = 14, Minute = 6 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2OnOff = false;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3Start = new TimeOfDay() { Hour = 18, Minute = 30 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3End = new TimeOfDay() { Hour = 24, Minute = 00 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3OnOff = true;

            _tradePeriodsSettings.TradeInSunday = false;
            _tradePeriodsSettings.TradeInSaturday = false;

            _tradePeriodsSettings.Load();

            // Basic settings
            _regime = CreateParameter("Regime base", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort" }, "Base");

            _icebergCount = CreateParameter("Iceberg orders count", 1, 1, 3, 1, "Base");
            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods", "Base");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            _bollingerLength = CreateParameter("Bollinger Length", 230, 40, 300, 10, "Base");
            _bollingerDeviation = CreateParameter("Bollinger deviation", 2.1m, 0.5m, 4, 0.1m, "Base");

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Base");
            _volume = CreateParameter("Volume", 15, 1.0m, 50, 4, "Base");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Base");

            _contangoFilterRegime = CreateParameter("Contango filter regime", "On_MOEXStocksAuto", new[] { "Off", "On" }, "Dev Indicator");
            _contangoStageToTradeLong = CreateParameter("Contango stage to trade Long", 1, 1, 2, 1, "Dev Indicator");
            _contangoStageToTradeShort = CreateParameter("Contango stage to trade Short", 2, 1, 2, 1, "Dev Indicator");
            _contangoCoefficient1 = CreateParameter("Contango coeff", 1, 1.0m, 50, 4, "Dev Indicator");
            _contangoIndicatorLength = CreateParameter("Indicator length", 15, 1, 50, 4, "Dev Indicator");
            _contangoIndicatorDeviation = CreateParameter("Indicator deviation", 1.7m, 1.0m, 50, 4, "Dev Indicator");

            // Настройки: Длина скользяшки. Мультипликатор STD

            StrategyParameterButton buttonShowContango = CreateParameterButton("Show contango", "Dev Indicator");
            buttonShowContango.UserClickOnButtonEvent += ButtonShowContango_UserClickOnButtonEvent;

            // Auto Securities

            if (startProgram == StartProgram.IsOsTrader)
            {
                _portfolioNum = CreateParameter("Portfolio number", "", "Auto deploy");
                StrategyParameterButton buttonAutoDeploy = CreateParameterButton("Deploy standard securities", "Auto deploy");
                buttonAutoDeploy.UserClickOnButtonEvent += ButtonAutoDeploy_UserClickOnButtonEvent;
            }

            // Source creation

            _base1 = TabCreate<BotTabSimple>();
            _base1.CandleFinishedEvent += _base1_CandleFinishedEvent;
            _futs1 = TabCreate<BotTabScreener>();
            _futs1.CandleFinishedEvent += _futs1_CandleFinishedEvent;
            CreateIndicators(_base1, _futs1);

            ParametrsChangeByUser += FuturesStartContangoScreener_ParametrsChangeByUser;

          /*
            Description = OsLocalization.ConvertToLocString(
              "Eng:Trend futures screener on the Bollinger channel breakout. With a filter by the stage of the futures deviation from the base. Designed for the MOEX stock futures market_" +
              "Ru:Трендовый скринер фьючерсов на пробое канала Боллинджер. С фильтром по стадии отклонения фьючерса от базы. Рассчитана на рынок фьючерсов на акции MOEX_");
          */

        }

        private void ButtonShowContango_UserClickOnButtonEvent()
        {
            try
            {
                string message = "";


                if (_deviation.Count > 0)
                {
                        message +=
                            " Time: " + _base1.TimeServerCurrent.ToString()
                            + "\nValue deviation %: " + Math.Round(_deviation[^1], 3)
                            + "\nUp channel: " + Math.Round(_upChannel, 3) + " Down channel: " + Math.Round(_downChannel, 3)
                            + "\nStage: " + _stage;
                    
                }
                else
                {
                    message = "No values contango";
                }

                SendNewLogMessage(message, Logging.LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }

        }

        private void FuturesStartContangoScreener_ParametrsChangeByUser()
        {
            UpdateSettingsInIndicators(_base1, _futs1);
        }

        private void CreateIndicators(BotTabSimple baseSource, BotTabScreener futuresSource)
        {
            futuresSource.CreateCandleIndicator(1, "Bollinger", new List<string>() {
                _bollingerLength.ValueInt.ToString(), _bollingerDeviation.ValueDecimal.ToString() }, "Prime");

        }

        private void UpdateSettingsInIndicators(BotTabSimple baseSource, BotTabScreener futuresSource)
        {
            futuresSource._indicators[0].Parameters
             = new List<string>()
             {
                 _bollingerLength.ValueInt.ToString(),
                 _bollingerDeviation.ValueDecimal.ToString()
             };

            futuresSource.UpdateIndicatorsParameters();
        }

        #region Logic Entry

        private void _futs1_CandleFinishedEvent(List<Candle> candles, BotTabSimple arg2)
        {
            TryEntryLogic(_base1, _futs1);
        }

        private void _base1_CandleFinishedEvent(List<Candle> candles)
        {
            TryEntryLogic(_base1, _futs1);
        }

        #endregion

        #region Logic

        private void TryEntryLogic(BotTabSimple baseSource, BotTabScreener futuresScreener)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (baseSource.IsConnected == false
                || baseSource.IsReadyToTrade == false)
            {
                return;
            }

            List<Candle> baseCandles = baseSource.CandlesFinishedOnly;

            if (baseCandles == null
                || baseCandles.Count < 20)
            {
                return;
            }

            if (_tradePeriodsSettings.CanTradeThisTime(baseCandles[^1].TimeStart) == false)
            {
                return;
            }

            BotTabSimple futuresSource = GetFuturesToTrade(baseSource, futuresScreener, baseCandles[^1].TimeStart);

            if (futuresSource == null)
            {
                return;
            }

            if (futuresSource.IsConnected == false
                || futuresSource.IsReadyToTrade == false)
            {
                return;
            }

            List<Candle> futuresCandles = futuresSource.CandlesFinishedOnly;

            if (futuresCandles == null
                || futuresCandles.Count < 20)
            {
                return;
            }

            if (futuresCandles[^1].TimeStart != baseCandles[^1].TimeStart)
            {
                return;
            }

            if (_contangoFilterRegime.ValueString != "Off")
            {
                ProcessDeviationIndicator(baseSource, futuresSource);
            }

            return;

            if (this.StartProgram == StartProgram.IsOsTrader)
            {
                DateTime lastPairTradeTime = GetLastEntryLogicTime(baseSource.Security.Name);

                if (lastPairTradeTime.AddMinutes(1) > DateTime.Now)
                { // если по этой паре в реале уже был вход в логику, за последнюю минуту
                    return;
                }
                SetLastLogicEntryTime(baseSource.Security.Name, DateTime.Now);
            }

            List<Position> futuresPositions = futuresSource.PositionsOpenAll;

            if (futuresPositions.Count > 0)
            { // вход в логику закрытия позиции
                TryClosePositionLogic(baseSource, futuresSource, baseCandles, futuresCandles, futuresPositions[0]);
            }
            else
            { // вход в логику открытия позиций
                TryOpenPositionLogic(baseSource, futuresSource, baseCandles, futuresCandles);
            }
        }

        private BotTabSimple GetFuturesToTrade(BotTabSimple baseSource, BotTabScreener futures, DateTime currentTime)
        {
            /*
            Берём фьюч в пару:
            1) Если уже есть позиция
            2) Берём ближайшую пару фьюч / спот. 
            2.2) Если до ближайшего фьючерса меньше 5 дней до экспирации, не учитываем его как точку входа.
            2.3) Но не дальше чем 4 месяца, на случай если пропущена серия в тестере.
            */

            // 1 берём фьючерс, если по нему уже есть открытая позиция

            for (int i = 0; i < futures.Tabs.Count; i++)
            {
                BotTabSimple currentFutures = futures.Tabs[i];

                if (currentFutures.PositionsOpenAll.Count != 0)
                {
                    return currentFutures;
                }
            }

            // 2 теперь пробуем найти ближайший

            BotTabSimple selectedFutures = null;

            for (int i = 0; i < futures.Tabs.Count; i++)
            {
                Security sec = futures.Tabs[i].Security;

                if (sec == null)
                {
                    continue;
                }

                if (sec.Expiration == DateTime.MinValue)
                {
                    continue;
                }

                double daysByExpiration = (sec.Expiration - currentTime).TotalDays;

                if (daysByExpiration < 3
                    || daysByExpiration > 100)
                {
                    continue;
                }

                if (selectedFutures != null
                    && selectedFutures.Security.Expiration < sec.Expiration)
                {
                    continue;
                }

                selectedFutures = futures.Tabs[i];
            }

            return selectedFutures;
        }

        private void TryOpenPositionLogic(
            BotTabSimple baseSource,
            BotTabSimple futuresSource,
            List<Candle> baseCandles,
            List<Candle> futuresCandles)
        {
            // 1 берём по обоим вкладкам боллинджеры

            Aindicator futuresBollinger = (Aindicator)futuresSource.Indicators[0];

            if (futuresBollinger.DataSeries[0].Last == 0)
            {
                return;
            }

            // 2 проверяем условия 

            decimal futuresLastPrice = futuresCandles[^1].Close;

            if (_regime.ValueString != "OnlyShort"
                && futuresLastPrice > futuresBollinger.DataSeries[0].Last)   // фьючерс выше верхнего боллинджера
            {// Лонг

                if (_contangoFilterRegime.ValueString != "Off")
                {
                    int stageContango = _stage;

                    if (stageContango != _contangoStageToTradeLong.ValueInt)
                    {
                        return;
                    }
                }

                futuresSource.BuyAtIcebergMarket(GetVolume(futuresSource), _icebergCount.ValueInt, 1000);


            }
            else if (_regime.ValueString != "OnlyLong"
                && futuresLastPrice < futuresBollinger.DataSeries[1].Last) // фьючерс ниже нижнего боллинджера
            {// Шорт

                if (_contangoFilterRegime.ValueString != "Off")
                {
                    int stageContango = _stage;

                    if (stageContango != _contangoStageToTradeShort.ValueInt)
                    {
                        return;
                    }
                }

                futuresSource.SellAtIcebergMarket(GetVolume(futuresSource), _icebergCount.ValueInt, 1000);
            }

        }

        private void TryClosePositionLogic(
            BotTabSimple baseSource,
            BotTabSimple futuresSource,
            List<Candle> baseCandles,
            List<Candle> futuresCandles,
            Position pos)
        {

            /*
            Выход:
                   1) Фьючерс закрылся с обратной стороны боллинджера. Подключаемый
                   2) Выходим из позиции по фьючу, если до экспирации меньше или равно 2 торговых дня. По любой цене. 
            */

            if (StartProgram != StartProgram.IsOsTrader)
            {
                if (pos.State != PositionStateType.Open)
                {// в тестере и оптимизаторе не допускаем спама ордерами
                    return;
                }
            }

            Aindicator futuresBollinger = (Aindicator)futuresSource.Indicators[0];

            if (futuresBollinger.DataSeries[0].Last == 0)
            {
                return;
            }

            decimal baseLastPrice = baseCandles[^1].Close;
            decimal futuresLastPrice = futuresCandles[^1].Close;

            bool needToExit = false;


            if (pos.Direction == Side.Buy
                && futuresLastPrice < futuresBollinger.DataSeries[1].Last)
            {
                needToExit = true;
            }

            if (pos.Direction == Side.Sell
                && futuresLastPrice > futuresBollinger.DataSeries[0].Last)
            {
                needToExit = true;
            }

            double daysByExpiration = (futuresSource.Security.Expiration - futuresCandles[^1].TimeStart).TotalDays;

            if (daysByExpiration < 3)
            {
                needToExit = true;
            }

            if (needToExit == true)
            {
                futuresSource.CloseAtIcebergMarket(pos, pos.OpenVolume, _icebergCount.ValueInt, 1000);
            }
        }

        #endregion

        #region Helpers

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume.ValueDecimal;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (_volumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (_tradeAssetInPortfolio.ValueString == "Prime")
                {
                    portfolioPrimeAsset = myPortfolio.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                    if (positionOnBoard == null)
                    {
                        return 0;
                    }

                    for (int i = 0; i < positionOnBoard.Count; i++)
                    {
                        if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                       && tab.Security.PriceStep != tab.Security.PriceStepCost
                       && tab.PriceBestAsk != 0
                       && tab.Security.PriceStep != 0
                       && tab.Security.PriceStepCost != 0)
                    {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                        qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                    }
                    qty = Math.Round(qty, tab.Security.DecimalsVolume);
                }
                else
                {
                    qty = Math.Round(qty, 7);
                }

                return qty;
            }

            return volume;
        }

        private List<LastTradeTimeValue> _entryLogicByBaseSecurityInReal = new List<LastTradeTimeValue>();

        private void SetLastLogicEntryTime(string securityBase, DateTime time)
        {
            for (int i = 0; i < _entryLogicByBaseSecurityInReal.Count; i++)
            {
                if (_entryLogicByBaseSecurityInReal[i].SecurityName == securityBase)
                {
                    _entryLogicByBaseSecurityInReal[i].Time = time;
                    return;
                }
            }

            LastTradeTimeValue newValue = new LastTradeTimeValue();
            newValue.SecurityName = securityBase;
            newValue.Time = time;
            _entryLogicByBaseSecurityInReal.Add(newValue);
        }

        private DateTime GetLastEntryLogicTime(string securityBase)
        {
            for (int i = 0; i < _entryLogicByBaseSecurityInReal.Count; i++)
            {
                if (_entryLogicByBaseSecurityInReal[i].SecurityName == securityBase)
                {
                    return _entryLogicByBaseSecurityInReal[i].Time;
                }
            }

            return DateTime.MinValue;
        }

        #endregion

        #region Deviation indicator

        List<decimal> _deviation = new List<decimal>();
        int _stage = 0;
        decimal _sma = 0;
        decimal _upChannel = 0;
        decimal _downChannel = 0;

        private void ProcessDeviationIndicator(BotTabSimple baseSource, BotTabSimple futuresSource)
        {
            /*

            Индикатор на пару. 

            Настройки: Длина скользяшки. Мультипликатор STD

            "Стадия отклонения раздвижки"
            0 серия - состояние.
            0 - нейтрально 1 - растёт. -1 падает
            1 серия - раздвижка
            2 серия - скользяшка раздвижки
            3 серия - канал сверху
            4 серия - канал снизу

            Вывести значения в окно лога

           */

            _stage = 0;
            _deviation.Clear();
            _sma = 0;
            _upChannel = 0;
            _downChannel = 0;

            // 1 синхронизируем свечи. Берём по размеру

            List<Candle> baseCandles = baseSource.CandlesFinishedOnly;
            List<Candle> futuresCandles = futuresSource.CandlesFinishedOnly;

            if(baseCandles.Count == 0 
                || futuresCandles.Count == 0)
            {
                return;
            }

            if (baseCandles[^1].TimeStart != futuresCandles[^1].TimeStart)
            {
                return;
            }

            List<Candle> baseCandlesCleared = new List<Candle>();
            List<Candle> futuresCandlesCleared = new List<Candle>();

            for(int baseCandInd = baseCandles.Count-1, futCandInd = futuresCandles.Count-1; 
                baseCandInd >= 0 && futCandInd >= 0;baseCandInd--,futCandInd--)
            {
                Candle baseCandle = baseCandles[baseCandInd];
                Candle futuresCandle = futuresCandles[futCandInd];

                if (baseCandle.TimeStart == futuresCandle.TimeStart)
                {
                    baseCandlesCleared.Insert(0,baseCandle);
                    futuresCandlesCleared.Insert(0, futuresCandle);

                    if(baseCandlesCleared.Count >= _contangoIndicatorLength.ValueInt * 2)
                    {
                        break;
                    }
                }
                else
                {
                    if (baseCandle.TimeStart > futuresCandle.TimeStart)
                    {
                        futCandInd++;
                    }
                    else
                    {
                        baseCandInd++;
                    }
                }
            }

            // 2 считаем раздвижку с коэффициентом

            decimal coeff = _contangoCoefficient1.ValueDecimal;

            for(int i = 0;i < baseCandlesCleared.Count;i++)
            {
                Candle baseCandle = baseCandlesCleared[i];
                Candle futuresCandle = futuresCandlesCleared[i];

                decimal contangoAbs = (futuresCandle.Close / coeff) - baseCandle.Close;
                decimal contangoPercent = contangoAbs / (baseCandle.Close / 100);

                _deviation.Add(contangoPercent);
            }

            // 3 делаем скользяшку

            _sma = GetMoving(_deviation, _deviation.Count - 1, _contangoIndicatorLength.ValueInt);

            // 4 делаем боллинжер 

            decimal std = GetBollingerStd(_deviation, _contangoIndicatorLength.ValueInt);

            _upChannel = _sma + std * _contangoIndicatorDeviation.ValueDecimal;
            _downChannel = _sma - std * _contangoIndicatorDeviation.ValueDecimal;

            // 5 считаем стадию

            if (_deviation[^1] > _upChannel)
            {
                _stage = 1;
            }
            else if (_deviation[^1] < _downChannel)
            {
                _stage = -1;
            }
            else
            {
                // zero
            }
        }

        private decimal GetMoving(List<decimal> values, int index, int length)
        {
            if (index - length <= 0 ||
                index >= values.Count)
            {
                return 0;
            }

            decimal average = 0;
            int valuesCount = 0;

            for (int i = index; i > index - length && i > 0; i--)
            {
                try
                {
                    average += values[i];
                    valuesCount++;
                }
                catch
                {
                    // ignore
                }
            }

            average = average / valuesCount;

            return Math.Round(average, 5);
        }

        private decimal GetBollingerStd(List<decimal> values, int length)
        {

            decimal[] valueDev = new decimal[length];

            for (int i = values.Count - length + 1, i2 = 0; i < values.Count; i++, i2++)
            {
                decimal valueSma = GetMoving(_deviation, i, _contangoIndicatorLength.ValueInt);

                valueDev[i2] = values[i] - valueSma;
            }

            for (int i = 0; i < valueDev.Length; i++)
            {
                valueDev[i] = Convert.ToDecimal(Math.Pow(Convert.ToDouble(valueDev[i]), 2));
            }

            decimal summ = 0;

            for (int i = 0; i < valueDev.Length; i++)
            {
                summ += valueDev[i];
            }

            if (length > 30)
            {
                summ = summ / (length - 1);
            }
            else
            {
                summ = summ / length;
            }

            double result = Math.Sqrt(Convert.ToDouble(summ));

            return Convert.ToDecimal(result);
        
        }

        #endregion

        #region Auto-set securities to T-Investment

        private void ButtonAutoDeploy_UserClickOnButtonEvent()
        {
            SetTSecurities();
        }

        public void SetTSecurities()
        {
            // 1 сервер Т-Банк должен быть включен

            List<AServer> servers = ServerMaster.GetAServers();

            if (servers == null
                || servers.Count == 0)
            {
                SendNewLogMessage("Сначала подключите коннектор к Т-Инвестиции", Logging.LogMessageType.Error);
                return;
            }

            if (servers.Find(s => s.ServerType == ServerType.TInvest) == null)
            {
                SendNewLogMessage("Сначала подключите коннектор к Т-Инвестиции", Logging.LogMessageType.Error);
                return;
            }

            // 2 номер портфеля должен быть указан

            string portfolioName = _portfolioNum.ValueString;

            if (string.IsNullOrEmpty(portfolioName) == true)
            {
                SendNewLogMessage("Не указан портфель для развёртывания источников", Logging.LogMessageType.Error);
                return;
            }

            Portfolio myPortfolio = null;
            AServer myServer = null;

            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].ServerType != ServerType.TInvest)
                {
                    continue;
                }

                List<Portfolio> portfoliosInServer = servers[i].Portfolios;

                if (portfoliosInServer == null
                    || portfoliosInServer.Count == 0)
                {
                    continue;
                }

                for (int j = 0; j < portfoliosInServer.Count; j++)
                {
                    if (portfoliosInServer[j].Number == portfolioName)
                    {
                        myServer = servers[i];
                        myPortfolio = portfoliosInServer[j];
                        break;
                    }
                }

                if (myServer != null)
                {
                    break;
                }
            }

            if (myServer == null)
            {
                SendNewLogMessage("Не найден портфель и сервер. Возможно указан не верный портфель", Logging.LogMessageType.Error);
                return;
            }

            // 3 фьючерсная площадка и спот, должны быть подключены к коннектору

            List<Security> securitiesAll = myServer.Securities;

            if (securitiesAll == null
                || securitiesAll.Count == 0)
            {
                SendNewLogMessage("В коннекторе не найдены бумаги. Возможно он не подключен", Logging.LogMessageType.Error);
                return;
            }

            if (securitiesAll.Find(s => s.SecurityType == SecurityType.Futures) == null)
            {
                SendNewLogMessage("В коннекторе не найдены фьючерсы. Возможно в коннекторе выключено разрешение на их скачивание. Это настраивается в коннекторе", Logging.LogMessageType.Error);
                return;
            }

            if (securitiesAll.Find(s => s.SecurityType == SecurityType.Stock) == null)
            {
                SendNewLogMessage("В коннекторе не найдены акции. Возможно в коннекторе выключено разрешение на их скачивание. Это настраивается в коннекторе", Logging.LogMessageType.Error);
                return;
            }

            // 4 устанавливаем инструменты

            Security spotSber = securitiesAll.Find(s => s.Name == "SBER" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresSber =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("SRH") || s.Name.StartsWith("SRM")
                || s.Name.StartsWith("SRZ") || s.Name.StartsWith("SRU")));

            SetSecurities(_base1, _futs1, spotSber, futuresSber, myPortfolio, myServer);
        }

        private void SetSecurities(BotTabSimple tabSpot, BotTabScreener tabFutures,
            Security spotSecurity, List<Security> futuresSecurity, Portfolio portfolio, AServer server)
        {
            if (spotSecurity == null || futuresSecurity == null)
            {
                return;
            }

            tabSpot.Connector.ServerType = server.ServerType;
            tabSpot.Connector.ServerFullName = server.ServerNameAndPrefix;
            tabSpot.Connector.TimeFrame = TimeFrame.Min15;
            tabSpot.Connector.SecurityName = spotSecurity.Name;
            tabSpot.Connector.SecurityClass = spotSecurity.NameClass;
            tabSpot.Connector.PortfolioName = portfolio.Number;

            tabFutures.SecuritiesClass = futuresSecurity[0].NameClass;
            tabFutures.TimeFrame = TimeFrame.Min15;
            tabFutures.PortfolioName = portfolio.Number;
            tabFutures.ServerType = server.ServerType;
            tabFutures.ServerName = server.ServerNameAndPrefix;

            tabFutures.CandleCreateMethodType = CandleCreateMethodType.Simple.ToString();
            ((Simple)tabFutures.CandleSeriesRealization).TimeFrame = TimeFrame.Min15;
            ((Simple)tabFutures.CandleSeriesRealization).TimeFrameParameter.ValueString = TimeFrame.Min15.ToString();

            List<ActivatedSecurity> securitiesToScreener = new List<ActivatedSecurity>();

            for (int i = 0; i < futuresSecurity.Count; i++)
            {
                ActivatedSecurity sec = new ActivatedSecurity();
                sec.SecurityClass = futuresSecurity[i].NameClass;
                sec.SecurityName = futuresSecurity[i].Name;
                sec.IsOn = true;
                securitiesToScreener.Add(sec);
            }

            for (int i = 0; i < securitiesToScreener.Count; i++)
            {
                if (tabFutures.SecuritiesNames.Find(s => s.SecurityName == securitiesToScreener[i].SecurityName) == null)
                {
                    tabFutures.SecuritiesNames.Add(securitiesToScreener[i]);
                }
            }

            tabFutures.SaveSettings();
            tabFutures.NeedToReloadTabs = true;
        }

        #endregion

    }

    public class LastTradeTimeValue
    {
        public string SecurityName;

        public DateTime Time;

    }
}
