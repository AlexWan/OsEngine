/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using OsEngine.Logging;
using OsEngine.Market;

namespace OsEngine.Themes
{
    /// <summary>
    /// менеджер цветовых тем приложения.
    /// Тема — ResourceDictionary с палитрой (Themes/Theme[id].xaml),
    /// подменяемый в Application.Resources.MergedDictionaries.
    /// Выбор хранится в Engine\Color\theme.txt
    /// </summary>
    public static class ThemeManager
    {
        #region Properties

        /// <summary>
        /// тема по умолчанию (текущая тёмно-оранжевая)
        /// </summary>
        public const string DefaultTheme = "DarkOrange";

        /// <summary>
        /// встроенные темы (скомпилированы в сборку)
        /// </summary>
        private static readonly string[] _builtInThemes = { "DarkOrange", "Midnight", "Tiffany", "Gray" };

        private static List<string> _availableThemes;

        /// <summary>
        /// все доступные темы: встроенные + пользовательские из Engine\Themes\*.xaml
        /// (порядок — порядок плиток в окне выбора: сначала встроенные)
        /// </summary>
        public static string[] AvailableThemes
        {
            get
            {
                if (_availableThemes == null)
                {
                    _availableThemes = new List<string>(_builtInThemes);

                    try
                    {
                        if (Directory.Exists(@"Engine\Themes"))
                        {
                            string[] files = Directory.GetFiles(@"Engine\Themes", "*.xaml");

                            for (int i = 0; i < files.Length; i++)
                            {
                                string id = Path.GetFileNameWithoutExtension(files[i]);

                                if (_availableThemes.Contains(id) == false)
                                {
                                    _availableThemes.Add(id);
                                }
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                    }
                }

                return _availableThemes.ToArray();
            }
        }

        /// <summary>
        /// текущая тема
        /// </summary>
        public static string CurrentTheme { get; private set; } = DefaultTheme;

        /// <summary>
        /// отображаемое имя темы (для окна выбора).
        /// Берётся из ресурса ThemeDisplayName в словаре темы,
        /// если ресурса нет — из id
        /// </summary>
        public static string GetThemeDisplayName(string themeId)
        {
            ResourceDictionary dict = GetThemeDictionary(themeId);

            if (dict != null
                && dict.Contains("ThemeDisplayName")
                && dict["ThemeDisplayName"] is string name
                && name.Length > 0)
            {
                return name;
            }

            return themeId;
        }

        /// <summary>
        /// тема изменилась (после Apply)
        /// </summary>
        public static event Action ThemeChangedEvent;

        #endregion

        #region Apply, Load, Save

        /// <summary>
        /// применить тему. Если словарь темы не найден — откат на тему по умолчанию
        /// </summary>
        public static void Apply(string themeId)
        {
            ResourceDictionary dict = LoadDictionary(themeId);

            if (dict == null && themeId != DefaultTheme)
            {
                themeId = DefaultTheme;
                dict = LoadDictionary(DefaultTheme);
            }

            if (dict == null || Application.Current == null)
            {
                return;
            }

            IList<ResourceDictionary> merged = Application.Current.Resources.MergedDictionaries;

            for (int i = 0; i < merged.Count; i++)
            {
                if (merged[i].Source != null
                    && merged[i].Source.OriginalString.Contains("/Themes/Theme"))
                {
                    merged[i] = dict;
                    CurrentTheme = themeId;

                    if (ThemeChangedEvent != null)
                    {
                        ThemeChangedEvent();
                    }

                    return;
                }
            }

            merged.Add(dict);
            CurrentTheme = themeId;

            if (ThemeChangedEvent != null)
            {
                ThemeChangedEvent();
            }
        }

        /// <summary>
        /// прочитать выбранную тему из Engine\Color\theme.txt
        /// </summary>
        public static string Load()
        {
            try
            {
                if (File.Exists(@"Engine\Color\theme.txt"))
                {
                    string theme = File.ReadAllText(@"Engine\Color\theme.txt").Trim();

                    if (Array.IndexOf(AvailableThemes, theme) >= 0)
                    {
                        return theme;
                    }
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return DefaultTheme;
        }

        /// <summary>
        /// сохранить текущую тему в Engine\Color\theme.txt
        /// </summary>
        public static void Save()
        {
            try
            {
                if (!Directory.Exists(@"Engine\Color"))
                {
                    Directory.CreateDirectory(@"Engine\Color");
                }

                File.WriteAllText(@"Engine\Color\theme.txt", CurrentTheme);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Palette access

        /// <summary>
        /// цвет из ресурсов текущей темы по ключу
        /// (для обращения из code-behind вместо зашитых констант)
        /// </summary>
        public static System.Windows.Media.Color GetColor(string key)
        {
            object res = Application.Current.TryFindResource(key);

            if (res is System.Windows.Media.SolidColorBrush brush)
            {
                return brush.Color;
            }

            if (res is System.Windows.Media.Color color)
            {
                return color;
            }

            return System.Windows.Media.Colors.Transparent;
        }

        /// <summary>
        /// кисть из ресурсов текущей темы по ключу
        /// </summary>
        public static System.Windows.Media.SolidColorBrush GetBrush(string key)
        {
            object res = Application.Current.TryFindResource(key);

            if (res is System.Windows.Media.SolidColorBrush brush)
            {
                return brush;
            }

            if (res is System.Windows.Media.Color color)
            {
                return new System.Windows.Media.SolidColorBrush(color);
            }

            // градиентные кисти палитры — берём первый стоп как сплошной цвет
            if (res is System.Windows.Media.GradientBrush gradient
                && gradient.GradientStops.Count > 0)
            {
                return new System.Windows.Media.SolidColorBrush(gradient.GradientStops[0].Color);
            }

            return null;
        }

        /// <summary>
        /// цвет темы для WinForms-контролов (System.Drawing.Color)
        /// </summary>
        public static System.Drawing.Color GetColorWinForms(string key)
        {
            System.Windows.Media.Color c = GetColor(key);
            return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        }

        /// <summary>
        /// строка из ресурсов текущей темы по ключу
        /// </summary>
        public static string GetString(string key)
        {
            object res = Application.Current.TryFindResource(key);

            if (res is string str)
            {
                return str;
            }

            return "";
        }

        /// <summary>
        /// число из ресурсов текущей темы по ключу (0 если не найдено)
        /// </summary>
        public static double GetDouble(string key)
        {
            object res = Application.Current.TryFindResource(key);

            if (res is double number)
            {
                return number;
            }

            return 0;
        }

        /// <summary>
        /// имя шрифта чарта из текущей темы
        /// </summary>
        public static string GetChartFontFamily()
        {
            string family = GetString("ChartFontFamily");

            if (string.IsNullOrEmpty(family))
            {
                family = "Microsoft Sans Serif";
            }

            return family;
        }

        /// <summary>
        /// шрифт осей чарта из текущей темы
        /// </summary>
        public static System.Drawing.Font GetChartAxisFont()
        {
            double size = GetDouble("ChartAxisFontSize");

            if (size <= 0)
            {
                size = 8;
            }

            return new System.Drawing.Font(GetChartFontFamily(), (float)size);
        }

        /// <summary>
        /// шрифт подписей значений на чарте из текущей темы
        /// </summary>
        public static System.Drawing.Font GetChartLabelFont()
        {
            double size = GetDouble("ChartLabelFontSize");

            if (size <= 0)
            {
                size = 7;
            }

            return new System.Drawing.Font(GetChartFontFamily(), (float)size);
        }

        /// <summary>
        /// тень свечей на чарте (0 — выключена)
        /// </summary>
        public static int GetChartCandleShadow()
        {
            return (int)GetDouble("ChartCandleShadowSize");
        }

        /// <summary>
        /// тень линий индикаторов на чарте (0 — выключена)
        /// </summary>
        public static int GetChartIndicatorShadow()
        {
            return (int)GetDouble("ChartIndicatorShadowSize");
        }

        /// <summary>
        /// тень прочих серий на чартах (0 — выключена)
        /// </summary>
        public static int GetChartSeriesShadow()
        {
            return (int)GetDouble("ChartSeriesShadowSize");
        }

        /// <summary>
        /// шрифт строк таблиц из текущей темы
        /// </summary>
        public static System.Drawing.Font GetGridFont()
        {
            string family = GetString("GridFontFamily");

            if (string.IsNullOrEmpty(family))
            {
                family = "Microsoft Sans Serif";
            }

            double size = GetDouble("GridFontSize");

            if (size <= 0)
            {
                size = 8.25;
            }

            return new System.Drawing.Font(family, (float)size);
        }

        /// <summary>
        /// шрифт шапки таблиц из текущей темы (жирный)
        /// </summary>
        public static System.Drawing.Font GetGridHeaderFont()
        {
            string family = GetString("GridHeaderFontFamily");

            if (string.IsNullOrEmpty(family))
            {
                family = "Microsoft Sans Serif";
            }

            double size = GetDouble("GridHeaderFontSize");

            if (size <= 0)
            {
                size = 8.25;
            }

            return new System.Drawing.Font(family, (float)size, System.Drawing.FontStyle.Bold);
        }

        /// <summary>
        /// словарь конкретной темы (без применения к приложению, для превью)
        /// </summary>
        public static ResourceDictionary GetThemeDictionary(string themeId)
        {
            lock (_themeDictsLocker)
            {
                ResourceDictionary dict;

                if (!_themeDicts.TryGetValue(themeId, out dict))
                {
                    dict = LoadDictionary(themeId);

                    if (dict == null)
                    {
                        dict = LoadDictionary(DefaultTheme);
                    }

                    _themeDicts[themeId] = dict;
                }

                return dict;
            }
        }

        /// <summary>
        /// кисть из палитры конкретной темы по ключу
        /// </summary>
        public static System.Windows.Media.SolidColorBrush GetBrush(string themeId, string key)
        {
            ResourceDictionary dict = GetThemeDictionary(themeId);

            if (dict != null && dict.Contains(key))
            {
                object res = dict[key];

                if (res is System.Windows.Media.SolidColorBrush brush)
                {
                    return brush;
                }

                if (res is System.Windows.Media.Color color)
                {
                    return new System.Windows.Media.SolidColorBrush(color);
                }

                // градиентные кисти палитры — берём первый стоп как сплошной цвет
                if (res is System.Windows.Media.GradientBrush gradient
                    && gradient.GradientStops.Count > 0)
                {
                    return new System.Windows.Media.SolidColorBrush(gradient.GradientStops[0].Color);
                }
            }

            return null;
        }

        /// <summary>
        /// цвет из палитры конкретной темы по ключу
        /// </summary>
        public static System.Windows.Media.Color GetColor(string themeId, string key)
        {
            System.Windows.Media.SolidColorBrush brush = GetBrush(themeId, key);

            if (brush != null)
            {
                return brush.Color;
            }

            return System.Windows.Media.Colors.Transparent;
        }

        private static readonly object _themeDictsLocker = new object();

        private static readonly Dictionary<string, ResourceDictionary> _themeDicts
            = new Dictionary<string, ResourceDictionary>();

        #endregion

        #region Internal

        private static ResourceDictionary LoadDictionary(string themeId)
        {
            // сначала — встроенная тема из сборки
            try
            {
                return new ResourceDictionary
                {
                    Source = new Uri("/OsEngine;component/Themes/Theme" + themeId + ".xaml", UriKind.Relative)
                };
            }
            catch
            {
                // встроенной темы нет — ищем пользовательскую в Engine\Themes
            }

            try
            {
                string path = @"Engine\Themes\" + themeId + ".xaml";

                if (File.Exists(path))
                {
                    using (FileStream stream = new FileStream(path, FileMode.Open))
                    {
                        ResourceDictionary dict =
                            (ResourceDictionary)System.Windows.Markup.XamlReader.Load(stream);

                        ValidateTheme(dict, themeId);
                        return dict;
                    }
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }

            return null;
        }

        /// <summary>
        /// проверить, что в теме есть все ключи из темы по умолчанию.
        /// Недостающие — в лог
        /// </summary>
        private static void ValidateTheme(ResourceDictionary dict, string themeId)
        {
            try
            {
                ResourceDictionary reference = GetThemeDictionary(DefaultTheme);

                if (reference == null)
                {
                    return;
                }

                List<string> missing = new List<string>();

                foreach (object key in reference.Keys)
                {
                    if (dict.Contains(key) == false)
                    {
                        missing.Add(key.ToString());
                    }
                }

                if (missing.Count > 0)
                {
                    ServerMaster.SendNewLogMessage(
                        "Theme " + themeId + ": missing keys: " + string.Join(", ", missing),
                        LogMessageType.Error);
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion
    }
}
