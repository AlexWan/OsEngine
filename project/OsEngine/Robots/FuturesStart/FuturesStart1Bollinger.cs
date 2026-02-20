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
using System.Linq;

/*

Трендовушка на пробое боллинджера. С фильтром по стадии отклонения фьючерса от базы.

Рассчитана на рынок фьючерсов на акции MOEX

Индикаторы
Bollinger

ВХОД в позицию
Пересечение верхней или нижней линии боллинджера

Выход из позиции
Пересечение обратной стороны канала боллинджера

Фильтр на вход. Рэнкинг раздвижек
Считаем по каждому инструменту раздвижку в %. И делим ренкинг на 2 части. Самые дальние и самые ближние.
Самые дальние - можно только шорт. Их и так спекулянты перекупили
Самые ближние - можно только лонг. Их и так спекулянты перепродали

*/

namespace OsEngine.Robots.FuturesStart
{
    [Bot("FuturesStart1Bollinger")]
    public class FuturesStart1Bollinger : BotPanel
    {
        BotTabSimple _base1;
        BotTabScreener _futs1;

        BotTabSimple _base2;
        BotTabScreener _futs2;

        BotTabSimple _base3;
        BotTabScreener _futs3;

        BotTabSimple _base4;
        BotTabScreener _futs4;

        BotTabSimple _base5;
        BotTabScreener _futs5;

        BotTabSimple _base6;
        BotTabScreener _futs6;

        BotTabSimple _base7;
        BotTabScreener _futs7;

        BotTabSimple _base8;
        BotTabScreener _futs8;

        BotTabSimple _base9;
        BotTabScreener _futs9;

        BotTabSimple _base10;
        BotTabScreener _futs10;

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
        private StrategyParameterInt _contangoFilterCountSecurities;
        private StrategyParameterInt _contangoStageToTradeLong;
        private StrategyParameterInt _contangoStageToTradeShort;
        private StrategyParameterDecimal _contangoCoefficient1;
        private StrategyParameterDecimal _contangoCoefficient2;
        private StrategyParameterDecimal _contangoCoefficient3;
        private StrategyParameterDecimal _contangoCoefficient4;
        private StrategyParameterDecimal _contangoCoefficient5;
        private StrategyParameterDecimal _contangoCoefficient6;
        private StrategyParameterDecimal _contangoCoefficient7;
        private StrategyParameterDecimal _contangoCoefficient8;
        private StrategyParameterDecimal _contangoCoefficient9;
        private StrategyParameterDecimal _contangoCoefficient10;

        private StrategyParameterBool _tradeRegimeSecurity1;
        private StrategyParameterBool _tradeRegimeSecurity2;
        private StrategyParameterBool _tradeRegimeSecurity3;
        private StrategyParameterBool _tradeRegimeSecurity4;
        private StrategyParameterBool _tradeRegimeSecurity5;
        private StrategyParameterBool _tradeRegimeSecurity6;
        private StrategyParameterBool _tradeRegimeSecurity7;
        private StrategyParameterBool _tradeRegimeSecurity8;
        private StrategyParameterBool _tradeRegimeSecurity9;
        private StrategyParameterBool _tradeRegimeSecurity10;

