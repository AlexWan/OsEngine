# BinanceSpotNonceUnits — BUG-0352 юниты (T1/R3/T2)

Юниты к фиксу `fix/bug-0352` (`BinanceTime.serverTime` возвращён в `long`; арифметика
в `BinanceServerSpot.cs` :3321 post-merge). ТЗ приёмки: ОдТС plan-20261115-013 (R2–R4, R6).
Не входит в `OsEngine.sln` — собирается явным `dotnet build` этого csproj.

## Прогон

1. `dotnet build project\Tests\BinanceSpotNonceUnits\OsEngine.BinanceSpot.NonceUnits.csproj -p:EngineTree=<корень дерева>\project\OsEngine --no-incremental`
   По умолчанию `EngineTree` — это же дерево. `ProjectReference` принудительно
   компилирует движок из исходников: закоммиченный `..\..\OsEngine\bin\Debug\OsEngine.dll`
   целью НЕ является (отстаёт от исходников, OdTC R1.3).
2. `OsEngine.BinanceSpot.NonceUnits.exe red` — unfixed база (ожидаем конкатенацию,
   16 цифр `1763000000000500`).
   `OsEngine.BinanceSpot.NonceUnits.exe green` — фикс (арифметика, `1763000000500`).

Выход: 0 — все кейсы совпали с ожиданиями режима; 1 — расхождение; 2 — фатальный preflight.
Первые строки вывода — location и `sha256` факчески загруженной `OsEngine.dll` (пин).

## Кейсы

- `R3` type-gate: `BinanceTime.serverTime` имеет тип `Int64` (GREEN) / `String` (RED-пин) —
  анти-регрессия повторного `long -> string`.
- `T1` точный fixture-оракул `{"serverTime":1763000000000}` -> `1763000000500` плюс
  premise-probe «epoch-fallback не использован» (|nonce−now| > 30 суток): 13 цифр
  недискриминирующи — fallback на сетевом отказе тоже даёт 13 цифр (R2).
- `T2` characterization-матрица (R4): quoted-число; `{}` -> `500` на обеих; `null` — A/B
  (red `500`; green — `JsonSerializationException`, замер на Newtonsoft null->long; в проде
  перехватывает catch CreateQuery -> Error+null -> фолбэк через гейт 0348);
  `0` (A/B: red `0500`, green `500` — стенд bug-0348 уже пиннил `0500`); `MaxValue` (green — unchecked wrap в
  отрицательный nonce, новый failure mode; гейт `0 < serverTime <= long.MaxValue-500`
  — follow-up); `"abc"` (green — throw, в проде перехватывает catch CreateQuery ->
  Error+null; red — `abc500`); сырой не-JSON (кидает на обеих). Culture-тест не введён:
  `Int64.ToString("G")` инвариантен.

## Маркировка (OdTC R2/R6)

T1/T2 — type-level реплика выражения :3321 против реального типа, НЕ прод-путь
`GetNonce` (private; `CreateQuery` non-virtual; URL захардкожен; единственный шов —
MITM `Connect(WebProxy)`/`_myProxy`). Прод-путь auth/GetNonce покрыт стендом bug-0348
(C1–C4). Прод-покрытие 0352 = разбор + live-пробел (R5, ConnectorsTestStand / Мастер
Коннекторов, оракул — отсутствие `-1021`). T3 (сборка `OsEngine.sln`) — build-gate,
не покрытие.

На ветке фикса пин `U3` (`1700000000000500`, конкатенация) в полном прогоне стенда
bug-0348 инвертируется в арифметику — ожидаемо, инверсия была оговорена на 0348.
