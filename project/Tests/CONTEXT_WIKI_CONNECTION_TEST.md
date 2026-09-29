# CONTEXT_WIKI_CONNECTION_TEST — сбор справочных данных коннекторов (консольная программа)

> Описание консольной утилиты `WikiConnectionTest` — автоматический сбор справочных данных
> по бумагам из коннекторов OsEngine и сохранение их в файлы `Wiki/*.md` (JSON Lines).

## 1. Цель

Собрать актуальные списки бумаг и метаданные из настроенных коннекторов OsEngine
и сохранить их в `Wiki/*.md` рядом с `OsEngine.exe`. Эти файлы затем читают другие части
OsEngine (вики бумаг, `WikiSecuritiesReader` в `DividendsUpdater` и т.д.).

При запуске приложение:
1. Подключается к уже запущенному `OsEngine.exe` или запускает его само.
2. Подключается к OsEngine по MCP API.
3. Последовательно обрабатывает настроенные коннекторы.
4. Сохраняет списки бумаг и метаданные в папку `Wiki/` рядом с `OsEngine.exe`.
5. Останавливает OsEngine, если запускало его само.

## 2. Расположение и имя

- Путь: `project\Tests\WikiConnectionTest\`.
- Имя проекта: `WikiConnectionTest` (`.csproj`, `net10.0-windows`, консоль).

## 3. Как запускать

```bash
cd Tests/WikiConnectionTest
dotnet run
```

- При первом запуске двойным кликом приложение запросит путь к `OsEngine.exe` и параметры
  `TInvest`/`Alor` (Enter — пропустить).
- Из скрипта/другого процесса ввод не блокируется: при отсутствии файлов создаётся пустой
  `connection-secrets.json`, а `app-settings.json` готовится заранее.
- После завершения при двойном клике консоль не закрывается.

## 4. Настройки (файлы рядом с `.exe`, оба игнорируются Git)

### app-settings.json

| Поле | Описание | По умолчанию |
|------|----------|--------------|
| `OsEnginePath` | Путь к `OsEngine.exe` | `..\..\..\..\..\OsEngine\bin\Debug\OsEngine.exe` |
| `McpBaseUrl` | URL MCP API | `http://localhost:6500` |
| `McpApiKey` | API-ключ MCP | `osengine-mcp-default-key` |
| `McpReadyTimeoutSeconds` | Таймаут готовности MCP | `60` |
| `SecurityLoadTimeoutSeconds` | Таймаут загрузки бумаг коннектора | `300` (5 мин) |

### connection-secrets.json

Для каждого коннектора — словарь `name → value`, который напрямую передаётся в
`server_instance_set_params`:

```json
{
  "connectors": {
    "TInvest": { "Token": "t.<token>" },
    "Alor": {
      "Token": "<token>",
      "Portfolio Spot": "D12345",
      "Portfolio FORTS": "F23423",
      "Portfolio currency": "",
      "Portfolio spare": ""
    }
  }
}
```

- `Alor` требует хотя бы одно имя портфеля в дополнение к токену.
- Для `TInvest`/`Alor` приложение автоматически включает все секции (акции/фьючерсы/опционы/валюта…).

## 5. Поддерживаемые коннекторы

| Коннектор | Схема | Требует параметров |
|-----------|-------|-------------------|
| `MoexDataServer` | `dataSecurity` | нет |
| `QscalpMarketDepth` | `dataSecurity` | нет |
| `TInvest` | `tradeSecurity` | `Token` |
| `Alor` | `tradeSecurity` | `Token` + портфели |

- `MoexDataServer` и `QscalpMarketDepth` обрабатываются всегда.
- `TInvest` и `Alor` — только если есть секреты в `connection-secrets.json`.

## 6. Архитектура

```
Program.cs
├── Models/
│   ├── AppSettings.cs          # настройки
│   ├── ConnectionSecrets.cs    # секреты коннекторов
│   ├── ConnectorMetadata.cs    # metadata выходного файла
│   └── WikiSecurity.cs         # модель бумаги
└── Services/
    ├── AppSettingsService.cs   # app-settings.json
    ├── SecretsService.cs       # connection-secrets.json
    ├── ConsoleHelper.cs        # определение интерактивного запуска
    ├── McpApiClient.cs         # HTTP-клиент MCP
    ├── McpService.cs           # высокоуровневые вызовы MCP
    ├── OsEngineProcessService.cs # управление процессом OsEngine
    ├── SecurityCollector.cs    # сбор бумаг
    └── WikiFileService.cs      # сохранение Wiki/*.md
```

## 7. Сценарий сбора (для одного коннектора)

