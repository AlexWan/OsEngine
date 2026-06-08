# CONTEXT_PAIRS_AND_FUTURES — Парный арбитраж и фьючерсы OsEngine

Специализированные стратегии: парный арбитраж, ротация фьючерсов, контанго-арбитраж.
Базовая архитектура — в `CONTEXT.md`, каталог роботов — в `CONTEXT_ROBOTS.md`.

## 1. Парный арбитраж (`Robots/PairArbitrage/`)

Торговля расхождением двух коррелирующих инструментов. Используется `BotTabPair` — специальный тип таба для парного трейдинга, который автоматически рассчитывает корреляцию и отклонения между инструментами.

### 1.1 Стратегии парного арбитража

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `PairCorrelationTrader` | Конвергенция при высокой корреляции | **Базовый парный арбитраж**: вход при корреляции > 0.9; использование события `CorrelationChangeEvent`; проверка `pair.SideCointegrationValue` (Up/Down); методы `SellSec1BuySec2()` / `BuySec1SellSec2()`; выход при инверсии стороны коинтеграции (`pair.LastEntryCointegrationSide`); ограничение `_pairTrader.PairsWithPositionsCount`; `pair.ClosePositions()` для закрытия всех позиций пары |
| `PairCorrelationNegative` | Торговля в тренд при отрицательной корреляции | **Отрицательная корреляция**: вход при корреляции < -0.8 (параметр `_maxCorrelationToEntry`); выход при корреляции > 0.8 (`_minCorrelationToExit`); логика для антикоррелирующих инструментов; проверка `pair.CorrelationLast`; использование тех же методов входа/выхода что и `PairCorrelationTrader` |
| `PairCointegrationSideTrader` | Пробой каналов коинтеграции | **Событие CointegrationPositionSideChangeEvent**: реакция на пробой верхней/нижней границы канала; вход при смене стороны (`pair.SideCointegrationValue != pair.LastEntryCointegrationSide`); закрытие предыдущей позиции перед открытием новой; торговля на возврат к среднему (mean reversion); методы `SellSec1BuySec2()` при пробое вверх, `BuySec1SellSec2()` при пробое вниз |

**Ключевые понятия парного арбитража:**

```csharp
// Создание Pair-таба
TabCreate(BotTabType.Pair);
BotTabPair _pairTrader = TabsPair[0];

// Подписка на событие изменения корреляции
_pairTrader.CorrelationChangeEvent += _pairTrader_CorrelationChangeEvent;

// Подписка на событие изменения стороны коинтеграции
_pairTrader.CointegrationPositionSideChangeEvent += _pairTrader_CointegrationPositionSideChangeEvent;

// Обработчик события корреляции
private void _pairTrader_CorrelationChangeEvent(
    List<PairIndicatorValue> correlation, 
    PairToTrade pair)
{
    if (_regime.ValueString == "Off") return;
    
    if (pair.HavePositions) {
        ClosePositionLogic(pair);
    } else {
        OpenPositionLogic(pair);
    }
}

// Проверка корреляции перед входом
if (pair.CorrelationLast < 0.9m) {
    return;  // корреляция недостаточно высокая
}

// Ограничение на количество пар с позициями
if (_pairTrader.PairsWithPositionsCount >= _maxPositionsCount.ValueInt) {
    return;
}

// Вход: продажа первого инструмента, покупка второго
if (pair.SideCointegrationValue == CointegrationLineSide.Up) {
    pair.SellSec1BuySec2();
}
// Вход: покупка первого инструмента, продажа второго
else if (pair.SideCointegrationValue == CointegrationLineSide.Down) {
    pair.BuySec1SellSec2();
}

// Выход: закрытие всех позиций пары
pair.ClosePositions();

// Проверка стороны коинтеграции для выхода
if (pair.SideCointegrationValue == CointegrationLineSide.Up 
    && pair.LastEntryCointegrationSide == CointegrationLineSide.Down) {
    pair.ClosePositions();  // инверсия стороны - выход
}

// Для отрицательной корреляции: вход при корреляции < -0.8
if (pair.CorrelationLast > _maxCorrelationToEntry.ValueDecimal) {
    return;  // корреляция слишком высокая (близка к положительной)
}

// Выход при росте корреляции выше 0.8
if (pair.CorrelationLast > _minCorrelationToExit.ValueDecimal) {
    pair.ClosePositions();
}

// Проверка: есть ли уже позиции по этой паре
if (pair.HavePositions) {
    // позиции есть
}

// Проверка: сменилась ли сторона коинтеграции
if (pair.SideCointegrationValue != pair.LastEntryCointegrationSide) {
    // сторона сменилась - можно открывать новую позицию
}
```

