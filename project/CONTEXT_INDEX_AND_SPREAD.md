# CONTEXT — Индексные и спредовые стратегии (`BotTabIndex`)

> **Область**: Роботы, строящие индекс / спред из нескольких инструментов и торгующие на основе его динамики.
> **Ключевое правило**: `BotTabIndex` — **только для анализа**. Открывать позиции напрямую через `_indexTab.BuyAtMarket()` нельзя. Реальные сделки совершаются на `BotTabSimple` или `BotTabScreener`.

---

## 1.1 Что такое `BotTabIndex`

`BotTabIndex` — это таб, на котором формируется **свечной график индекса или спреда** по формуле из нескольких бумаг. В терминале OsEngine пользователь задаёт формулу вида:

```
(A0 + A1 + A2) / 3
```

или

```
A0 - A1
```

Под A - имеется ввиду какая-то из выбранных бумаг в пользовательском интерфейсе. А номер соответствует номеру инструмента в списке подключенных инструментов в индекс.

Робот получает готовые свечи индекса через событие `SpreadChangeEvent` и принимает торговые решения.

**Типичная архитектура робота на индексе:**

| Компонент | Назначение |
|-----------|------------|
| `BotTabIndex` | Получение свечей индекса, построение индикаторов на индексе |
| `BotTabSimple` (1-2 шт.) | Торговля конкретными бумагами, входы/выходы |
| `BotTabScreener` | Торговля портфелем бумаг против индекса |

---

## 1.2 Бойлерплейт робота на `BotTabIndex`

```csharp
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.IndexArbitrage
{
    [Bot("IndexSpreadSample")]
    public class IndexSpreadSample : BotPanel
    {
        // 1) Таб индекса — ТОЛЬКО для анализа
        private BotTabIndex _indexTab;

        // 2) Табы для реальной торговли
        private BotTabSimple _tab1;
        private BotTabSimple _tab2;

        // Индикатор на индексе
        private Aindicator _sma;

        // Параметры
        private StrategyParameterString _regime;
        private StrategyParameterInt _slippage;
        private StrategyParameterDecimal _volume;

        public IndexSpreadSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Создаём индексный таб
            TabCreate(BotTabType.Index);
            _indexTab = TabsIndex[0];
            _indexTab.SpreadChangeEvent += IndexTab_SpreadChangeEvent;

            // Создаём торговые табы
            TabCreate(BotTabType.Simple);
            _tab1 = TabsSimple[0];
            _tab1.CandleFinishedEvent += Tab1_CandleFinishedEvent;

            TabCreate(BotTabType.Simple);
            _tab2 = TabsSimple[1];
            _tab2.CandleFinishedEvent += Tab2_CandleFinishedEvent;

            // Параметры
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            _slippage = CreateParameter("Slippage", 0, 0, 20, 1);
            _volume = CreateParameter("Volume", 1, 1m, 100, 1);

            // Индикатор на индексе
            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
            _sma = (Aindicator)_indexTab.CreateCandleIndicator(_sma, "Prime");
            ((IndicatorParameterInt)_sma.Parameters[0]).ValueInt = 20;
            _sma.Save();
        }

        private void IndexTab_SpreadChangeEvent(List<Candle> candlesIndex)
        {
            // Анализируем индекс, но НЕ торгуем здесь
            if (_sma.DataSeries[0].Values == null || _sma.DataSeries[0].Values.Count == 0)
                return;

            decimal lastSma = _sma.DataSeries[0].Values[_sma.DataSeries[0].Values.Count - 1];
            decimal lastClose = candlesIndex[candlesIndex.Count - 1].Close;

            // Сохраняем сигнал во внутреннее состояние или торгуем на Simple-табах
        }

        private void Tab1_CandleFinishedEvent(List<Candle> candles)
        {
            // Синхронизация и торговля на _tab1
            if (!IsTabsSynced()) return;
            // ... логика входа/выхода
        }

        private void Tab2_CandleFinishedEvent(List<Candle> candles)
        {
            if (!IsTabsSynced()) return;
        }

        private bool IsTabsSynced()
        {
            if (_tab1.CandlesFinishedOnly == null || _tab2.CandlesFinishedOnly == null || _indexTab.Candles == null)
                return false;
            if (_tab1.CandlesFinishedOnly.Count == 0 || _tab2.CandlesFinishedOnly.Count == 0 || _indexTab.Candles.Count == 0)
                return false;

            DateTime t1 = _tab1.CandlesFinishedOnly[_tab1.CandlesFinishedOnly.Count - 1].TimeStart;
            DateTime t2 = _tab2.CandlesFinishedOnly[_tab2.CandlesFinishedOnly.Count - 1].TimeStart;
            DateTime ti = _indexTab.Candles[_indexTab.Candles.Count - 1].TimeStart;

            return t1 == ti && t2 == ti;
        }
    }
}
```

