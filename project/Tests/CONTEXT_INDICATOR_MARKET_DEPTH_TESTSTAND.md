# CONTEXT_INDICATOR_MARKET_DEPTH_TESTSTAND — Стенд стаканных индикаторов

Проверяет стаканные индикаторы на базе `AMarketDepth` через MCP API OsEngine.

Общая архитектура индикаторов — в `CONTEXT_INDICATORS_MARKET_DEPTH.md`. Образец стенда — `CONTEXT_OSTESTER_TESTSTAND.md`.

## 1. Что делает

Стенд поднимает OsEngine в режиме `testerlight`, через MCP качает спец-сет QSH со стаканами, разворачивает специального робота `MarketDepthIndicatorTestBot`, который прикрепляет тестируемый индикатор и прогоняет батарею проверок, затем разбирает JSON-отчёт и формирует PASS/FAIL.

## 2. Данные

- Стаканный сет: `IndicatorTestQsh`.
  - Источник: `QscalpMarketDepth`, таймфрейм `MarketDepthHistory` (только `Quotes.qsh`).
  - Тестер: `MarketDepthAllCandleState`, свечи строятся из стакана.
- Тиковый сет: `IndicatorTestQshTicks`.
  - Источник: `TDataHistory`, таймфрейм `Tick`.
  - Тестер: `TickAllCandleState`.
  - Архив TDataHistory публикуется с задержкой; если запрошенный диапазон ещё не доступен, стенд автоматически сдвигает диапазон на 7 дней назад (до 6 попыток).
- По умолчанию: инструмент `SBER`, последние 10 календарных дней.
- Если сет с нужными файлами уже есть, повторно не скачивается.

## 3. Специальный робот

`OsEngine/Robots/AutoTestBots/MarketDepthIndicatorTestBot.cs`.

Параметры:

- `IndicatorType` — тестируемый тип индикатора;
- `MinDataDays` — минимальное число торговых дней для проверки покрытия;
- `TestState` — состояние (`Waiting / Deployed / Running / Completed / Failed`);
- `ReportJson` — JSON-отчёт;
- кнопка `DeployIndicator`.

Робот создаёт два одинаковых индикатора (основной и контрольный) через `BotTabSimple.MarketDepthIndicators`, получает стакан и сделки через события таба, завершает проверки по `Connector.TestOverEvent` и пишет результат в `ReportJson` с лог-маркером `MDI_TEST_DONE`.

## 4. Проверки робота (17)

`Deploy`, `Description`, `SeriesPresence`, `ParameterNames`, `Schema`, `InputCoverage`, `ValuesProduced`, `HistoryLimit`, `StorageMonotonicity`, `Throttling`, `TwinDeterminism`, `ValueRange`, `ReferenceValue`, `RecalculateStability`, `HistoryResize`, `ClearReset`, `NoExceptions`.

`Description` требует непустое описание, `SeriesPresence` — хотя бы одну серию, `ParameterNames` — уникальные непустые имена параметров, `NoExceptions` — отсутствие ошибок во время расчёта (базовый класс `AMarketDepth` оборачивает `OnProcess`/`OnProcessTrade`/`OnStateChange` в `try/catch` и фиксирует `ErrorCount`/`LastError`).

Профили диапазона и эталонного значения реализованы для `OrderBookImbalance`, `DepthVolume`, `Spread`, `Delta`, `SpeedOfTape`, `Rvol`, `MicroPrice`, `Ofi`, `LiquidityWalls`, `ExpectedSlippage`, `TradeSizeAnalytics`. Для `Spoofing` проверяется диапазон `0..1`, эталон не задан. Для типов без профиля тест `ValueRange` помечается как `SKIP`, а `ReferenceValue` — для типов без эталона.

## 5. MCP-сценарий одного индикатора

1. `tester_data_set_config` — сет и тип `MarketDepthAllCandleState`.
2. `bot_create` — `MarketDepthIndicatorTestBot`.
3. `bot_set_config_tab_simple` — Tester, инструмент, `candle_market_data_type=MarketDepth`.
4. `bot_set_params` — `IndicatorType` и `MinDataDays`.
5. `bot_click_param_button` — `DeployIndicator`.
6. `tester_start` с `fast_forward=true`.
7. `bot_wait_message` — маркер `MDI_TEST_DONE`.
8. `bot_get_params` — забрать `ReportJson`.
9. Стенд разбирает `tests[]` в PASS/FAIL.
10. `bot_delete`.

Новые MCP-методы не требуются.

## 6. Запуск

```bash
cd Tests/IndicatorMarketDepthTestStand/OsEngine.IndicatorMarketDepth.TestStand/bin/Debug/net10.0
./OsEngine.IndicatorMarketDepth.TestStand.exe --indicator OrderBookImbalance
./OsEngine.IndicatorMarketDepth.TestStand.exe --module DepthVolume
./OsEngine.IndicatorMarketDepth.TestStand.exe --security SBER --data-from 2026-09-21 --data-to 2026-09-30
```

Параметры командной строки: `--os-engine-path`, `--port`, `--api-key`, `--timeout`, `--module`, `--indicator`, `--security`, `--data-from`, `--data-to`, `--min-data-days`, `--no-wait`.

Запуск стенда MCP — только с явного разрешения пользователя (как и другие стенды MCP). Итог — ненулевой код выхода при фейлах.
