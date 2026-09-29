# CONTEXT_DIVIDENDS_UPDATER — дивиденды в OsEngine (сбор данных и использование)

> Контекст про дивиденды в OsEngine: как данные собираются (утилита `DividendsUpdater`)
> и как они используются роботами, тестером и оптимизатором через `WikiMaster`.

## 1. Цель

OsEngine использует дивидендные данные по российским акциям Мосбиржи: роботы фильтруют
инструменты по факту/размеру/доходности выплат, а тестер и оптимизатор начисляют/списывают
дивиденды по открытым позициям. Данные собирает консольная утилита `DividendsUpdater` с сайта
**Smart-Lab** (`https://smart-lab.ru/q/{TICKER}/dividend/`) и сохраняет в markdown-файлы
`OsEngine/bin/Debug/Wiki/Dividends/{TICKER}.md`. Читает их статический класс `WikiMaster`
(`OsEngine/Wiki/WikiMaster.cs`) — основной способ доступа из роботов.

## 2. Апдейтер: расположение и имя

- Путь: `project\Tests\DividendsUpdater\`.
- Имя проекта: `DividendsUpdater` (`.csproj`, `net10.0-windows`, консоль).
- После сборки артефакты (`exe`/`dll`/`runtimeconfig.json`/`deps.json`/`pdb`) копируются
  в `OsEngine\bin\Debug\` (target `CopyDividendsUpdaterToOsEngineBin` в `.csproj`) —
  `WikiMaster.UpdateDividendsBase()` запускает именно копию рядом с `OsEngine.exe`,
  с fallback на `Tests/DividendsUpdater/`.

## 3. Апдейтер: как устроен

- `Program.cs` — вход: загрузка настроек, разбор аргументов, запуск `DividendUpdater`.
- `Models/AppSettings.cs` — настройки (путь, задержка, таймаут).
- `Services/AppSettingsService.cs` — загрузка/сохранение `app-settings.json` (рядом с `.exe`).
- `Services/OsEnginePathResolver.cs` — определение пути к `OsEngine.exe` (по настройкам или подъём вверх по дереву).
- `Services/WikiSecuritiesReader.cs` — чтение списка российских акций из `Wiki/tinvest_securities.md`.
- `Services/SmartLabParser.cs` — HTTP-парсинг страницы дивидендов Smart-Lab.
- `Services/WikiDividendsWriter.cs` — запись markdown-файла дивидендов.
- `Services/NumberParser.cs` — нормализация чисел (запятая/точка).

## 4. Апдейтер: как работает

1. `EnsureDividendsFolder` — создаёт `Wiki/Dividends`, если нет.
2. `WikiSecuritiesReader.ReadRussianStocks` — из `Wiki/tinvest_securities.md` (блок `jsonl`)
   берёт бумаги, где `SecurityType == "Stock"` и `NameClass` содержит `rub`.
3. Если передан `--ticker` — оставляет только указанные тикеры.
4. Для каждой бумаги `SmartLabParser.ParseDividends` парсит страницу дивидендов
   и возвращает записи (год, дата отсечки Т-1, сумма, доходность).
5. `WikiDividendsWriter.SaveDividends` пишет `Wiki/Dividends/{TICKER}.md`.
6. В конце — сводка `Completed. Success: N, Failed: M`.

## 5. Апдейтер: как запускать

```bash
cd Tests/DividendsUpdater
dotnet run -- [--ticker SBER,GAZP] [--interactive]
```

Либо собранный exe (в `OsEngine/bin/Debug/` или `Tests/DividendsUpdater/bin/Debug/net10.0-windows/`):

```bash
./DividendsUpdater.exe                    # все российские акции
./DividendsUpdater.exe --ticker SBER,GAZP # только указанные тикеры (через запятую)
./DividendsUpdater.exe --interactive      # спросить настройки интерактивно
```

- Без `--ticker` обрабатываются все акции из Wiki.
- `--interactive` — промпт по пути/задержке и сохранение в `app-settings.json`.

## 6. Апдейтер: настройки (`app-settings.json` рядом с `.exe`)

| Поле | По умолчанию | Описание |
|---|---|---|
| `OsEnginePath` | авто-поиск | Путь к `OsEngine.exe` (определяет папку `Wiki/Dividends`) |
| `RequestDelayMs` | `500` | Задержка между HTTP-запросами к Smart-Lab |
| `HttpTimeoutSeconds` | `30` | Таймаут запроса |

Файл создаётся автоматически при первом запуске; `--interactive` перезаписывает.

## 7. Апдейтер: что на выходе

Консоль (построчно):

```
[Updater] Starting dividends update...
[Updater] OsEngine path: <...>
[Updater] Found 245 stocks to process
[FileService] Saved dividends: <...>\Wiki\Dividends\SBER.md
[Updater] No dividends found for TATNP
[Updater] Completed. Success: 240, Failed: 5
```

Файлы `Wiki/Dividends/{TICKER}.md`:

```markdown
# Dividends: SBER

