# CONTEXT_THEMES.md — Цветовые темы OsEngine

> Руководство для авторов тем (люди и ИИ-агенты): как устроена система
> тем и как создать свою. Перед работой читай
> [`CONTEXT_CODING_GUIDELINES.md`](CONTEXT_CODING_GUIDELINES.md).

---

## 1. Что есть

В OsEngine поддерживаются цветовые темы. Переключение — кнопка с иконкой
в главном меню (между API и флажками языка) → окно `ThemeSelectUi`
с живыми превью и прокруткой (любое число тем).

Встроенные темы:

| id | Имя | Характер |
|---|---|---|
| `DarkOrange` | SmartLabXXX | Тёмная, оранжевый акцент. **Дефолт. Её палитру менять нельзя** |
| `Midnight` | Midnight | Тёмно-синяя |
| `Tiffany` | Tiffany | Светлая мятная |
| `Gray` | BloombergLight | Светлая сине-серая |

## 2. Как устроена система

- **`OsEngine/Themes/Theme<Id>.xaml`** — словарь ресурсов темы
  (ResourceDictionary). Набор ключей одинаков во всех темах,
  различаются только значения.
- **`OsEngine/Themes/ThemeManager.cs`** — применение темы
  (`Apply` подменяет словарь в `Application.Resources`),
  хранение выбора (`Engine\Color\theme.txt`), событие
  `ThemeChangedEvent`, доступ к ресурсам темы из кода:
  `GetColor(key)`, `GetBrush(key)`, `GetColorWinForms(key)`,
  `GetString(key)`, `GetDouble(key)`, геттеры шрифтов.
- **Пользовательские темы без пересборки**: файлы
  `Engine\Themes\<Id>.xaml` рядом с exe подхватываются на старте
  (загрузка через `XamlReader`, id = имя файла).
- При загрузке пользовательской темы ключи валидируются по DarkOrange,
  недостающие пишутся в лог.
- WinForms-часть: таблицы — `DataGridFactory` (стили из темы,
  `ApplyTheme` для перекраски), чарты —
  `ChartMasterColorKeeper.SetThemeScheme(themeId)`, отдельные чарты —
  ключи `Chart*`/`Journal*`/`Optimizer*` и геттеры шрифтов чарта.

## 3. Как создать свою тему

### 3.1. Быстрый путь — БЕЗ пересборки (рекомендуется для экспериментов)

1. Скопируй любой словарь `OsEngine/Themes/Theme<Id>.xaml` (например,
   `ThemeMidnight.xaml`) → `Engine\Themes\MyTheme.xaml` **рядом с exe**
   (`OsEngine/bin/Debug/Engine/Themes/` — создай папку, если её нет).
2. Заполни все ключи своими значениями (реестр — раздел 4).
   Валидация при загрузке подскажет в логе, чего не хватает.
3. Задай `<system:String x:Key="ThemeDisplayName">Имя темы</system:String>`.
4. Перезапусти приложение — тема появится в окне выбора сама.
   Картинки главного меню не нужны — подставятся из DarkOrange.
   Схема чарта без своей ветки в `SetThemeScheme` — из DarkOrange.

**Важно:** темы из `Engine\Themes` НЕ попадают в git-репозиторий
(папка `Engine` — рабочая, рядом с exe). Это локальный способ.

### 3.2. Путь с PR на GitHub (тема для всех пользователей)

1. Скопируй словарь → `OsEngine/Themes/Theme<MyTheme>.xaml`.
   XAML подхватится сборкой автоматически (SDK-глоб), в csproj ничего
   добавлять не надо.
2. Заполни все ключи + `ThemeDisplayName`.
3. **Пересобери проект** (`dotnet build OsEngine/OsEngine.csproj`) и
   запусти из `OsEngine/bin/Debug` — только после пересборки тема
   появится в окне выбора.
4. Картинки главного меню — опционально:
   `OsEngine/Images/MainWIndow/Themes/<Id>/{data,test,trading,gear}.png`
   + записи `<Resource Include>` в `OsEngine.csproj`.
   Без них подставятся картинки DarkOrange.
5. Схема чарта: в `ChartMasterColorKeeper.SetThemeScheme` добавь ветку
   для своего id (без неё чарты будут в цветах DarkOrange).
6. Проверь: окно выбора темы (твоя плитка с превью), главное меню,
   Тестер, Журнал, OsData, настройки.
7. PR на GitHub: словарь в `OsEngine/Themes/`, при необходимости
   картинки и ветка в `SetThemeScheme`.

## 4. Реестр ключей (обязательны все)

### 4.1. Имя и шрифты

- `ThemeDisplayName` — отображаемое имя темы.
- `ChartFontFamily` — имя шрифта чартов.
- `ChartAxisFontSize`, `ChartLabelFontSize` — множитель размера
  (дефолт 1) для осей / подписей точек: размер = базовый × ключ,
  округление до 2 знаков.
- `ChartCandleShadowSize`, `ChartIndicatorShadowSize`,
  `ChartSeriesShadowSize` — тень (px, 0 = выкл) свечей / линий
  индикаторов / прочих серий.
- `GridFontFamily`, `GridFontSize`, `GridHeaderFontFamily`,
  `GridHeaderFontSize` — шрифт таблиц (строки / шапка): семейство и
  множитель размера (дефолт 1) от `grid.Font`, округление до 2 знаков.

### 4.2. Основная палитра (базовые ключи из App.xaml)

