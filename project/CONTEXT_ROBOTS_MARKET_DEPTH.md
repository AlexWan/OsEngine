# CONTEXT_ROBOTS_MARKET_DEPTH — HFT-роботы на стаканных/лентовых индикаторах

Как создавать и тестировать торговых роботов на базе индикаторов слоя `AMarketDepth`.

Сами индикаторы — в `CONTEXT_INDICATORS_MARKET_DEPTH.md`, их описания — в `CONTEXT_INDICATORS_MARKET_DEPTH_DESCRIPTIONS.md`.
Описания готовых роботов (примеры) — в `CONTEXT_ROBOTS_MARKET_DEPTH_DESCRIPTIONS.md`.
Общая архитектура роботов — в `CONTEXT_ROBOTS_ARCHITECTURE.md`.

## 1. Данные для тестирования

Роботы на стакане/ленте тестируются на исторических данных QSH (Qscalp).

Форматы:

| Файл | Содержимое | streamCount |
|---|---|---|
| `Quotes.qsh` | только стакан | 1 |
| `Deals.qsh` | только лента сделок | 1 |
| `.Both.qsh` | стакан + лента | 2 (кадры чередуются по времени) |

### 1.1 Историческая скачка (OsData)

Источник `QscalpMarketDepth` отдаёт за день `Quotes.qsh` и `Deals.qsh`; если есть оба — склейка в `.Both.qsh` (`QshMerger`), иначе остаётся `Quotes.qsh`.

Через MCP:

1. `terminal_open_mode(mode="data")`.
2. `data_create_set(name, source="QscalpMarketDepth", source_name, timeframes=["MarketDepthHistory"], date_from, date_to)`.
3. `data_set_securities_add(name, securities)`.
4. `data_set_on(name)` — запустить загрузку.
5. `data_get_set_status(name)` — ждать `percent_load >= 100`.
6. `data_set_off(name)`.

Файлы: `Data/Set_<name>/<Security>/MarketDepth/*.qsh`. Если сет уже есть на диске, повторно не качается.

### 1.2 Реал (онлайн-запись)

В OsData реального времени `MarketDepthLoader` пишет стакан + ленту в `.Both.qsh` (streamCount = 2).
Файл `<Sec>.<yyyy-MM-dd>.Both.qsh`, время кадров — ПК UTC, порядок прихода сохраняется (без буферов).

## 2. Тестовый стенд

`Tests/IndicatorMarketDepthTestStand` — прогоняет робота в тестере через MCP API.

```bash
cd Tests/IndicatorMarketDepthTestStand/OsEngine.IndicatorMarketDepth.TestStand/bin/Debug/net10.0
./OsEngine.IndicatorMarketDepth.TestStand.exe --robot HhfOfiBot --module Smoke
./OsEngine.IndicatorMarketDepth.TestStand.exe --robot HhfOfiBot --module Smoke --params "ImbalanceThreshold=0.5,ImpulsePercent=60"
```

Полезные ключи: `--robot <StrategyName>`, `--params "k=v,k2=v2"`, `--module Smoke` (только smoke, без индикаторов), `--security`, `--data-from`, `--data-to`.

Smoke-тест: деплой робота → автоопределение Simple/Screener → настройка таба на `MarketDepth` → `tester_start` (fast-forward) → отчёт:
сделки, прибыль %, Long/Short, странные статусы (любой кроме `Done` — ошибка), среднее удержание (> 5 ч — ошибка).

## 3. Паттерн робота

Робот — класс-наследник `BotPanel` с атрибутом `[Bot("Имя")]`. Два варианта источника.

### 3.1 Simple (одна бумага)

```csharp
TabCreate(BotTabType.Simple);
_tab = TabsSimple[0];

_ofi = _tab.MarketDepthIndicators.CreateIndicator("Ofi", "OFI", false);          // стаканный
_sma = (Aindicator)IndicatorsFactory.CreateIndicatorByName("Sma", "SMA", false); // свечной
_tab.CreateCandleIndicator(_sma, "Prime");

_tab.MarketDepthUpdateEvent += Tab_MarketDepthUpdateEvent;
_tab.PositionOpeningSuccesEvent += Tab_PositionOpeningSuccesEvent;
```

### 3.2 Screener (несколько бумаг)

```csharp
TabCreate(BotTabType.Screener);
_screenerTab = TabsScreener[0];

_screenerTab.CreateMarketDepthIndicator(1, "Ofi", null, false);
_screenerTab.CreateMarketDepthIndicator(2, "OrderBookImbalance", new List<string> { "Simple", "5" }, false);

_screenerTab.MarketDepthUpdateEvent += Screener_MarketDepthUpdateEvent;            // (MarketDepth md, BotTabSimple tab)
_screenerTab.PositionOpeningSuccesEvent += Screener_PositionOpeningSuccesEvent;    // (Position pos, BotTabSimple tab)
```

