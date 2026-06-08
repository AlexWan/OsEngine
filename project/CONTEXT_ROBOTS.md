# CONTEXT_ROBOTS — Каталог готовых роботов OsEngine

Этот файл содержит описания ~100+ готовых роботов из папки `OsEngine/Robots/`.
Используй его, когда нужно найти похожий пример для заимствования кода.
Базовая архитектура — в `CONTEXT.md`.

## 1. Примеры хороших роботов

### 1.1 Трендовые роботы (`Robots/Trend/`)

Стратегии, торгующие в направлении тренда. Все роботы используют `BotTabSimple` и работают на свечных событиях.

#### 1.1.1 Пробой каналов и полос

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `EnvelopTrend` | Пробой Envelops | `BuyAtStop`/`SellAtStop` на пробой границ; `PositionOpeningSuccesEvent` для установки `CloseAtTrailingStop` сразу после открытия; пересчёт цены активации трейлинг-стопа от границы канала; `ParametrsChangeByUser` с перезагрузкой индикатора |
| `PriceChannelTrade` | Пробой PriceChannel | Реверс позиции: при закрытии Long по пробою нижней границы сразу открывается Short (`SellAtLimit` внутри `LogicClosePosition`); проверка `_lastPriceH > _lastPriceChUp && _lastPriceL < _lastPriceChDown` — исключение противоречивого сигнала; ручное Save/Load настроек через `StreamWriter`/`StreamReader` |
| `BreakLinearRegressionChannel` | Пробой Linear Regression | `BuyAtMarket`/`SellAtMarket` при пробое; выход через `CloseAtStop` по противоположной границе канала; **SMA-фильтры** (`BuySignalIsFiltered`/`SellSignalIsFiltered`): фильтр по положению цены от SMA и по наклону SMA; отмена всех стопов при выходе за время торговли (`CancelStopsAndProfits`); группировка параметров через строку-третий аргумент `CreateParameter` |

#### 1.1.2 Parabolic-стратегии

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `ParabolicSarTrade` | Пробой Parabolic SAR | Реверс по Parabolic SAR: при пересечении SAR закрытие + открытие противоположной позиции; **внешнее управление**: обработчики `ServerTelegram.TelegramCommandEvent` и `ServerVk.VkCommandEvent` — удалённый старт/стоп бота, получение статуса; `Command`/`CommandVk` перечисления |
| `ParabolicPriceChannel` | Parabolic Price Channel | `BuyAtStop`/`SellAtStop` с `StopActivateType.HigherOrEqual`/`LowerOrEqual`; выход через `CloseAtTrailingStop` по Parabolic Stop; **SMA-фильтры** (положение + наклон); время торговли (`StrategyParameterTimeOfDay`); активация/деактивация индикатора (`_smaFilter.IsOn = false`/`true` + `Reload()`) |
| `ParabolicBollinger` | Parabolic Bollinger | Комбинация Bollinger + Parabolic Stop; `BuyAtStop`/`SellAtStop` на пробой Bollinger; `CloseAtTrailingStop` по Parabolic; **SMA-фильтры**; `StrategyParameterLabel` для визуального разделения групп параметров; `IndicatorParameterString` для строкового параметра индикатора (период волатильности) |

#### 1.1.3 Комбинации осцилляторов и скользящих

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `MomentumMacd` | MACD + Momentum | Два индикатора: `MacdLine` (2 DataSeries: up/down) + `Momentum`; вход при пересечении MACD + подтверждении Momentum (>100/<100); реверс позиции: внутри закрытия вызывается открытие противоположной; `DeleteEvent` для удаления файла настроек при удалении робота |
| `SmaStochastic` | SMA + Stochastic | Два индикатора в разных областях (`Prime` + `NewArea0`); вход при пересечении SMA со сдвигом (`_lastSma + Step`) + пересечении уровней Stochastic; **ручное сохранение/загрузка** всех настроек в текстовый файл (`Engine\NameSettingsBot.txt`); WPF-диалог настроек (`ShowIndividualSettingsDialog`) |

#### 1.1.4 Сложные и мульти-таймфреймовые системы

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `StrategyBillWilliams` | Alligator + Fractal + AO | Три индикатора Билла Вильямса: `Alligator` (3 линии), `Fractal` (поиск последнего фрактала вручную в цикле), `AO` (Awesome Oscillator); вход по пробою фрактала + Alligator; **докупка (пирамидинг)**: `_maximumPositions` позиций, `LogicOpenPositionSecondary` — добавление по дивергенции AO (`_secondAo < _lastAo && _secondAo < _thirdAo`); лимитные заявки со сдвигом (`_slippage * PriceStep`); ограничение времени торговли (11:00–18:00) |
| `TwoTimeFramesBot` | Два таймфрейма | **Две вкладки**: `_tabToTrade` (младший ТФ) + `_tabBigTf` (старший ТФ); `TabCreate(BotTabType.Simple)` ×2; вход только при совпадении сигналов: пробой PriceChannel на младшем + цена выше SMA на старшем (`_tabBigTf.CandlesAll`); индикаторы привязаны к разным табам (`_pc` на `_tabToTrade`, `_sma` на `_tabBigTf`) |

**Ключевые паттерны трендовых роботов:**

