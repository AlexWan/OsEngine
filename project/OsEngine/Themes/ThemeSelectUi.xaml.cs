using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using OsEngine.Charts.ColorKeeper;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Logging;
using OsEngine.Market;

namespace OsEngine.Themes
{
    /// <summary>
    /// Theme selection window. Four previews (2x2) drawn from each theme palette.
    /// OK applies and saves the theme, Cancel closes without changes
    /// </summary>
    public partial class ThemeSelectUi
    {
        public ThemeSelectUi()
        {
            InitializeComponent();
            StickyBorders.Listen(this);
            StartupLocation.Start_MouseInCentre(this);

            Title = OsLocalization.MainWindow.ThemeWindowTitle;
            ButtonAccept.Content = OsLocalization.MainWindow.ThemeButtonAccept;
            ButtonCancel.Content = OsLocalization.MainWindow.ThemeButtonCancel;

            ButtonAccept.Click += ButtonAccept_Click;
            ButtonCancel.Click += ButtonCancel_Click;
            Closing += ThemeSelectUi_Closing;

            _selectedTheme = ThemeManager.CurrentTheme;

            BuildTiles();
            UpdateSelection();
        }

        private string _selectedTheme;

        private readonly Dictionary<string, Border> _tiles = new Dictionary<string, Border>();

        private void BuildTiles()
        {
            string[] themes = ThemeManager.AvailableThemes;

            for (int i = 0; i < themes.Length; i++)
            {
                string themeId = themes[i];
                Border tile = CreatePreviewTile(themeId);
                tile.Tag = themeId;
                tile.MouseLeftButtonDown += Tile_MouseLeftButtonDown;

                _tiles[themeId] = tile;

                Grid targetGrid = (i / 2 == 0) ? TilesGridRow0 : TilesGridRow1;
                Grid.SetColumn(tile, i % 2);
                targetGrid.Children.Add(tile);
            }
        }

        private void UpdateSelection()
        {
            foreach (KeyValuePair<string, Border> pair in _tiles)
            {
                if (pair.Key == _selectedTheme)
                {
                    pair.Value.BorderBrush = ThemeManager.GetBrush(pair.Key, "ControlForeground");
                    pair.Value.BorderThickness = new Thickness(4);
                }
                else
                {
                    pair.Value.BorderBrush = ThemeManager.GetBrush(pair.Key, "BorderBrushSolidColor");
                    pair.Value.BorderThickness = new Thickness(1);
                }
            }
        }

        private void Tile_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                _selectedTheme = (string)((Border)sender).Tag;
                UpdateSelection();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTheme != ThemeManager.CurrentTheme)
                {
                    ThemeManager.Apply(_selectedTheme);
                }

                ThemeManager.Save();
                Close();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Close();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ThemeSelectUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                ButtonAccept.Click -= ButtonAccept_Click;
                ButtonCancel.Click -= ButtonCancel_Click;
                Closing -= ThemeSelectUi_Closing;

                foreach (KeyValuePair<string, Border> pair in _tiles)
                {
                    pair.Value.MouseLeftButtonDown -= Tile_MouseLeftButtonDown;
                    pair.Value.Child = null;
                }

                _tiles.Clear();

