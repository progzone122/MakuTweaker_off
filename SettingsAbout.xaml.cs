using MakuTweakerNew.Properties;
using MicaWPF.Core.Enums;
using MicaWPF.Core.Services;
using Microsoft.Win32;
using ModernWpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MakuTweakerNew
{
    public partial class SettingsAbout : Page
    {
        private MainWindow mw = (MainWindow)System.Windows.Application.Current.MainWindow;
        bool isLoaded = false;

        // Данные для локализации
        private readonly string[] _languages = { "en", "ru", "ua", "cz", "de", "es", "pl", "et", "zh", "ja", "tl" };
        
        // Словарь переводчиков: Код языка -> (Заголовок, Имя)
        private readonly Dictionary<string, (string Label, string Name)> _translators = new()
        {
            ["cz"] = ("Pomohl s lokalizací:", "qCLairvoyant"),
            ["de"] = ("Hilfe bei der Lokalisierung:", "Scorazio"),
            ["pl"] = ("Pomoc w lokalizacji:", "dfa_jk"),
            ["et"] = ("Aitas lokaliseerimisega:", "KirTeanEesti")
        };

        public SettingsAbout()
        {
            InitializeComponent();
            
            // Базовая инициализация
            credN.Text = "Mark Adderly\nNikitori\nNikitori, Massgrave";
            lang.SelectedIndex = Settings.Default.langSI;
            
            if (checkWinVer() < 22000)
            {
                style.Visibility = Visibility.Collapsed;
                styleL.Visibility = Visibility.Collapsed;
            }

            // Установка темы
            var currentTheme = MicaWPFServiceUtility.ThemeService.CurrentTheme;
            theme.SelectedIndex = currentTheme == WindowsTheme.Dark ? 1 : 0;

            // Установка индекса стиля через switch-выражение
            style.SelectedIndex = Settings.Default.style switch
            {
                "Mica" => 0,
                "Tabbed" => 1,
                "Acrylic" => 2,
                "None" => 3,
                _ => 0
            };

            relang();
            UpdateLocalizationCredits();
            isLoaded = true;
        }

        private int checkWinVer()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                var value = key?.GetValue("CurrentBuild");
                return (value != null && int.TryParse(value.ToString(), out int build)) ? build : 19045;
            }
            catch { return 19045; }
        }

        // Вспомогательный метод для ссылок
        private void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        #region Обработчики событий (имена оставлены как в XAML)

        private void Button_Click(object sender, RoutedEventArgs e) => OpenUrl("https://adderly.top");

        private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => OpenUrl("https://boosty.to/adderly");

        private void Image_MouseLeftButtonUp_2(object sender, MouseButtonEventArgs e) => OpenUrl("https://t.me/adderly324");

        private void Image_MouseLeftButtonUp_3(object sender, MouseButtonEventArgs e) => OpenUrl("https://youtube.com/@MakuAdarii");

        private void theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;

            bool isDark = theme.SelectedIndex == 1;
            Settings.Default.theme = isDark ? "Dark" : "Light";
            
            MicaWPFServiceUtility.ThemeService.ChangeTheme(isDark ? WindowsTheme.Dark : WindowsTheme.Light);
            ThemeManager.Current.ApplicationTheme = isDark ? ApplicationTheme.Dark : ApplicationTheme.Light;
            
            Brush color = isDark ? Brushes.White : Brushes.Black;
            mw.Foreground = color;
            mw.Separator.Stroke = color;

            Settings.Default.Save();
        }

        private void lang_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;

            int idx = lang.SelectedIndex;
            if (idx >= 0 && idx < _languages.Length)
            {
                Settings.Default.lang = _languages[idx];
            }
            
            Settings.Default.langSI = idx;
            Settings.Default.Save();
            
            mw.LoadLang(Settings.Default.lang);
            relang();
            UpdateLocalizationCredits();
        }

        private void style_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;

            // Кортеж: тип фона и строковое имя
            var styleData = style.SelectedIndex switch
            {
                0 => (Type: BackdropType.Mica, Name: "Mica"),
                1 => (Type: BackdropType.Tabbed, Name: "Tabbed"),
                2 => (Type: BackdropType.Acrylic, Name: "Acrylic"),
                _ => (Type: BackdropType.None, Name: "None")
            };

            MicaWPFServiceUtility.ThemeService.EnableBackdrop(mw, styleData.Type);
            Settings.Default.style = styleData.Name;
            Settings.Default.Save();
        }

        #endregion

        private void relang()
        {
            var languageCode = Settings.Default.lang ?? "en";
            var ab = MainWindow.Localization.LoadLocalization(languageCode, "ab");
            var b = MainWindow.Localization.LoadLocalization(languageCode, "base");

            credL.Text = ab["main"]["credL"];
            label.Text = ab["main"]["label"];
            web.Content = ab["main"]["atop"];
            langL.Text = ab["main"]["lang"];
            styleL.Text = ab["main"]["st"];
            themeL.Text = ab["main"]["th"];
            l.Content = " " + ab["main"]["l"];
            d.Content = " " + ab["main"]["d"];
            off.Content = " " + b["def"]["off"];
        }

        private void UpdateLocalizationCredits()
        {
            string currentLang = Settings.Default.lang ?? "en";

            if (_translators.TryGetValue(currentLang, out var credits))
            {
                credLang.Visibility = Visibility.Visible;
                credLangtext.Visibility = Visibility.Visible;
                credLang.Text = credits.Label;
                credLangtext.Text = credits.Name;
            }
            else
            {
                credLang.Visibility = Visibility.Collapsed;
                credLangtext.Visibility = Visibility.Collapsed;
            }
        }
    }
}
