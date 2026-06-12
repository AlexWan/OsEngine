# CONTEXT_INDICATORS — Индикаторы OsEngine

Как писать и использовать индикаторы на базе `Aindicator`.
Базовая архитектура роботов — в `CONTEXT.md`. Шаблоны промптов для заказа новых индикаторов — в `CONTEXT_PROMPTS_INDICATORS.md`.

## 1. Индикаторы

### 1.1 Две системы индикаторов

OsEngine поддерживает **две системы индикаторов**. При создании новых индикаторов всегда используйте только актуальную систему.

#### А) Устаревшие встроенные индикаторы

**Путь:** `OsEngine/Charts/CandleChart/Indicators/`

- Содержит ~60 индикаторов в виде обычных C#-классов (`Bollinger.cs`, `Rsi.cs`, `Sma.cs`, `ParabolicSAR.cs` и др.)
- Каждый индикатор — пара файлов: логика (`.cs`) + окно настроек (`Ui.xaml.cs`)
- Эти индикаторы являются **устаревшими** и оставлены для обратной совместимости
- **Не создавайте новые индикаторы по этому пути**

#### Б) Актуальные скриптовые индикаторы (рекомендуется)

**Базовый класс:** `OsEngine/Indicators/Aindicator.cs`

- Все новые индикаторы наследуются от абстрактного класса `Aindicator`
- Индикаторы хранятся как обычные C#-файлы `.cs`
- Компилируются в runtime через **Roslyn** при первом обращении
- Кэшируются в памяти (`_compiledIndicatorTypesCache`) — повторная компиляция не требуется
- Поддержка внешних DLL: положите `.dll` в подпапку `Dlls` рядом со скриптом

**Где лежат в репозитории:**
- `OsEngine/Indicators/Scripts/` — **~110 готовых индикаторов** (Sma, Ema, MACD, RSI, Bollinger, ATR, ZigZag и др.)
- `OsEngine/Indicators/Samples/` — 3 обучающих шаблона (Sample1Blank, Sample2IndicatorParameters, Sample3IndicatorDataSeries)

---

### 1.2 Минимальный рабочий индикатор — бойлерплейт

Скопируйте этот шаблон, переименуйте `MyIndicator` и `MyIndicatorName` — и начинайте писать логику.

```csharp
using OsEngine.Entity;
using System.Collections.Generic;

namespace OsEngine.Indicators
{
    [Indicator("MyIndicator")]
    public class MyIndicator : Aindicator
    {
        // 1. Объявление параметров и серий
        public IndicatorParameterInt _length;
        public IndicatorDataSeries _series;

        // 2. Конфигурация (вместо конструктора)
        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 14);
                _series = CreateSeries("Line", System.Drawing.Color.Red,
                                       IndicatorChartPaintType.Line, true);
            }
        }

        // 3. Логика расчёта
        public override void OnProcess(List<Candle> source, int index)
        {
            // Защита от недостатка данных
            if (index < _length.ValueInt)
            {
                _series.Values[index] = 0;
                return;
            }

            // Твоя логика здесь. Используй source[index], а не source[source.Count-1]
            _series.Values[index] = source[index].Close;
        }
    }
}
```

**Правила именования:**
- Имя файла должно совпадать с именем класса: `MyIndicator.cs` → `public class MyIndicator`
- Атрибут `[Indicator("Name")]` — имя для отображения в UI и вызова из фабрики
- Пространство имён может быть любым, класс обязательно `public`

---

### 1.3 Жизненный цикл индикатора

```
Создание через IndicatorsFactory
        ↓
Init(name, startProgram) — загрузка сохранённых параметров
        ↓
OnStateChange(IndicatorState.Configure) — создаём параметры и серии
        ↓
Process(List<Candle> candles) — внешний вызов при новых свечах
        ↓
  ├─ ProcessAll   — если свечей стало больше/меньше (пересчёт всего)
  ├─ ProcessLast  — если количество не изменилось (пересчёт последней)
  └─ ProcessNew   — если добавилась ровно одна свеча (оптимально)
        ↓
OnProcess(List<Candle> source, int index) — ваш расчёт
        ↓
Reload() — принудительный полный пересчёт
        ↓
Delete() — удаление файлов настроек, очистка
```