**Типы событий в PairTab:**

| Событие | Когда срабатывает | Что передаёт |
|---------|-------------------|--------------|
| `CorrelationChangeEvent` | При изменении корреляции между инструментами | `List<PairIndicatorValue> correlation`, `PairToTrade pair` |
| `CointegrationPositionSideChangeEvent` | При пробое верхней/нижней границы канала коинтеграции | `CointegrationLineSide side`, `PairToTrade pair` |

**Свойства PairToTrade:**

| Свойство | Тип | Описание |
|----------|-----|----------|
| `CorrelationLast` | `decimal` | Последнее значение корреляции (-1 до 1) |
| `SideCointegrationValue` | `CointegrationLineSide` | Текущая сторона коинтеграции (Up/Down/None) |
| `LastEntryCointegrationSide` | `CointegrationLineSide` | Сторона коинтеграции на момент последнего входа |
| `HavePositions` | `bool` | Есть ли открытые позиции по этой паре |
| `SecurityName1`, `SecurityName2` | `string` | Имена инструментов в паре |

**Методы для торговли парой:**

| Метод | Описание |
|-------|----------|
| `pair.BuySec1SellSec2()` | Купить первый инструмент, продать второй (ставка на сближение если первый дешевле) |
| `pair.SellSec1BuySec2()` | Продать первый инструмент, купить второй (ставка на сближение если первый дороже) |
| `pair.ClosePositions()` | Закрыть все позиции по паре |

**Стратегии по типу корреляции:**

| Тип корреляции | Значение | Стратегия |
|----------------|----------|-----------|
| Положительная | > 0.7 | Инструменты движутся в одном направлении. Вход при расхождении, выход при сближении |
| Отрицательная | < -0.7 | Инструменты движутся в противоположных направлениях. Вход при сближении, выход при расхождении |
| Околонулевая | -0.3 до 0.3 | Нет статистической связи. Парный арбитраж не рекомендуется |

**Изучать:** `PairCorrelationTrader.cs` — базовая стратегия с положительной корреляцией. `PairCorrelationNegative.cs` — торговля антикоррелирующих инструментов. `PairCointegrationSideTrader.cs` — торговля изменения графика минимальных остатков от разницы, между двумя ценовыми рядами с оптимальным мультипликатором.

---


---

## 2. Перекладывание экспирируемых фьючерсов (`Robots/FuturesTrend/`)

Торговля фьючерсами с автоматическим перекладыванием из одной серии в другую при приближении экспирации. Используется `BotTabScreener` для работы с несколькими сериями фьючерса одновременно.

### 2.1 Пробой адаптивного канала

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `FuturesTrendPriceChannel` | Пробой PriceChannelAdaptive | **Автоматический выбор серии фьючерса**: метод `GetFuturesToTrade()` с приоритетом позиции; проверка `Security.Expiration`; правило 3 дней до экспирации для выхода; правило 3-100 дней для входа; `BuyAtIcebergMarket()` / `CloseAtIcebergMarket()`; индикатор `PriceChannelAdaptive` с параметрами `_pcAdxLength` и `_pcRatio`; обновление индикатора через `_futuresSource.UpdateIndicatorsParameters()`; `NonTradePeriods` для ограничения торгового времени; кнопка `_tradePeriodsShowDialogButton` для настройки периодов |