                TilesGridRow0.Children.Clear();
                TilesGridRow1.Children.Clear();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private Border CreatePreviewTile(string themeId)
        {
            SolidColorBrush panel = ThemeManager.GetBrush(themeId, "StandardBackGroundColorLight");
            SolidColorBrush input = ThemeManager.GetBrush(themeId, "StandardBackGroundColor");
            SolidColorBrush titleBar = ThemeManager.GetBrush(themeId, "WindowTitleBarBackgroundBrush");
            SolidColorBrush mainText = ThemeManager.GetBrush(themeId, "ControlForegroundWhite");
            SolidColorBrush secondText = ThemeManager.GetBrush(themeId, "TextSecondaryBrush");
            SolidColorBrush accent = ThemeManager.GetBrush(themeId, "ControlForeground");
            SolidColorBrush hover = ThemeManager.GetBrush(themeId, "ControlBackgroundOver");
            SolidColorBrush borderBrush = ThemeManager.GetBrush(themeId, "BorderBrushSolidColor");
            SolidColorBrush gridText = ThemeManager.GetBrush(themeId, "GridTextColor");
            SolidColorBrush selected = ThemeManager.GetBrush(themeId, "HighlightComboBoxItem1");

            StackPanel content = new StackPanel { Margin = new Thickness(14) };

            // полоска заголовка окна

            Border title = new Border
            {
                Height = 34,
                Background = titleBar,
                CornerRadius = new CornerRadius(4)
            };

            title.Child = new TextBlock
            {
                Text = "OsEngine",
                Foreground = mainText,
                FontSize = 15,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            content.Children.Add(title);
            content.Children.Add(Spacer(12));

            // тексты

            content.Children.Add(new TextBlock
            {
                Text = ThemeManager.GetThemeDisplayName(themeId),
                Foreground = accent,
                FontSize = 20
            });

            content.Children.Add(new TextBlock
            {
                Text = OsLocalization.MainWindow.ThemePreviewMainText,
                Foreground = mainText,
                FontSize = 14
            });

            content.Children.Add(new TextBlock
            {
                Text = OsLocalization.MainWindow.ThemePreviewSecondaryText,
                Foreground = secondText,
                FontSize = 13
            });

            content.Children.Add(Spacer(12));

            // кнопки (обычная / hover) + чекбокс + бегунок

            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };

            row.Children.Add(CreateButtonPreview(OsLocalization.MainWindow.ThemePreviewButton, input, borderBrush, mainText));
            row.Children.Add(SpacerH(12));
            row.Children.Add(CreateButtonPreview(OsLocalization.MainWindow.ThemePreviewHover, hover, borderBrush, mainText));
            row.Children.Add(SpacerH(12));
            row.Children.Add(CreateCheckBoxPreview(GetCheckBoxBack(themeId, input, mainText), borderBrush));
            row.Children.Add(SpacerH(20));
            row.Children.Add(CreateSliderPreview(accent, borderBrush));

            content.Children.Add(row);
            content.Children.Add(Spacer(12));

            // мини-таблица

            content.Children.Add(CreateTablePreview(panel, input, gridText, selected, borderBrush));
            content.Children.Add(Spacer(12));

            // мини-чарт (50 свечей) + полоска версии в фирменном цвете темы

            DockPanel chartWithStrip = new DockPanel();

            Border versionStrip = new Border
            {
                Width = 22,
                Background = ThemeManager.GetBrush(themeId, "BrandStripBrush")
            };

            versionStrip.Child = new TextBlock
            {
                Text = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version,
                Foreground = Brushes.Black,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                LayoutTransform = new RotateTransform(-90)
            };

            DockPanel.SetDock(versionStrip, Dock.Left);
            chartWithStrip.Children.Add(versionStrip);
            chartWithStrip.Children.Add(CreateChartPreview(themeId, borderBrush));

            content.Children.Add(chartWithStrip);

            return new Border
            {
                Margin = new Thickness(10),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = borderBrush,
                Background = panel,
                Cursor = Cursors.Hand,
                Child = content
            };
        }

        private static Border CreateButtonPreview(string text, Brush back, Brush border, Brush foreground)
        {
            Border button = new Border
            {
                Width = 120,
                Height = 34,
                Background = back,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3)
            };

