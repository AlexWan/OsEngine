# CONTEXT_CONNECTORS_TESTSTAND — тестовый стенд робота WServerTester

> Консольная программа — **штатный авто-тест робота WServerTester** (модуль OsEngine):
> проверяет, что сам робот не сломан, прогоняя все его 36 тестов на **эталонном
> коннекторе** (по умолчанию TInvest) с реальным подключением и реальной торговлей.
> Работает только через MCP API OsEngine (localhost:6500), прямой ссылки на OsEngine нет.

## Что проверяет стенд (и чего не проверяет)

- **Объект проверки — робот WServerTester** (`project\OsEngine\Robots\AutoTestBots\ServerTests\`).
  Коннектор — лишь эталонная среда, на которой робот должен корректно отработать.
- **Стенд НЕ является инструментом тестирования коннекторов.** Коннекторы тестирует
  сам робот WServerTester, когда его запускают вручную из интерфейса OsEngine.
  Стенд лишь отвечает на вопрос «робот не сломан?».

### Критерий PASS

Стенд проверяет только инфраструктуру запуска тестов, а не их содержимое:

- **PASS** — кнопка теста нажалась через MCP, тест реально стартовал (маркер
  `Tests started <Класс>` в логе робота) и прислал корректный отчёт
  (`REPORT <Класс>` + `STATUS: OK|FAIL`). **Любой STATUS — успех**: если тест
  зафиксировал ошибки в данных, значит он отработал успешно — ловить ошибки
  это работа самого теста, а не стенда. Пойманные ошибки стенд пишет в лог
  как информацию.
- **FAIL стенда** — инфраструктурная поломка: кнопка не нажалась, тест не
  стартовал (нет маркера за `TestStartTimeoutMinutes`), отчёт не пришёл
  (таймаут `TestTimeoutMinutes`) или пришёл битый (нет `STATUS:`).

## Расположение

- Проект: `project\Tests\ConnectorsTestStand\OsEngine.Connectors.TestStand\` (консоль, net10.0).
- Exe: `project\Tests\ConnectorsTestStand\OsEngine.Connectors.TestStand\bin\Debug\net10.0\OsEngine.Connectors.TestStand.exe`.
- Конфиг: `test-stand-config.json` рядом с exe (необязателен — есть встроенные дефолты).
- Токен эталонного коннектора: файл `*-token.txt` рядом с exe (по умолчанию
  `tinvest-token.txt`), одна строка с токеном. **Не коммитится** (в `.gitignore`).
  Без файла модули — SKIPPED.

## Как устроен

Стенд сам поднимает OsEngine в режиме BotStationLight (`-robotslight`) перед каждым
модулем и гасит его после. Не держи другой OsEngine на порту 6500 — конфликт порта.

Поток модуля (все модули одинаковые, отличается набор тестов):

1. Проверка токена (нет файла — модуль SKIPPED).
2. Модули Orders/BotTabOrders — только с `--live-trade` (иначе SKIPPED): они выставляют
   реальные ордера на счёте (минимальный объём, тесты всё закрывают сами).
3. `bot_get_list` — дождаться RobotMaster (до 90 с).
4. Ровно один инстанс коннектора: `server_management_get_list` (ждём стабилизации),
   лишние инстансы — disconnect + delete; нет ни одного — `server_management_activate`
   или `server_instance_create`. WServerTester требует **ровно один сервер в системе**.
5. Токен из файла в Password-параметр коннектора (`server_instance_get_params` /
   `server_instance_set_params`).
6. `server_instance_connect` → ждём статус `Connect` (до 120 с) → ждём бумаги
   (`server_instance_get_securities`, до 120 с) → пауза 15 с (AServer отклоняет
   ордера первые секунды после старта).
7. `bot_create("WServerTester")` → для каждого теста: `bot_set_params`
   (бумага/класс/портфель/объём из конфига; портфель — первый с коннектора) →
   `bot_click_param_button` (кнопка запуска теста) → **проверка запуска**: ждём
   маркер `Tests started <КлассТеста>` в логе робота (до `TestStartTimeoutMinutes`,
   дефолт 2 мин; нет маркера — FAIL «test did not start») → чтение лог-файла робота
   `Engine\Log\*<BotName>*Log_*.txt` до отчёта `REPORT <Класс>` + `STATUS:`
   (таймаут 25 мин на тест) → отчёт с корректным STATUS = PASS, пойманные тестом
   ошибки пишутся в лог как информация.
8. Cleanup: `bot_delete`, `server_instance_disconnect`, удаление созданного инстанса.

Реестр тестов — `KnownTests.cs`: id → кнопка, маркер отчёта, модуль, флаг live-trade,
список параметров. Имена параметров должны совпадать с
конструктором робота (`project\OsEngine\Robots\AutoTestBots\ServerTests\AServerTester.cs`).

## Модули

| № | Модуль | Тесты | Что проверяет у робота | live-trade |
|---|---|---|---|---|
| 1 | Securities | V1 | валидация полей бумаг (ловит битые поля) | нет |
| 2 | MarketDepth | V2 | прогон стаканов N минут по списку бумаг | нет |
| 3 | Trades | V3 | прогон ленты сделок N минут | нет |
| 4 | Data | D1–D5 | целостность истории, валидация свечей/тиков, стресс-загрузка | нет |
| 5 | Connection | C1–C5 | циклы connect/disconnect, подписки, стресс памяти, свечи реального времени, скринер | нет |
| 6 | Portfolio | P1 | валидация портфеля, позиций, актива | нет |
| 7 | Orders | O1–O16 | реальные ордера: лимиты/маркет/отмены/спам/реконнект/стопы | **да** |
| 8 | BotTabOrders | B1–B6 | методы BotTabSimple (стоп-ордера) | **да** |

## Как запускать

```bash
# весь стенд (без ордерных модулей)
dotnet run --project project\Tests\ConnectorsTestStand\OsEngine.Connectors.TestStand -- --connector TInvest

# полный прогон включая реальные ордера
OsEngine.Connectors.TestStand.exe --connector TInvest --live-trade

# один модуль (номер или подстрока имени)
OsEngine.Connectors.TestStand.exe --connector TInvest -m Securities
OsEngine.Connectors.TestStand.exe --connector TInvest -m 4

# отдельные тесты внутри модуля
OsEngine.Connectors.TestStand.exe --connector TInvest -m Orders --test O2,O3 --live-trade

# другой эталонный коннектор / бумага / класс / токен-файл
OsEngine.Connectors.TestStand.exe --connector BinanceFutures --security ETHUSDT --class Futures --token-file binance-token.txt
```

Аргументы: `--connector`, `--security`, `--class`, `--securities` (список через `_`),
`--volume`, `--token-file`, `--live-trade`, `--test`, `--module/-m`, `--port`,
`--api-key`, `--timeout`, `--no-wait`, позиционный — путь к OsEngine.exe.
Приоритет: CLI > `test-stand-config.json` > встроенные дефолты (TInvest / SBER / Stock rub).

## Что на выходе

- `Total: X/Y passed` + код выхода 0 (всё прошло/скипнуто) / 1 (есть упавшие).
- Лог `connectors-test-stand-yyyyMMdd-HHmmss.log` рядом с exe (старые логи чистятся).
- Отчёт каждого теста — в логе робота (`Engine\Log\*ConnectorsTesterBot*Log_*.txt`
  в папке OsEngine): `REPORT <Класс>` + `STATUS: OK|FAIL` + ошибки + service info.

## Длительность

- Модуль без стриминга (Securities, Portfolio): ~3–5 мин (подъём OsEngine + коннект).
- Стриминговые тесты (V2, V3, C5): + `MinutesToTest` минут каждый (дефолт 1).
- D4/D5 (стресс-загрузка): до 25 мин на тест.
- Полный прогон (все 36 тестов): 1–3 часа, зависит от коннектора и сессии.

## Примечания

- V2/V3/C4/C5 и все ордерные тесты требуют **торговую сессию** (живые тики/стаканы).
  Вне сессии они закономерно падают — это не поломка робота; гоняй в сессию.
- Ордерные тесты выставляют реальные ордера минимального объёма — запускать
  осознанно, на счёте должны быть средства.
- Тестируется **один эталонный коннектор за раз**; в системе должен быть ровно один сервер.