```csharp
// Отложенное открытие позиции через Стоп-ордер на пробой с активацией HigherOrEqual
_tab.BuyAtStop(volume, price, price + slippage, StopActivateType.HigherOrEqual, 1);

// Трейлинг-стоп сразу после открытия позиции
_tab.PositionOpeningSuccesEvent += (pos) => {
    _tab.CloseAtTrailingStop(pos, activationPrice, orderPrice);
};

// Реверс позиции: закрыть Long и сразу открыть Short
if (position.Direction == Side.Buy && exitSignal) {
    _tab.CloseAtLimit(position, price, volume);
    _tab.SellAtLimit(GetVolume(_tab), price);  // реверс
}

// Две вкладки (два ТФ)
TabCreate(BotTabType.Simple);  // [0] — торговая
TabCreate(BotTabType.Simple);  // [1] — фильтр на старшем ТФ
_tabBigTf = TabsSimple[1];
```

**Изучать:** `EnvelopTrend.cs` — базовый пробой канала с трейлинг-стопом. `BreakLinearRegressionChannel.cs` — пробой с SMA-фильтрами и стоп-ордерами. `StrategyBillWilliams.cs` — мульти-индикаторная система с пирамидингом. `TwoTimeFramesBot.cs` — работа с двумя таймфреймами. `ParabolicSarTrade.cs` — внешнее управление через Telegram/VK.

---

### 1.2 Контртрендовые роботы (`Robots/CounterTrend/`)

Стратегии на отскок от границ канала или экстремальных уровней осцилляторов. Вход против тренда — в зоны перекупленности/перепроданности.

#### 1.2.1 Отскок от полос Боллинджера

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `StrategyBollinger` | Отскок от Bollinger + SMA | **Классика контртренда**: покупка при `Close < BollingerDown`, продажа при `Close > BollingerUp`; выход при пересечении ценой SMA (возврат к среднему); `BotTradeRegime` как публичное поле (не `StrategyParameter`); ручное Save/Load через `StreamWriter`/`StreamReader`; WPF-диалог настроек (`StrategyBollingerUi`); `DeleteEvent` для удаления файла настроек; ограничение времени закрытия (до 18:00) |

#### 1.2.2 Осцилляторные стратегии

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `WilliamsRangeTrade` | Williams %R | **Горизонтальные линии на графике индикатора**: `LineHorisontal` в области `WilliamsArea` (`_tab.SetChartElement`); `_upline.Value = -20`, `_downline.Value = -80` — уровни перекупленности/перепроданности; вход при `_lastWr < _downline` (Buy) / `_lastWr > _upline` (Sell); выход на возврат к противоположному уровню; реверс позиции при выходе; `_upline.Refresh()` / `_downline.Refresh()` для обновления линий на графике |
| `RsiContrtrend` | RSI + SMA (тренд-фильтр) | **Контртренд с фильтром по тренду**: вход только если цена выше SMA (падение в зону перепроданности RSI при восходящем тренде) или ниже SMA (рост в перекупленность при нисходящем); два индикатора в разных областях (`Prime` + `RsiArea`); `LineHorisontal` на RSI для уровней 65/35; выход по возвратному сигналу RSI **или** пересечению SMA — что наступит раньше (`\|\|` в условии); настройка цвета серии индикатора (`_sma.DataSeries[0].Color = Color.CornflowerBlue`) |

#### 1.2.3 Объёмный анализ (кластеры)

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `ClusterCountertrend` | Кластерный контртренд | **Две вкладки разных типов**: `BotTabSimple` (торговля) + `BotTabCluster` (анализ объёмов); `TabCreate(BotTabType.Cluster)`; поиск максимального кластера покупок/продаж за N свечей (`FindMaxVolumeCluster` с `ClusterType.BuyVolume`/`SellVolume`); вход при пробое ценой уровня максимального объёма; автоматический выход противоположной позиции при сигнале; работа с `HorizontalVolumeCluster` и `MaxBuyVolumeLine.Price` |


**Изучать:** `StrategyBollinger.cs` — базовый отскок с ручным Save/Load. `RsiContrtrend.cs` — осциллятор + тренд-фильтр + линии на графике. `ClusterCountertrend.cs` — работа с кластерным табом и объёмным анализом.


### 1.3 Паттерновые стратегии (`Robots/Patterns/`)

Торговля по свечным паттернам, паттернам и уровням.

#### 1.3.1 Свечные паттерны

| Робот | Паттерн | Что почерпнуть |
|-------|---------|----------------|
| `PinBarTrade` | Пин-бар | **Классический пин-бар**: проверка `candle.ShadowUp` / `candle.ShadowDown` относительно `candle.Body`; фильтр по SMA (`_tab.PriceBestBid > smaValue` для Buy); выход через `CloseAtTrailingStopMarket`; настройка минимальной длины тени через `StrategyParameterDecimal` |
| `ThreeSoldier` | Три солдата | **Три свечи подряд в одном направлении**: проверка `candles[i].IsUp` / `IsDown` в цикле; вход на 4-й свече после подтверждения паттерна; простой стоп-лосс в процентах от входа; `BotTradeRegime` для управления режимами торговли |
| `ThreeSoldierVolatilityAdaptive` | Адаптивные три солдата | **Волатильностная адаптация**: расчёт волатильности за N дней (`High - Low` в процентах); автоматическая подстройка параметров `_heightSignalCandle` и `_trailingStopPercent` от волатильности; `AdaptSignalCandleHeight()` вызывается при смене даты; группировка свечей по дате через `candle.TimeStart.Date` |
| `CandlePatternBoost` | Импульс + Van Gerchik | **Паттерн "Буст" за N свечей**: расчёт процентного движения за `_candleForBoost` свечей относительно канала Ван-Герчика; два фильтра SMA — позиционный (`_lastPrice < lastSma`) и по наклону (`lastSma < previousSma`); два режима выхода — трейлинг-стоп или по количеству свечей; `ParametrsChangeByUser` для динамического обновления индикаторов; `Reload()` индикаторов при изменении параметров |