---

## 1.3 API `BotTabIndex` — справочник

### События

| Событие | Сигнатура | Когда срабатывает |
|---------|-----------|-------------------|
| `SpreadChangeEvent` | `Action<List<Candle>>` | При каждом обновлении свечи индекса (новая или закрытая). Передаёт **все** свечи индекса. |

### Свойства

| Свойство | Тип | Описание |
|----------|-----|----------|
| `Candles` | `List<Candle>` | Все свечи индекса. Доступ к последней: `Candles[Candles.Count - 1]`. |
| `TabName` | `string` | Уникальное имя таба. |
| `Indicators` | `List<IIndicator>` | Список индикаторов, созданных на этом табе. |
| `Tabs` | `List<ConnectorCandles>` | Список источников (коннекторов), формирующих индекс. |

### Методы

| Метод | Описание |
|-------|----------|
| `CreateCandleIndicator(IIndicator, string areaName)` | Создаёт индикатор на графике индекса. Возвращает `IIndicator` — нужен каст к `Aindicator`. |
| `Save()` | Сохраняет состояние таба. |

---

## 1.4 Синхронизация свечей между табами

Когда робот использует `BotTabIndex` + `BotTabSimple`, свечи приходят **асинхронно** от разных источников. **Обязательна проверка синхронизации по `TimeStart`** перед торговлей.

**Паттерн синхронизации (3 таба):**

```csharp
private void Tab1_CandleFinishedEvent(List<Candle> candles)
{
    if (_tab1.CandlesFinishedOnly == null || _tab2.CandlesFinishedOnly == null || _indexTab.Candles == null)
        return;
    if (_tab1.CandlesFinishedOnly.Count == 0 || _tab2.CandlesFinishedOnly.Count == 0 || _indexTab.Candles.Count == 0)
        return;

    DateTime tIndex = _indexTab.Candles[_indexTab.Candles.Count - 1].TimeStart;
    DateTime t1 = _tab1.CandlesFinishedOnly[_tab1.CandlesFinishedOnly.Count - 1].TimeStart;
    DateTime t2 = _tab2.CandlesFinishedOnly[_tab2.CandlesFinishedOnly.Count - 1].TimeStart;

    if (tIndex == t1 && tIndex == t2)
    {
        TradeLogic(_tab1.CandlesFinishedOnly, _tab2.CandlesFinishedOnly, _indexTab.Candles);
    }
}
```

> **Важно**: Проверяйте синхронизацию **во всех** обработчиках (`CandleFinishedEvent` каждого Simple-таба и `SpreadChangeEvent` индекса).

---

## 1.5 Встроенные математические инструменты

OsEngine предоставляет готовые классы для парного анализа на индексе.

### `CorrelationBuilder`

```csharp
CorrelationBuilder correlationIndicator = new CorrelationBuilder();
PairIndicatorValue correlation = correlationIndicator.ReloadCorrelationLast(
    candlesFirst,           // свечи первого набора
    candlesSecond,          // свечи второго набора
    lookBackPeriod          // глубина расчёта (int)
);

if (correlation != null && correlation.Value >= _minCorrelation.ValueDecimal)
{
    // Корреляция достаточна для входа
}
```