### 2.2 Пробой Bollinger Bands

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `FuturesTrendBollinger` | Пробой Bollinger | **Двунаправленная торговля**: вход в лонг при `price > BollingerUpper`, вход в шорт при `price < BollingerLower`; режимы `_regime` ("Off"/"On"/"OnlyLong"/"OnlyShort"); выход при пробое противоположной границы; проверка `daysByExpiration < 3` для принудительного выхода; параметры `_bollingerLength` и `_bollingerDeviation`; айсберг-ордера на вход и выход; та же логика выбора фьючерса что и в `FuturesTrendPriceChannel` |

**Ключевые понятия перекладывания фьючерсов:**

```csharp
// Создание скринера для фьючерсов
BotTabScreener _futuresSource = TabCreate<BotTabScreener>();
_futuresSource.CandleFinishedEvent += _futs1_CandleFinishedEvent;

// Создание индикатора на скринере
_futuresSource.CreateCandleIndicator(2, "PriceChannelAdaptive", 
    new List<string>() { _pcAdxLength.ValueInt.ToString(), _pcRatio.ValueInt.ToString() }, 
    "Prime");

// Обновление параметров индикатора
ParametrsChangeByUser += FuturesStartContangoScreener_ParametrsChangeByUser;
private void FuturesStartContangoScreener_ParametrsChangeByUser()
{
    _futuresSource._indicators[0].Parameters = 
        new List<string>() { _pcAdxLength.ValueInt.ToString(), _pcRatio.ValueInt.ToString() };
    _futuresSource.UpdateIndicatorsParameters();
}

// Выбор фьючерса для торговли
private BotTabSimple GetFuturesToTrade(BotTabScreener futures, DateTime currentTime)
{
    // 1. Приоритет: фьючерс с открытой позицией
    for (int i = 0; i < futures.Tabs.Count; i++) {
        BotTabSimple currentFutures = futures.Tabs[i];
        if (currentFutures.PositionsOpenAll.Count != 0) {
            return currentFutures;  // торгуем по позиции
        }
    }

    // 2. Выбор ближайшей серии с учётом экспирации
    BotTabSimple selectedFutures = null;
    
    for (int i = 0; i < futures.Tabs.Count; i++) {
        Security sec = futures.Tabs[i].Security;
        
        if (sec == null || sec.Expiration == DateTime.MinValue) continue;
        
        double daysByExpiration = (sec.Expiration - currentTime).TotalDays;
        
        // Пропускаем если меньше 3 дней или больше 100 дней до экспирации
        if (daysByExpiration < 3 || daysByExpiration > 100) continue;
        
        // Выбираем ближайшую экспирацию
        if (selectedFutures != null && selectedFutures.Security.Expiration < sec.Expiration) {
            continue;
        }
        
        selectedFutures = futures.Tabs[i];
    }
    
    return selectedFutures;
}

**Алгоритм выбора фьючерса:**

```
1. Есть ли открытая позиция?
   └─ ДА → Торгуем по этой серии (независимо от экспирации)
   └─ НЕТ → Переходим к шагу 2

2. Поиск ближайшей серии:
   └─ Expiration < 3 дней → Пропускаем (слишком близко к экспирации)
   └─ Expiration > 100 дней → Пропускаем (слишком далеко)
   └─ Иначе → Выбираем серию с ближайшей экспирацией

