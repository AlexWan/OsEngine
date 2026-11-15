# BinanceSpotNonceUnits — BUG-0352 юниты + перенос шва (BUG-0431)

Юниты перенесены на новый шов `GetNonce` (кэш offset 5 мин) фиксом `fix/bug-0432` по ТЗ ОдТС
plan-20261115-016. Первоначально (PR #2137) пинилось выражение `result.serverTime + 500`
репликой на типе `BinanceTime` (:3321 post-merge). Fix bug-0432 удалил это выражение из
`GetNonce`; смысл проверок («+500» — арифметика long, а не конкатенация строк; fixture-оракул
отличим от epoch-fallback) сохранён и проверяется через реальный `GetNonce` с инжектированным
кэшем при свежем TTL — без сети.

## Прогон

1. `dotnet build project\Tests\BinanceSpotNonceUnits\OsEngine.BinanceSpot.NonceUnits.csproj -p:EngineTree=<корень дерева>\project\OsEngine --no-incremental`
   По умолчанию `EngineTree` — это же дерево. `ProjectReference` принудительно компилирует
   движок из исходников: закоммиченный `bin\Debug\OsEngine.dll` целью НЕ является.
2. `OsEngine.BinanceSpot.NonceUnits.exe green` — 0 = все кейсы совпали.
   Режим `red` (характеризация unfixed-базы через конкатенацию) больше не поддерживается
   (exit=2): базового выражения в дереве нет, на новом шве база невоспроизводима.

## Кейсы (новый шов; свежий TTL = сеть не трогается)

- `R3` type-gate: `BinanceTime.serverTime` имеет тип `Int64` — анти-регрессия повторного
  `long -> string` (OdTC plan-20261115-013). Без изменений.
- `T1` fixture-оракул = инжектированный skew +30 с → `nonce = local + 30с + 500` (±1 с);
  premise-probe: НОЛЬ записей журнала движка за вызов (попадание в кэш — проба на /time
  и fallback не использовались). Конкатенация-регрессия дал бы нечисло / 16+ цифр / нет +30 с.
- `T2a` offset=0 → pad ровно +500; `T2b` offset=−30 с → signed-арифметика (конкатенация
  невозможна); `T2c` offset=+30 суток → nonce отличим от fallback более чем на 30 суток
  (перенос исходного premise-probe «|nonce−now| > 30 суток»).
- `T2d` quoted-число `{"serverTime":"1763..."}` — ПРИНЯТО через `ApplyTimeResponse`
  (Newtonsoft коэрция в анонимный long-тип — carryover case из матрицы #2137, пин CDN-формы).

Первые строки вывода — location и `sha256` фактически загруженной `OsEngine.dll` (пин).