#### 1.3.2 Уровни и паттерны

| Робот | Паттерн | Что почерпнуть |
|-------|---------|----------------|
| `PivotPointsRobot` | Pivot Points (R1/S1, R3/S3) | **Торговля на пробой уровней**: индикатор `PivotFloor` с сериями данных (R1=`DataSeries[1]`, S1=`DataSeries[4]`, R3=`DataSeries[3]`, S3=`DataSeries[6]`); вход при пересечении цены уровня (`Close > R1 && Open < R1`); выход по R3/S3 или стопу в процентах; WPF-диалог настроек (`PivotPointsRobotUi`) |
| `VolatilityAdaptiveCandlesTrader` | Адаптивные свечи | **Адаптация под волатильность**: расчёт средней волатильности за N дней в процентах; автоматическая подстройка `_heightSignalCandle` и `_trailingStopPercent`; вход только если размер свечи > порога в % от цены; проверка бычьей/медвежьей свечи через `Open < Close` / `Open > Close`; выход через `CloseAtTrailingStop` |
| `CustomCandlesImpulseTrader` | Импульс N свечей | **Серия свечей в одном направлении за заданное время**: проверка `IsUp`/`IsDown` в цикле; ограничение по времени между первой и последней свечой паттерна (`TimeSpan.TotalSeconds`); запись времени входа в `SignalTypeOpen` для последующего выхода; выход по количеству свечей после входа (`endCandlesFromOpenPosition`); `DateTime.ParseExact` для парсинга времени из `SignalTypeOpen` |

**Изучать:** `PinBarTrade.cs` — классический пин-бар с фильтром по SMA. `ThreeSoldierVolatilityAdaptive.cs` — адаптация параметров по волатильности. `CandlePatternBoost.cs` — сложный паттерн с фильтрами SMA и двумя режимами выхода. `CustomCandlesImpulseTrader.cs` — импульс с ограничением по времени.

### 1.4 Скринеры (`Robots/Screeners/`)

Торговля по множеству инструментов одновременно. Используется `BotTabScreener` — специальный тип таба, который автоматически создаёт отдельные вкладки для каждого инструмента из списка.

#### 1.4.1 Паттерновые скринеры

| Робот | Паттерн | Что почерпнуть |
|-------|---------|----------------|
| `PinBarScreener` | Пин-бар + SMA | **Базовый скринер пин-баров**: проверка входа в верхнюю/нижнюю треть диапазона (`lastClose >= lastHigh - ((lastHigh - lastLow) / 3)`); фильтр по высоте свечи в % (`lenCandlePercent`); фильтр по SMA (ручной расчёт `Sma()`); ограничение `_maxPositions` на количество одновременных позиций; выход через `CloseAtTrailingStop` |
| `PinBarVolatilityScreener` | Адаптивный пин-бар | **Волатильностная адаптация на инструмент**: класс `SecuritiesVolatilitySettings` для хранения настроек на каждый инструмент (`SecName`, `SecClass`, `HeightPinBar`); сохранение/загрузка в файл через `GetSaveString()`/`LoadFromString()`; `AdaptPinBarHeight()` вызывается раз в день; SMA-фильтр по наклону (`smaValue < smaPrev`); стоп в % от высоты паттерна; `CloseAtTrailingStopMarket` |
| `ThreeSoldierAdaptiveScreener` | Три солдата (адаптивный) | **Контртренд на три солдата**: проверка трёх свечей подряд в одном направлении; расчёт общего движения за 3 свечи в %; адаптация `_heightSoldiers` и `_minHeightOneSoldier` по волатильности; класс `SecuritiesTradeSettings` с сохранением на диск; фильтр SMA по наклону; выход через `CloseAtStop` + `CloseAtProfit` с расчётом от высоты паттерна |

#### 1.4.2 Индикаторные скринеры

| Робот | Индикаторы | Что почерпнуть |
|-------|------------|----------------|
| `SmaScreener` | SMA | **Простейший скринер**: вход если N свечей подряд выше SMA; ограничение `_maxPositions`; выход по трейлинг-стопу; базовая структура скринера для новичков |
| `BollingerMomentumScreener` | Bollinger + Momentum | **Пробой Bollinger с моментумом**: вход при `lastCandleClose > lastUpBollingerLine && lastMomentum > _minMomentumValue`; динамическое обновление параметров индикатора (`bollinger.ParametersDigit[0].Value = _bollingerLen.ValueInt`); `Reload()` индикатора; выход через `CloseAtTrailingStop` |
| `LinearRegressionFastScreener` | LinearRegression + ADX + SMA | **Три индикатора одновременно**: создание через `_screenerTab.CreateCandleIndicator(id, name, parameters, area)`; ADX-фильтр (`adxLast < _minAdxValue || adxLast > _maxAdxValue`); SMA-фильтр положения; вход по пробою верхней линии канала (`candleClose > lrUp`); выход по пробою нижней (`lastCandleClose < lrDown`); `BuyAtIcebergMarket` / `CloseAtIcebergMarket`; ограничение по времени торговли (`_timeStart`/`_timeEnd`) |
| `PriceChannelAdaptiveRsiScreener` | PriceChannelAdaptive + RSI + SMA | **RSI-фильтр + PriceChannel**: вход только если `rsi.Last > _minRsiValueToEntry` (высокий RSI); пробой `pcUp`; SMA-фильтр по наклону; выход через `CloseAtTrailingStopMarket(pos, pcDown)`; обновление параметров индикаторов через `ParametrsChangeByUser` |

