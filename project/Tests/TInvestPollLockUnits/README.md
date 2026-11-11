# TInvestPollLockUnits — юниты U-9'/U-11' линии BUG-0097

In-process юниты к зоне `_pollSubscribedSecurities` / `_pollLocker` (`Market/Servers/TInvest/TInvestServer.cs`),
по ТЗ тестовых стендов план-20261109-016 (V-2). Сети, ключей и живого коннектора не требуют.

| Кейс | Что проверяет |
|---|---|
| `u9` | U-9': гонка `Find×3` — реальные `SendOrder`/`ChangeOrderPrice`/`SendStopOrder` против конкурентных Add/Clear poll-списка; known-seed найден каждым раундом; ЛЮБАЯ FirstChance = 0 |
| `u11` | U-11': Connect/Subscribe/Dispose ×4 + Subscribe-после-Dispose: bookkeeping пуст, unhandled = 0, Error-логи только ожидаемые (гейт пустого токена) |

Харнесс: инстанс `TInvestServerRealization` без конструктора + инжект приватных полей; gRPC — наследники
codegen-клиентов с успешными заглушками (сетевых вызовов нет). Пин `sha256 OsEngine.dll` — первой строкой отчёта.

## Сборка и прогон

```powershell
dotnet build project\OsEngine.sln -c Debug --no-incremental          # ПЕРВЫМ: bin\Debug в git отстаёт от исходников
dotnet build project\Tests\TInvestPollLockUnits\OsEngine.TInvest.PollLockUnits.csproj
& project\Tests\TInvestPollLockUnits\bin\Debug\OsEngine.TInvest.PollLockUnits.exe u9
& project\Tests\TInvestPollLockUnits\bin\Debug\OsEngine.TInvest.PollLockUnits.exe u11
```

Код возврата 0 = GREEN; 1 = кейс упал; 2 = fatal/preflight (в том числе dll без поля `_pollLocker` —
на unfixed-движке юниты не запускаются by design, гонка unfixed-базы доказана стендом V-1).
Отчёты: `units-report-u9.txt` / `units-report-u11.txt` в каталоге сборки.