**Важные правила:**
- `OnStateChange(Configure)` — только создание параметров и серий. Никакой логики!
- `OnProcess` — вызывается для **каждой** свечи (index от 0 до Count-1). При добавлении новой свечи вызывается в основном только для неё, но при Reload — для всех.
- Не используйте `source[source.Count - 1]` внутри `OnProcess` — это сломает исторические значения. Используйте `source[index]`.

---

### 1.4 Параметры индикатора

Параметры создаются в `OnStateChange(Configure)` и автоматически сохраняются в файл `Engine\{Name}Parametrs.txt`.

| Метод создания | Тип C# | Для чего | Пример |
|----------------|--------|----------|--------|
| `CreateParameterInt(name, value)` | `int` | Периоды, длины | `CreateParameterInt("Length", 14)` |
| `CreateParameterDecimal(name, value)` | `decimal` | Отклонения, множители | `CreateParameterDecimal("Deviation", 2.0m)` |
| `CreateParameterBool(name, value)` | `bool` | Включить/выключить | `CreateParameterBool("Show line", true)` |
| `CreateParameterString(name, value)` | `string` | Свободный ввод | `CreateParameterString("Path", "C:/Data")` |
| `CreateParameterStringCollection(name, value, collection)` | `string` | Выпадающий список | `CreateParameterStringCollection("Price type", "Close", new List<string>{"Open","High","Low","Close"})` |

**Доступ к значениям:**
```csharp
int len = _length.ValueInt;
decimal dev = _deviation.ValueDecimal;
bool on = _showLine.ValueBool;
string pt = _priceType.ValueString;
```

**Связывание параметров (Bind):**
Когда индикатор включает другой индикатор внутри себя, можно синхронизировать их параметры:
```csharp
_emaFast = CreateIndicator("Ema", "EmaFast", new IndicatorParameterInt("Length", 12));
_lengthFast.Bind(_emaFast.ParametersDigit[0]); // изменение одного меняет другой
```

---

### 1.5 Серии данных (DataSeries)

Серии — это выходные данные индикатора, которые рисуются на графике.

```csharp
_series = CreateSeries("Name", Color.Red, IndicatorChartPaintType.Line, isPaint: true);
```

**Типы отрисовки (`IndicatorChartPaintType`):**

| Тип | Описание | Когда использовать |
|-----|----------|-------------------|
| `Line` | Линия | Скользящие средние, уровни |
| `Column` | Столбцы (гистограмма) | MACD, AO, объёмы |
| `Point` | Точки | Parabolic SAR, экстремумы |
| `Candle` | Свечи | Специальные индикаторы |

**Работа со значениями:**
```csharp
// Запись в OnProcess
_series.Values[index] = calculatedValue;

// Доступ к последнему значению из робота
decimal last = _indicator.DataSeries[0].Values[_indicator.DataSeries[0].Values.Count - 1];
// или
decimal last = _indicator.DataSeries[0].Last;

// Настройка цвета серии
_series.Color = Color.CornflowerBlue;
```

**Перерисовка истории:**
Для индикаторов типа ZigZag, которые могут менять прошлые значения при появлении новых данных:
```csharp
_series.CanReBuildHistoricalValues = true;
```

---

### 1.6 Встроенные (дочерние) индикаторы

Один индикатор может включать другие индикаторы внутри себя. Например, MACD использует два EMA, а KeltnerChannel — EMA + ATR.

```csharp
public class MyCompositeIndicator : Aindicator
{
    public IndicatorParameterInt _emaLength;
    public IndicatorParameterInt _atrLength;
    public Aindicator _ema;
    public Aindicator _atr;
    public IndicatorDataSeries _upperLine;
    public IndicatorDataSeries _lowerLine;

    public override void OnStateChange(IndicatorState state)
    {
        if (state == IndicatorState.Configure)
        {
            _emaLength = CreateParameterInt("EMA Length", 20);
            _atrLength = CreateParameterInt("ATR Length", 10);

            // Создаём встроенные индикаторы
            _ema = CreateIndicator("Ema", "EmaBase", new IndicatorParameterInt("Length", _emaLength.ValueInt));
            _atr = CreateIndicator("ATR", "AtrBase", new IndicatorParameterInt("Length", _atrLength.ValueInt));

            _upperLine = CreateSeries("Upper", Color.Blue, IndicatorChartPaintType.Line, true);
            _lowerLine = CreateSeries("Lower", Color.Blue, IndicatorChartPaintType.Line, true);
        }
    }

    public override void OnProcess(List<Candle> source, int index)
    {
        if (index < 1) return;

        decimal emaVal = _ema.DataSeries[0].Values[index];
        decimal atrVal = _atr.DataSeries[0].Values[index];
        decimal mult = 2.0m;

        _upperLine.Values[index] = emaVal + (atrVal * mult);
        _lowerLine.Values[index] = emaVal - (atrVal * mult);
    }
}
```

