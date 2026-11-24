# WooHedgeModeUnits — юниты S1-S3 линии BUG-0651 (bug-0655)

In-process юниты к маппингу галки «Hedge Mode» → `WooServerRealization.HedgeMode`
(`Market/Servers/Woo/WooServer.cs`), по ТЗ тестовых стендов plan-20261124-010. Сети, ключей и живого
коннектора не требуют: `new WooServer()` без `Connect`, а `SetPositionMode()` коротится guard'ом
`ServerStatus == Disconnect` (`WooServer.cs:236`) до `CreatePrivateQuery` — PUT физически не уходит.

| Кейс | Что проверяет |
|---|---|
| `s1` | Дискриминатор: посев `[3]="Extended Data"=true` + реальный toggle `[2]="Hedge Mode"`. На unfixed-базе обработчик читает `[3]` → `FAIL S1.4`/`FAIL S1.5` (живая инверсия tr380). На фиксе — `PASS`. Плюс premise-счётчик событий (защита от ложного RED) и проверка, что `AServer.SaveParam` отработал |
| `s2` | Toggle только `[3]` при неподвижном `[2]` → поле не меняется, событий нет. Возврат подписки на `[3]` будет пойман здесь |
| `s3` | Равное значение `[2]` → ни `ValueChange`, ни изменения поля (`ServerParameter.cs:226-229`); прямой сеттер с равным полем → `SetPositionMode` не позван (Error-строк нет) |

Пины до кейсов (`P*`, падение = `exit=2`, кейсы не гоняются): собранный `OsEngine.dll` движка равен dll рядом
с юнитами; `--expect-sha`; `[2].Name=="Hedge Mode"`, `[3].Name=="Extended Data"` — индексы держит только механизм
вставки параметров в голову списка (`AServer.cs:617-624`); дефолты `[2]=true`/`[3]=false`; `ServerStatus==Disconnect`;
ключи пустые.

## Побочка, обязательная к учёту

Каждый `ValueChange` будит и `AServer.userChangeParameter_ValueChange` → `SaveParam()` (`AServer.cs:705`), который
пишет `Engine\<ServerNameUnique>Params.txt` **относительно текущего каталога**, и конструктор читает тот же файл.
Поэтому процесс сам ставит CWD в пустой `%TEMP%`-каталог и удаляет его в конце. Из каталога с живыми
`Engine\*Params.txt` запускать нельзя — затрёт настройки.

## Сборка и прогон

```powershell
dotnet build project\OsEngine\OsEngine.csproj --no-incremental    # ПЕРВЫМ: bin\Debug в git отстаёт от исходников
dotnet build project\Tests\WooHedgeModeUnits\OsEngine.Woo.HedgeModeUnits.csproj
& project\Tests\WooHedgeModeUnits\bin\Debug\OsEngine.Woo.HedgeModeUnits.exe all --expect-sha <sha256 OsEngine.dll из bin\Debug>
```

Код возврата 0 = GREEN; 1 = расхождение (FAIL); 2 = preflight/fatal (пин индексов, dll не пересобрана, падения).

## RED на unfixed-базе (проверка, что юнит вообще ловит баг)

```powershell
git checkout master -- project/OsEngine/Market/Servers/Woo/WooServer.cs
dotnet build project\OsEngine\OsEngine.csproj --no-incremental
dotnet build project\Tests\WooHedgeModeUnits\OsEngine.Woo.HedgeModeUnits.csproj
& project\Tests\WooHedgeModeUnits\bin\Debug\OsEngine.Woo.HedgeModeUnits.exe s1     # expect exit=1: FAIL S1.4, S1.5
git checkout fix/bug-0655 -- project/OsEngine/Market/Servers/Woo/WooServer.cs      # вернуть фикс
```

## Замечания

- Не входит в `OsEngine.sln` (как `TInvestPollLockUnits`/`BinanceSpotNonceUnits`): собирается отдельной командой,
  чтобы не добавлять в решение исполняемый проект вне основного контура. В `bin`/`obj` артефакты не коммитить:
  в клоне они закрыты локальным `.git/info/exclude` (`bin/`), `obj/` — `.gitignore`.
- `--expect-sha` — защита от прогона против вендорного `bin\Debug\OsEngine.dll` из git вместо собранного
  (тот же класс аварии, что у снапшейтов). `P0` сверяет sha собранный ↔ скопированный к юнитам всегда.