#### 1.4.3 Специализированные скринеры

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `PlateDetectorScreener` | Анализ стакана | **Событие MarketDepthUpdateEvent**: подписка `_tabScreener.MarketDepthUpdateEvent += ...`; анализ объёмов в стакане (`md.Bids[0].Bid`); соотношение объёмов (`bestBidVolume / curVolume`); вход лимитным ордером над лучшим бидом; отмена ордера по времени (`_orderLifeTime`); стоп/профит в %; `StopOrderRedLine` для уровня стопа; `CloseAllOrderToPosition` для отмены; `ManualPositionSupport.DisableManualSupport()` |
| `PumpDetectorScreener` | Обнаружение пампа | **Резкий рост объёма/цены**: детектирование аномального движения; быстрый вход и выход; работа с высоковолатильными инструментами |

**Ключевые паттерны скринеров:**

```csharp
// Базовая структура скринера
TabCreate(BotTabType.Screener);
BotTabScreener _tabScreener = TabsScreener[0];
_tabScreener.CandleFinishedEvent += _tabScreener_CandleFinishedEvent;

// Обработчик получает tab конкретного инструмента
private void _tabScreener_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
{
    List<Position> positions = tab.PositionsOpenAll;
    
    if (positions.Count == 0) {
        LogicOpenPosition(candles, tab);
    } else {
        LogicClosePosition(candles, tab, positions[0]);
    }
}

// Ограничение на количество позиций across all tabs
if (_tabScreener.PositionsOpenAll.Count >= _maxPositions.ValueInt) {
    return;
}

// Создание индикаторов на скринере
_screenerTab.CreateCandleIndicator(1, "ADX", new List<string>() { "30" }, "Second");
_screenerTab.CreateCandleIndicator(2, "LinearRegressionChannelFast_Indicator", 
    new List<string>() { "50", "Close", "2", "2" }, "Prime");
_screenerTab.CreateCandleIndicator(3, "Sma", new List<string>() { "100", "Close" }, "Prime");

// Динамическое обновление параметров индикатора
Aindicator bollinger = (Aindicator)tab.Indicators[0];
if (bollinger.ParametersDigit[0].Value != _bollingerLen.ValueInt) {
    bollinger.ParametersDigit[0].Value = _bollingerLen.ValueInt;
    bollinger.Save();
    bollinger.Reload();
}

// MarketDepthUpdateEvent для анализа стакана
_tabScreener.MarketDepthUpdateEvent += _tabScreener_MarketDepthUpdateEvent;
private void _tabScreener_MarketDepthUpdateEvent(MarketDepth marketDepth, BotTabSimple tab)
{
    MarketDepth md = marketDepth.GetCopy();
    decimal bestBidVolume = md.Bids[0].Bid.ToDecimal();
    
    // Проверка соотношения объёмов для обнаружения "плиты"
    for (int i = 1; i < md.Bids.Count; i++) {
        decimal curVolume = md.Bids[i].Bid.ToDecimal();
        decimal ratio = bestBidVolume / curVolume;
        if (ratio < _bestBidMinRatioToAll.ValueDecimal) {
            return;  // не плита
        }
    }
    
    tab.BuyAtLimit(volume, md.Bids[0].Price + tab.Security.PriceStep);
}

// Выход по стопу и профиту от высоты паттерна
decimal heightPattern = Math.Abs(tab.CandlesAll[tab.CandlesAll.Count - 4].Open - 
                                  tab.CandlesAll[tab.CandlesAll.Count - 2].Close);
decimal priceStop = _lastPrice - (heightPattern * _procHeightStop.ValueDecimal) / 100;
decimal priceTake = _lastPrice + (heightPattern * _procHeightTake.ValueDecimal) / 100;
tab.CloseAtStop(position, priceStop, slippage);
tab.CloseAtProfit(position, priceTake, slippage);

// Айсберг-ордера на скринере
tab.BuyAtIcebergMarket(GetVolume(tab), _icebergOrdersCount.ValueInt, 2000);
tab.CloseAtIcebergMarket(pos, pos.OpenVolume, _icebergOrdersCount.ValueInt, 2000);
```

**Изучать:** `SmaScreener.cs` — простейший скринер для понимания базовой структуры. `PinBarScreener.cs` — паттерн пин-бар с ограничением позиций. `PinBarVolatilityScreener.cs` — адаптация на инструмент с сохранением настроек. `LinearRegressionFastScreener.cs` — работа с тремя индикаторами и айсбергами. `PlateDetectorScreener.cs` — анализ стакана через `MarketDepthUpdateEvent`.

---

### 1.5 Продвинутое управление позициями (`Robots/PositionsMicromanagement/`)

Роботы, демонстрирующие сложные техники управления позицией: усреднение, пирамидинг, частичное закрытие, кастомные айсберги, работу с несколькими позициями.

#### 1.5.1 Частичное закрытие и многоуровневые тейки

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `UnsafeLimitsClosingSample` | Контртренд на Envelops | `CloseAtLimitUnsafe` — выставление двух лимитных ордеров на закрытие частями; пересчёт остатка по `executeCloseOrdersCount`; стоп и профит в процентах от входа |
| `CandlesTurnaroundPattern` | Разворот по свечам + ATR | Трёхэтапный выход (`CloseAtLimit` 1/3 → 1/3 → остаток); отслеживание `executeCloseOrdersCount` через `CloseOrders`; вход по `Body` свечи и ATR |
| `PriceChannelCounterTrend` | Контртренд на PriceChannel | **Несколько позиций одновременно**: открытие двух позиций с разными `SignalTypeOpen` ("First"/"Second"); разные уровни тейка для каждой позиции; проход по `PositionsOpenAll` в цикле |