3. Возвращаем выбранный фьючерс или null
```

**Правила экспирации:**

| Ситуация | Дней до экспирации | Действие |
|----------|-------------------|----------|
| Вход в позицию | 3-100 дней | Разрешён |
| Вход в позицию | < 3 дней | Запрещён |
| Вход в позицию | > 100 дней | Запрещён |
| Выход из позиции | < 3 дней | Принудительный выход |
| Выход из позиции | По сигналу индикатора | Выход по стратегии |
| Позиция открыта | Любое | Держим до сигнала или экспирации |

**Преимущества такого подхода:**

| Преимущество | Реализация |
|--------------|------------|
| Непрерывность торговли | Автоматический переход на следующую серию |
| Избегание экспирации | Выход за 3 дня до экспирации |
| Приоритет позиции | Если позиция есть — не переключаемся |
| Работа в тестере | Защита от пропуска серий (лимит 100 дней) |

**Изучать:** `FuturesTrendPriceChannel.cs` — базовая логика перекладывания с PriceChannel. `FuturesTrendBollinger.cs` — двунаправленная торговля с Bollinger. Оба робота демонстрируют ключевой паттерн `GetFuturesToTrade()` для автоматического выбора серии фьючерса.

---

## 3. Ротация фьючерсов по раздвижке к базе (`Robots/FuturesStart/`)

Торговля фьючерсами с ротацией по размеру премии/дисконта к базовому активу (споту). Используется архитектура "пара спот/фьючерс" с фильтром по стадии контанго/бэквордации. Скринер фьючерсов ранжирует инструменты по проценту отклонения и позволяет торговать только определённые стадии.

### 3.1 Пробой Bollinger с фильтром контанго

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `FuturesStart1Bollinger` | Пробой Bollinger Bands + ранкинг раздвижек | **Архитектура 10 пар спот/фьючерс**: 10 табов `BotTabSimple` для спота + 10 табов `BotTabScreener` для фьючерсов; расчёт контанго в %: `contangoPercent = (futuresPrice - spotPrice) / spotPrice * 100`; ранжирование `_contangoValues.OrderBy(x => x.ContangoPercent)`; стадии 1 (мин. контанго — лонг) и 2 (макс. контанго — шорт); фильтр `_contangoStageToTradeLong/Short`; авто-режим для MOEX (`On_MOEXStocksAuto`) с коэффициентами для разных бумаг; ручной режим (`On_Manual`) с индивидуальными коэффициентами на инструмент; кнопка `Show contango` для отладки; `SetTSecurities()` для авто-настройки инструментов через T-Invest API; проверка `SecurityType.Futures` и `SecurityType.Stock`; фильтрация по префиксам серий (SRH/SRM/SRZ/SRU для SBER и т.д.); `CanTradeThisSecurity()` для включения/выключения инструментов; `_entryLogicByBaseSecurityInReal` для защиты от повторного входа в течение минуты |

### 3.2 Пробой Keltner Channel с фильтром контанго

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `FuturesStart2Keltner` | Пробой Keltner Channel + ранкинг раздвижек | **Индикатор KeltnerChannel**: параметры `_keltnerEmaLength`, `_keltnerAtrLength`, `_keltnerDeviation`; вход по пробою верхней линии (`DataSeries[1]`) для лонга, нижней (`DataSeries[2]`) для шорта; та же архитектура контанго-фильтра что и `FuturesStart1Bollinger`; те же 10 пар спот/фьючерс; те же стадии торговли; отличие только в индикаторе (Keltner вместо Bollinger); `CreateIndicators()` с передачей 5 параметров в индикатор |

**Ключевые понятия ротации фьючерсов:**

```csharp
// Архитектура: 10 пар спот/фьючерс
BotTabSimple _base1, _base2, ..., _base10;          // спот (базовый актив)
BotTabScreener _futs1, _futs2, ..., _futs10;        // фьючерсы (серии)

// Создание пар
_base1 = TabCreate<BotTabSimple>();
_base1.CandleFinishedEvent += _base1_CandleFinishedEvent;
_futs1 = TabCreate<BotTabScreener>();
_futs1.CandleFinishedEvent += _futs1_CandleFinishedEvent;
CreateIndicators(_base1, _futs1);

