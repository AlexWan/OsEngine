# CONTEXT_DIVIDENDS — Дивиденды в OsEngine

> Постоянный контекст для ИИ-агентов, работающих с дивидендами в OsEngine. Здесь объяснено, откуда берутся данные, как получить к ним доступ из робота, какие паттерны использования поддерживаются, и как создать нового робота с дивидендным фильтром.

---

## Введение

OsEngine может использовать дивидендные данные при торговле акциями на Московской бирже. Данные загружаются из markdown-файлов Wiki (`OsEngine/bin/Debug/Wiki/Dividends/`) и доступны из кода робота через статический класс `WikiMaster` (`OsEngine/Wiki/WikiMaster.cs`). Это основной и рекомендуемый способ для торговых роботов.

Цель использования дивидендов в роботе — фильтровать инструменты по факту выплаты дивидендов, их размеру или доходности.

---

## 1. Примеры роботов

### 1.1. KeltnerDividendScreener

- **Файл:** `OsEngine/Robots/Dividends/KeltnerDividendScreener.cs`
- **Направление:** только Long.
- **Индикатор:** Keltner Channel.
- **Вход:** Close выше верхней линии канала.
- **Выход:** Close ниже нижней линии канала.
- **Дивидендный фильтр:**
  - ближайшие прошлые дивиденды за `LookbackDays`;
  - доходность выше `MinDividendYieldPercent`.

### 1.2. ShortBadDividends

- **Файл:** `OsEngine/Robots/Dividends/ShortBadDividends.cs`
- **Направление:** только Short.
- **Индикатор:** Adaptive Price Channel (`PriceChannelAdaptive`).
- **Вход:** Close ниже нижней линии канала.
- **Выход:** Close выше верхней линии канала.
- **Дивидендный фильтр:**
  - ближайшие прошлые дивиденды за `LookbackDays`;
  - доходность ниже `MaxDividendYieldPercent` (разочаровывающие дивиденды).

### 1.3. DividendCaptureScreener

- **Файл:** `OsEngine/Robots/Dividends/DividendCaptureScreener.cs`
- **Направление:** только Long.
- **Индикатор:** SMA (`Close`).
- **Вход:**
  - в окне `Days before registry` дней до ближайшей даты Т-1;
  - Close выше SMA (если включён параметр `Filter sma is on`).
- **Выход:** на следующий торговый день после даты Т-1 в заданное время (`Exit time`).
- **Дивидендный фильтр:** будущие дивиденды через `WikiMaster.GetDividendsFuture`.

---

## 2. Откуда берутся данные

### 2.1. Источник

- **Smart-Lab** — единственный источник дивидендов.
- Данные хранятся в markdown-файлах по пути `OsEngine/bin/Debug/Wiki/Dividends/<TICKER>.md`.
- Один файл — один тикер.
- Важно: после нормализации поле `RegistryCloseDate` в Wiki хранит **дату Т-1** (последний день, когда позиция должна быть открыта для получения дивиденда), а не дату реестра. Это позволяет единообразно работать с периодами Т+1 и Т+2.

### 2.2. Кэш

- `WikiDividendsApi` и `WikiSecuritiesApi` держат в памяти статический кэш (`_cache`, `_cacheLoaded`, `_cacheLocker`).
- `WikiMaster` использует этот же кэш: файл загружается один раз, последующие вызовы идут из памяти.
- Если файл не найден — методы возвращают пустой результат, а не исключение.

### 2.3. Обновление данных

Обновлятор дивидендов — это отдельная консольная программа в `Tests/DividendsUpdater/`. После сборки запускается как `DividendsUpdater.exe` в двух местах:

- `Tests/DividendsUpdater/bin/Debug/net10.0-windows/DividendsUpdater.exe` — исходная сборка;
- `OsEngine/bin/Debug/DividendsUpdater.exe` — копия рядом с `OsEngine.exe`.

`WikiMaster.UpdateDividendsBase()` запускает именно ту копию updater’а, которая лежит рядом с `OsEngine.exe`. Если её нет — fallback на поиск в `Tests/DividendsUpdater/`.

`DividendsUpdater.exe` сам определяет своё местоположение:

- если рядом с ним лежит `OsEngine.exe`, он считает эту папку рабочей;
- если `OsEngine.exe` рядом нет (запуск из `Tests`), updater ищет `OsEngine.exe` по настройкам или относительно расположения своего exe, поднимаясь вверх по дереву каталогов.

В обоих случаях данные пишутся в `OsEngine/bin/Debug/Wiki/Dividends/{TICKER}.md`.