| Поле/метод | Описание |
|------------|----------|
| `ReloadCorrelationLast(List<Candle>, List<Candle>, int)` | Рассчитывает корреляцию по последним N свечам. Возвращает `PairIndicatorValue`. |
| `PairIndicatorValue.Value` | `decimal` — значение корреляции от -1 до 1. |

### `CointegrationBuilder`

```csharp
CointegrationBuilder cointegration = new CointegrationBuilder();
cointegration.CointegrationLookBack = _cointegrationCandlesLookBack.ValueInt;
cointegration.CointegrationDeviation = _cointegrationDeviationMult.ValueDecimal;
cointegration.ReloadCointegration(candlesIndex, candlesSecurity, false);

if (cointegration.Cointegration == null || cointegration.Cointegration.Count == 0)
    return;

if (cointegration.SideCointegrationValue == CointegrationLineSide.Up)
{
    // Бумага отклонилась ВВЕРХ от индекса → шорт бумаги
}
else if (cointegration.SideCointegrationValue == CointegrationLineSide.Down)
{
    // Бумага отклонилась ВНИЗ от индекса → лонг бумаги
}
```

| Поле/метод | Описание |
|------------|----------|
| `CointegrationLookBack` | `int` — глубина истории для расчёта. |
| `CointegrationDeviation` | `decimal` — множитель стандартного отклонения (типично 1–3). |
| `ReloadCointegration(List<Candle>, List<Candle>, bool)` | Пересчитывает коинтеграцию. Третий параметр — пересчитывать ли историю полностью. |
| `Cointegration` | `List<decimal>` — значения линии коинтеграции. |
| `SideCointegrationValue` | `CointegrationLineSide.Up` или `.Down` — направление отклонения. |

---

## 1.6 Каталог роботов на `BotTabIndex`

### 1.6.1 `IndexArbitrageClassic`
- **Путь**: `Robots/IndexArbitrage/IndexArbitrageClassic.cs`
- **Суть**: Классический арбитраж двух индексов через корреляцию и коинтеграцию.
- **Табы**: `BotTabIndex` ×2 + `BotTabScreener` ×2.
- **Логика**: Если первая нога индекса отклоняется вверх — покупаем скринер первой ноги, продаём скринер второй. Выход: по обратному сигналу или «No signal».
- **Ключевые параметры**: `Cointegration candles look back`, `Deviation mult`, `Correlation min value`.

### 1.6.2 `MultiOneLegArbitrageInTrend`
- **Путь**: `Robots/IndexArbitrage/MultiOneLegArbitrageInTrend.cs`
- **Суть**: Одноногий арбитраж в тренде — ищет бумаги, отклонившиеся от индекса, но только при определённой волатильности.
- **Табы**: `BotTabIndex` ×1 + `BotTabScreener` ×1.
- **Особенность**: Использует индикатор `VolatilityStagesAW` на индексе. Торгует только если `volatilityStage == заданной`.
- **Ключевые параметры**: `Volatility Stage To Trade`, `Stop mult`.

### 1.6.3 `MultiOneLegArbitrageMeanReversion`
- **Путь**: `Robots/IndexArbitrage/MultiOneLegArbitrageMeanReversion.cs`
- **Суть**: Одноногий mean-reversion — бумаги возвращаются к индексу.
- **Табы**: `BotTabIndex` ×1 + `BotTabScreener` ×1.
- **Логика**: Корреляция + коинтеграция каждой бумаги скринера против индекса. Отклонение вверх → шорт, вниз → лонг.
- **Выход**: `Reverse signal` или `No signal`.

### 1.6.4 `MultiExchangePairArbitrageOnTheIndex`
- **Путь**: `Robots/IndexArbitrage/MultiExchangePairArbitrageOnTheIndex.cs`
- **Суть**: Межбиржевой арбитраж нескольких валютных пар относительно общего индекса.
- **Табы**: `BotTabIndex` ×1 + `BotTabSimple` ×5.
- **Логика**: Находит бумаги с максимальным отклонением от индекса (вверх и вниз). Если разница между верхней и нижней ≥ `Min Deviation SecToSec ToEntry` — входим в пару.
- **Выход**: Когда отклонение сокращается до `Min Deviation To Exit`.