**Правила встроенных индикаторов:**
- Используйте `CreateIndicator(typeName, name, params IndicatorParameter[] parameters)`
- Дочерний индикатор автоматически добавляется в `IncludeIndicators`
- Его серии доступны через `_child.DataSeries[n].Values[index]`
- Для синхронизации параметров используйте `Bind()`

---

### 1.7 Каталог популярных индикаторов из `Scripts/`

В `OsEngine/Indicators/Scripts/` лежит **~110 готовых индикаторов**. Когда нужно написать новый индикатор, не пишите с нуля — найдите похожий в таблице, откройте файл и скопируйте логику.

| Имя файла | Что считает | Параметры | Серии | Особенности | Смотреть, если нужно |
|-----------|-------------|-----------|-------|-------------|----------------------|
| `Sma.cs` | Простая скользящая | Length, CandlePoint | 1 линия | Классика | Базовый шаблон |
| `Ema.cs` | Экспоненциальная скользящая | Length, CandlePoint | 1 линия | Рекурсивный расчёт | EMA вместо SMA |
| `WMA.cs` | Взвешенная скользящая | Length, CandlePoint | 1 линия | Линейно-взвешенная | Взвешенная по порядку |
| `VWMA.cs` | Объёмная скользящая | Length | 1 линия | Использует Volume | VWAP-подобное |
| `Bollinger.cs` | Полосы Боллинджера | Length, Deviation | 3 линии (Up, Down, Centre) | Встроенный Sma | Каналы, волатильность |
| `KeltnerChannel.cs` | Канал Кельтнера | EmaLength, AtrLength, Mult | 3 линии | Встроенные Ema + Atr | Альтернатива Bollinger |
| `RSI.cs` | Индекс относительной силы | Length | 1 линия + уровни | Диапазон 0–100 | Перекупленность/перепроданность |
| `MACD.cs` | MACD | Fast, Slow, Signal | 3 серии (MACD, Signal, Histogram) | Встроенные 2×Ema | Дивергенция, импульс |
| `Momentum.cs` | Моментум | Length | 1 линия | Close[i] / Close[i-Len] | Скорость изменения цены |
| `ATR.cs` | Средний истинный диапазон | Length | 1 линия | True Range | Волатильность, расчёт стопов |
| `CCI.cs` | Индекс товарного канала | Length | 1 линия | — | Циклический осциллятор |
| `Stochastic.cs` | Стохастик | %K, %D, Slow | 2 линии | Сглаживание | Осциллятор |
| `ADX.cs` | Индекс среднего направления | Period | 3 серии (+DI, -DI, ADX) | Встроенные +DI/-DI | Сила тренда |
| `ParabolicSAR.cs` | Параболик SAR | Step, MaxStep | 1 серия (точки) | `Point` тип | Трейлинг-стоп |
| `Ichimoku.cs` | Ишимоку | 4 периода | 5 линий | Сложная логика | Тренд, облако |
| `Alligator.cs` | Аллигатор | 3 периода | 3 линии (Jaw, Teeth, Lips) | — | Тренд Билла Вильямса |
| `AO.cs` | Awesome Oscillator | Fast, Slow | 1 гистограмма (Column) | Встроенные 2×Sma | Гистограмма импульса |
| `AC.cs` | Accelerator Oscillator | — | 1 гистограмма | Встроенный AO | Ускорение импульса |
| `ZigZag.cs` | Зигзаг | Depth, Deviation, Length | 2 серии | `CanReBuildHistoricalValues = true` | Свинги, волны |
| `ZigZagMACD.cs` | ZigZag по MACD | Depth, Deviation, Fast, Slow | 2 серии | Встроенный MACD | Свинги по MACD |
| `SuperTrend_indicator.cs` | Супертренд | Period, Mult | 1 линия + тренд | На базе ATR | Трендовый индикатор |
| `PivotFloor.cs` | Пивот-уровни | — | 7 серий (P, R1-R3, S1-S3) | — | Уровни поддержки/сопротивления |
| `LinearRegression.cs` | Линейная регрессия | Length | 3 серии (Line, Up, Down) | — | Статистический канал |
| `Volume.cs` | Объём | — | 1 Column | Прямое отображение | Объём |
| `AccumulationDistribution.cs` | A/D | — | 1 линия | Накопление/распределение | Объёмный индикатор |
| `WilliamsRangeTrade.cs` | Williams %R | Length | 1 линия + уровни | — | Осциллятор |
| `Envelops.cs` | Конверты | Length, Deviation | 3 линии | Встроенный Sma | Канал вокруг SMA |
| `PriceChannel.cs` | Ценовой канал | Length | 3 линии (Up, Down, Centre) | High/Low за период | Пробойные уровни |
| `PriceChannelAdaptive.cs` | Адаптивный ценовой канал | AdxLength, Ratio | 3 линии | Встроенный ADX | Динамический канал |
| `Fractal.cs` | Фрактал | Length | 1 серия (точки) | `Point` тип | Точки разворота |
| `Fisher.cs` | Fisher Transform | Length | 2 линии | Нормализация | Осциллятор |