        private bool CanTradeThisSecurity(string securityName)
        {
            if (this.TabsSimple[0].Security != null
                   && this.TabsSimple[0].Security.Name == securityName)
            {
                return _tradeRegimeSecurity1.ValueBool;
            }
            if (this.TabsSimple[1].Security != null
                && this.TabsSimple[1].Security.Name == securityName)
            {
                return _tradeRegimeSecurity2.ValueBool;
            }
            if (this.TabsSimple[2].Security != null
                && this.TabsSimple[2].Security.Name == securityName)
            {
                return _tradeRegimeSecurity3.ValueBool;
            }
            if (this.TabsSimple[3].Security != null
                && this.TabsSimple[3].Security.Name == securityName)
            {
                return _tradeRegimeSecurity4.ValueBool;
            }
            if (this.TabsSimple[4].Security != null
                && this.TabsSimple[4].Security.Name == securityName)
            {
                return _tradeRegimeSecurity5.ValueBool;
            }
            if (this.TabsSimple[5].Security != null
               && this.TabsSimple[5].Security.Name == securityName)
            {
                return _tradeRegimeSecurity6.ValueBool;
            }
            if (this.TabsSimple[6].Security != null
               && this.TabsSimple[6].Security.Name == securityName)
            {
                return _tradeRegimeSecurity7.ValueBool;
            }
            if (this.TabsSimple[7].Security != null
                && this.TabsSimple[7].Security.Name == securityName)
            {
                return _tradeRegimeSecurity8.ValueBool;
            }
            if (this.TabsSimple[8].Security != null
                && this.TabsSimple[8].Security.Name == securityName)
            {
                return _tradeRegimeSecurity9.ValueBool;
            }
            if (this.TabsSimple[9].Security != null
                && this.TabsSimple[9].Security.Name == securityName)
            {
                return _tradeRegimeSecurity10.ValueBool;
            }
            return false;
        }

        // Trade periods
        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;
        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        // Auto connection securities

        private StrategyParameterString _portfolioNum;

        public FuturesStart1Bollinger(string name, StartProgram startProgram) : base(name, startProgram)
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

            _contangoFilterRegime = CreateParameter("Contango filter regime", "On_MOEXStocksAuto", new[] { "Off", "On_MOEXStocksAuto", "On_Manual" }, "Contango");
            _contangoFilterCountSecurities = CreateParameter("Contango filter count securities", 5, 1, 2, 1, "Contango");
            _contangoStageToTradeLong = CreateParameter("Contango stage to trade Long", 1, 1, 2, 1, "Contango");
            _contangoStageToTradeShort = CreateParameter("Contango stage to trade Short", 2, 1, 2, 1, "Contango");

            _contangoCoefficient1 = CreateParameter("Manual coeff 1", 1, 1.0m, 50, 4, "Contango");
            _contangoCoefficient2 = CreateParameter("Manual coeff 2", 1, 1.0m, 50, 4, "Contango");
            _contangoCoefficient3 = CreateParameter("Manual coeff 3", 1, 1.0m, 50, 4, "Contango");
            _contangoCoefficient4 = CreateParameter("Manual coeff 4", 1, 1.0m, 50, 4, "Contango");
            _contangoCoefficient5 = CreateParameter("Manual coeff 5", 1, 1.0m, 50, 4, "Contango");
            _contangoCoefficient6 = CreateParameter("Manual coeff 6", 1, 1.0m, 50, 4, "Contango");
            _contangoCoefficient7 = CreateParameter("Manual coeff 7", 1, 1.0m, 50, 4, "Contango");
            _contangoCoefficient8 = CreateParameter("Manual coeff 8", 1, 1.0m, 50, 4, "Contango");
            _contangoCoefficient9 = CreateParameter("Manual coeff 9", 1, 1.0m, 50, 4, "Contango");
            _contangoCoefficient10 = CreateParameter("Manual coeff 10", 1, 1.0m, 50, 4, "Contango");

            StrategyParameterButton buttonShowContango = CreateParameterButton("Show contango", "Contango");
            buttonShowContango.UserClickOnButtonEvent += ButtonShowContango_UserClickOnButtonEvent;

            _tradeRegimeSecurity1 = CreateParameter("Trade security 1", true, "Trade securities");
            _tradeRegimeSecurity2 = CreateParameter("Trade security 2", true, "Trade securities");
            _tradeRegimeSecurity3 = CreateParameter("Trade security 3", true, "Trade securities");
            _tradeRegimeSecurity4 = CreateParameter("Trade security 4", true, "Trade securities");
            _tradeRegimeSecurity5 = CreateParameter("Trade security 5", true, "Trade securities");
            _tradeRegimeSecurity6 = CreateParameter("Trade security 6", true, "Trade securities");
            _tradeRegimeSecurity7 = CreateParameter("Trade security 7", true, "Trade securities");
            _tradeRegimeSecurity8 = CreateParameter("Trade security 8", false, "Trade securities");
            _tradeRegimeSecurity9 = CreateParameter("Trade security 9", false, "Trade securities");
            _tradeRegimeSecurity10 = CreateParameter("Trade security 10", false, "Trade securities");