1. `server_management_activate(type)`.
2. `server_instance_create(type)` — временный экземпляр (для коннекторов без `multiple instances` — инстанс `#0`).
3. `server_instance_get_params(type, number)`.
4. `server_instance_set_params(type, number, parameters)` — токены/портфели; для `TInvest`/`Alor` включаются все секции.
5. `server_instance_connect(type, number)`.
6. Ожидание статуса `Connect` или таймаута.
7. `server_instance_get_securities(type, number, reload=true)`.
8. `server_instance_disconnect(type, number)`.
9. `server_instance_delete(type, number)`.
10. Преобразование в JSON Lines и сохранение в `.md`.

Ошибка на любом шаге — коннектор пропускается, ошибка логируется, переход к следующему.

## 8. Выходные файлы (папка `Wiki/` рядом с `OsEngine.exe`)

```
Wiki/
  moex_iss_securities.md      # MoexDataServer
  qscalp_securities.md        # QscalpMarketDepth
  tinvest_securities.md       # TInvest
  alor_securities.md          # Alor
```

Файл имеет расширение `.md` (для GitHub), но тело — **JSON Lines** (по одному JSON-объекту на строку):

```markdown
# TInvest Securities

## Metadata

```json
{
  "connector": "TInvest",
  "collectedAt": "2026-06-24T20:30:00+03:00",
  "source": "server_instance_get_securities",
  "permissions": {
    "isTradingSupported": true,
    "isDataFeedSupported": true,
    "tradeTimeFrames": ["1min", "5min", "15min", "1hour", "1day"],
    "dataFeedTimeFrames": ["1min", "5min", "15min", "1hour", "1day", "tick"]
  }
}
```

## Securities

```jsonl
{"schema":"tradeSecurity","name":"SBER","nameClass":"TQBR",...}
{"schema":"tradeSecurity","name":"GAZP","nameClass":"TQBR",...}
```
```

### Metadata

Собирается из `IServerPermission` через `server_management_get_connector_permissions`:

| Поле | Тип | Описание |
|------|-----|----------|
| `connector` | `string` | Имя коннектора |
| `collectedAt` | `string` | ISO-8601 дата/время сбора |
| `source` | `string` | Источник (`server_instance_get_securities`) |
| `permissions.isTradingSupported` | `bool` | Реальная торговля |
| `permissions.isDataFeedSupported` | `bool` | Загрузка истории |
| `permissions.tradeTimeFrames` | `string[]` | ТФ для торговли |
| `permissions.dataFeedTimeFrames` | `string[]` | ТФ для скачивания |

### Схемы записи о бумаге

- **`tradeSecurity`** (`TInvest`, `Alor`): `name`, `nameClass`, `nameFull`, `nameId`, `exchange`, `state`, `securityType`, `lot`, `priceStep`, `priceStepCost`, `volumeStep`, `minTradeAmount`, `minTradeAmountType`, `decimals`, `decimalsVolume`, `priceLimitLow/High`, `marginBuy/Sell`. Для опционов дополнительно `optionType`/`strike`/`expiration`/`underlyingAsset`; для облигаций — `nominalInitial`/`nominalCurrent`/`maturityDate`/`placementDate`/`placementPrice`/`aciValue`.
- **`dataSecurity`** (`MoexDataServer`, `QscalpMarketDepth`): урезанная схема (`name`, `nameClass`, `nameFull`, `nameId`, `exchange`, `state`, `securityType`) — эти коннекторы не отдают лотность/шаг цены.

## 9. Используемые методы MCP API

`initialize`, `tools/list`, `terminal_stop`, `server_management_activate`, `server_management_get_list`,
`server_management_get_connector_permissions`, `server_instance_get_params`, `server_instance_create`,
`server_instance_set_params`, `server_instance_connect`, `server_instance_get_status`,
`server_instance_get_securities`, `server_instance_disconnect`, `server_instance_delete`.

## 10. Длительность

Минуты: коннекторы обрабатываются последовательно, загрузка бумаг одного коннектора —
до `SecurityLoadTimeoutSeconds` (300 с). Точного замера нет.

## 11. Ограничения

- Приложение автономно: запускается → собирает → сохраняет → завершается.
- Коннекторы — последовательно, не параллельно.
- Если OsEngine уже был запущен — приложение его не останавливает (останавливает только свой экземпляр).
- `MoexDataServer`/`QscalpMarketDepth` не поддерживают доп. инстансы — используется `#0`.
- Для актуального списка `MoexDataServer` — `reload=true` в `server_instance_get_securities`.

## 12. Расширение на другие коннекторы

1. Добавить запись в `GetConnectorConfigs` (в `Program.cs`).
2. Указать схему (`dataSecurity`/`tradeSecurity`).
3. Добавить префикс имени файла в `GetFileNamePrefix`.
4. При необходимости — параметры в `connection-secrets.json` и валидацию в `ValidateConnectorSecrets`.