#### 1.5.2 Усреднение (усреднение убытка)

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `UnsafeAveragePosition` | Контртренд на Envelops | `BuyAtLimitToPositionUnsafe` / `SellAtLimitToPositionUnsafe` — добавление к позиции на двух уровнях; отслеживание `executeOpenOrdersCount`; стоп и профит в процентах |
| `EnvelopsCountertrend` | Контртренд на Envelops | **Усреднение через StopMarket**: `BuyAtStopMarket` / `SellAtStopMarket` с `StopActivateType.LowerOrEqual`; усреднение на 2 уровнях; пересчёт средней цены входа `middleEntryPrice` по всем позициям; стоп и профит от средней цены |
| `AlligatorTrendAverage` | Тренд на Alligator | **Усреднение + пирамидинг + стандартный выход**: `BuyAtMarketToPosition` при откате (усреднение); `BuyAtMarketToPosition` при продолжении тренда (пирамидинг по `_pyramidPercent`); закрытие по противоположному сигналу Alligator; использование `Position.Comment` для маркировки операций |

#### 1.5.3 Мульти-позиционные стратегии

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `TwoEntrySample` | Тренд: PriceChannel + Envelops | **Две независимые позиции**: проверка `SignalTypeOpen` ("PriceChannel"/"Envelops"); открытие второй позиции только если первая от другого индикатора; каждая позиция закрывается по своему трейлинг-стопу |

#### 1.5.4 Кастомные айсберг-ордера

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `CustomIcebergSample` | Контртренд на Bollinger | **Собственный класс `IcebergMaker`** с `Thread`: разбиение объёма на N частей, округление через `Security.DecimalsVolume`, корректировка остатка на первый ордер; айсберг и на вход, и на выход; отличие поведения в тестере (`BuyAtMarket`) от реального торгов (`IcebergMaker.Start()`) |

**Ключевые паттерны управления позицией:**

```csharp
// Частичное закрытие: сколько ордеров уже исполнилось
int executed = 0;
for (int i = 0; position.CloseOrders != null && i < position.CloseOrders.Count; i++)
{
    if (position.CloseOrders[i].State == OrderStateType.Done)
        executed++;
}

// Пересчёт средней цены входа по нескольким позициям
decimal middlePrice = 0, allVolume = 0;
for (int i = 0; i < positions.Count; i++)
{
    middlePrice += positions[i].EntryPrice * positions[i].OpenVolume;
    allVolume += positions[i].OpenVolume;
}
middlePrice = middlePrice / allVolume;

// Усреднение: добавление к позиции
_tab.BuyAtLimitToPositionUnsafe(position, price, volume);

// Пирамидинг: докупка по тренду
_tab.BuyAtMarketToPosition(position, GetVolume(_tab));

// Маркировка операций через Comment
if (position.Comment.Contains("Average") == false)
{
    position.Comment += "Average";
    _tab.BuyAtMarketToPosition(position, volume);
}
```

**Изучать:** `UnsafeLimitsClosingSample.cs` — базовое частичное закрытие. `EnvelopsCountertrend.cs` — усреднение через StopMarket. `CustomIcebergSample.cs` — кастомный айсберг. `AlligatorTrendAverage.cs` — комбинация усреднения, пирамидинга и стандартного выхода.

---

### 1.6 Технические примеры (`Robots/TechSamples/`)

Примеры работы с API OsEngine, демонстрация технических возможностей платформы.

#### 1.6.1 Работа с индикаторами

| Робот | Что демонстрирует | Ключевые API |
|-------|-------------------|--------------|
| `CustomDataInIndicatorSample` | Запись собственных данных в индикатор | `EmptyIndicator`, `DataSeries[0].Values[index] = value`, `RePaint()` |
| `BlockIndicatorsSample` | Блокировка/разблокировка индикаторов | `Indicator.IsOn = bool`, `Reload()` через параметры `StrategyParameterBool` |
| `BlockIndicatorsOnScreenerSample` | Блокировка индикаторов на скринере | Доступ к индикаторам через `tab.Indicators[n]` в `CandleFinishedEvent`, `IsOn`, `Reload()` |

**Паттерн: Запись данных в EmptyIndicator**
```csharp
_indicatorEmpty = IndicatorsFactory.CreateIndicatorByName("EmptyIndicator", name, false);
_indicatorEmpty = (Aindicator)_tab.CreateCandleIndicator(_indicatorEmpty, "SecondArea");

private void _tab_CandleFinishedEvent(List<Candle> candles)
{
    decimal dataPoint = candles[candles.Count - 1].Close / 2;
    _indicatorEmpty.DataSeries[0].Values[_indicatorEmpty.DataSeries[0].Values.Count-1] = dataPoint;
    _indicatorEmpty.RePaint();
}
```

**Паттерн: Блокировка индикаторов**
```csharp
// В простом табе
Aindicator bollinger = (Aindicator)_tab.Indicators[0];
if (_bollingerIsOn.ValueBool != bollinger.IsOn) {
    bollinger.IsOn = _bollingerIsOn.ValueBool;
    bollinger.Reload();
}

// На скринере (в CandleFinishedEvent)
private void _screenerSource_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab) {
    Aindicator bollinger = (Aindicator)tab.Indicators[0];
    if (_bollingerIsOn.ValueBool != bollinger.IsOn) {
        bollinger.IsOn = _bollingerIsOn.ValueBool;
        bollinger.Reload();
    }
}
```