            // Auto Securities

            if(startProgram == StartProgram.IsOsTrader)
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

            _base2 = TabCreate<BotTabSimple>();
            _base2.CandleFinishedEvent += _base2_CandleFinishedEvent; 
            _futs2 = TabCreate<BotTabScreener>();
            _futs2.CandleFinishedEvent += _futs2_CandleFinishedEvent;
            CreateIndicators(_base2, _futs2);

            _base3 = TabCreate<BotTabSimple>();
            _base3.CandleFinishedEvent += _base3_CandleFinishedEvent;
            _futs3 = TabCreate<BotTabScreener>();
            _futs3.CandleFinishedEvent += _futs3_CandleFinishedEvent;
            CreateIndicators(_base3, _futs3);

            _base4 = TabCreate<BotTabSimple>();
            _base4.CandleFinishedEvent += _base4_CandleFinishedEvent; 
            _futs4 = TabCreate<BotTabScreener>();
            _futs4.CandleFinishedEvent += _futs4_CandleFinishedEvent;
            CreateIndicators(_base4, _futs4);

            _base5 = TabCreate<BotTabSimple>();
            _base5.CandleFinishedEvent += _base5_CandleFinishedEvent; 
            _futs5 = TabCreate<BotTabScreener>();
            _futs5.CandleFinishedEvent += _futs5_CandleFinishedEvent;
            CreateIndicators(_base5, _futs5);

            _base6 = TabCreate<BotTabSimple>();
            _base6.CandleFinishedEvent += _base6_CandleFinishedEvent; 
            _futs6 = TabCreate<BotTabScreener>();
            _futs6.CandleFinishedEvent += _futs6_CandleFinishedEvent;
            CreateIndicators(_base6, _futs6);

            _base7 = TabCreate<BotTabSimple>();
            _base7.CandleFinishedEvent += _base7_CandleFinishedEvent; 
            _futs7 = TabCreate<BotTabScreener>();
            _futs7.CandleFinishedEvent += _futs7_CandleFinishedEvent;
            CreateIndicators(_base7, _futs7);

            _base8 = TabCreate<BotTabSimple>();
            _base8.CandleFinishedEvent += _base8_CandleFinishedEvent;
            _futs8 = TabCreate<BotTabScreener>();
            _futs8.CandleFinishedEvent += _futs8_CandleFinishedEvent;
            CreateIndicators(_base8, _futs8);

            _base9 = TabCreate<BotTabSimple>();
            _base9.CandleFinishedEvent += _base9_CandleFinishedEvent; 
            _futs9 = TabCreate<BotTabScreener>();
            _futs9.CandleFinishedEvent += _futs9_CandleFinishedEvent;
            CreateIndicators(_base9, _futs9);

            _base10 = TabCreate<BotTabSimple>();
            _base10.CandleFinishedEvent += _base10_CandleFinishedEvent; 
            _futs10 = TabCreate<BotTabScreener>();
            _futs10.CandleFinishedEvent += _futs10_CandleFinishedEvent;
            CreateIndicators(_base10, _futs10);

            ParametrsChangeByUser += FuturesStartContangoScreener_ParametrsChangeByUser;

            Description = OsLocalization.ConvertToLocString(
              "Eng:Trend futures screener on the Bollinger channel breakout. With a filter by the stage of the futures deviation from the base. Designed for the MOEX stock futures market_" +
              "Ru:Трендовый скринер фьючерсов на пробое канала Боллинджер. С фильтром по стадии отклонения фьючерса от базы. Рассчитана на рынок фьючерсов на акции MOEX_");
        }