**Как использовать каталог:**
1. Пользователь просит «индикатор RSI с фильтром» → открывай `RSI.cs`, копируй логику
2. Нужен канал с волатильностью → смотри `Bollinger.cs` или `KeltnerChannel.cs`
3. Нужен трендовый индикатор → `SuperTrend_indicator.cs`, `ParabolicSAR.cs`
4. Нужна гистограмма → `MACD.cs`, `AO.cs`
5. Нужна перерисовка истории → `ZigZag.cs` (обрати внимание на `CanReBuildHistoricalValues`)

---

### 1.8 Где размещать новый индикатор

**Жёсткое правило:** Перед созданием файла ОБЯЗАТЕЛЬНО спросить пользователя:
- «В какую папку сохранить индикатор? По умолчанию: `Indicators/Scripts/MyIndicators/`»
- Ждать ответа пользователя. Если молчит — предложить `Indicators/Scripts/MyIndicators/MyInd.cs`
- ЗАПРЕЩЕНО самостоятельно сохранять индикатор в корень `Indicators/Scripts/` рядом с ~110 системными индикаторами
- Пространство имён в файле должно соответствовать папке: `namespace OsEngine.Indicators.MyIndicators`

| Способ | Путь | Когда использовать |
|--------|------|------------------|
| Внутри проекта | `OsEngine/Indicators/Scripts/MyIndicators/MyInd.cs` | Если вы работаете в репозитории и собираете проект из исходников |
| Вне проекта (runtime) | `Custom/Indicators/Scripts/MyIndicators/MyInd.cs` (рядом с `.exe`) | Если пользователь кладёт файл рядом с собранным терминалом |
| DLL | `Custom/Indicators/Scripts/Dlls/MyLib.dll` | Внешние зависимости для скриптов |

**Правила размещения:**
1. Файл: `MyInd.cs`
2. Класс: `public class MyInd : Aindicator`
3. Атрибут: `[Indicator("MyInd")]` — по этому имени индикатор ищется в `IndicatorsFactory.CreateIndicatorByName`
4. Пространство имён: должно соответствовать папке, например `namespace OsEngine.Indicators.MyIndicators`
5. Компиляция: если скрипт сохраняется вне проекта, то не требуется — индикатор автоматически подхватывается фабрикой при первом обращении

---

### 1.9 Использование индикатора в роботе

В OsEngine индикаторы создаются **по-разному** в зависимости от типа таба. Ниже — полное руководство с примерами из реальных роботов.

---

#### 1.9.1 Создание индикатора на `BotTabSimple`