---

#### 1.6.2 Визуальные элементы на графике

| Робот | Что демонстрирует | Ключевые API |
|-------|-------------------|--------------|
| `ElementsOnChartSampleBot` | Рисование трёх типов элементов на чарте | `PointElement`, `LineHorisontal`, `Line` (наклонная), `SetChartElement()`, `DeleteChartElement()`, `DeleteAllChartElement()` |
| `TradeLineExample` | Торговля по наклонным уровням ZigZag | Построение наклонной линии через `Line`, расчёт шага `stepCorner = (high3 - high2) / (index3 - index2 + 1)`, `MarkerStyle.Star4` для точек |
| `FakeOutExample` | Ложный пробой + уровни | `LineHorisontal` для уровней, `PointElement` для экстремумов, локальные High/Low через цикл по свечам |

**Паттерн: Точка на графике**
```csharp
PointElement point = new PointElement("Some label", "Prime");
point.Y = candles[candles.Count - 2].Close;
point.TimePoint = candles[candles.Count - 2].TimeStart;
point.Label = "Some label";
point.Font = new Font("Arial", 10);
point.LabelTextColor = Color.White;
point.LabelBackColor = Color.Blue;
point.Color = Color.Red;
point.Style = MarkerStyle.Star4;
point.Size = 12;
_tab.SetChartElement(point);
```

**Паттерн: Горизонтальная линия**
```csharp
LineHorisontal line = new LineHorisontal("Some line", "Prime", false);
line.Value = candles[candles.Count - 1].Close;
line.TimeStart = candles[0].TimeStart;
line.TimeEnd = candles[candles.Count-1].TimeStart;
line.CanResize = true;
line.Color = Color.White;
line.LineWidth = 3;
line.Label = "Some label on Line";
line.Font = new Font("Arial", 10);
line.LabelTextColor = Color.White;
line.LabelBackColor = Color.Green;
_tab.SetChartElement(line);
```

**Паттерн: Наклонная линия**
```csharp
Line line = new Line("Inclined line", "Prime");
line.ValueYStart = candles[candles.Count - 11].Close;
line.TimeStart = candles[candles.Count - 11].TimeStart;
line.ValueYEnd = candles[candles.Count - 1].Close;
line.TimeEnd = candles[candles.Count - 1].TimeStart;
line.Color = Color.Bisque;
line.LineWidth = 3;
_tab.SetChartElement(line);
```

**Паттерн: Обновление линии в конце свечи**
```csharp
private void _tab_CandleFinishedEvent(List<Candle> candles)
{
    if (_lineOnPrimeChart != null) {
        _lineOnPrimeChart.TimeEnd = candles[candles.Count - 1].TimeStart;
        _lineOnPrimeChart.Refresh();
    }
}
```

---

#### 1.6.3 Кастомизация окна параметров

| Робот | Что демонстрирует | Ключевые API |
|-------|-------------------|--------------|
| `VisualSettingsParametersExample` | Цветовое оформление параметров | `ParamGuiSettings.SetForeColorParameter()`, `SetSelectionColorParameter()`, `SetBorderUnderParameter()`, `RePaintParameterTables()` |
| `CustomParamsUseBotSample` | Кастомная вкладка с таблицей | `ParamGuiSettings.CreateCustomTab()`, `WindowsFormsHost`, `DataGridView`, `DataGridFactory.GetDataGridView()` |
| `CustomTableInTheParamWindowSample` | Динамическая таблица на скринере | `DataGridView.CellValueChanged`, сохранение/загрузка строк таблицы в файл, `ComboBoxCell` для выбора Side |
| `CustomChartInParamWindowSample` | График в окне параметров | `Chart` (System.Windows.Forms.DataVisualization), `Series`, `ChartArea`, `TextAnnotation`, отдельный `Thread` для обновления |

**Паттерн: Цветовое оформление параметров**
```csharp
// Цвет текста параметра
this.ParamGuiSettings.SetForeColorParameter("VolumeLong", Color.Green);
this.ParamGuiSettings.SetForeColorParameter("VolumeShort", Color.DarkRed);

// Цвет выделения
this.ParamGuiSettings.SetSelectionColorParameter("VolumeLong", Color.LightGreen);

// Разделительная линия
this.ParamGuiSettings.SetBorderUnderParameter("Regime", Color.LightGray, 1);

// Перерисовка
this.ParamGuiSettings.RePaintParameterTables();
```

**Паттерн: Кастомная вкладка с таблицей**
```csharp
// Настройка окна параметров
this.ParamGuiSettings.Title = "Custom param gui sample";
this.ParamGuiSettings.Height = 800;
this.ParamGuiSettings.Width = 600;

// Создание вкладки
CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab("Indicators values");

// Создание таблицы
WindowsFormsHost _host = new WindowsFormsHost();
DataGridView grid = DataGridFactory.GetDataGridView(
    DataGridViewSelectionMode.FullRowSelect, 
    DataGridViewAutoSizeRowsMode.AllCells);

// Добавление колонок
DataGridViewColumn col = new DataGridViewColumn();
col.CellTemplate = new DataGridViewTextBoxCell();
col.HeaderText = "Time";
col.ReadOnly = true;
col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
grid.Columns.Add(col);

// Добавление на вкладку
customTab.AddChildren(_host);
```