        private void ButtonShowContango_UserClickOnButtonEvent()
        {
            try
            {
                string message = "";


                if(_contangoValues.Count > 0)
                {
                    for (int i = 0; i < _contangoValues.Count; i++)
                    {
                        message +=
                            _contangoValues[i].SecurityName
                            + " Time: " + _contangoValues[i].LastTimeUpdate
                            + " Value%: " + Math.Round(_contangoValues[i].ContangoPercent, 3)
                            + "\n";
                    }
                }
                else
                {
                    message = "No values contango";
                }

                SendNewLogMessage(message, Logging.LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
            } 
            
        }

        private void FuturesStartContangoScreener_ParametrsChangeByUser()
        {
            UpdateSettingsInIndicators(_base1, _futs1);
            UpdateSettingsInIndicators(_base2, _futs2);
            UpdateSettingsInIndicators(_base3, _futs3);
            UpdateSettingsInIndicators(_base4, _futs4);
            UpdateSettingsInIndicators(_base5, _futs5);
            UpdateSettingsInIndicators(_base6, _futs6);
            UpdateSettingsInIndicators(_base7, _futs7);
            UpdateSettingsInIndicators(_base8, _futs8);
            UpdateSettingsInIndicators(_base9, _futs9);
            UpdateSettingsInIndicators(_base10, _futs10);
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

        private void _futs2_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base2, _futs2);
        }

