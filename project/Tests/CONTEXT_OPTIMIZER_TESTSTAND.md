# CONTEXT_OPTIMIZER_TESTSTAND — тестовый стенд оптимизатора (консольная программа)

> Описание тестового стенда `OsEngine.Optimizer.TestStand` — детерминированная проверка
> оптимизатора OsEngine через MCP. Стенд доступен внешним агентам для штатного авто-теста оптимизатора.

## 1. Цель

Дать внешним агентам (Мастер Optimizer) готовый инструмент проверки оптимизатора, чтобы агент
**не выдумывал проверки заново** на каждом запуске, а:
1) запускал стенд (стенд сам поднимает OsEngine в режиме `optimizer`);
2) получал структурированный отчёт pass/fail по проверкам;
3) интерпретировал результат и отчитывался / эскалировал.

## 2. Расположение и имя

- Путь: `project\Tests\OptimizerTestStand\OsEngine.Optimizer.TestStand\`.
- Имя проекта: `OsEngine.Optimizer.TestStand` (`.csproj`, `net10.0`, консоль).
- Работает только через MCP OsEngine — **без прямой ссылки на OsEngine**.

## 3. Как устроен

- `Program.cs` — вход: лог-файл, парсинг аргументов, запуск OsEngine с `-optimizer`, перебор модулей.
- `OsEngineProcessController.cs` — запуск/остановка OsEngine, ожидание MCP-порта.
- `McpApiClient.cs` — MCP-клиент (`ToolsCall`, V1/V2).
- `TestContext.cs` — отчётность (`RecordPass/RecordFail/PrintSummary`), маскирование секретов.
- `TestResult.cs` — pass/fail.
- `TestSecrets.cs` — ключи/токены (не в git).
- `ProtocolValidator.cs` — валидация JSON-RPC/MCP (протокольные отклонения, не фейлы).
- `Tests/OsOptimizerTestsBase.cs` — общая база: вызовы MCP, разбор ответов, помощники оптимизатора.
- `Tests/DataPreparation.cs` — подготовка данных (качает недостающие сеты через OsData).
- `Tests/*.cs` — модули тестов (см. п. 4).

> **За основу взят тестовый стенд тестера** (`OsEngine.OsTester.TestStand`, `project\Tests\OsTesterTestStand\…`):
> переиспользуется его инфраструктура (`McpApiClient`, `OsEngineProcessController`, `TestContext`,
> `TestSecrets`, `TestResult`, `ProtocolValidator`) — меняются только namespace и тесты.

## 4. Модули (что проверяет)

Сеты для прогонов: `TesterSBER` (SBER, Min30) для Simple и `Tester10Liq` (10 бумаг, Min30) для Screener.
Оба сета готовит сам стенд (модуль Data, `DataPreparation`) через MoexDataServer. Диапазон оптимизации
в тестах — короткий (`2024-01-01 … 2024-06-30`), чтобы прогон был быстрым.

### 4.1. Данные (Data)

- **4.1.1.** Сеты `TesterSBER` и `Tester10Liq` доступны (`optimizer_data_get_status.available_sets`).
- **4.1.2.** Сет выбирается (`optimizer_data_set_config`), в конфиге `set_name` совпадает.
- **4.1.3.** Хранилище загружено (`is_loaded`, `securities_count > 0`).

### 4.2. Очистка (Cleanup)

- **4.2.1.** Фильтры отсева сброшены (все `*_is_on: false`).
- **4.2.2.** Фазы пересобраны (iteration_count 1, last_in_sample false → InSample+OutOfSample).

### 4.3. BotTabSimple (Simple)

- **4.3.1.** Робот `SmaTrendSample` загружен (`optimizer_bot_set`).
- **4.3.2.** Вкладка Simple настроена (бумага `SBER.txt` + `Min30`).
- **4.3.3.** Включён перебор числового параметра (диапазон 20–22 шаг 1) + `Regime: On`.
- **4.3.4.** Прогон завершился, `reports_count > 0`, `fazes_count > 0`.

### 4.4. BotTabScreener (Screener)

- **4.4.1.** Робот `SmaScreener` загружен.
- **4.4.2.** Вкладка Screener настроена (массив бумаг `SBER.txt/GAZP.txt/VTBR.txt` + `Min30`).
- **4.4.3.** Перебор параметра + прогон → `reports_count > 0`.

### 4.5. Параметры (Params)

- **4.5.1.** EnumerationAll — каждый из 6 числовых параметров включается в перебор (`on: true`).
- **4.5.2.** MultiEnumeration — два параметра вместе: `pass_count` = 2×2×2 фазы = 8, реальный прогон → 8 отчётов.
- **4.5.3.** DecimalStep — дробный шаг (`Volume`, step 0.5): 3 значения × 2 фазы = 6 проходов.
- **4.5.4.** FixedValue — значение по умолчанию (`value`) устанавливается и читается (`value` = `default_value`).
- **4.5.5.** StringParams — строковые параметры включаются (`on: true`), но не перебираются.
- **4.5.6.** Reset — `optimizer_params_reset` сбрасывает значения на стандартные.
- **4.5.7.** Threads — `optimizer_set_threads(2)` → `optimizer_get_threads == 2`.
- **4.5.8.** StepTypeGet — `step_type` ("Absolute"/"Percent") устанавливается и читается (round-trip).
- **4.5.9.** StepTypeAffects — `step_type` реально меняет число проходов (перебор 100..200 шаг 10: `Percent` ≠ `Absolute`).

### 4.6. Этапы и фильтры (Phases)

- **4.6.1.** Период + round-trip: `optimizer_phases_set` (`time_start`/`time_end`, iteration_count 1, OOS 30%, last_in_sample false) → `get` возвращает период/итерации/%/флаги, `fazes_count == 2`.
- **4.6.2.** Итерации: `iteration_count 2` → `fazes_count == 4`.
- **4.6.3.** % OutOfSample: 30% → 50% увеличивает OutOfSample-фазу (дней больше).
- **4.6.4.** `last_in_sample: true` → фаза одна (InSample).
- **4.6.5.** Фильтры: все 5 (`profit`, `max_draw_down`, `middle_profit`, `profit_factor`, `deals_count`) включаются/выключаются.
- **4.6.6.** Фильтр отсева реально сокращает отчёты (`deals_count` с большим порогом → 0 отчётов в OutOfSample).
- **4.6.7.** MultiPeriodRun: `iteration_count 2` → реальный прогон → 4 фазы (InSample/OutOfSample/InSample/OutOfSample) с отчётами в каждой.

### 4.7. Настройки (Settings)

- **4.7.1.** Комиссия `Percent` (`commission_value=0.1`) меняет прибыль прогона vs базового.
- **4.7.2.** Комиссия `OneLotFix` (`commission_value=10`) меняет прибыль прогона vs базового.
- **4.7.3.** Стартовый депозит меняет прибыль прогона vs базового.
- **4.7.4.** Проскальзывание (`slippage_to_simple_order=10`) меняет прибыль прогона vs базового.
- **4.7.5.** Дивиденды/налоги/маржа: `optimizer_dividends_set_config` → `get_config` возвращает то же (round-trip).
- **4.7.6.** `optimizer_trade_settings_get` возвращает то, что установил `set` (комиссия/исполнение/проскальзывание).
- **4.7.7.** Кеширование индикаторов (`optimizer_cache_set/get`) включается/выключается.
- **4.7.8.** Отзыв ордеров внутри дня (`clearing`) устанавливается/читается (round-trip).
- **4.7.9.** Неторговые периоды (`non_trade_periods`) устанавливаются/читаются (round-trip).
- **4.7.10.** Неторговый период на весь диапазон → 0 сделок (прибыль = 0).

### 4.8. Сопровождение позиции (PositionSupport)

- **4.8.1.** Стоп (`stop_is_on: true`, `stop_distance: 1`) меняет прибыль прогона vs базового.
- **4.8.2.** Тейк-профит (`profit_is_on: true`, `profit_distance: 1`) меняет прибыль прогона vs базового.
- **4.8.3.** `optimizer_position_support_get` возвращает то, что установил `set`.

### 4.9. Отчёты (Report)

- **4.9.1.** Разделы отчёта на месте: `fazes` (type InSample/OutOfSample) + `reports` с метриками.
- **4.9.2.** Сортировка (`sort_type`): `TotalProfit` → первый отчёт имеет максимум прибыли.
- **4.9.3.** Лимит (`limit`): `limit=2` → ≤2 отчётов на фазу.
- **4.9.4.** `optimizer_save_report` → `optimizer_load_report` (round-trip, `reports_count` совпадает).

### 4.10. Досрочная остановка (Stop)

- **4.10.1.** `optimizer_stop` прерывает прогон: `stopped: true`, отчётов меньше полного числа проходов (частичные результаты, прогон не доделался).

### 4.11. SSE-события (Sse)

- **4.11.1.** Во время прогона приходят события `optimizer.test.progress` / `optimizer.test.finished` (SSE-стрим `GET /api/v2/mcp`).

### 4.12. БД результатов (Db)

- **4.12.1.** RoundTrip (2 фазы) — перебор 2 параметров (Sma length × Envelop length = 10 комбинаций). `optimizer_save_report_db` → `run_id`; `optimizer_list_report_db` — прогон виден; `optimizer_load_report_db` — целостность: бот, прибыль, фазы InSample+OutOfSample, параметры Sma/Envelop восстановлены; `optimizer_delete_report_db` — прогон удалён.
- **4.12.2.** FourFazes — `iteration_count=2` → 4 фазы, round-trip save/load/delete, `fazes_count == 4`.
- **4.12.3.** SingleFaze — `last_in_sample=true` → 1 фаза InSample (без OutOfSample), round-trip, `fazes_count == 1`.
- **4.12.4.** QuerySort — `optimizer_query_report_db`: сортировка по метрике (`sort_by` + `order`), `limit`, фильтр по `faze_type`; в ответе `returned`, `reports_count`, `fazes_count`.
- **4.12.5.** ListFiltersCleanup — `optimizer_list_report_db { robot, date_from, date_to }`: `robot` (точное совпадение) и `date_from`/`date_to` (по `saved_at`, `yyyy-MM-dd`) фильтруют список; `optimizer_db_cleanup { before_date }` удаляет прогоны раньше даты (возвращает `cleaned`).

## 5. Как запускать

```bash
dotnet run --project project\Tests\OptimizerTestStand\OsEngine.Optimizer.TestStand -- [--module/-m NAME] [--set TesterSBER] [--port 6500] [--api-key KEY] [--timeout 60] [--no-wait] [--transport v1|v2]
```

- `--module/-m` — фильтр модулей (имя или номер, через запятую). Без флага — все 12 модулей разом.
- `--set` — имя сета данных (по умолчанию `TesterSBER`).
- `--port` / `--api-key` — MCP OsEngine (по умолчанию `6500` / `osengine-mcp-default-key`).
- `--timeout` — таймаут ожидания MCP после запуска OsEngine (сек).
- `--no-wait` — неинтерактивный режим (не спрашивать секреты).
- `--transport v1|v2` — транспорт MCP (по умолчанию v2, Streamable HTTP).

Пример — один модуль:

```bash
dotnet run --project project\Tests\OptimizerTestStand\OsEngine.Optimizer.TestStand -- -m Simple
```

## 6. Что на выходе

Во время прогона — построчно `[Module N] ...` + `Status: PASS/FAIL - ...`. В конце — сводка:

```
--- Module Summary ---
Simple: 4/4 passed
...

Total: 30/30 passed in 612.3s

--- Protocol Violations ---
none
```

- `Total: X/Y passed` — главный итог; при `> 0` failed стенд завершается с **ненулевым кодом выхода**.
- `--- Protocol Violations ---` — отклонения от протокола MCP (заголовки/JSON-RPC/статус-коды), не считаются фейлом.
- Вывод дублируется в консоль + лог-файл `ooptimizer-test-stand-yyyyMMdd-HHmmss.log` рядом с `.exe`.

## 7. Длительность

| Модуль | Ориентир |
|---|---|
| Data | ~1 мин (проверка/докачка сетов) |
| Cleanup | ~1 с |
| Simple | ~2 с |
| Screener | ~2 с |
| Params | ~1 с |
| Phases | ~1 с |
| Settings | ~4 с (три прогона) |
| PositionSupport | ~3 с (два прогона) |
| Report | ~2 с |
| Stop | ~4 с (широкий перебор + досрочная остановка) |
| Sse | ~3 с (прогон + ожидание событий) |

Основное время — прогоны оптимизатора на Min30, а не сами проверки.

## 8. Примечания

- Стенд работает через MCP OsEngine (`optimizer_*`, `terminal_*`) — тот же путь, что и у внешних агентов.
- Штатные роботы: `SmaTrendSample` (BotTabSimple), `SmaScreener` (BotTabScreener).
- Сеты готовит сам стенд (модуль Data, `DataPreparation`): `TesterSBER` (SBER Min30) и `Tester10Liq` (10 бумаг Min30).
- Имена бумаг в оптимизаторе — с расширением (`SBER.txt`), как в хранилище.
- Начисления (дивиденды/маржа/налоги) проверяются только как round-trip настроек (`set` → `get`).
  Поведенческой проверки «начисления меняют прибыль» нет — в отчёте оптимизатора нет `cash_flows`,
  точную проверку начислений даёт только тестер (`tester_get_report.cash_flows`).
- Ключи/токены — в `test-secrets.json` (не в git), по образцу `McpApi.TestStand`/`OsTester.TestStand`.