**Паттерн: График в параметрах (отдельный поток)**
```csharp
// Создание графика
Chart _chart = new Chart();
ChartArea area = new ChartArea("ChartAreaSpread");
area.CursorX.IsUserSelectionEnabled = true;
area.CursorX.IsUserEnabled = true;
_chart.ChartAreas.Add(area);

// События зума и курсора
_chart.AxisScrollBarClicked += chart_AxisScrollBarClicked;
_chart.AxisViewChanged += chart_AxisViewChanged;
_chart.CursorPositionChanged += chart_CursorPositionChanged;
_chart.MouseClick += Chart_MouseClick;

// Отдельный поток для обновления
Thread worker = new Thread(StartPaintChart);
worker.Start();

private void StartPaintChart() {
    while (true) {
        Thread.Sleep(1000);
        LoadValueOnChart();  // добавление точек в Series
        SetSeries(lineSeries);  // отрисовка
    }
}
```

**Паттерн: Annotation на графике**
```csharp
TextAnnotation _annotation = new TextAnnotation {
    Text = $"{xValue}: {yValue}",
    X = 0, Y = -1,
    AnchorX = xValue,
    AnchorY = yValue,
    Font = new Font("Arial", 12, FontStyle.Bold),
    ForeColor = Color.Gray,
    BackColor = Color.Gray,
    LineColor = Color.Gray,
    AnchorAlignment = ContentAlignment.MiddleCenter
};
_chart.Annotations.Add(_annotation);
```

---

#### 1.6.4 Работа с ордерами и позициями

| Робот | Что демонстрирует | Ключевые API |
|-------|-------------------|--------------|
| `ChangePriceBotExtStopMarket` | Изменение цены лимитного ордера | `ChangeOrderPrice(order, newPrice)`, `ManualPositionSupport.DisableManualSupport()`, `MarketDepthUpdateEvent`, пересчёт цены по `%` от BestBid/BestAsk |

**Паттерн: Изменение цены ордера**
```csharp
_tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;
_tab.ManualPositionSupport.DisableManualSupport();

private void _tab_MarketDepthUpdateEvent(MarketDepth md)
{
    Position pos = _tab.PositionsOpenAll[0];
    
    if (pos.State == PositionStateType.Opening) {
        // Изменение цены ордера каждые N секунд
        if (_lastChangeOrderTime.AddSeconds(_seconds) > DateTime.Now) return;
        
        decimal newPrice = _tab.PriceBestBid - _tab.PriceBestBid * (_slippagePercent / 100);
        newPrice = Math.Round(newPrice, _tab.Security.Decimals);
        
        _tab.ChangeOrderPrice(pos.OpenOrders[0], newPrice);
        _lastChangeOrderTime = DateTime.Now;
    }
}
```

---

#### 1.6.5 События и логирование

| Робот | Что демонстрирует | Ключевые API |
|-------|-------------------|--------------|
| `CandlesLoggingSample` | Базовое логирование | `SendNewLogMessage(message, LogMessageType.User/Error)` |
| `StopByTradeFeedSample` | Стоп по ленте сделок | `NewTickEvent(Trade trade)`, трейлинг-стоп по цене сделки, `CloseAtTrailingStop(pos, stopPrice, orderPrice)` |

**Паттерн: Логирование**
```csharp
// Только в окно логов
SendNewLogMessage(message, Logging.LogMessageType.User);

// Как ошибка
SendNewLogMessage(message, Logging.LogMessageType.Error);
```

**Паттерн: Стоп по ленте (NewTickEvent)**
```csharp
_tab.NewTickEvent += _tab_NewTickEvent;

private void _tab_NewTickEvent(Trade trade)
{
    Position myPos = _tab.PositionsOpenAll[0];
    
    if (myPos.Direction == Side.Buy) {
        stopPrice = trade.Price - (trade.Price * (_trailStopPercent / 100));
        orderPrice = stopPrice - _slippage * _tab.Security.PriceStep;
    } else {
        stopPrice = trade.Price + (trade.Price * (_trailStopPercent / 100));
        orderPrice = stopPrice + _slippage * _tab.Security.PriceStep;
    }
    
    _tab.CloseAtTrailingStop(myPos, stopPrice, orderPrice);
}
```

---

#### 1.6.6 Работа с открытым интересом

| Робот | Что демонстрирует | Ключевые API |
|-------|-------------------|--------------|
| `OpenInterestBotSample` | Торговля по изменению Open Interest | `Candle.OpenInterest`, сравнение текущего и предыдущего OI, вход при падении OI на заданную величину |

**Паттерн: Анализ Open Interest**
```csharp
private void LogicEntry(List<Candle> candles)
{
    Candle currentCandle = candles[^1];
    Candle prevCandle = candles[^2];
    
    if (currentCandle.OpenInterest == 0 || prevCandle.OpenInterest == 0) return;
    
    decimal currentOi = currentCandle.OpenInterest;
    decimal prevOi = prevCandle.OpenInterest;
    
    // Вход если OI упал
    if (currentOi < prevOi) {
        decimal oiDownSize = prevOi - currentOi;
        if (oiDownSize > _oiDownsizeToEntry.ValueDecimal) {
            _tab.BuyAtMarket(GetVolume(_tab));
        }
    }
}
```

---

#### 1.6.7 Инициализация всех типов табов

| Робот | Что демонстрирует | Ключевые API |
|-------|-------------------|--------------|
| `AllSourcesInOneSample` | Создание всех 7 типов табов | `TabCreate(BotTabType.Simple/Index/Pair/Screener/Polygon/Cluster/News)` |