        private void _base2_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base2, _futs2);
        }

        private void _futs3_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base3, _futs3);
        }

        private void _base3_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base3, _futs3);
        }

        private void _futs4_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base4, _futs4);
        }

        private void _base4_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base4, _futs4);
        }

        private void _futs5_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base5, _futs5);
        }

        private void _base5_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base5, _futs5);
        }

        private void _futs6_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base6, _futs6);
        }

        private void _base6_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base6, _futs6);
        }

        private void _futs7_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base7, _futs7);
        }

        private void _base7_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base7, _futs7);
        }

        private void _futs8_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base8, _futs8);
        }

        private void _base8_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base8, _futs8);
        }

        private void _futs9_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base9, _futs9);
        }

        private void _base9_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base9, _futs9);
        }

        private void _futs10_CandleFinishedEvent(List<Candle> arg1, BotTabSimple arg2)
        {
            TryEntryLogic(_base10, _futs10);
        }

        private void _base10_CandleFinishedEvent(List<Candle> obj)
        {
            TryEntryLogic(_base10, _futs10);
        }

        #endregion

        #region Logic

        private void TryEntryLogic(BotTabSimple baseSource, BotTabScreener futuresScreener)
        {
            if(_regime.ValueString == "Off")
            {
                return;
            }

            if(baseSource.IsConnected == false
                || baseSource.IsReadyToTrade == false)
            {
                return;
            }

            List<Candle> baseCandles = baseSource.CandlesFinishedOnly;

            if(baseCandles == null 
                || baseCandles.Count < 20)
            {
                return;
            }

            if (_tradePeriodsSettings.CanTradeThisTime(baseCandles[^1].TimeStart) == false)
            {
                return;
            }

            BotTabSimple futuresSource = GetFuturesToTrade(baseSource, futuresScreener, baseCandles[^1].TimeStart);

            if(futuresSource == null)
            {
                return;
            }

            if(futuresSource.IsConnected == false
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
                SetContangoValues(baseSource, futuresSource);
            }

            if (CanTradeThisSecurity(baseSource.Security.Name) == false)
            {
                return;
            }

            List<Position> futuresPositions = futuresSource.PositionsOpenAll;

            if(futuresPositions.Count > 0)
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

            for (int i = 0;i < futures.Tabs.Count;i++)
            {
                BotTabSimple currentFutures = futures.Tabs[i];

                if(currentFutures.PositionsOpenAll.Count != 0)
                {
                    return currentFutures;
                }
            }

            // 2 теперь пробуем найти ближайший

            BotTabSimple selectedFutures = null;

            for (int i = 0;i < futures.Tabs.Count;i++)
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

                if(daysByExpiration < 3
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

            if(_regime.ValueString != "OnlyShort"
                && futuresLastPrice > futuresBollinger.DataSeries[0].Last)   // фьючерс выше верхнего боллинджера
            {// Лонг

                if(_contangoFilterRegime.ValueString != "Off")
                {
                    int stageContango = GetContangoStage(futuresSource.Security.Name);

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
                    int stageContango = GetContangoStage(futuresSource.Security.Name);

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

        #endregion

        #region Contango values

        private List<ContangoValue> _contangoValues = new List<ContangoValue>();

        private void SetContangoValues(BotTabSimple baseSource, BotTabSimple futuresSource)
        {
            ContangoValue value = null;

            for(int i = 0;i < _contangoValues.Count;i++)
            {
                if (_contangoValues[i].SecurityName == futuresSource.Connector.SecurityName)
                {
                    value = _contangoValues[i];
                    value.LastTimeUpdate = futuresSource.TimeServerCurrent;
                    break;  
                }
            }

            for (int i = 0; i < _contangoValues.Count; i++)
            {
                if (_contangoValues[i].LastTimeUpdate > futuresSource.TimeServerCurrent)
                {
                    _contangoValues.RemoveAt(i);
                    i--;
                    continue;
                }
                if (_contangoValues[i].LastTimeUpdate.AddHours(2) < futuresSource.TimeServerCurrent)
                {
                    _contangoValues.RemoveAt(i);
                    i--;
                    continue;
                }
            }

            if (value == null)
            {
                value = new ContangoValue();
                value.SecurityName = futuresSource.Connector.SecurityName;

                _contangoValues.Add(value);
            }

            decimal coeff = 1;

            if(_contangoFilterRegime.ValueString == "On_MOEXStocksAuto")
            {
                if (baseSource.Security.Name.Contains("MGNT") == false
                    && baseSource.Security.Name.Contains("VTB") == false
                     && baseSource.Security.Name.Contains("GMKN") == false)
                {
                    for (int i = 0; i < baseSource.Security.Decimals; i++)
                    {
                        coeff = coeff * 10;
                    }
                }
                else if (baseSource.Security.Name.Contains("VTB") == true)
                {
                    DateTime time = baseSource.TimeServerCurrent;

                    if (time.Year < 2024)
                    {
                        coeff = 20;
                    }
                    else if (time.Year == 2024
                        && time.Month < 7)
                    {
                        coeff = 20;
                    }
                    else if (time.Year == 2024
                            && time.Month == 7
                            && time.Day < 15)
                    {
                        coeff = 20;
                    }
                    else
                    {
                        coeff = 100;
                    }
                }
                else if (baseSource.Security.Name.Contains("GMKN") == true)
                {
                    DateTime time = baseSource.TimeServerCurrent;

                    if (time.Year < 2024)
                    {
                        coeff = 100;
                    }
                    else if (time.Year == 2024
                        && time.Month < 4)
                    {
                        coeff = 100;
                    }
                    else if (time.Year == 2024
                            && time.Month == 4
                            && time.Day < 4)
                    {
                        coeff = 100;
                    }
                    else
                    {
                        coeff = 10;
                    }
                }
            }
            else if (_contangoFilterRegime.ValueString == "On_Manual") 
            {
                if(this.TabsSimple[0].Security != null
                    && this.TabsSimple[0].Security.Name == baseSource.Security.Name)
                {
                    coeff = _contangoCoefficient1.ValueDecimal;
                }
                if (this.TabsSimple[1].Security != null
                    && this.TabsSimple[1].Security.Name == baseSource.Security.Name)
                {
                    coeff = _contangoCoefficient2.ValueDecimal;
                }
                if (this.TabsSimple[2].Security != null
                    && this.TabsSimple[2].Security.Name == baseSource.Security.Name)
                {
                    coeff = _contangoCoefficient3.ValueDecimal;
                }
                if (this.TabsSimple[3].Security != null
                    && this.TabsSimple[3].Security.Name == baseSource.Security.Name)
                {
                    coeff = _contangoCoefficient4.ValueDecimal;
                }
                if (this.TabsSimple[4].Security != null
                    && this.TabsSimple[4].Security.Name == baseSource.Security.Name)
                {
                    coeff = _contangoCoefficient5.ValueDecimal;
                }
                if (this.TabsSimple[5].Security != null
                   && this.TabsSimple[5].Security.Name == baseSource.Security.Name)
                {
                    coeff = _contangoCoefficient6.ValueDecimal;
                }
                if (this.TabsSimple[6].Security != null
                   && this.TabsSimple[6].Security.Name == baseSource.Security.Name)
                {
                    coeff = _contangoCoefficient7.ValueDecimal;
                }
                if (this.TabsSimple[7].Security != null
                    && this.TabsSimple[7].Security.Name == baseSource.Security.Name)
                {
                    coeff = _contangoCoefficient8.ValueDecimal;
                }
                if (this.TabsSimple[8].Security != null
                    && this.TabsSimple[8].Security.Name == baseSource.Security.Name)
                {
                    coeff = _contangoCoefficient9.ValueDecimal;
                }
                if (this.TabsSimple[9].Security != null
                    && this.TabsSimple[9].Security.Name == baseSource.Security.Name)
                {
                    coeff = _contangoCoefficient10.ValueDecimal;
                }
            }

            decimal contangoAbs = (futuresSource.PriceBestBid / coeff) - baseSource.PriceBestAsk;
            decimal contangoPercent = contangoAbs / (baseSource.PriceBestAsk / 100);

            value.ContangoPercent = contangoPercent;
            value.LastTimeUpdate = futuresSource.TimeServerCurrent;

            _contangoValues = _contangoValues.OrderBy(x => x.ContangoPercent).ToList();
        }

        private int GetContangoStage(string secName)
        {
            for (int i = 0; i < _contangoValues.Count; i++)
            {
                if (_contangoValues[i].SecurityName == secName)
                {
                    if(i <= _contangoFilterCountSecurities.ValueInt)
                    {
                        return 1;
                    }
                    else if (i >= _contangoValues.Count - _contangoFilterCountSecurities.ValueInt)
                    {
                        return 2;
                    }
                    else
                    {
                        return 0;
                    }
                }
            }

            return 0;
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

            if(servers == null
                || servers.Count == 0)
            {
                SendNewLogMessage("Сначала подключите коннектор к Т-Инвестиции",Logging.LogMessageType.Error);
                return;
            }

            if(servers.Find(s => s.ServerType == ServerType.TInvest) == null)
            {
                SendNewLogMessage("Сначала подключите коннектор к Т-Инвестиции", Logging.LogMessageType.Error);
                return;
            }

            // 2 номер портфеля должен быть указан

            string portfolioName = _portfolioNum.ValueString;

            if(string.IsNullOrEmpty(portfolioName) == true)
            {
                SendNewLogMessage("Не указан портфель для развёртывания источников", Logging.LogMessageType.Error);
                return;
            }

            Portfolio myPortfolio = null;
            AServer myServer = null;

            for(int i = 0;i < servers.Count;i++)
            {
                if (servers[i].ServerType != ServerType.TInvest)
                {
                    continue;
                }

                List<Portfolio> portfoliosInServer = servers[i].Portfolios;

                if(portfoliosInServer == null
                    || portfoliosInServer.Count == 0)
                {
                    continue;
                }

                for(int j = 0;j < portfoliosInServer.Count;j++)
                {
                    if (portfoliosInServer[j].Number == portfolioName)
                    {
                        myServer = servers[i];
                        myPortfolio = portfoliosInServer[j];
                        break;
                    }
                }

                if(myServer != null)
                {
                    break;
                }
            }

            if(myServer == null)
            {
                SendNewLogMessage("Не найден портфель и сервер. Возможно указан не верный портфель", Logging.LogMessageType.Error);
                return;
            }

            // 3 фьючерсная площадка и спот, должны быть подключены к коннектору

            List<Security> securitiesAll = myServer.Securities;

            if(securitiesAll == null 
                || securitiesAll.Count == 0)
            {
                SendNewLogMessage("В коннекторе не найдены бумаги. Возможно он не подключен", Logging.LogMessageType.Error);
                return;
            }

            if(securitiesAll.Find(s => s.SecurityType == SecurityType.Futures) == null)
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

            Security spotSberPref = securitiesAll.Find(s => s.Name == "SBERP" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresSberPref =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("SPH") || s.Name.StartsWith("SPM")
                || s.Name.StartsWith("SPZ") || s.Name.StartsWith("SPU")));

            SetSecurities(_base2, _futs2, spotSberPref, futuresSberPref, myPortfolio, myServer);

            Security spotGazp = securitiesAll.Find(s => s.Name == "GAZP" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresGazp =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("GZH") || s.Name.StartsWith("GZM")
                || s.Name.StartsWith("GZZ") || s.Name.StartsWith("GZU")));

            SetSecurities(_base3, _futs3, spotGazp, futuresGazp, myPortfolio, myServer);

            Security spotRosn = securitiesAll.Find(s => s.Name == "ROSN" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresRosn =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("RNH") || s.Name.StartsWith("RNM")
                || s.Name.StartsWith("RNZ") || s.Name.StartsWith("RNU")));

            SetSecurities(_base4, _futs4, spotRosn, futuresRosn, myPortfolio, myServer);

            Security spotLkoh = securitiesAll.Find(s => s.Name == "LKOH" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresLkoh =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("LKH") || s.Name.StartsWith("LKM")
                || s.Name.StartsWith("LKZ") || s.Name.StartsWith("LKU")));

            SetSecurities(_base5, _futs5, spotLkoh, futuresLkoh, myPortfolio, myServer);

            Security spotVtb = securitiesAll.Find(s => s.Name == "VTBR" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresVtb =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("VBH") || s.Name.StartsWith("VBM")
                || s.Name.StartsWith("VBZ") || s.Name.StartsWith("VBU")));

            SetSecurities(_base6, _futs6, spotVtb, futuresVtb, myPortfolio, myServer);

            Security spotGmk = securitiesAll.Find(s => s.Name == "GMKN" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresGmk =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("GKH") || s.Name.StartsWith("GKM")
                || s.Name.StartsWith("GKZ") || s.Name.StartsWith("GKU")));

            SetSecurities(_base7, _futs7, spotGmk, futuresGmk, myPortfolio, myServer);

            Security spotAlrs = securitiesAll.Find(s => s.Name == "ALRS" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresAlrs =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("ALH") || s.Name.StartsWith("ALM")
                || s.Name.StartsWith("ALZ") || s.Name.StartsWith("ALU")));

            SetSecurities(_base8, _futs8, spotAlrs, futuresAlrs, myPortfolio, myServer);

            Security spotAflt = securitiesAll.Find(s => s.Name == "AFLT" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresAflt =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
               (s.Name.StartsWith("AFH") || s.Name.StartsWith("AFM")
                || s.Name.StartsWith("AFZ") || s.Name.StartsWith("AFU")));

            SetSecurities(_base9, _futs9, spotAflt, futuresAflt, myPortfolio, myServer);

            Security spotMgnt = securitiesAll.Find(s => s.Name == "MGNT" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresMgnt =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("MNH") || s.Name.StartsWith("MNM")
                || s.Name.StartsWith("MNZ") || s.Name.StartsWith("MNU")));

            SetSecurities(_base10, _futs10, spotMgnt, futuresMgnt, myPortfolio, myServer);
        }

        private void SetSecurities(BotTabSimple tabSpot, BotTabScreener tabFutures, 
            Security spotSecurity, List<Security> futuresSecurity, Portfolio portfolio, AServer server)
        {
            if(spotSecurity == null || futuresSecurity == null)
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

            for (int i = 0;i < futuresSecurity.Count;i++)
            {
                ActivatedSecurity sec = new ActivatedSecurity();
                sec.SecurityClass = futuresSecurity[i].NameClass;
                sec.SecurityName = futuresSecurity[i].Name;
                sec.IsOn = true;
                securitiesToScreener.Add(sec);
            }

            for(int i = 0;i < securitiesToScreener.Count;i++)
            {
                if(tabFutures.SecuritiesNames.Find(s => s.SecurityName == securitiesToScreener[i].SecurityName) == null)
                {
                    tabFutures.SecuritiesNames.Add(securitiesToScreener[i]);
                }
            }

            tabFutures.SaveSettings();
            tabFutures.NeedToReloadTabs = true;
        }

        #endregion

    }

    public class ContangoValue
    {
        public string SecurityName;

        public decimal ContangoPercent;

        public DateTime LastTimeUpdate;

    }
}
