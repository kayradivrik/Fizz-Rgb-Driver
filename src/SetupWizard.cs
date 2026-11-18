using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using Microsoft.Win32;

namespace RedragonK617Setup
{
    public sealed class SetupWindow : Window
    {
        private TextBox _txtInstallPath;
        private CheckBox _chkDesktopShortcut;
        private CheckBox _chkStartMenuShortcut;
        private CheckBox _chkAutostart;
        private CheckBox _chkLaunchAfter;
        private ProgressBar _progressBar;
        private TextBlock _txtStatus;
        private Button _btnInstall;

        private static SolidColorBrush BrushHex(string hex)
        {
            var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
            b.Freeze();
            return b;
        }

        public SetupWindow()
        {
            Title = "FizzCtl — Redragon K617 Fizz Studio Kurulum Sihirbazı";
            Width = 640;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanMinimize;
            Background = BrushHex("#0B0C0E");
            Foreground = BrushHex("#F1F3F5");
            FontFamily = new FontFamily("Segoe UI");

            BuildUI();
        }

        private void BuildUI()
        {
            var root = new Grid { Margin = new Thickness(24) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header Banner
            var headerBorder = new Border
            {
                Background = BrushHex("#0D121B"),
                BorderBrush = BrushHex("#243044"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(0, 0, 0, 16)
            };

            var headerStack = new StackPanel();
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            titleRow.Children.Add(new TextBlock
            {
                Text = "FizzCtl",
                FontSize = 21,
                FontWeight = FontWeights.Black,
                Foreground = BrushHex("#E53935")
            });
            titleRow.Children.Add(new TextBlock
            {
                Text = "  —  Redragon K617 Fizz Studio Setup",
                FontSize = 19,
                FontWeight = FontWeights.Bold,
                Foreground = BrushHex("#F1F3F5")
            });
            headerStack.Children.Add(titleRow);
            headerStack.Children.Add(new TextBlock
            {
                Text = "Açık Kaynaklı RGB Sürücüsü, 23 Aydınlatma Modu ve Makro Motoru (Sinowealth SH68F90A)",
                FontSize = 11.5,
                Foreground = BrushHex("#949BA6"),
                Margin = new Thickness(0, 6, 0, 0)
            });
            headerBorder.Child = headerStack;
            Grid.SetRow(headerBorder, 0);
            root.Children.Add(headerBorder);

            // Body Options
            var bodyBorder = new Border
            {
                Background = BrushHex("#0D121B"),
                BorderBrush = BrushHex("#243044"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20)
            };

            var bodyStack = new StackPanel();
            bodyStack.Children.Add(new TextBlock
            {
                Text = "KURULUM KONUMU",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = BrushHex("#00F0FF"),
                Margin = new Thickness(0, 0, 0, 6)
            });

            var pathGrid = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string defaultDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RedragonK617Studio");

            _txtInstallPath = new TextBox
            {
                Text = defaultDir,
                Background = BrushHex("#131B28"),
                Foreground = BrushHex("#F8FAFC"),
                BorderBrush = BrushHex("#243044"),
                Padding = new Thickness(10, 7, 10, 7),
                FontSize = 12
            };
            Grid.SetColumn(_txtInstallPath, 0);
            pathGrid.Children.Add(_txtInstallPath);

            var btnBrowse = new Button
            {
                Content = "Gözat...",
                Background = BrushHex("#1A273B"),
                Foreground = BrushHex("#F8FAFC"),
                BorderBrush = BrushHex("#243044"),
                Padding = new Thickness(14, 7, 14, 7),
                Margin = new Thickness(8, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btnBrowse.Click += (s, e) =>
            {
                using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dlg.Description = "Redragon K617 Studio kurulum klasörünü seçin:";
                    dlg.SelectedPath = _txtInstallPath.Text;
                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        _txtInstallPath.Text = dlg.SelectedPath;
                    }
                }
            };
            Grid.SetColumn(btnBrowse, 1);
            pathGrid.Children.Add(btnBrowse);
            bodyStack.Children.Add(pathGrid);

            bodyStack.Children.Add(new TextBlock
            {
                Text = "KURULUM SEÇENEKLERİ",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = BrushHex("#00F0FF"),
                Margin = new Thickness(0, 0, 0, 8)
            });

            _chkDesktopShortcut = MakeCheck("Masaüstüne kısayol oluştur (Redragon K617 RGB Studio)", true);
            _chkStartMenuShortcut = MakeCheck("Başlat Menüsüne kısayol ekle", true);
            _chkAutostart = MakeCheck("Windows açılışında Sistem Tepsisinde (Minimized) otomatik başlat", false);
            _chkLaunchAfter = MakeCheck("Kurulum bittiğinde Redragon K617 Studio'yu hemen başlat", true);

            bodyStack.Children.Add(_chkDesktopShortcut);
            bodyStack.Children.Add(_chkStartMenuShortcut);
            bodyStack.Children.Add(_chkAutostart);
            bodyStack.Children.Add(_chkLaunchAfter);

            // Progress area
            _txtStatus = new TextBlock
            {
                Text = "Kuruluma hazır. 'Kurulumu Başlat' butonuna tıklayın.",
                Foreground = BrushHex("#94A3B8"),
                FontSize = 12,
                Margin = new Thickness(0, 14, 0, 6)
            };
            bodyStack.Children.Add(_txtStatus);

            _progressBar = new ProgressBar
            {
                Height = 10,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Background = BrushHex("#131B28"),
                Foreground = BrushHex("#00FF88"),
                BorderThickness = new Thickness(0)
            };
            bodyStack.Children.Add(_progressBar);

            bodyBorder.Child = bodyStack;
            Grid.SetRow(bodyBorder, 1);
            root.Children.Add(bodyBorder);

            // Footer Buttons
            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };

            var btnCancel = new Button
            {
                Content = "İptal",
                Background = BrushHex("#131B28"),
                Foreground = BrushHex("#94A3B8"),
                BorderBrush = BrushHex("#243044"),
                Padding = new Thickness(18, 9, 18, 9),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => Close();
            footer.Children.Add(btnCancel);

            _btnInstall = new Button
            {
                Content = "KURULUMU BAŞLAT (INSTALL)",
                Background = BrushHex("#E62E2D"),
                Foreground = BrushHex("#FFFFFF"),
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(24, 9, 24, 9),
                Cursor = Cursors.Hand
            };
            _btnInstall.Click += async (s, e) => await RunInstallAsync();
            footer.Children.Add(_btnInstall);

            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            Content = root;
        }

        private CheckBox MakeCheck(string text, bool isChecked)
        {
            return new CheckBox
            {
                Content = new TextBlock { Text = text, Foreground = BrushHex("#F8FAFC"), FontSize = 12.5 },
                IsChecked = isChecked,
                Margin = new Thickness(0, 4, 0, 4),
                Cursor = Cursors.Hand
            };
        }

        private async Task RunInstallAsync()
        {
            _btnInstall.IsEnabled = false;
            string targetDir = _txtInstallPath.Text.Trim();
            bool createDesktop = _chkDesktopShortcut.IsChecked == true;
            bool createStartMenu = _chkStartMenuShortcut.IsChecked == true;
            bool enableAutostart = _chkAutostart.IsChecked == true;
            bool launchNow = _chkLaunchAfter.IsChecked == true;

            try
            {
                _txtStatus.Text = "Klasörler hazırlanıyor...";
                _progressBar.Value = 15;
                await Task.Delay(120);

                Directory.CreateDirectory(targetDir);
                Directory.CreateDirectory(Path.Combine(targetDir, "templates"));
                Directory.CreateDirectory(Path.Combine(targetDir, "profiles"));

                _txtStatus.Text = "Uygulama ve donanım şablonları çıkartılıyor...";
                _progressBar.Value = 45;
                await Task.Delay(150);

                ExtractOrCopyFiles(targetDir);

                string installedExe = Path.Combine(targetDir, "RedragonK617_Studio.exe");
                string installedIcon = Path.Combine(targetDir, "icon.ico");

                // Also create shortcut in the current workspace if different
                if (createDesktop)
                {
                    _txtStatus.Text = "Masaüstü kısayolu oluşturuluyor...";
                    _progressBar.Value = 75;
                    string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(Path.Combine(desktopDir, "Redragon K617 RGB Studio.lnk"), installedExe, targetDir, installedIcon);
                }

                if (createStartMenu)
                {
                    _txtStatus.Text = "Başlat Menüsü kısayolu oluşturuluyor...";
                    _progressBar.Value = 85;
                    string startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                    CreateShortcut(Path.Combine(startMenuDir, "Redragon K617 RGB Studio.lnk"), installedExe, targetDir, installedIcon);
                }

                if (enableAutostart)
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("RedragonK617Studio", "\"" + installedExe + "\" --minimized");
                        }
                    }
                }

                _progressBar.Value = 100;
                _txtStatus.Text = "✔ Kurulum başarıyla tamamlandı!";
                _txtStatus.Foreground = BrushHex("#00FF88");

                await Task.Delay(350);

                if (launchNow && File.Exists(installedExe))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = installedExe,
                        WorkingDirectory = targetDir,
                        UseShellExecute = true
                    });
                }

                Close();
            }
            catch (Exception ex)
            {
                _txtStatus.Text = "Hata: " + ex.Message;
                _txtStatus.Foreground = BrushHex("#FF4466");
                _btnInstall.IsEnabled = true;
            }
        }

        public static void ExtractOrCopyFiles(string targetDir)
        {
            var asm = Assembly.GetExecutingAssembly();
            string[] resNames = asm.GetManifestResourceNames();

            foreach (string res in resNames)
            {
                string outRelPath = res;
                if (res.StartsWith("tpl_", StringComparison.OrdinalIgnoreCase))
                    outRelPath = Path.Combine("templates", res.Substring(4));
                else if (res.StartsWith("prf_", StringComparison.OrdinalIgnoreCase))
                    outRelPath = Path.Combine("profiles", res.Substring(4));
                else if (res.StartsWith("web_", StringComparison.OrdinalIgnoreCase))
                    outRelPath = Path.Combine("web", res.Substring(4));

                string destFile = Path.Combine(targetDir, outRelPath);
                string destFolder = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destFolder))
                    Directory.CreateDirectory(destFolder);

                using (Stream s = asm.GetManifestResourceStream(res))
                {
                    if (s != null)
                    {
                        using (var fs = new FileStream(destFile, FileMode.Create, FileAccess.Write))
                        {
                            s.CopyTo(fs);
                        }
                    }
                }
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetExe, string workDir, string iconPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                Type scType = shortcut.GetType();
                scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetExe });
                scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workDir });
                scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Redragon K617 Fizz RGB & Macro Studio" });
                if (File.Exists(iconPath))
                    scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconPath });
                scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch { }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            var app = new Application();
            app.Run(new SetupWindow());
        }
    }
}
