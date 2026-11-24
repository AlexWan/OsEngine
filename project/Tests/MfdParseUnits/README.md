# MfdParseUnits — G1–G4 durable юниты парсера MFD (bug-0633 / публ. bug-0640, gap C)

In-process юниты к `MfdServerRealization` (`Market/Servers/MFD/MfdServer.cs`): структурный guard
дедупа PR 2275 + golden-таблица парсера + culture-independence. Сети, ключей и живого коннектора
не требуют; прод-код не исполняется, кроме чистого статического хелпера-парсера.

| Кейс | Что проверяет |
|---|---|
| `g1` | Ровно один ОБЪЯВЛЕННЫЙ в `MfdServerRealization` метод сигнатуры `bool (string, out decimal)` (DeclaredOnly, все видимости/статичности). RED при 0 или >=2 — дедуп 2275 не должен молча врасти обратно в два хелпера. Канон: `private static TryParseMfdDecimal` |
| `g2` | Golden 17 границ парсера, пин фактического прогона на tip 2d5c36add: quirk `"1,234"→1.234` пинится как есть; провал всегда `value=0`; E-формы и мусор → false |
| `g3` | Тот же набор при `InvariantCulture` / `ru-RU` / `en-US` — 17/17 совпадают побитово (хелпер хардкодит InvariantCulture) |
| `g4` | Golden дёргает единственный метод, найденный G1 (`NonPublic|Static`, вызов с `target=null`); имя не литералится, инстанс `MfdServerRealization` не создаётся |

Выход exe: `0` = GREEN; `1` = кейс упал (в т.ч. G1 при 0/≥2 хелперах — golden тогда не исполняется);
`2` = fatal/preflight (не собираемый тип, нарушен static-контракт единственного хелпера).
Отчёт — stdout, первой строкой `sha256 OsEngine.dll=` фактически загруженной сборки.
Аргумент (необязательный) — кейс: `g1` / `g2` / `g3` / `g4`; без аргумента — все.

## Сборка и прогон

```powershell
dotnet build project\OsEngine.sln -c Debug --no-incremental          # ПЕРВЫМ: bin\Debug в git отстаёт от исходников
dotnet build project\Tests\MfdParseUnits\OsEngine.MfdParseUnits.csproj
& project\Tests\MfdParseUnits\bin\Debug\OsEngine.MfdParseUnits.exe
```

OsEngine.dll копируется в каталог сборки юнитов из `EngineBin` на момент сборки юнитов —
после пересборки движка юниты тоже пересобирать.

RED-доказательство не-вакуумности G1 (мустант НЕ коммитить): временно добавить в
`MfdServerRealization` второй хелпер той же сигнатуры (`private static bool TryParseMfdTickDecimal(string value, out decimal result)`
— копия тела), пересобрать движок и юниты, прогнать — G1 обязан дать FAIL и exit 1; затем откатить файл.