**Паттерн:** `IndicatorsFactory.CreateIndicatorByName` → `_tab.CreateCandleIndicator` → `_indicator.Save()`

**Пример 1: Bollinger + SMA (из `StrategyBollinger`)**

```csharp
public StrategyBollinger(string name, StartProgram startProgram) : base(name, startProgram)
{
    TabCreate(BotTabType.Simple);
    _tab = TabsSimple[0];

    // Создание Bollinger
    _bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "bollinger", false);
    _bollinger = (Aindicator)_tab.CreateCandleIndicator(_bollinger, "Prime");
    _bollinger.Save();

    // Создание SMA с изменением параметра
    _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
    _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");
    ((IndicatorParameterInt)_sma.Parameters[0]).ValueInt = 15;
    _sma.Save();

    _tab.CandleFinishedEvent += Bot_CandleFinishedEvent;
}
```

**Пример 2: ParabolicSAR с параметрами `Decimal` (из `ParabolicSarTrade`)**

```csharp
// Создание ParabolicSAR
_parabolic = IndicatorsFactory.CreateIndicatorByName("ParabolicSAR", name + "Par", false);
_parabolic = (Aindicator)_tab.CreateCandleIndicator(_parabolic, "Prime");
((IndicatorParameterDecimal)_parabolic.Parameters[0]).ValueDecimal = _parabolicAf.ValueDecimal;
((IndicatorParameterDecimal)_parabolic.Parameters[1]).ValueDecimal = _parabolicMaxAf.ValueDecimal;
_parabolic.Save();
```

**Пример 3: Envelops с перезагрузкой при смене параметров (из `EnvelopTrend`)**

```csharp
public EnvelopTrend(string name, StartProgram startProgram) : base(name, startProgram)
{
    // ...
    _envelop = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "Envelops", false);
    _envelop = (Aindicator)_tab.CreateCandleIndicator(_envelop, "Prime");
    ((IndicatorParameterInt)_envelop.Parameters[0]).ValueInt = _envelopMovingLength.ValueInt;
    ((IndicatorParameterDecimal)_envelop.Parameters[1]).ValueDecimal = _envelopDeviation.ValueDecimal;
    _envelop.Save();

    ParametrsChangeByUser += EnvelopTrend_ParametrsChangeByUser;
}

private void EnvelopTrend_ParametrsChangeByUser()
{
    ((IndicatorParameterInt)_envelop.Parameters[0]).ValueInt = _envelopMovingLength.ValueInt;
    ((IndicatorParameterDecimal)_envelop.Parameters[1]).ValueDecimal = _envelopDeviation.ValueDecimal;
    _envelop.Save();
    _envelop.Reload();
}
```

---

#### 1.9.2 Создание индикатора на `BotTabScreener`

**Паттерн:** `_screenerTab.CreateCandleIndicator(id, name, parameters, area)` — параметры передаются сразу строками.

**Пример 1: SMA на скринере (из `SmaScreener`)**

```csharp
public SmaScreener(string name, StartProgram startProgram) : base(name, startProgram)
{
    TabCreate(BotTabType.Screener);
    _screenerTab = TabsScreener[0];
    _screenerTab.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;

    // Создание индикатора: ID=1, имя="Sma", параметры=[длина, цена], область="Prime"
    _screenerTab.CreateCandleIndicator(1, "Sma",
        new List<string>() { _smaLength.ValueInt.ToString(), "Close" }, "Prime");

    ParametrsChangeByUser += SmaScreener_ParametrsChangeByUser;
}

private void SmaScreener_ParametrsChangeByUser()
{
    _screenerTab._indicators[0].Parameters
        = new List<string>() { _smaLength.ValueInt.ToString(), "Close" };
    _screenerTab.UpdateIndicatorsParameters();
}
```

**Пример 2: Два индикатора на разных областях (из `BollingerMomentumScreener`)**

```csharp
// Bollinger на основной области
_tabScreener.CreateCandleIndicator(1, "Bollinger", new List<string>() { "100", "2" }, "Prime");

// Momentum на отдельной области
_tabScreener.CreateCandleIndicator(2, "Momentum", new List<string>() { "15", "Close" }, "Second");
```