            button.Child = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            return button;
        }

        private static Brush GetCheckBoxBack(string themeId, Brush input, Brush mainText)
        {
            // на светлых темах фон чекбокса тёмный (бренд-правило: галочка всегда белая)
            if (themeId == "Tiffany" || themeId == "Gray")
            {
                return mainText;
            }

            return input;
        }

        private static Border CreateCheckBoxPreview(Brush back, Brush border)
        {
            Border box = new Border
            {
                Width = 30,
                Height = 30,
                Background = back,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2)
            };

            box.Child = new TextBlock
            {
                Text = "✓",
                Foreground = Brushes.White,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            return box;
        }

        private static Canvas CreateSliderPreview(Brush accent, Brush trackBrush)
        {
            Canvas canvas = new Canvas { Width = 160, Height = 34 };

            Rectangle track = new Rectangle
            {
                Width = 150,
                Height = 3,
                Fill = trackBrush
            };

            Canvas.SetLeft(track, 5);
            Canvas.SetTop(track, 16);
            canvas.Children.Add(track);

            Ellipse thumb = new Ellipse
            {
                Width = 16,
                Height = 16,
                Fill = accent
            };

            Canvas.SetLeft(thumb, 90);
            Canvas.SetTop(thumb, 9);
            canvas.Children.Add(thumb);

            return canvas;
        }

        private static Grid CreateTablePreview(Brush panel, Brush input, Brush gridText,
            Brush selected, Brush borderBrush)
        {
            Grid table = new Grid();

            string[] headers = { "Class", "Code", "Last" };

            string[][] rows =
            {
                new[] { "TestClass", "AFKS.txt", "10" },
                new[] { "TestClass", "AFLT.txt", "20" },
                new[] { "TestClass", "ALRS.txt", "30" },
                new[] { "TestClass", "CHMF.txt", "40" }
            };

            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Border header = CreateTableRow(headers, panel, gridText, borderBrush);
            Grid.SetRow(header, 0);
            table.Children.Add(header);

            for (int i = 0; i < rows.Length; i++)
            {
                // вторая строка — выделенная, фон остальных чередуется
                Brush rowBack = (i == 1) ? selected : (i % 2 == 0 ? input : panel);
                table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Border row = CreateTableRow(rows[i], rowBack, gridText, borderBrush);
                Grid.SetRow(row, i + 1);
                table.Children.Add(row);
            }

            return table;
        }

        private static Border CreateTableRow(string[] cells, Brush back, Brush foreground, Brush borderBrush)
        {
            Border row = new Border
            {
                Background = back,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            Grid grid = new Grid();

            for (int i = 0; i < cells.Length; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition());

                TextBlock cell = new TextBlock
                {
                    Text = cells[i],
                    Foreground = foreground,
                    FontSize = 12,
                    Margin = new Thickness(6, 3, 0, 3)
                };

                Grid.SetColumn(cell, i);
                grid.Children.Add(cell);
            }

            row.Child = grid;
            return row;
        }

        private static Canvas CreateChartPreview(string themeId, Brush borderBrush)
        {
            ChartMasterColorKeeper keeper = new ChartMasterColorKeeper("themePreview");
            keeper.SetThemeScheme(themeId);

            SolidColorBrush backChart = ToBrush(keeper.ColorBackChart);
            SolidColorBrush backSecond = ToBrush(keeper.ColorBackSecond);
            SolidColorBrush cursor = ToBrush(keeper.ColorBackCursor);
            SolidColorBrush axisText = ToBrush(keeper.ColorText);
            SolidColorBrush upBody = ToBrush(keeper.ColorUpBodyCandle);
            SolidColorBrush upBorder = ToBrush(keeper.ColorUpBorderCandle);
            SolidColorBrush downBody = ToBrush(keeper.ColorDownBodyCandle);
            SolidColorBrush downBorder = ToBrush(keeper.ColorDownBorderCandle);

            const int width = 520;
            const int height = 230;
            const int candleCount = 50;

            Canvas canvas = new Canvas
            {
                Width = width,
                Height = height,
                Background = backChart,
                ClipToBounds = true
            };

            // генерация 50 свечей (данные одинаковые для всех тем)

            Random rand = new Random(42);
            double[] open = new double[candleCount];
            double[] close = new double[candleCount];
            double[] high = new double[candleCount];
            double[] low = new double[candleCount];

            double price = 100;
            double min = price;
            double max = price;

            for (int i = 0; i < candleCount; i++)
            {
                open[i] = price;
                double change = (rand.NextDouble() - 0.48) * 4;
                close[i] = price + change;
                high[i] = Math.Max(open[i], close[i]) + rand.NextDouble() * 1.5;
                low[i] = Math.Min(open[i], close[i]) - rand.NextDouble() * 1.5;
                price = close[i];

                if (high[i] > max) max = high[i];
                if (low[i] < min) min = low[i];
            }

            double scale = (height - 30) / (max - min);

            // горизонтальные линии сетки и подписи осей

            Brush gridLineBrush = backChart.Color == backSecond.Color ? borderBrush : backSecond;

            for (int i = 0; i <= 3; i++)
            {
                double value = min + (max - min) * i / 3;
                double y = 10 + (max - value) * scale;

                Rectangle line = new Rectangle
                {
                    Width = width - 45,
                    Height = 1,
                    Fill = gridLineBrush
                };

                Canvas.SetLeft(line, 0);
                Canvas.SetTop(line, y);
                canvas.Children.Add(line);

                TextBlock label = new TextBlock
                {
                    Text = value.ToString("F1"),
                    Foreground = axisText,
                    FontSize = 10
                };

                Canvas.SetLeft(label, width - 42);
                Canvas.SetTop(label, y - 7);
                canvas.Children.Add(label);
            }

            // свечи

            double candleWidth = (width - 55.0) / candleCount;

            for (int i = 0; i < candleCount; i++)
            {
                bool isUp = close[i] >= open[i];
                Brush bodyBrush = isUp ? upBody : downBody;
                Brush borderCandle = isUp ? upBorder : downBorder;

                double x = 5 + i * candleWidth;
                double bodyTop = 10 + (max - Math.Max(open[i], close[i])) * scale;
                double bodyBottom = 10 + (max - Math.Min(open[i], close[i])) * scale;

                Rectangle wick = new Rectangle
                {
                    Width = 1,
                    Height = (high[i] - low[i]) * scale,
                    Fill = borderCandle
                };

                Canvas.SetLeft(wick, x + candleWidth / 2);
                Canvas.SetTop(wick, 10 + (max - high[i]) * scale);
                canvas.Children.Add(wick);

                Rectangle body = new Rectangle
                {
                    Width = Math.Max(candleWidth - 2, 2),
                    Height = Math.Max(bodyBottom - bodyTop, 1),
                    Fill = bodyBrush,
                    Stroke = borderCandle,
                    StrokeThickness = 1
                };

                Canvas.SetLeft(body, x + 1);
                Canvas.SetTop(body, bodyTop);
                canvas.Children.Add(body);
            }

            // перекрестие курсора

            Rectangle vLine = new Rectangle
            {
                Width = 1,
                Height = height,
                Fill = cursor
            };

            Canvas.SetLeft(vLine, width * 0.6);
            Canvas.SetTop(vLine, 0);
            canvas.Children.Add(vLine);

            Rectangle hLine = new Rectangle
            {
                Width = width,
                Height = 1,
                Fill = cursor
            };

            Canvas.SetLeft(hLine, 0);
            Canvas.SetTop(hLine, height * 0.45);
            canvas.Children.Add(hLine);

            return canvas;
        }

        private static SolidColorBrush ToBrush(System.Drawing.Color color)
        {
            return new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
        }

        private static Border Spacer(double height)
        {
            return new Border { Height = height };
        }

        private static Border SpacerH(double width)
        {
            return new Border { Width = width };
        }
    }
}