## Metadata
| Field | Value |
| Security | SBER |
| LastUpdated | dd.MM.yyyy |
| Source | https://smart-lab.ru/q/SBER/dividend/ |

## Historical Dividends
| Year | RegistryCloseDate | DividendAmount | DividendYield |
...

## Future Registry Close Dates
| Year | RegistryCloseDate | DividendAmount | DividendYield |
...
```

## 8. Апдейтер: длительность

Минуты (не секунды): сотни акций × HTTP-запрос + задержка `RequestDelayMs` (500 мс) между ними.
Точного замера нет — зависит от числа акций в Wiki и сети.

## 9. Откуда берутся данные

- **Источник — только Smart-Lab**, данные только по акциям Мосбиржи.
- Один markdown-файл — один тикер: `Wiki/Dividends/<TICKER>.md`.
- После нормализации поле `RegistryCloseDate` хранит **дату Т-1** (последний день владения для получения дивиденда), а не дату реестра — единообразно для периодов Т+1 и Т+2.
- **Привилегированные акции** (`SBERP`, `SNGSP`, …) — отдельной страницы на Smart-Lab нет (404 или пусто). Апдейтер делает fallback на страницу базовой бумаги (`SBERP` → `SBER`) и берёт строки только своего тикера. `Source` в файле указывает фактически использованную страницу. Часть префов Smart-Lab не покрывает — файла не будет.
- **Кэш**: `WikiDividendsApi`/`WikiSecuritiesApi` держат статический кэш; `WikiMaster` грузит файл один раз, дальше из памяти. Файл не найден — возвращается пустой результат, не исключение.

## 10. Использование в роботах: WikiMaster

`WikiMaster` — статическая обёртка над `WikiDividendsApi`/`WikiSecuritiesApi`. Все методы безопасны (при ошибке пустой результат + лог в `ServerMaster`).

| Метод | Возвращает | Описание |
|-------|-----------|----------|
| `GetDividendsHistory(ticker, date?)` | `WikiDividendHistory` | Все записи с `registry_close_date <= date`. |
| `GetDividendsFuture(ticker, date?)` | `WikiDividendFuture` | Ближайшая будущая запись с `registry_close_date >= date`. |
| `GetDividendsPast(ticker, date?)` | `WikiDividendPast` | Ближайшая прошлая запись с `registry_close_date <= date`. |
| `SearchDividendsByDate(ticker, date)` | `WikiDividendSearch` | Все записи на конкретную дату. |

Модели:

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

Минимальный пример (внутри `CandleFinishedEvent`):

```csharp
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

## 11. Примеры роботов