// Расчёт контанго в процентах
private void SetContangoValues(BotTabSimple baseSource, BotTabSimple futuresSource)
{
    decimal coeff = 1;  // коэффициент для нормализации цен (зависит от количества знаков)
    
    // Авто-режим для MOEX
    if (_contangoFilterRegime.ValueString == "On_MOEXStocksAuto") {
        if (baseSource.Security.Name.Contains("VTB")) {
            coeff = 20;  // VTB имеет специфичный масштаб цен
        } else if (baseSource.Security.Name.Contains("GMKN")) {
            coeff = 100; // GMKN тоже специфичный
        } else {
            // Для остальных бумаг: 10^decimals
            for (int i = 0; i < baseSource.Security.Decimals; i++) {
                coeff = coeff * 10;
            }
        }
    }
    // Ручной режим: индивидуальные коэффициенты
    else if (_contangoFilterRegime.ValueString == "On_Manual") {
        coeff = _contangoCoefficient1.ValueDecimal;  // для первого инструмента
        // ... для остальных
    }
    
    // Расчёт абсолютной разницы
    decimal contangoAbs = (futuresSource.PriceBestBid / coeff) - baseSource.PriceBestAsk;
    
    // Расчёт в процентах от спота
    decimal contangoPercent = contangoAbs / (baseSource.PriceBestAsk / 100);
    
    // Сохранение значения
    value.ContangoPercent = contangoPercent;
    
    // Сортировка по возрастанию контанго
    _contangoValues = _contangoValues.OrderBy(x => x.ContangoPercent).ToList();
}

// Определение стадии контанго
private int GetContangoStage(string secName)
{
    for (int i = 0; i < _contangoValues.Count; i++) {
        if (_contangoValues[i].SecurityName == secName) {
            // Стадия 1: самые низкие значения контанго (дисконт) — для лонга
            if (i <= _contangoFilterCountSecurities.ValueInt) {
                return 1;
            }
            // Стадия 2: самые высокие значения контанго (премия) — для шорта
            else if (i >= _contangoValues.Count - _contangoFilterCountSecurities.ValueInt) {
                return 2;
            }
            // Стадия 0: средние значения — не торгуем
            else {
                return 0;
            }
        }
    }
    return 0;
}

// Проверка стадии перед входом
if (_contangoFilterRegime.ValueString != "Off") {
    int stageContango = GetContangoStage(futuresSource.Security.Name);
    
    // Для лонга: только стадия 1 (дисконт)
    if (_regime.ValueString != "OnlyShort" && stageContango != _contangoStageToTradeLong.ValueInt) {
        return;
    }
    
    // Для шорта: только стадия 2 (премия)
    if (_regime.ValueString != "OnlyLong" && stageContango != _contangoStageToTradeShort.ValueInt) {
        return;
    }
}

// Кнопка для отображения текущих значений контанго
StrategyParameterButton buttonShowContango = CreateParameterButton("Show contango", "Contango");
buttonShowContango.UserClickOnButtonEvent += ButtonShowContango_UserClickOnButtonEvent;

private void ButtonShowContango_UserClickOnButtonEvent()
{
    string message = "";
    for (int i = 0; i < _contangoValues.Count; i++) {
        message += _contangoValues[i].SecurityName
                 + " Value%: " + Math.Round(_contangoValues[i].ContangoPercent, 3)
                 + "\n";
    }
    SendNewLogMessage(message, Logging.LogMessageType.Error);
}

// Выбор фьючерса для торговли (с учётом экспирации)
private BotTabSimple GetFuturesToTrade(BotTabSimple baseSource, BotTabScreener futures, DateTime currentTime)
{
    // 1. Приоритет: фьючерс с открытой позицией
    for (int i = 0; i < futures.Tabs.Count; i++) {
        if (futures.Tabs[i].PositionsOpenAll.Count != 0) {
            return futures.Tabs[i];
        }
    }

    // 2. Выбор ближайшей серии (3-100 дней до экспирации)
    BotTabSimple selectedFutures = null;
    for (int i = 0; i < futures.Tabs.Count; i++) {
        Security sec = futures.Tabs[i].Security;
        double daysByExpiration = (sec.Expiration - currentTime).TotalDays;
        
        if (daysByExpiration < 3 || daysByExpiration > 100) continue;
        
        if (selectedFutures == null || sec.Expiration < selectedFutures.Security.Expiration) {
            selectedFutures = futures.Tabs[i];
        }
    }
    return selectedFutures;
}