**Пример 3: Три индикатора + динамическое обновление (из `LinearRegressionFastScreener`)**

```csharp
// Создание в конструкторе
_screenerTab.CreateCandleIndicator(1, "ADX", new List<string>() { _adxFilterLength.ValueInt.ToString() }, "Second");
_screenerTab.CreateCandleIndicator(2, "LinearRegressionChannelFast_Indicator",
    new List<string>() { _lrLength.ValueInt.ToString(), "Close", _lrDeviation.ValueDecimal.ToString(), _lrDeviation.ValueDecimal.ToString() }, "Prime");
_screenerTab.CreateCandleIndicator(3, "Sma", new List<string>() { _smaFilterLen.ValueInt.ToString(), "Close" }, "Prime");

// Обновление при смене параметров пользователем
private void SmaScreener_ParametrsChangeByUser()
{
    _screenerTab._indicators[0].Parameters = new List<string>() { _adxFilterLength.ValueInt.ToString() };
    _screenerTab._indicators[1].Parameters = new List<string>()
    {
        _lrLength.ValueInt.ToString(),
        "Close",
        _lrDeviation.ValueDecimal.ToString(),
        _lrDeviation.ValueDecimal.ToString()
    };
    _screenerTab._indicators[2].Parameters = new List<string>() { _smaFilterLen.ValueInt.ToString(), "Close" };
    _screenerTab.UpdateIndicatorsParameters();
}
```

**Доступ к индикаторам в обработчике скринера:**

```csharp
private void _screener_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
{
    Aindicator bollinger = (Aindicator)tab.Indicators[0];
    Aindicator momentum = (Aindicator)tab.Indicators[1];

    decimal lastUp = bollinger.DataSeries[0].Values[bollinger.DataSeries[0].Values.Count - 1];
    decimal lastMomentum = momentum.DataSeries[0].Values[momentum.DataSeries[0].Values.Count - 1];
}
```

---

#### 1.9.3 Таблица сравнения: `BotTabSimple` vs `BotTabScreener`

| Аспект | `BotTabSimple` | `BotTabScreener` |
|--------|----------------|------------------|
| **Создание** | `IndicatorsFactory.CreateIndicatorByName(name, instanceName, false)` → `_tab.CreateCandleIndicator(ind, area)` | `_screenerTab.CreateCandleIndicator(id, name, parameters, area)` |
| **Тип параметров** | `IndicatorParameterInt`, `Decimal`, `Bool`, `String` | Строки `List<string>()` |
| **Параметры при создании** | Задаются после создания через `_indicator.Parameters[n]` | Передаются сразу в `CreateCandleIndicator` |
| **Изменение параметров** | `_indicator.ParametersDigit[0].Value = X; _indicator.Save(); _indicator.Reload();` | `_screenerTab._indicators[n].Parameters = new List<string>(); _screenerTab.UpdateIndicatorsParameters();` |
| **Доступ в обработчике** | Через поле класса `_bollinger` | Через `tab.Indicators[n]` (где `tab` — конкретный инструмент) |
| **Области отрисовки** | `"Prime"`, `"NewArea0"`, `"NewArea1"` | `"Prime"`, `"Second"` |
| **Количество** | Обычно 1–3 индикатора | Можно много, на каждый инструмент скринера |

---

#### 1.9.4 Изменение параметров из робота

**На `BotTabSimple`:**

```csharp
// Типизированный доступ
((IndicatorParameterInt)_sma.Parameters[0]).ValueInt = 50;
((IndicatorParameterDecimal)_bollinger.Parameters[1]).ValueDecimal = 2.5m;
_sma.Save();
_sma.Reload();
```

**На `BotTabScreener`:**

```csharp
// Строковый доступ через _indicators
_screenerTab._indicators[0].Parameters = new List<string>() { "50", "Close" };
_screenerTab.UpdateIndicatorsParameters(); // важно: вызываем обновление у мастера
```

---

#### 1.9.5 Доступ к значениям из обработчика

**Проверка готовности — обязательна:**