Он читает список российских акций из Wiki, для каждой бумаги парсит страницу `https://smart-lab.ru/q/{TICKER}/dividend/` и сохраняет даты Т-1, суммы и доходности. Поддерживает запуск по всем бумагам, по списку тикеров (`--ticker SBER,GAZP`) и интерактивный режим (`--interactive`). В роботах данные считаются статичными на момент запуска; параметр `refresh` в `WikiMaster` позволяет перечитать файл с диска, но обычно не нужен.

### 2.4. Выплаты дивидендов в тестере

**Файл:** `OsEngine/Market/Servers/Tester/TesterServer.cs`

- В окне тестера есть вкладка **Dividends** с таблицей выплат и кнопками:
  - **Open data base** — открывает папку `Wiki/Dividends`.
  - **Update data base** — запускает `DividendsUpdater.exe`, перепарсивает Smart-Lab и сбрасывает кэш Wiki.
- Синтетическая позиция `<тикер>_divs` в журнале робота создаётся на первый торговый день после даты Т-1.
- Запись в таблицу выплат тестера и зачисление денег на виртуальный портфель происходят с задержкой **7 календарных дней** после Т-1.
- Позиция должна быть открыта до или в дату Т-1.
- Сумма выплаты и доходность синтетической позиции рассчитываются с учётом НДФЛ **13%** (брокер удерживает налог при зачислении дивиденда).
- В таблице отображаются: бумага, дата выплаты, сумма, имя робота.

### 2.5. Выплаты дивидендов в оптимизаторе

**Файл:** `OsEngine/Market/Servers/Optimizer/OptimizerServer.cs`

- Логика начисления идентична тестеру.
- Доступ к текущему боту прогона осуществляется через поле `MyRobot`, которое заполняется в `OptimizerExecutor` и `OptimizerMaster`.
- В UI хранилища данных оптимизатора (`OptimizerDataStorageUi`) есть вкладка **Dividends** с чекбоксом включения и кнопками:
  - **Open data base** — открывает папку `Wiki/Dividends`.
  - **Update data base** — запускает `DividendsUpdater.exe`.
- Синтетические позиции `<тикер>_divs` создаются в журнале бота на первый торговый день после Т-1. Зачисление в портфель происходит с задержкой **7 календарных дней** после Т-1. Сумма выплаты рассчитывается с учётом НДФЛ **13%**.

---

## 3. Доступ к дивидендам из робота: WikiMaster

### 3.1. Расположение

```
OsEngine/Wiki/WikiMaster.cs
```

`WikiMaster` — статический класс-обёртка над `WikiDividendsApi` / `WikiSecuritiesApi`. Все методы безопасны: при ошибке возвращается пустой результат, а ошибка логируется в `ServerMaster`.

### 3.2. Методы для дивидендов

| Метод | Возвращает | Описание |
|-------|-----------|----------|
| `GetDividendsHistory(string ticker, DateTime? date = null)` | `WikiDividendHistory` | Все записи с `registry_close_date <= date`. |
| `GetDividendsFuture(string ticker, DateTime? date = null)` | `WikiDividendFuture` | Одна ближайшая будущая запись с `registry_close_date >= date`. |
| `GetDividendsPast(string ticker, DateTime? date = null)` | `WikiDividendPast` | Одна ближайшая прошлая запись с `registry_close_date <= date`. |
| `SearchDividendsByDate(string ticker, DateTime date)` | `WikiDividendSearch` | Все записи на конкретную дату. |

### 3.3. Модели данных

```csharp
public class WikiDividendRecord
{
    public int year { get; set; }
    public string registry_close_date { get; set; } // формат "dd.MM.yyyy"
    public decimal dividend_amount { get; set; }
    public decimal dividend_yield { get; set; }     // в процентах, например 5.0 = 5%
}

public class WikiDividendPast
{
    public string ticker { get; set; }
    public string date { get; set; }
    public string source { get; set; }
    public string last_updated { get; set; }
    public WikiDividendRecord past { get; set; }
}
```

### 3.4. Минимальный пример использования

```csharp
using OsEngine.Wiki;

// Внутри CandleFinishedEvent
string ticker = tab.Security.Name;
DateTime referenceDate = candles[candles.Count - 1].TimeStart;

WikiDividendPast dividendPast = WikiMaster.GetDividendsPast(ticker, referenceDate);

if (dividendPast?.past != null)
{
    string date = dividendPast.past.registry_close_date;
    decimal yield = dividendPast.past.dividend_yield;
    decimal amount = dividendPast.past.dividend_amount;

    // ... логика фильтра
}
```