**Паттерн: Создание всех типов источников**
```csharp
TabCreate(BotTabType.Simple);
TabCreate(BotTabType.Index);
TabCreate(BotTabType.Pair);
TabCreate(BotTabType.Screener);
TabCreate(BotTabType.Polygon);
TabCreate(BotTabType.Cluster);
TabCreate(BotTabType.News);

// Доступ через массивы:
// TabsSimple[0], TabsIndex[0], TabsPair[0], TabsScreener[0],
// TabsPolygon[0], TabsCluster[0], TabsNews[0]
```

---

**Изучать:** `ElementsOnChartSampleBot.cs` — все типы элементов на графике. `CustomParamsUseBotSample.cs` — кастомная таблица в параметрах. `CustomChartInParamWindowSample.cs` — график в реальном времени. `StopByTradeFeedSample.cs` — работа с NewTickEvent. `ChangePriceBotExtStopMarket.cs` — изменение цены ордера.

---


### 1.7 Сеточные стратегии (`Robots/Grids/`)

Сеточные стратегии вынесены в отдельный файл контекста.

**См. `CONTEXT_GRIDS.md`** — полное руководство по созданию сеточных роботов:
- Бойлерплейт сеточного робота
- Справочник API `TradeGrid` (GridCreator, StopAndProfit, TrailingUp, StopBy)
- Каталог 8 сеточных роботов с разбором кода
- Примеры из `GridBollinger`, `GridTwoSides`, `GridLinearRegression`
- Частые ошибки и быстрый справочник

### 1.8 Учебные роботы (`Robots/BotsFromStartLessons/`)

Пошаговый курс «C# для алготрейдера». Каждый робот демонстрирует одну конкретную тему. Рекомендуется изучать по порядку.

#### 1.8.1 Базовые концепции

| Робот | Тема | Что почерпнуть |
|-------|------|----------------|
| `Lesson1HelloWorld` | Кнопка в параметрах | `CreateParameterButton`, обработчик `UserClickOnButtonEvent`, `SendNewLogMessage` |
| `Lesson2Bot1` | Типы данных C# | Работа со строками, int, decimal, bool, DateTime через кнопки и лог |
| `Lesson2Bot2` | Параметры робота | `StrategyParameterString`, `Int`, `Bool`, `Decimal`, `TimeOfDay` — как создавать |

#### 1.8.2 Первая торговля

| Робот | Тема | Что почерпнуть |
|-------|------|----------------|
| `Lesson3Bot1` | Событие свечи | `CandleFinishedEvent`, `IsUp`, `BuyAtMarket`, `CloseAtTrailingStopMarket` |
| `Lesson3Bot2` | Индикатор + лимит | Создание SMA, `BuyAtLimit`, `CloseAtMarket`, проверка `Position.State` |
| `Lesson3Bot3` | Два индикатора | Две SMA (fast/slow), `ParametrsChangeByUser`, `Reload()`/`Save()` индикаторов |

#### 1.8.3 Продвинутая архитектура

| Робот | Тема | Что почерпнуть |
|-------|------|----------------|
| `Lesson4Bot1` | Все события таба | `CandleFinished`, `CandleUpdate`, `OrderUpdate`, `MarketDepthUpdate`, `PositionOpeningSucces`, `NewTick` + `GetVolume` |
| `Lesson5Bot1` | Время в позиции | `Position.TimeOpen`, `AddMinutes()`, двунаправленная торговля (Long/Short) |
| `Lesson5Bot2` | Три индикатора | Alligator + PriceChannel + AO, докупка `BuyAtMarketToPosition` |
| `Lesson6Bot1` | Пирамидинг | 3 последовательных входа `BuyAtStop`, расчёт цены через ATR, `CloseAtTrailingStop` на все позиции |
| `Lesson7Bot1` | Адаптивность | Ручной расчёт SMA, волатильность за N дней, адаптивные параметры, время торговли `TimeOfDay` |

#### 1.8.4 Стакан и потоки

| Робот | Тема | Что почерпнуть |
|-------|------|----------------|
| `Lesson8Bot1` | Стакан + поток | `Thread` в конструкторе, `MarketDepth`, `Bids[0].Bid`, `CloseAtStopMarket` + `CloseAtProfitMarket` |
| `Lesson8Bot2` | Ручные экстремумы | Самостоятельный расчёт High/Low за период, `BuyAtStop`, `CloseAtTrailingStopMarket` |

#### 1.8.5 Справочник ордеров (Lesson 9)

Эти роботы не торгуют сами — они демонстрируют **все методы API** через кнопки.

| Робот | Тема | Что почерпнуть |
|-------|------|----------------|
| `Lesson9Bot1` | Входы в позицию | `BuyAtMarket`, `BuyAtLimit`, `BuyAtIceberg`, `BuyAtFake`, `BuyAtStop` + Sell-аналоги |
| `Lesson9Bot2` | Добавление к позиции | `BuyAtLimitToPosition`, `BuyAtLimitToPositionUnsafe`, `BuyAtMarketToPosition`, `BuyAtIcebergToPosition` |
| `Lesson9Bot3` | Закрытие позиции | `CloseAllAtMarket`, `CloseAtMarket`, `CloseAtLimit`, `CloseAtLimitUnsafe`, `CloseAtIceberg`, `CloseAtFake` |
| `Lesson9Bot4` | Стопы и профиты | `CloseAtStop`, `CloseAtTrailingStop`, `CloseAtProfit` (Limit и Market) + отмена |
| `Lesson9Bot5` | Управление ордерами | `CloseAllOrderToPosition`, `CloseAllOrderInSystem`, `CloseOrder`, `ChangeOrderPrice` |

**Изучать:** По порядку от Lesson1 к Lesson9. Lesson9Bot1-5 — как интерактивный справочник API.

---

