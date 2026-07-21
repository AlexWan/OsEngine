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
        /// все доступные темы (порядок — порядок плиток в окне выбора: сверху тёмные, снизу светлые)
        /// </summary>
        public static readonly string[] AvailableThemes = { "DarkOrange", "Midnight", "Tiffany", "Gray" };

        /// <summary>
        /// текущая тема
        /// </summary>
        public static string CurrentTheme { get; private set; } = DefaultTheme;

        /// <summary>
        /// отображаемое имя темы (для окна выбора)
        /// </summary>
        public static string GetThemeDisplayName(string themeId)
        {
            if (themeId == "Tiffany")
            {
                return "Tiffany";
            }

            if (themeId == "Gray")
            {
                return "BloombergLight";
            }

            if (themeId == "Midnight")
            {
                return "Midnight";
            }

            return "SmartLabXXX";
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
            try
            {
                return new ResourceDictionary
                {
                    Source = new Uri("/OsEngine;component/Themes/Theme" + themeId + ".xaml", UriKind.Relative)
                };
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        #endregion
    }
}