---

## 4. Паттерны использования дивидендов в роботах

### 4.1. Фильтр по наличию дивидендов

Проверить, что у бумаги вообще были дивиденды на заданную дату или ранее:

```csharp
WikiDividendPast past = WikiMaster.GetDividendsPast(ticker, referenceDate);
bool hasDividends = past?.past != null;
```

### 4.2. Фильтр по свежести (lookback)

Проверить, что ближайшие прошлые дивиденды выплачены не позже, чем N дней назад:

```csharp
DateTime minDate = referenceDate.AddDays(-_lookbackDays.ValueInt);

if (!DateTime.TryParseExact(dividendPast.past.registry_close_date, "dd.MM.yyyy",
    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime recordDate))
{
    return false;
}

bool isFresh = recordDate >= minDate;
```

### 4.3. Фильтр по доходности

Поле `dividend_yield` содержит доходность в процентах.

**Только высокодивидендные бумаги (long-фильтр):**

```csharp
bool isHighYield = dividendPast.past.dividend_yield > _minDividendYieldPercent.ValueDecimal;
```

**Только низкодивидендные / разочаровывающие бумаги (short-фильтр):**

```csharp
bool isLowYield = dividendPast.past.dividend_yield < _maxDividendYieldPercent.ValueDecimal;
```

### 4.4. Комбинированный фильтр

Обычно используется комбинация: `свежесть + доходность`.

```csharp
private bool IsDividendFilterPassed(BotTabSimple tab, DateTime referenceDate)
{
    try
    {
        string ticker = tab.Security?.Name;
        if (string.IsNullOrWhiteSpace(ticker))
            return false;

        WikiDividendPast dividendPast = WikiMaster.GetDividendsPast(ticker, referenceDate);

        if (dividendPast?.past == null
            || string.IsNullOrWhiteSpace(dividendPast.past.registry_close_date))
        {
            return false;
        }

        if (!DateTime.TryParseExact(dividendPast.past.registry_close_date, "dd.MM.yyyy",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime recordDate))
        {
            return false;
        }

        DateTime minDate = referenceDate.AddDays(-_lookbackDays.ValueInt);

        if (recordDate < minDate)
            return false;

        return dividendPast.past.dividend_yield > _minDividendYieldPercent.ValueDecimal;
    }
    catch (Exception error)
    {
        SendNewLogMessage($"Dividend filter error: {error}", LogMessageType.Error);
        return false;
    }
}
```

---

## 5. Ограничения и особенности

1. **Только акции Мосбиржи.** Дивидендные данные собираются по российским акциям; для других рынков файлов нет.
2. **Smart-Lab — единственный источник.** Если данные в файле устарели или ошибочные, робот будет использовать именно их.
3. **Формат дат:** `dd.MM.yyyy`. При парсинге используйте `DateTime.TryParseExact` с `CultureInfo.InvariantCulture`.
4. **Проценты, а не доли:** `dividend_yield` = 5.0 означает 5%, а не 0.05.
5. **Отсутствие файла ≠ ошибка.** `WikiMaster` вернёт пустой объект; в фильтре это трактуется как «дивидендов не было».
6. **Кэш статический.** Первый вызов загружает файл с диска; последующие вызовы идут из памяти.
7. **Короткие позиции.** Для входа в шорт используйте `tab.SellAtIcebergMarket(...)`, для выхода — `tab.CloseAtIcebergMarket(...)`.

---

## 6. Чек-лист: создание робота с дивидендным фильтром

1. Наследовать `BotPanel`, добавить атрибут `[Bot("RobotName")]`.
2. Добавить параметры:
   - `LookbackDays` — глубина истории дивидендов;
   - `MinDividendYieldPercent` / `MaxDividendYieldPercent` — порог доходности;
   - параметры индикатора, объёма, режима, лимита позиций.
3. В `CandleFinishedEvent` получить `WikiDividendPast` через `WikiMaster.GetDividendsPast(ticker, referenceDate)`.
4. Проверить:
   - запись не null;
   - дата `registry_close_date` распарсилась;
   - дата в окне `referenceDate.AddDays(-LookbackDays)`;
   - доходность удовлетворяет условию.
5. Обработать фильтр в `try-catch`: при ошибке вернуть `false` и залогировать.
6. Собрать решение (`dotnet build OsEngine.sln`).
7. Проверить робота в тестере на инструментах с известными дивидендами.

---

## 7. Полезные ссылки

- `CONTEXT_ROBOTS.md` — каталог готовых роботов.