### 1.6.5 `OneLegArbitrage`
- **Путь**: `Robots/OnScriptIndicators/OneLegArbitrage.cs`
- **Суть**: Простейшая одноногая стратегия на пересечении MA на индексе.
- **Табы**: `BotTabIndex` ×1 + `BotTabSimple` ×1.
- **Логика**: Пересечение цены индекса и SMA на индексе. Если цена индекса > SMA — лонг бумаги, иначе — шорт.
- **Особенность**: Робот из раздела `OnScriptIndicators` — демонстрационный, минималистичный.

### 1.6.6 `PairTraderSpreadSma`
- **Путь**: `Robots/MarketMaker/PairTraderSpreadSma.cs`
- **Суть**: Парный трейдинг на спреде двух бумаг с пересечением SMA на графике спреда.
- **Табы**: `BotTabSimple` ×2 + `BotTabIndex` ×1 (спред).
- **Логика**: Строит индекс-спред из двух бумаг. На спреде — две SMA (быстрая и медленная). Пересечение вниз → шорт первой, лонг второй. Пересечение вверх → наоборот.
- **Выход**: По обратному сигналу.

### 1.6.7 `TwoLegArbitrage`
- **Путь**: `Robots/MarketMaker/TwoLegArbitrage.cs`
- **Суть**: Двуногий арбитраж по RSI, построенному на индексе.
- **Табы**: `BotTabIndex` ×1 + `BotTabSimple` ×2.
- **Логика**: RSI на индексе выше верхней линии → шорт обеих бумаг. RSI ниже нижней линии → лонг обеих. Выход по обратному сигналу.
- **Синхронизация**: Проверяет `TimeStart` всех трёх табов перед торговлей.

### 1.6.8 `BollingerTrendVolatilityStagesFilter`
- **Путь**: `Robots/VolatilityStageRotationSamples/BollingerTrendVolatilityStagesFilter.cs`
- **Суть**: Трендовая стратегия на Bollinger с фильтром волатильности.
- **Табы**: `BotTabSimple` ×1 (не использует `BotTabIndex` напрямую, но относится к семейству индексно-волатильных фильтров).
- **Логика**: Пробой верхней линии Bollinger → лонг, нижней → шорт. Дополнительно проверяет стадию волатильности через `VolatilityStagesAW`.
- **Выход**: Трейлинг-стоп по противоположной линии Bollinger.

---

## 1.7 Примеры кода из роботов

### Пример 1: Индикатор на индексе + обновление параметров

```csharp
// Создание RSI на индексном табе
_rsi = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
_rsi = (Aindicator)_tabIndex.CreateCandleIndicator(_rsi, "RsiArea");
((IndicatorParameterInt)_rsi.Parameters[0]).ValueInt = _rsiLength.ValueInt;
_rsi.Save();

// Обновление при изменении параметров пользователем
void TwoLegArbitrage_ParametrsChangeByUser()
{
    ((IndicatorParameterInt)_rsi.Parameters[0]).ValueInt = _rsiLength.ValueInt;
    _rsi.Save();
    _rsi.Reload();
}
```

### Пример 2: Распределение объёма по скринеру

```csharp
decimal legVolume = _moneyPercentFromDepoOnOneLeg.ValueDecimal / _screenerFirst.Tabs.Count;

for (int i = 0; i < _screenerFirst.Tabs.Count; i++)
{
    decimal volume = GetVolume(_screenerFirst.Tabs[i], legVolume);
    _screenerFirst.Tabs[i].BuyAtMarket(volume, signalComment);
}
```

### Пример 3: Проверка отклонения цены бумаги от индекса (%)