```csharp
private void _tab_CandleFinishedEvent(List<Candle> candles)
{
    // 1. Достаточно ли свечей
    if (candles.Count < _smaLength.ValueInt + 5) return;

    // 2. Индикатор успел рассчитаться?
    if (_sma.DataSeries[0].Values.Count < candles.Count) return;

    // 3. Получаем значения
    decimal lastSma = _sma.DataSeries[0].Last;
    decimal lastClose = candles[candles.Count - 1].Close;

    // 4. Доступ к нескольким сериям одного индикатора
    decimal bollingerUp = _bollinger.DataSeries[0].Last;    // верхняя линия
    decimal bollingerDown = _bollinger.DataSeries[1].Last;  // нижняя линия
    decimal bollingerCenter = _bollinger.DataSeries[2].Last;// средняя
}
```

**На скринере:**

```csharp
private void _screener_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
{
    if (candles.Count < 10) return;
    if (tab.Indicators.Count == 0) return;

    Aindicator sma = (Aindicator)tab.Indicators[0];
    if (sma.DataSeries[0].Values.Count < candles.Count) return;

    decimal lastSma = sma.DataSeries[0].Last;
}
```

---

#### 1.9.6 Удаление индикатора

```csharp
// На BotTabSimple
_tab.DeleteCandleIndicator(_sma);

// На скринере (в обработчике)
tab.DeleteCandleIndicator(tab.Indicators[0]);
```

---

### 1.10 Частые ошибки при написании индикаторов

| Ошибка | Почему плохо | Как правильно |
|--------|-------------|---------------|
| **Создавать параметры в `OnProcess`** | Параметры будут пересоздаваться на каждой свече, всё сломается | Только в `OnStateChange(IndicatorState.Configure)` |
| **Не проверять `index < Length`** | `IndexOutOfRangeException` на первых свечах | Всегда защитный `if` в начале `OnProcess` |
| **Использовать `source[source.Count-1]` вместо `source[index]`** | Индикатор считает только последнюю свечу, история пересчитается неверно | Использовать `source[index]` |
| **Забыть `[Indicator("Name")]`** | Фабрика может не найти индикатор при рефлексии | Атрибут обязателен |
| **Имя класса ≠ имени файла** | Путаница, сложно искать, фабрика ищет по имени класса | `MyInd.cs` → `public class MyInd` |
| **Для ZigZag не установить `CanReBuildHistoricalValues = true`** | Исторические точки не пересчитаются при появлении новых данных | `series.CanReBuildHistoricalValues = true` |
| **Не использовать `decimal`** | Ценовые данные — `decimal`, `double`/`float` недопустимы | Всегда `decimal` для цен и значений |
| **Забыть `using OsEngine.Entity`** | Нет доступа к `Candle`, `List<Candle>` | Обязательный using |
| **Менять параметр без `Save()` + `Reload()`** | Изменение не применится, график не обновится | `_ind.ParametersDigit[0].Value = X; _ind.Save(); _ind.Reload();` |
| **Создавать серии вне `Configure`** | Серии создаются только один раз при инициализации | Только в `OnStateChange(Configure)` |

---

### 1.11 Быстрый справочник: от задачи к индикатору

| Задача пользователя | Искать пример в | Что копировать |
|---------------------|-----------------|----------------|
| «Скользящая средняя» | `Sma.cs`, `Ema.cs`, `WMA.cs`, `VWMA.cs` | Расчёт средней, параметр Length |
| «Канал вокруг цены» | `Bollinger.cs`, `KeltnerChannel.cs`, `Envelops.cs` | Три серии (Up, Centre, Down), встроенный SMA/EMA |
| «Осциллятор 0–100» | `RSI.cs`, `Stochastic.cs`, `WilliamsRangeTrade.cs` | Нормализация, уровни |
| «Трендовый индикатор» | `SuperTrend_indicator.cs`, `ParabolicSAR.cs`, `ADX.cs` | ATR-логика, точки |
| «Гистограмма» | `MACD.cs`, `AO.cs`, `AC.cs` | `Column`, встроенные SMA/EMA |
| «Уровни поддержки/сопротивления» | `PivotFloor.cs`, `PriceChannel.cs` | Фиксированные/плавающие уровни |
| «Перерисовывающийся индикатор» | `ZigZag.cs`, `ZigZagMACD.cs` | `CanReBuildHistoricalValues`, поиск экстремумов |
| «Сложный индикатор из нескольких» | `MACD.cs`, `KeltnerChannel.cs`, `Ichimoku.cs` | `CreateIndicator()`, `Bind()` |
| «Индикатор объёма» | `Volume.cs`, `AccumulationDistribution.cs` | Работа с `candle.Volume` |
| «Свечной паттерн в индикаторе» | `Fractal.cs` | Поиск High/Low в окне, `Point` тип |

