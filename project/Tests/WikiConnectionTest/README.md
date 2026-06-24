# WikiConnectionTest

Консольное приложение для автоматического сбора справочных данных по бумагам из коннекторов OsEngine и сохранения их в файлы `Wiki/*.md`.

При запуске приложение:
1. Подключается к запущенному `OsEngine.exe` или запускает его самостоятельно.
2. Подключается к OsEngine по MCP API.
3. Последовательно обрабатывает настроенные коннекторы.
4. Сохраняет списки бумаг и метаданные в папку `Wiki/` рядом с `OsEngine.exe`.
5. Останавливает OsEngine, если приложение его запускало.

---

## Содержание

- [Запуск](#запуск)
- [Настройки](#настройки)
  - [app-settings.json](#app-settingsjson)
  - [connection-secrets.json](#connection-secretsjson)
- [Поддерживаемые коннекторы](#поддерживаемые-коннекторы)
- [Архитектура](#архитектура)
- [Сценарий сбора для одного коннектора](#сценарий-сбора-для-одного-коннектора)
- [Выходные файлы](#выходные-файлы)
  - [Формат файла](#формат-файла)
  - [Metadata](#metadata)
  - [Схемы записи о бумаге](#схемы-записи-о-бумаге)
  - [Именование файлов](#именование-файлов)
- [Используемые методы MCP API](#используемые-методы-mcp-api)
- [Ограничения](#ограничения)
- [Расширение на другие коннекторы](#расширение-на-другие-коннекторы)

---

## Запуск

```bash
cd Tests/WikiConnectionTest
dotnet run
```

При первом запуске, запущенном человеком (двойным кликом из проводника), приложение запросит:
- путь к `OsEngine.exe`;
- параметры подключения Alor / TInvest (можно нажать Enter, чтобы пропустить).

Если приложение запущено из другого процесса или скрипта, оно не блокируется вводом: при отсутствии файлов создаётся пустой `connection-secrets.json`, а `app-settings.json` следует подготовить заранее.

После завершения сбора приложение не закрывает консоль, если было запущено двойным кликом.

---

## Настройки

Все настройки и секреты хранятся рядом с `WikiConnectionTest.exe`. Оба файла игнорируются Git.

### app-settings.json

| Поле | Описание | По умолчанию |
|------|----------|--------------|
| `OsEnginePath` | Путь к `OsEngine.exe` | `..\..\..\..\..\OsEngine\bin\Debug\OsEngine.exe` |
| `McpBaseUrl` | URL MCP API | `http://localhost:6500` |
| `McpApiKey` | API-ключ MCP | `osengine-mcp-default-key` |
| `McpReadyTimeoutSeconds` | Таймаут ожидания готовности MCP API | 60 |
| `SecurityLoadTimeoutSeconds` | Таймаут ожидания загрузки бумаг из коннектора | 300 (5 минут) |

### connection-secrets.json

Формат универсальный: для каждого коннектора хранится словарь параметров `name → value`, который напрямую передаётся в MCP-метод `server_instance_set_params`.

```json
{
  "connectors": {
    "TInvest": {
      "Token": "t.<token>"
    },
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

Для Alor коннектор требует хотя бы одно заполненное имя портфеля в дополнение к токену. Имена портфелей можно посмотреть на сайте Alor.

Для торговых коннекторов `TInvest` и `Alor` приложение автоматически включает все доступные секции (акции, фьючерсы, опционы, валюта и др.), чтобы загрузить максимально полный список бумаг.

---

## Поддерживаемые коннекторы

| Коннектор | Схема | Требует параметров |
|-----------|-------|-------------------|
| `MoexDataServer` | `dataSecurity` | Нет |
| `QscalpMarketDepth` | `dataSecurity` | Нет |
| `TInvest` | `tradeSecurity` | `Token` |
| `Alor` | `tradeSecurity` | `Token` + один или несколько портфелей (`Portfolio Spot`, `Portfolio FORTS`, `Portfolio currency`, `Portfolio spare`) |

Приложение автоматически обрабатывает:
- `MoexDataServer` и `QscalpMarketDepth` всегда;
- `TInvest` и `Alor` — только если для них есть секреты в `connection-secrets.json`.

---

## Архитектура

```
Program.cs
├── Models/
│   ├── AppSettings.cs          # настройки приложения
│   ├── ConnectionSecrets.cs    # секреты коннекторов
│   ├── ConnectorMetadata.cs    # metadata для выходного файла
│   └── WikiSecurity.cs         # модель бумаги для выходного файла
└── Services/
    ├── AppSettingsService.cs   # загрузка/сохранение app-settings.json
    ├── SecretsService.cs       # загрузка/сохранение connection-secrets.json
    ├── ConsoleHelper.cs        # определение интерактивного запуска
    ├── McpApiClient.cs         # HTTP-клиент MCP API
    ├── McpService.cs           # высокоуровневые вызовы MCP
    ├── OsEngineProcessService.cs # управление процессом OsEngine
    ├── SecurityCollector.cs    # сбор бумаг из коннектора
    └── WikiFileService.cs      # сохранение файлов Wiki/*.md
```

---

## Сценарий сбора для одного коннектора

1. `server_management_activate(type)` — активация типа коннектора.
2. `server_instance_create(type)` — создание временного экземпляра (для коннекторов без `multiple instances` используется инстанс `#0`).
3. `server_instance_get_params(type, number)` — получение списка параметров экземпляра.
4. `server_instance_set_params(type, number, parameters)` — установка токенов и портфелей. Для `TInvest` и `Alor` дополнительно автоматически включаются все доступные секции.
5. `server_instance_connect(type, number)` — подключение.
6. Ожидание статуса `Connect` или таймаута.
7. `server_instance_get_securities(type, number, reload=true)` — получение списка бумаг.
8. `server_instance_disconnect(type, number)` — отключение.
9. `server_instance_delete(type, number)` — удаление созданного временного экземпляра.
10. Преобразование списка бумаг в JSON Lines и сохранение в `.md` файл.

Если на любом шаге произошла ошибка — коннектор пропускается, ошибка логируется, приложение переходит к следующему.

---

## Выходные файлы

Все файлы сохраняются в папку `Wiki/`, расположенную рядом с `OsEngine.exe`:

```
Wiki/
  moex_iss_securities.md      # dataSecurity, MoexDataServer
  qscalp_securities.md        # dataSecurity, QscalpMarketDepth
  tinvest_securities.md       # tradeSecurity, TInvest
  alor_securities.md          # tradeSecurity, Alor
```

### Формат файла

Файл имеет расширение `.md` для удобства просмотра на GitHub, но тело файла — это **JSON Lines** (по одному JSON-объекту на строку).

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
{"schema":"tradeSecurity","name":"SBER","nameClass":"TQBR","nameFull":"ПАО Сбербанк","nameId":"...","exchange":"MOEX","state":"Activ","securityType":"Stock","lot":10,...}
{"schema":"tradeSecurity","name":"GAZP","nameClass":"TQBR","nameFull":"ПАО Газпром","nameId":"...","exchange":"MOEX","state":"Activ","securityType":"Stock",...}
```
```

### Metadata

Метаданные собираются из `IServerPermission` коннектора через MCP-метод `server_management_get_connector_permissions`.

| Поле | Тип | Описание |
|------|-----|----------|
| `connector` | `string` | Имя коннектора |
| `collectedAt` | `string` | ISO-8601 дата и время сбора |
| `source` | `string` | Источник данных, например `server_instance_get_securities` |
| `permissions.isTradingSupported` | `bool` | Поддерживается ли реальная торговля |
| `permissions.isDataFeedSupported` | `bool` | Поддерживается ли загрузка исторических данных |
| `permissions.tradeTimeFrames` | `string[]` | Разрешённые таймфреймы для торговли |
| `permissions.dataFeedTimeFrames` | `string[]` | Разрешённые таймфреймы для скачивания истории |

### Схемы записи о бумаге

#### `tradeSecurity` — для торговых коннекторов

Используется для **TInvest** и **Alor**.

```json
{
  "schema": "tradeSecurity",
  "name": "SBER",
  "nameClass": "TQBR",
  "nameFull": "ПАО Сбербанк",
  "nameId": "...",
  "exchange": "MOEX",
  "state": "Activ",
  "securityType": "Stock",
  "lot": 10,
  "priceStep": 0.01,
  "priceStepCost": 1.0,
  "volumeStep": 1,
  "minTradeAmount": 1,
  "minTradeAmountType": "Contract",
  "decimals": 2,
  "decimalsVolume": 0,
  "priceLimitLow": 0,
  "priceLimitHigh": 0,
  "marginBuy": 0,
  "marginSell": 0
}
```

Дополнительные поля для опционов (`securityType == "Option"`):
- `optionType` — `Call` / `Put` / `None`
- `strike`
- `expiration`
- `underlyingAsset`

Дополнительные поля для облигаций (`securityType == "Bond"`):
- `nominalInitial`
- `nominalCurrent`
- `maturityDate`
- `placementDate`
- `placementPrice`
- `aciValue`

#### `dataSecurity` — для исторических коннекторов

Используется для **MoexDataServer** и **QscalpMarketDepth**.

```json
{
  "schema": "dataSecurity",
  "name": "SBER",
  "nameClass": "TQBR",
  "nameFull": "ПАО Сбербанк",
  "nameId": "...",
  "exchange": "MOEX",
  "state": "Activ",
  "securityType": "Stock"
}
```

> Примечание: `MoexDataServer` и `QscalpMarketDepth` не предоставляют лотность, шаг цены и прочие торговые параметры, поэтому для них используется урезанная схема.

### Именование файлов

| Файл | Коннектор |
|------|-----------|
| `moex_iss_securities.md` | `MoexDataServer` |
| `qscalp_securities.md` | `QscalpMarketDepth` |
| `tinvest_securities.md` | `TInvest` |
| `alor_securities.md` | `Alor` |

---

## Используемые методы MCP API

| MCP метод | Назначение |
|-----------|------------|
| `initialize` | Handshake при подключении |
| `tools/list` | Проверка доступности API |
| `terminal_stop` | Корректная остановка OsEngine |
| `server_management_activate` | Активация типа коннектора |
| `server_management_get_list` | Получение списка инстансов |
| `server_management_get_connector_permissions` | Получение permissions коннектора для metadata |
| `server_instance_get_params` | Получение параметров экземпляра |
| `server_instance_create` | Создание экземпляра коннектора |
| `server_instance_set_params` | Установка параметров экземпляра |
| `server_instance_connect` | Подключение экземпляра |
| `server_instance_get_status` | Ожидание статуса `Connect` |
| `server_instance_get_securities` | Получение списка бумаг |
| `server_instance_disconnect` | Отключение экземпляра |
| `server_instance_delete` | Удаление временного экземпляра |

---

## Ограничения

- Приложение полностью автономно: запускается, собирает данные, сохраняет, завершается.
- Коннекторы обрабатываются последовательно, а не параллельно.
- Папка `Wiki` создаётся рядом с `OsEngine.exe`.
- Если OsEngine уже был запущен до старта приложения — приложение не останавливает его; остановка выполняется только для экземпляра, запущенного самим приложением.
- `MoexDataServer` и `QscalpMarketDepth` не поддерживают создание дополнительных инстансов. Приложение автоматически использует инстанс `#0`.
- Для получения актуального списка бумаг у `MoexDataServer` используется параметр `reload=true` метода `server_instance_get_securities`.

---

## Расширение на другие коннекторы

Чтобы добавить новый коннектор:

1. Добавить запись в `GetConnectorConfigs` в `Program.cs`.
2. Указать схему (`dataSecurity` или `tradeSecurity`).
3. Добавить префикс имени файла в `GetFileNamePrefix`.
4. При необходимости добавить параметры в `connection-secrets.json` и валидацию в `ValidateConnectorSecrets`.