- **KeltnerDividendScreener** (`OsEngine/Robots/Dividends/KeltnerDividendScreener.cs`) — Long, Keltner Channel, вход Close выше верхней линии, выход ниже нижней. Фильтр: ближайшие прошлые дивиденды за `LookbackDays` и доходность выше `MinDividendYieldPercent`.
- **ShortBadDividends** (`OsEngine/Robots/Dividends/ShortBadDividends.cs`) — Short, Adaptive Price Channel (`PriceChannelAdaptive`). Фильтр: прошлые дивиденды за `LookbackDays` и доходность ниже `MaxDividendYieldPercent`.
- **DividendCaptureScreener** (`OsEngine/Robots/Dividends/DividendCaptureScreener.cs`) — Long, SMA. Вход в окне `Days before registry` до даты Т-1, выход на следующий торговый день после Т-1. Фильтр: будущие дивиденды (`GetDividendsFuture`).
- **RebalancerClassicDividend** (`OsEngine/Robots/Rebalancers/RebalancerClassicDividend.cs`) — Long, агрессивная часть — дивидендные акции, защитная — золото. Дивидендный режим: будущие дивиденды; классический: прошлые (до 380 дней). Fallback-тикеры (SBER/SBERP/GAZP/LKOH/VTBR) в тестере при отсутствии данных.

## 12. Паттерны использования

- **По наличию**: `WikiMaster.GetDividendsPast(ticker, date)?.past != null`.
- **По свежести (lookback)**: распарсить `registry_close_date` (`dd.MM.yyyy`) и проверить `recordDate >= referenceDate.AddDays(-LookbackDays)`.
- **По доходности**: `dividend_yield > MinDividendYieldPercent` (long) / `< MaxDividendYieldPercent` (short).
- Обычно комбинация `свежесть + доходность`, всё в `try-catch` (ошибка → `false` + лог).

## 13. Начисление в тестере/оптимизаторе

- В тестере вкладка **Dividends**: «Open data base» (папка `Wiki/Dividends`), «Update data base» (запускает `DividendsUpdater.exe`, перепарсивает Smart-Lab, сбрасывает кэш). В оптимизаторе — аналогично в хранилище данных.
- Синтетическая позиция `<тикер>_divs` создаётся на первый торговый день после даты Т-1; направленная: лонг — с профитом, шорт — с убытком на размер дивиденда.
- Начисление/списание идёт с задержкой **7 календарных дней** после Т-1: лонгу — плюс, шорту — минус.
- Сумма считается с учётом НДФЛ **13%** (для обоих направлений).
- Дедупликация — по ключу `робот + таб + номер позиции + тикер + дата` (каждый робот и каждая позиция обрабатываются независимо).
- Позиция должна быть открыта до или в дату Т-1.

## 14. Ограничения и особенности

1. **Только акции Мосбиржи.** Для других рынков файлов нет.
2. **Smart-Lab — единственный источник.** Устаревшие/ошибочные данные в файле робот использует как есть.
3. **Формат дат:** `dd.MM.yyyy`; парсить через `DateTime.TryParseExact` с `CultureInfo.InvariantCulture`.
4. **Проценты, а не доли:** `dividend_yield` = 5.0 означает 5%, не 0.05.
5. **Отсутствие файла ≠ ошибка** — `WikiMaster` вернёт пустой объект.
6. **Кэш статический** — первый вызов грузит с диска, дальше из памяти.

## 15. Чек-лист: создание робота с дивидендным фильтром

1. Наследовать `BotPanel`, добавить атрибут `[Bot("RobotName")]`.
2. Параметры: `LookbackDays`, `MinDividendYieldPercent`/`MaxDividendYieldPercent` + индикатор/объём/режим/лимит.
3. В `CandleFinishedEvent` — `WikiMaster.GetDividendsPast(ticker, referenceDate)`.
4. Проверить: запись не null, дата распарсилась, дата в окне lookback, доходность удовлетворяет условию.
5. Фильтр в `try-catch`: ошибка → `false` + лог.
6. Собрать решение, проверить робота в тестере на инструментах с известными дивидендами.