`ControlForeground` (акцент), `ControlForegroundWhite` (основной текст!),
`CaretBrush` (текстовый курсор в полях ввода: в светлых темах — чёрный),
`ColorForeground`, `ColorForegroundWhite`, `TextSecondaryBrush`,
`LabelForegroundBrush`, `StandardBackgroundBrush`,
`ControlBackgroundNormal` (кнопки/ввод), `ControlBackgroundNormalLight`
(панели), `StandardBackGroundColor`, `StandardBackGroundColorLight`,
`ControlBackgroundOver` (hover), `ControlBackgroundPressed`,
`ControlBorderBrush`, `ControlBorderBrushLight`,
`ControlActiveBorderBrush`, `BorderBrushSolidColor`,
`BorderBrushGradientColor1/2`, `BorderLightBrushGradientColor1/2`,
`HighlightComboBoxItem1/2`, `ArrowBrushComboBox`,
`WindowBackgroundGradientBrush`, `WindowTitleBarBackgroundBrush`,
`WindowForeground`, `WindowStatusForeground(Inactive)`,
`WindowBorderBrush(Inactive)`, `CheckBoxBackBrush`,
`WindowFrameBrush`, `WindowFrameInactiveBrush`, семейства `Disabled*`,
`ToggleButton*`, `ScrollBar*`, `*ProgressBar`, `*ToolTip`, `TabControl*`,
`DataGridRow*`, `ToolBar*`, `ControlLightBackground`,
`ControlBackgroundLine`, `ControlHighlight`, `FocusVisualBrush`,
`ControlShadowEffect`, `ColorForegroundShadowColor`,
`SliderSelectRange`, `HoverBrushScrollBar`, `HoverBackgroundCalendar`,
`TodayBackgroundBrush`, `IndeterminateBrushProgressbar`,
`DisabledArrowFrame`, `PressedBackgroundButtonFrame`,
`PressedBackgroundTabItem`, `SelectedBackgroundBrushToolBar`,
`ToolBarCheckedButton`, `ToolBarVerticalBackground`,
`NormalBackgroundTabControl`, `DisabledForeground2/3`,
`NormalBrushScrollBar`, `DisabledBrushScrollBar`, `ToggleButton*`,
`BrandStripBrush` (полоска версии).

### 4.3. WinForms-таблицы

`GridTextColor`, `GridSelectionBackColor`, `GridSelectionForeColor`,
`GridRowAltColor`, `GridLinesColor`, `GridFlashColor`,
`GridTextInactiveColor`, `GridCellDisabledColor`, `GridButtonBackColor`.

### 4.4. WPF DataGrid и панели

`DataGridWpfRowTextBrush`, `DataGridWpfCellTextBrush`,
`DataGridWpfBorderBrush`, `DataGridWpfHeaderBrush`,
`DataGridWpfHoverBrush`, `DataGridWpfSelectionBrush`,
`PanelAltBrush`, `PanelAltBorderBrush`.

### 4.5. Чарты

`ChartBackColor`, `ChartBorderColor`, `ChartTextColor`,
`ChartEquityColor`, `ChartBarPlusColor`, `ChartBarMinusColor`.

### 4.6. Журнал

`JournalChartBackColor`, `JournalChartBorderColor`,
`JournalChartTextColor`, `JournalChartCursorXColor`,
`JournalEquityTotalColor`, `JournalEquityTotalBrush`,
`JournalShortColor`, `JournalBarPlusColor`, `JournalBarZeroColor`,
`JournalSwatchLongBrush`, `JournalSwatchShortBrush`,
`JournalDodgerBlueColor`.

### 4.7. Стакан

`MarketDepthAskColor`, `MarketDepthBidColor`,
`MarketDepthAskBackColor`, `MarketDepthBidBackColor`.

### 4.8. Оптимизатор

`OptimizerChartTextColor`, `OptimizerCursorColor`,
`OptimizerStageCellColor`, `OptimizerProfitBarColor`,
`ProgressLabelBrush` (лейблы «Общий прогресс» и время до конца:
в тёмных — светлый, в светлых — тёмный).

## 5. Чек-лист приёмки темы

- [ ] Все ключи из раздела 4 заполнены (валидация в логе чистая).
- [ ] `ThemeDisplayName` задан.
- [ ] Текст читается: основной текст на панелях/вводе, лейблы, шапки
  таблиц, подписи осей чарта.
- [ ] Hover кнопок и выделенная строка таблицы отличаются от обычных.
- [ ] Семантика (профит зелёный / лосс красный) читается на фоне темы.
- [ ] Чарт свечей, стакан, журнал, оптимизатор — в цветах темы.
- [ ] Полоска версии и чекбоксы видны.
- [ ] DarkOrange не тронута.

## 6. Правила

- **DarkOrange — пиксельная идентичность дорелизному UI.** Её словарь
  не редактируем.
- Цвета в XAML — только `{DynamicResource <ключ>}` (исключения:
  `BasedOn=`, ссылки на Storyboard и значения анимаций — там
  `StaticResource`; цветовые анимации в шаблонах не добавляем).
- `Background/Fill/Stroke/Foreground` — Brush-ключи; `Color`-ключи —
  для WinForms-кода (`GetColorWinForms`).
- Новые таблицы — через `DataGridFactory`; перед Dispose —
  `DataGridFactory.ClearLinks`.
- Стиль кода — `CONTEXT_CODING_GUIDELINES.md`.