// Защита от повторного входа в течение минуты
private List<LastTradeTimeValue> _entryLogicByBaseSecurityInReal = new List<LastTradeTimeValue>();

private void SetLastLogicEntryTime(string securityBase, DateTime time)
{
    LastTradeTimeValue existing = _entryLogicByBaseSecurityInReal
        .Find(x => x.SecurityName == securityBase);
    
    if (existing != null) {
        existing.Time = time;
    } else {
        _entryLogicByBaseSecurityInReal.Add(new LastTradeTimeValue {
            SecurityName = securityBase,
            Time = time
        });
    }
}

// Проверка в логике
if (this.StartProgram == StartProgram.IsOsTrader) {
    DateTime lastPairTradeTime = GetLastEntryLogicTime(baseSource.Security.Name);
    if (lastPairTradeTime.AddMinutes(1) > DateTime.Now) {
        return;  // уже входили в логику за последнюю минуту
    }
    SetLastLogicEntryTime(baseSource.Security.Name, DateTime.Now);
}

// Включение/выключение отдельных инструментов
_tradeRegimeSecurity1 = CreateParameter("Trade security 1", true, "Trade securities");
// ... для 10 инструментов

private bool CanTradeThisSecurity(string securityName)
{
    if (this.TabsSimple[0].Security.Name == securityName) {
        return _tradeRegimeSecurity1.ValueBool;
    }
    // ... для остальных
    return false;
}
```

**Классы данных:**

```csharp
// Значение контанго для инструмента
public class ContangoValue
{
    public string SecurityName;      // имя фьючерса
    public decimal ContangoPercent;  // процент отклонения от спота
    public DateTime LastTimeUpdate;  // время последнего обновления
}

// Время последнего входа в логику
public class LastTradeTimeValue
{
    public string SecurityName;  // имя базового актива
    public DateTime Time;        // время последнего входа
}
```

**Стадии контанго:**

| Стадия | Описание | Когда торговать | Пример |
|--------|-----------|-----------------|--------|
| 1 | Минимальное контанго (дисконт) | Лонг | Фьючерс торгуется с дисконтом к споту |
| 2 | Максимальное контанго (премия) | Шорт | Фьючерс торгуется с премией к споту |
| 0 | Средние значения | Не торговать | Нейтральная зона |

**Режимы фильтра контанго:**

| Режим | Описание | Когда использовать |
|-------|----------|-------------------|
| `Off` | Фильтр отключён | Тестирование без фильтра |
| `On_MOEXStocksAuto` | Авто-коэффициенты для MOEX | Торговля фьючерсами на акции РФ |
| `On_Manual` | Ручные коэффициенты | Кастомные инструменты |

**Коэффициенты для MOEX (авто-режим):**

| Инструмент | Коэффициент | Примечание |
|------------|-------------|------------|
| SBER, GAZP, ROSN, LKOH | 10^decimals | Стандартные бумаги |
| VTB | 20 (до 2024.07), 100 (после) | Специфичный масштаб |
| GMKN | 100 (до 2024.04), 10 (после) | Сплит акций |
| MGNT | 10^decimals | Стандарт |

**Изучать:** `FuturesStart1Bollinger.cs` — базовая логика с Bollinger и полной реализацией контанго-фильтра. `FuturesStart2Keltner.cs` — альтернатива с Keltner Channel. Оба робота демонстрируют ключевые паттерны: расчёт контанго, ранжирование, стадии торговли, авто-настройку инструментов.

---