В Screener состояние на каждый таб хранить отдельно — словари по ключу `tab.TabName` (EMA-база, таймеры подтверждения). Ограничение числа позиций — параметр `MaxPositions` + `CountOpenPositions()` (сумма по всем табам).

### 3.3 Общее

- Индикатор ищем/читаем: `tab.MarketDepthIndicators.Indicators` (по `GetType().Name` или порядку создания), серию — `indicator.DataSeries[0].Last` после проверки `Values.Count`.
- Вход: `BuyAtMarket` / `SellAtMarket`.
- Выход: в `PositionOpeningSuccesEvent` — тейк `CloseAtProfit` (лимитка), стоп `CloseAtStop`; отзыватель лимиток выключить:
  ```csharp
  tab.ManualPositionSupport.SecondToCloseIsOn = false;
  tab.ManualPositionSupport.SecondToOpenIsOn = false;
  tab.CloseAtProfit(position, profitPrice, profitPrice);
  tab.CloseAtStop(position, stopPrice, stopPrice);
  ```
- Параметры: `Regime` (первый, `Off`/`On`/`OnlyLong`/`OnlyShort`), `VolumeType` (`Contracts`/`Contract currency`/`Deposit percent`), `Volume`, `TakeProfit`, `StopLoss`, `ConfirmationTime`; в Screener ещё `MaxPositions`.
- `NonTradePeriods` + кнопка «Non trade periods», проверка `CanTradeThisTime(tab.Connector.MarketTime)` перед входом.

## 4. Правила кода

1. Соблюдаем `CONTEXT_CODING_GUIDELINES.md` (кодировка, стиль, без комментариев).
2. `Description` — русский и английский через `OsLocalization.ConvertToLocString`, без `:` и `_` внутри текста.
3. `Regime` — первый параметр; гейты входа уважают `OnlyLong`/`OnlyShort`.
4. Делать неторговые периоды.
5. Название индикатора внутри робота — не больше 5 символов.
6. Выход — `CloseAtProfit` (лимитка) + `CloseAtStop`; не использовать сырую `CloseAtLimit` (см. грабли).

## 5. Грабли

| Ошибка | Последствие | Как правильно |
|---|---|---|
| `CloseAtLimit` для тейка | Позиция перекрывается (`OpenVolume < 0`), статус `ClosingSurplus`, робот залипает | `CloseAtProfit` — логический профит, связанный со стопом |
| Не выключить «отзыватель» лимиток | Тейк снимается через N сек (`SecondToClose`), позиция висит | `SecondToCloseIsOn=false`, `SecondToOpenIsOn=false` |
| Общее состояние в Screener | Сигналы бумаг мешаются | Словари по `tab.TabName` |
| Имя индикатора > 5 символов | Не влезает в UI | Короткое имя (например, `OFI`, `OBI`) |
| Читать `Last` без проверки серии | Ноль принят за расчёт | Проверять `Values.Count` перед чтением |
| Не учесть `MaxPositions` в Screener | Перебор позиций на всех бумагах | `CountOpenPositions() < MaxPositions` |

## 6. MCP-шпаргалка

- `bot_create(strategy_name, name)` — создать робота.
- `bot_get_sources(bot_id)` — тип таба (`Simple`/`Screener`) и `tab_name`.
- `bot_set_config_tab_simple` / `bot_set_config_tab_screener` — настроить таб на `Tester` + `candle_market_data_type=MarketDepth`.
- `bot_set_params(bot_id, parameters)` — задать параметры.
- `tester_start(fast_forward=true)` — прогон.
- `bot_journal_get_closed_positions(bot_name, include_failed=true)` — все закрытые позиции (иначе только `Done`).
- `bot_journal_get_statistics(bot_name, side=Long/Short)` — сделки, прибыль, среднее удержание.
- `bot_journal_get_summary(bot_name)` — итоговая прибыль.
- `bot_delete(bot_id)`.

## 7. Готовые примеры

| Робот | Индикаторы | Источник | Что взять за образец |
|---|---|---|---|
| `HhfOrderBookImbalanceScreener` | OrderBookImbalance + Bollinger | Screener | Screener, свечной фильтр, MaxPositions |
| `HhfDepthVolumeBot` | DepthVolume + Sma | Simple | Simple, стаканный + свечной индикатор |
| `HhfLiquidityWallsBot` | LiquidityWalls + Keltner | Screener | Screener, словари по tab, край стакана |
| `HhfOfiBot` | Ofi + OrderBookImbalance | Screener | Два стаканных индикатора, CloseAtProfit/CloseAtStop |
