# CONTEXT_CLUSTERING_TESTSTAND — тестовый стенд кластеризации (консольная программа)

> Описание тестового стенда `OsEngine.Clustering.TestStand` — детерминированная проверка
> кластеризации («Аллокация капитала», `OsEngine.OsCapitalAllocation`) через MCP OsEngine.
> Стенд доступен внешним агентам (Мастер кластеризации) для штатного авто-теста кластеризатора.

## 1. Цель

Дать внешним агентам (Мастер кластеризации) готовый инструмент проверки кластеризатора, чтобы агент
**не выдумывал проверки заново** на каждом запуске, а:
1) запускал стенд (стенд сам поднимает OsEngine и открывает режим `allocation` через MCP);
2) получал структурированный отчёт pass/fail по проверкам;
3) интерпретировал результат и отчитывался / эскалировал.

## 2. Расположение и имя

- Путь: `project\Tests\ClusteringTestStand\OsEngine.Clustering.TestStand\`.
- Имя проекта: `OsEngine.Clustering.TestStand` (`.csproj`, `net10.0`, консоль).
- Работает только через MCP OsEngine — **без прямой ссылки на OsEngine**.

## 3. Как устроен

- `Program.cs` — вход: лог-файл, парсинг аргументов, запуск OsEngine, перебор модулей.
- `OsEngineProcessController.cs` — запуск/остановка OsEngine, ожидание MCP-порта.
- `McpApiClient.cs` — MCP-клиент (`ToolsCall`, V1/V2).
- `TestContext.cs` — отчётность (`RecordPass/RecordFail/PrintSummary`), маскирование секретов.
- `TestResult.cs` — pass/fail.
- `TestSecrets.cs` — ключи/токены (не в git).
- `ProtocolValidator.cs` — валидация JSON-RPC/MCP (протокольные отклонения, не фейлы).
- `Tests/ClusteringTestsBase.cs` — общая база: вызовы MCP, разбор ответов, открытие режима, загрузка ботов.
- `Tests/*.cs` — модули тестов (см. п. 4).

> **За основу взят тестовый стенд тестера** (`OsEngine.OsTester.TestStand`, `project\Tests\OsTesterTestStand\…`):
> переиспользуется его инфраструктура (`McpApiClient`, `OsEngineProcessController`, `TestContext`,
> `TestSecrets`, `TestResult`, `ProtocolValidator`) — меняются только namespace и тесты.
> Кластеризатор уже покрыт и в `McpTestStand` (модули 22/23), но этот стенд — **отдельный**,
> изолированный от McpTestStand: его в будущем дорабатывает и модифицирует сам Мастер кластеризации.

## 4. Модули (что проверяет)

### 4.1. Данные (Data)

- **4.1.1.** Режим `allocation` открывается (`terminal_open_mode` → `allocation_get_status.is_open`).
- **4.1.2.** Статус отдаёт `is_open` и `bots_count`.
- **4.1.3.** Боты загружаются (`allocation_bots_set` → `bots_count` = числу файлов).
- **4.1.4.** Список ботов читается (`allocation_bots_get`).
- **4.1.5.** Очистка списка (`allocation_bots_clear` → 0).
- **4.1.6.** Боты загружаются заново после очистки.
- **4.1.7.** Настройки (`period`) выставляются и читаются (`allocation_settings_set/get`).

### 4.2. Кластеризация по корреляции (Clustering)

- **4.2.1.** `min_correlation` читается (`allocation_clustering_settings_get`).
- **4.2.2.** `min_correlation` выставляется (`allocation_clustering_settings_set`).
- **4.2.3.** Расчёт (`allocation_clustering_calculate` → `calculated=true`).
- **4.2.4.** Результат (`allocation_clustering_result_get`): `clusters` + `weights` (весов = числу ботов).

### 4.3. Иерархический риск-паритет (Hrp)

- **4.3.1.** Настройки читаются (`allocation_hrp_settings_get`: linkage/covariance/correlation).
- **4.3.2.** Настройки выставляются (`allocation_hrp_settings_set`).
- **4.3.3.** Расчёт (`allocation_hrp_calculate` → `calculated=true`).
- **4.3.4.** Результат (`allocation_hrp_result_get`): `weights` (50), `allocation_merges` (49), `allocation_leaf_order` (50).

### 4.4. Сохранение аллокации (Allocation)

- **4.4.1.** `allocation_result_save` → `saved=true`, `clusters_count` > 0.
- **4.4.2.** Файл аллокации создан на диске и не пустой.

### 4.5. Отчёт (Report)

- **4.5.1.** Заголовок файла валиден («Num cluster», колонки 100%..600%).
- **4.5.2.** Есть строки кластеров и строки ботов.

## 5. Как запускать

```bash
dotnet run -- [--module/-m NAME] [--data-dir PATH] [--port 6500] [--api-key KEY] [--timeout 60] [--no-wait] [--transport v1|v2]
```

- `--module/-m` — фильтр модулей (имя или номер, через запятую). Без флага — все 5 модулей разом.
- `--data-dir` — папка с входными файлами ботов (`*.txt`). По умолчанию — `Tests\McpTestStand\OsEngine.McpApi.TestStand\TestData`.
- `--port` / `--api-key` — MCP OsEngine (по умолчанию `6500` / `osengine-mcp-default-key`).
- `--timeout` — таймаут ожидания MCP после запуска OsEngine (сек).
- `--no-wait` — неинтерактивный режим (не спрашивать секреты).
- `--transport v1|v2` — транспорт MCP (по умолчанию v2, Streamable HTTP).

Пример — один модуль:

```bash
dotnet run -- -m Clustering
```

## 6. Что на выходе

Во время прогона — построчно `[Module N] ...` + `Status: PASS/FAIL - ...`. В конце — сводка:

```
--- Module Summary ---
Data: 7/7 passed
Clustering: 4/4 passed
Hrp: 4/4 passed
Allocation: 2/2 passed
Report: 2/2 passed

Total: 19/19 passed in 4.4s

--- Protocol Violations ---
none
```

- `Total: X/Y passed` — главный итог; при `> 0` failed стенд завершается с **ненулевым кодом выхода**.
- `--- Protocol Violations ---` — отклонения от протокола MCP (заголовки/JSON-RPC/статус-коды), не считаются фейлом.
- Вывод дублируется в консоль + лог-файл `osclustering-test-stand-yyyyMMdd-HHmmss.log` рядом с `.exe`.

## 7. Длительность

~5 секунд на полный прогон (50 ботов, оба алгоритма). Основное время — запуск OsEngine и ожидание MCP, а не сами проверки.

## 8. Примечания

- Стенд работает через MCP OsEngine (`allocation_*`, `terminal_open_mode`) — тот же путь, что и у внешних агентов.
- Входные файлы ботов — журналы сделок (`*.txt`), фикстуры `McpTestStand\…\TestData` (50 файлов `1_Deals.txt`…`50_Deals.txt`).
- Режим открывается через `terminal_open_mode { mode: "allocation" }` (стенд запускает OsEngine в главном меню, без спец-аргументов).
- Ключи/токены — в `test-secrets.json` (не в git), по образцу `OsTester.TestStand`/`OsData.TestStand`.
- Отдельный от `McpTestStand`: стенд дорабатывает Мастер кластеризации, не задевая другие стенды.