```csharp
private bool IsUpperThenIndex(decimal lastIndexPrice, BotTabSimple tab)
{
    if (tab.IsConnected == false || tab.IsReadyToTrade == false)
        return false;

    decimal lastBid = tab.PriceBestBid;
    if (lastBid == 0) return false;
    if (lastBid < lastIndexPrice) return false;

    decimal diff = lastBid - lastIndexPrice;
    decimal diffPercent = diff / (lastIndexPrice / 100);

    return diffPercent >= _minDeviationSecToIndexToEntry.ValueDecimal;
}
```

---

## 1.8 Типичные ошибки при работе с `BotTabIndex`

| № | Ошибка | Причина | Решение |
|---|--------|---------|---------|
| 1 | `NullReferenceException` в `SpreadChangeEvent` | Обращение к `_indexTab.Candles` до первого события | Проверять `!= null` и `Count > 0` |
| 2 | Торговля на `BotTabIndex` напрямую | Попытка вызвать `_indexTab.BuyAtMarket()` | Торговать только на `BotTabSimple` / `BotTabScreener` |
| 3 | Рассинхронизация сигналов | Свечи индекса и Simple-таба имеют разное `TimeStart` | Проверять `TimeStart` всех табов перед логикой |
| 4 | Индикатор на индексе не пересчитывается | Забыли вызвать `_indicator.Reload()` после смены параметров | Вызывать `Save()` → `Reload()` в `ParametrsChangeByUser` |
| 5 | `IndexOutOfRangeException` в индикаторе | Обращение к `Values[i]` до накопления достаточного количества свечей | Проверять `Values.Count >= period + 2` |
| 6 | Скринер не активен (`Tabs.Count == 0`) | Пользователь не подключил источники в скринере | Проверять `_screener.Tabs.Count > 0` и логировать ошибку |
| 7 | Нулевой объём (`volume == 0`) | `GetVolume` вернул 0 из-за отсутствия портфеля | Проверять `volume > 0` перед выставлением ордера |
| 8 | Двойной вход в одну свечу | `_lastCandleOpenPos` не сброшен или логика входа вызвана из нескольких обработчиков | Использовать флаг `_lastCandleOpenPos` или счётчик свечей |
| 9 | Индикатор создан на `_tab2` вместо `_indexTab` | Ошибка в имени таба при `CreateCandleIndicator` | Проверять первый аргумент `_tabIndex.CreateCandleIndicator(...)` |
| 10 | `PairIndicatorValue == null` | `ReloadCorrelationLast` вернул null из-за недостатка свечей | Проверять `!= null` перед доступом к `.Value` |

---

## 1.9 Шпаргалка

**Создать индексный таб:**
```csharp
TabCreate(BotTabType.Index);
_indexTab = TabsIndex[0];
_indexTab.SpreadChangeEvent += IndexTab_SpreadChangeEvent;
```

**Создать индикатор на индексе:**
```csharp
_sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
_sma = (Aindicator)_indexTab.CreateCandleIndicator(_sma, "Prime");
((IndicatorParameterInt)_sma.Parameters[0]).ValueInt = 20;
_sma.Save();
```

**Проверить синхронизацию 3 табов:**
```csharp
bool synced = t1 == ti && t2 == ti;
```

**Корреляция:**
```csharp
PairIndicatorValue c = new CorrelationBuilder().ReloadCorrelationLast(a, b, 100);
```

**Коинтеграция:**
```csharp
CointegrationBuilder cb = new CointegrationBuilder();
cb.ReloadCointegration(index, sec, false);
CointegrationLineSide side = cb.SideCointegrationValue;
```

**Торговля через скринер:**
```csharp
for (int i = 0; i < _screener.Tabs.Count; i++)
    _screener.Tabs[i].BuyAtMarket(volume, comment);
```

---

> **Связанные файлы контекста:**
> - `CONTEXT.md` — базовая архитектура, выбор типа таба, бойлерплейты
> - `CONTEXT_ROBOTS.md` — каталог остальных роботов
> - `CONTEXT_INDICATORS.md` — создание и использование индикаторов
> - `CONTEXT_PAIRS_AND_FUTURES.md` — парный арбитраж `BotTabPair` и фьючерсы
