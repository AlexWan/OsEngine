# CONTEXT_STOP_ORDERS_TESTSTAND — тестовый стенд стоп-ордеров (консольная программа)

> Описание тестового стенда `StopOrdersTestStand` — проверка стоп-ордеров OsEngine:
> обратная совместимость сохранений позиций и серверные стоп-ордера на реальном подключении T-Invest.

## 1. Цель

Проверить две вещи, связанные со стоп-ордерами:
1. **Обратная совместимость сохранений позиций** — журнал позиций (`Engine\<tab>DealController.txt`)
   в старом формате (строки ордеров без поля `PriceCondition`) должен корректно загружаться без потерь.
2. **Серверные стоп-ордера** — робот `WServerTester` прогоняет набор тестов на реальном подключении T-Invest
   (серверные стоп-ордера на сыром `IServer` и методы `BotTabSimple` из региона «Server stop orders»).

## 2. Расположение и имя

- Путь: `project\Tests\StopOrdersTestStand\`.
- Имя проекта: `StopOrdersTestStand` (`.csproj`, `net10.0`, консоль).
- Работает только через MCP (без прямой ссылки на OsEngine).

## 3. Модули

### Модуль 1 — SaveCompatibility

Обратная совместимость сохранений позиций:
- эталонный `DealController`-файл старого формата (без `PriceCondition`) подкладывается в `Engine\` до создания робота;
- через MCP создаётся робот (`TwoTimeFramesBot`, имя `StopSaveCompatBot`), вкладка грузит журнал;
- проверяется, что позиции загрузились без потерь и журнал переживает полный рестарт движка.
- Режим OsEngine: `-robotslight` (BotStationLight).

### Модуль 2 — ServerTests

Прогон тестов робота `WServerTester` на реальном подключении T-Invest:
- **O13..O16** — серверные стоп-ордера на сыром `IServer`;
- **B1..B6** — методы `BotTabSimple` из региона «Server stop orders».
- Выбор тестов: `--test O13,O14,B1` или `--test all` (по умолчанию — все зарегистрированные по порядку).
- Реальные ордера ставятся на счёт (минимальный объём, тесты закрывают их сами).
- Нужна торговая сессия (живые тики).
- Режим OsEngine: `-robotslight` (BotStationLight).

## 4. Как устроен

- `Program.cs` — вход: лог-файл, консольное окно, парсинг аргументов, оркестрация модулей через `RunModule`.
- `OsEngineProcessController.cs` — запуск/остановка OsEngine, ожидание MCP-порта.
- `McpApiClient.cs` — MCP-клиент.
- `TestContext.cs` — отчётность (`RecordPass/RecordFail/PrintSummary`), доступ к `Config`/`Secrets`/`LiveTrade`.
- `TestResult.cs` — pass/fail.
- `TestStandConfig.cs` — настройки прогона (`test-stand-config.json`).
- `TestSecrets.cs` — токен T-Invest (`tinvest-token.txt`).
- `Tests/Module1_SaveCompatibilityTests.cs`, `Tests/Module2_WServerTesterTests.cs`.

## 5. Как запускать

```bash
cd Tests/StopOrdersTestStand
dotnet run -- [--module/-m 1|2] [--test O13,O14,B1] [--live-trade] [--port 6500] [--api-key ...] [--timeout 60] [--no-wait]
```

- `--module/-m` — фильтр по номеру (1/2) или имени; без него — оба модуля.
- `--test` — какие серверные тесты гнать в модуле 2 (`O13,O14,B1` или `all`).
- `--live-trade` — разрешить модулям ставить реальные ордера на счёт.
- `--no-wait` — не ждать нажатия клавиши при запуске из проводника.

## 6. Настройки и секреты (файлы рядом с `.exe`, в git не коммитятся)

### test-stand-config.json

| Поле | По умолчанию | Описание |
|---|---|---|
| `OsEnginePath` | авто-поиск | Путь к `OsEngine.exe` |
| `Port` | `6500` | Порт MCP |
| `ApiKey` | `osengine-mcp-default-key` | API-ключ MCP |
| `TimeoutSeconds` | `60` | Таймаут готовности MCP |
| `ServerTests.ServerType` | `TInvest` | Коннектор для модуля 2 |
| `ServerTests.SecurityName` / `SecurityClass` | `SBER` / `Stock rub` | Бумага для тестов |
| `ServerTests.Volume` | `3` | Объём тестовых ордеров |
| `ServerTests.TesterBotName` | `StopServerTesterBot` | Имя тест-бота |
| `ServerTests.ConnectTimeoutSeconds` | `120` | Таймаут подключения |
| `ServerTests.SecuritiesTimeoutSeconds` | `120` | Таймаут загрузки бумаг |
| `ServerTests.TestTimeoutMinutes` | `25` | Таймаут одного прогона |

### tinvest-token.txt

Одна строка — токен T-Invest. Файл не коммитится. Если файл отсутствует/пуст — торговые модули
(ServerTests) **пропускаются** (SKIPPED), а не падают.

## 7. Что на выходе

- Построчно `[Module N] ...` + `Status: PASS/FAIL - ...`; в конце `--- Module Summary ---` и `Total: X/Y passed in Zs`.
- Модуль без токена отмечается как SKIPPED, а не как падение.
- Вывод дублируется в консоль + лог-файл `stop-orders-test-stand-yyyyMMdd-HHmmss.log` рядом с `.exe`.

## 8. Длительность

- Модуль 1 (SaveCompatibility) — быстрый (несколько перезапусков OsEngine, без торговли).
- Модуль 2 (ServerTests) — до ~25 минут на прогон (`TestTimeoutMinutes`), плюс ожидание
  подключения/бумаг (до 120 с каждое). Нужна торговая сессия.

## 9. Примечания

- Оба модуля запускают OsEngine в режиме `-robotslight` (BotStationLight) и гасят после себя.
- Серверные тесты ставят реальные ордера (минимальным объёмом, с самозакрытием) — запускать
  только по явному разрешению и с пониманием, что идёт реальная торговля.
- Без `tinvest-token.txt` модуль 2 пропускается; модуль 1 (совместимость) не требует токена.
