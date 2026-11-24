# MfdParseUnits — G1–G4 durable юниты парсера MFD (bug-0633 / публ. bug-0640, gap C)

In-process юниты к `MfdServerRealization` (`Market/Servers/MFD/MfdServer.cs`): структурный guard
дедупа PR 2275 + golden-таблица границ парсера + culture-independence. Сети, ключей и живого
коннектора не требуют; из прод-кода исполняется только чистый статический хелпер-парсер.

| Кейс | Что проверяет |
|---|---|
| `g1` | Ровно один ОБЪЯВЛЕННЫЙ в `MfdServerRealization` метод сигнатуры `bool (string, out decimal)` (DeclaredOnly, все видимости/статичности). RED при 0 или >=2 — дедуп 2275 не должен молча врасти обратно в два хелпера. Канон: `private static TryParseMfdDecimal`. **Scope: G1 type-local** — DeclaredOnly ловит дедуп только внутри класса; второй хелпер той же сигнатуры в ДРУГОМ классе этим guard'ом не ловится (осознанная область, вне объёма дедупа 2275) |
| `g2` | Golden 26 границ парсера, пин фактического прогона (включая quirk `"1,234"→1.234`, trailing-sign `"5-"→-5`, `MinValue`, overflow→false, comma-семейство, tab), провал всегда `value=0` |
| `g3` | Solo-safe: инвариантный baseline строится и сверяется с GoldenTable ВНУТРИ G3 (не зависит от прогона G2), затем тот же набор при `ru-RU`/`en-US` побитово совпадает с baseline. Premise-probe: у культуры реально ожидается её разделитель (при подмене культур, напр. invariant-mode, — loud FAIL). EX-строки в G3 запрещены → fatal |
| `g4` | Golden дёргает единственный метод, найденный G1 (`NonPublic|Static`, вызов `target=null`); имя не литералится, инстанс `MfdServerRealization` не создаётся. При фильтре `g4` исполняются G2-кейсы + утверждение G4 (G3 не утверждается) |

## Контракт запуска

* Аргумент — ровно один и только из `g1|g2|g3|g4`; без аргумента — все кейсы. **Неизвестный
  аргумент, лишние аргументы, регистр/пробелы иначе канона — FATAL exit 2** (вакуумный GREEN
  по опечатке фильтра запрещён).
* `1` = кейс упал (G1 при 0/≥2 хелперах; golden/culture-расхождения). При провале G1 golden
  не исполняется.
* `2` = fatal/preflight: неизвестный фильтр; единственный хелпер не static (контракт G4 нарушен,
  golden не исполняется — действует и для solo-`g3`); EX (исключение парсера) в любой фазе G3;
  fatal среды/загрузки.
* G3 при `instance`-хелпере не исполняется (exit 2), «26/26 IDENTICAL» вакуумно не печатается.
* Отчёт — stdout, ASCII-маркеры решений (`RESULT GREEN exit=0`, `G1 PASS/FAIL`, `G2 ...`,
  `G3 ... IDENTICAL`, `FATAL:`), первой строкой **после успешной загрузки движка** —
  `sha256 OsEngine.dll=` фактически загруженной сборки (при preflight-FATAL загрузки sha-строки
  нет легально); control-символы входов и текстов исключений экранированы (`\t`, `\r`, `\n`, прочие
  `\uXXXX`) — каждая строка отчёта ровно одна физическая, предусловие CR-пина и grep-приёмки.
* Юниты собраны под **x64** (`PlatformTarget=x64`, как `OsEngine.csproj`). На 32-битной ОС x64-exe
  вообще не стартует (отказ ОС, не перехватываемый). Decorous `FATAL exit 2` (перехват
  BadImageFormat/FNFE) верен для принудительной x86-сборки на x64-хосте — краха хоста нет.

## Сборка и прогон

```powershell
dotnet build project\OsEngine.sln -c Debug --no-incremental          # ПЕРВЫМ: bin\Debug в git отстаёт от исходников
dotnet build project\Tests\MfdParseUnits\OsEngine.MfdParseUnits.csproj
& project\Tests\MfdParseUnits\bin\Debug\OsEngine.MfdParseUnits.exe
```

`OsEngine.dll` копируется в каталог сборки юнитов из `EngineBin` на момент сборки юнитов — после
пересборки движка юниты тоже пересобирать.

RED-доказательства не-вакуумности (мутанты НЕ коммитить, протокол в README прецедента):
М1 — второй хелпер-дубликат той же сигнатуры в `MfdServerRealization` → G1 FAIL, exit 1 ДО golden;
М2 — правка тела хелпера (например, убрать `Replace(",", …)`) → G2 FAIL, exit 1;
М3 — `static` снять → exit 2 (static-гейт, и для solo-`g3`);
М4 — тело `throw` → solo-`g3`: EX fatal exit 2. После каждого — откат файла.
