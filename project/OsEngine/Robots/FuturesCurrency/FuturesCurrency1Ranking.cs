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

namespace OsEngine.Robots.FuturesCurrency
{
    /*  [Bot("FuturesCurrency1Ranking")]
      public class FuturesCurrency1Ranking : BotPanel
      {
          BotTabSimple _base1;
          BotTabScreener _futs1;

          BotTabSimple _base2;
          BotTabScreener _futs2;

          BotTabSimple _base3;
          BotTabScreener _futs3;

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

          private StrategyParameterBool _tradeRegimeSecurity1;
          private StrategyParameterBool _tradeRegimeSecurity2;
          private StrategyParameterBool _tradeRegimeSecurity3;

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

          public FuturesCurrency1Ranking(string name, StartProgram startProgram) : base(name, startProgram)
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

              StrategyParameterButton buttonShowContango = CreateParameterButton("Show contango", "Contango");
              buttonShowContango.UserClickOnButtonEvent += ButtonShowContango_UserClickOnButtonEvent;

              _tradeRegimeSecurity1 = CreateParameter("Trade security 1", true, "Trade securities");
              _tradeRegimeSecurity2 = CreateParameter("Trade security 2", true, "Trade securities");
              _tradeRegimeSecurity3 = CreateParameter("Trade security 3", true, "Trade securities");

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


                  if (_contangoValues.Count > 0)
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
                  SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
              }

          }

          private void FuturesStartContangoScreener_ParametrsChangeByUser()
          {
              UpdateSettingsInIndicators(_base1, _futs1);
              UpdateSettingsInIndicators(_base2, _futs2);
              UpdateSettingsInIndicators(_base3, _futs3);
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
                  SetContangoValues(baseSource, futuresSource);
              }

              if (CanTradeThisSecurity(baseSource.Security.Name) == false)
              {
                  return;
              }

              if(_contangoValues.Count <2)
              {
                  return;
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

              for (int i = 0; i < _contangoValues.Count; i++)
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

              if (_contangoFilterRegime.ValueString == "On_MOEXStocksAuto")
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
                  if (this.TabsSimple[0].Security != null
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
                      if (i <= _contangoFilterCountSecurities.ValueInt)
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

              Security spotSber = securitiesAll.Find(s => s.Name == "USDRUB_TOM" && s.SecurityType == SecurityType.CurrencyPair);

              List<Security> futuresSber =
                  securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                  (s.Name.StartsWith("SiH") || s.Name.StartsWith("SiM")
                  || s.Name.StartsWith("SiZ") || s.Name.StartsWith("SiU")));

              SetSecurities(_base1, _futs1, spotSber, futuresSber, myPortfolio, myServer);

              Security spotSberPref = securitiesAll.Find(s => s.Name == "EURRUB_TOM" && s.SecurityType == SecurityType.CurrencyPair);
              List<Security> futuresSberPref =
                  securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                  (s.Name.StartsWith("EuH") || s.Name.StartsWith("EuM")
                  || s.Name.StartsWith("EuZ") || s.Name.StartsWith("EuU")));

              SetSecurities(_base2, _futs2, spotSberPref, futuresSberPref, myPortfolio, myServer);

              Security spotGazp = securitiesAll.Find(s => s.Name == "CNYRUB_TOM" && s.SecurityType == SecurityType.CurrencyPair);
              List<Security> futuresGazp =
                  securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                  (s.Name.StartsWith("CRH") || s.Name.StartsWith("CRM")
                  || s.Name.StartsWith("CRZ") || s.Name.StartsWith("CRU")));

              SetSecurities(_base3, _futs3, spotGazp, futuresGazp, myPortfolio, myServer);

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

      public class ContangoValue
      {
          public string SecurityName;

          public decimal ContangoPercent;

          public DateTime LastTimeUpdate;

      }*/
}