**Изучать:**
- `Sample1Blank.cs` — минимальный скелет
- `Sample2IndicatorParameters.cs` — все типы параметров
- `Sample3IndicatorDataSeries.cs` — запись данных в серию
- `Sma.cs` — базовый индикатор
- `MACD.cs` — пример встроенных индикаторов
- `ZigZag.cs` — пример перерисовки истории

---

### 1.12 Области чарта при создании множества индикаторов на одном источнике

В OsEngine график разделён на области. Каждая область — отдельный слой под основным графиком цены.

**Доступные области:**

| Область | Назначение | Примеры индикаторов |
|---------|-----------|---------------------|
| `Prime` | Главная область. Там свечи и все ценовые индикаторы | EMA, SMA, Bollinger, Envelopes, ParabolicSAR, SuperTrend |
| `NewArea0` | Первая дополнительная область под графиком | RSI, Stochastic, CCI, ADX |
| `NewArea1` | Вторая дополнительная область | ATR, объёмы, гистограммы |
| `NewArea2` | Третья дополнительная область | Дополнительные осцилляторы |
| `NewAreaN` | И так далее — каждый новый индикатор на новую область | — |

**Главное правило:**

> Каждый неценовой индикатор размещается на **своей отдельной области**. Не кладите два разных индикатора на одну `NewArea` — они наложатся друг на друга и график станет нечитаемым.

**Правильно:**
```csharp
// EMA на главной области со свечами
_ema = (Aindicator)_tab.CreateCandleIndicator(_ema, "Prime");

// ADX — на первую дополнительную область
_adx = (Aindicator)_tab.CreateCandleIndicator(_adx, "NewArea0");

// ATR — на вторую дополнительную область
_atr = (Aindicator)_tab.CreateCandleIndicator(_atr, "NewArea1");

// RSI — на третью дополнительную область
_rsi = (Aindicator)_tab.CreateCandleIndicator(_rsi, "NewArea2");
```

**Неправильно:**
```csharp
// ADX и ATR на одной области — наложатся!
_adx = (Aindicator)_tab.CreateCandleIndicator(_adx, "NewArea0");
_atr = (Aindicator)_tab.CreateCandleIndicator(_atr, "NewArea0"); // ОШИБКА
```

**Классификация индикаторов по областям:**

| Тип | Область | Признаки |
|-----|---------|----------|
| **Ценовые** | `Prime` | Линии, каналы, точки на графике цены. Значения близки к цене. |
| **Осцилляторы** | `NewArea0`, `NewArea1`... | Диапазонные значения (0–100, −100..100). Не привязаны к цене напрямую. |
| **Волатильность** | `NewAreaN` | ATR, волатильность. Свои масштабы, не пересекаются с ценой. |
| **Объёмы** | `NewAreaN` | Volume, AccumulationDistribution. Отдельный масштаб. |
| **Гистограммы** | `NewAreaN` | MACD-гистограмма, AO, AC. Столбцы требуют свободного пространства. |

**На скринере:**

Скринер использует названия `Prime` и `Second` (вместо `NewArea0`):
```csharp
// Ценовой индикатор
_tabScreener.CreateCandleIndicator(1, "Bollinger", new List<string>() { "100", "2" }, "Prime");

// Осциллятор — на отдельную область
_tabScreener.CreateCandleIndicator(2, "RSI", new List<string>() { "14" }, "Second");
```

**Правило для ИИ:**

При создании робота с несколькими индикаторами:
1. Ценовые (EMA, SMA, Bollinger) → `Prime`
2. Первый осциллятор → `NewArea0` (или `Second` на скринере)
3. Каждый следующий неценовой индикатор → следующая `NewArea` (`NewArea1`, `NewArea2`...)
4. Никогда не класть два разных индикатора на одну и ту же `NewArea`
