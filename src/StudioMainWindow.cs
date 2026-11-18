using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace RedragonK617Studio
{
    public sealed class StudioWindow : Window
    {
        private readonly string _baseDir;
        private readonly K617HardwareDriver _driver;
        private readonly NativeMacroEngine _macroEngine;
        private readonly RgbEffectsEngine _fxEngine;

        // 61-key sculpted keycap visual cache
        private readonly Border[] _keyOuterBorders = new Border[61];
        private readonly Border[] _keyTopCaps = new Border[61];
        private readonly Border[] _keyLedDots = new Border[61];
        private readonly TextBlock[] _keyLegends = new TextBlock[61];
        private readonly SolidColorBrush[] _keyRimBrushes = new SolidColorBrush[61];
        private readonly SolidColorBrush[] _keyLegendBrushes = new SolidColorBrush[61];
        private readonly SolidColorBrush[] _keyLedBrushes = new SolidColorBrush[61];
        private readonly int[] _lastKeyPackedRgb = new int[61];

        // Navigation & UI state
        private string _activeTab = "effects";
        private Color _brushColor = Color.FromRgb(229, 57, 53);
        private bool _isPaintingDrag = false;
        private bool _livePreviewEnabled = true;
        private bool _minimizeToTray = true;
        private bool _isBindingMacroTrigger = false;

        // Telemetry & status controls
        private TextBlock _txtSidebarRam;
        private TextBlock _txtSidebarFps;
        private TextBlock _txtConnStatus;
        private Ellipse _connDot;
        private TextBlock _txtActiveModeBadge;
        private TextBlock _txtToast;
        private Border _toastBorder;
        private DispatcherTimer _toastTimer;

        // Tab views
        private readonly Dictionary<string, UIElement> _tabPanels = new Dictionary<string, UIElement>();
        private readonly Dictionary<string, Button> _navButtons = new Dictionary<string, Button>();

        // Effects browser
        private WrapPanel _effectsCardsGrid;
        private string _categoryFilter = "all";
        private string _searchQuery = "";
        private readonly List<Border> _effectCardBorders = new List<Border>();

        // Macro studio controls
        private StackPanel _macroListPanel;
        private StackPanel _macroStepsPanel;
        private TextBox _txtMacroName;
        private TextBox _txtMacroTrigger;
        private Button _btnBindTrigger;
        private ComboBox _cmbMacroRepeat;
        private TextBox _txtMacroRepeatCount;
        private Button _btnRecordMacro;
        private TextBox _txtStepKeyInput;
        private TextBox _txtStepDelayInput;
        private ComboBox _cmbStepMouseInput;
        private TextBox _txtStepTextInput;
        private TextBox _txtProfileSaveName;
        private string _editingMacroId = null;
        private List<MacroStepItem> _editingSteps = new List<MacroStepItem>();

        // Settings controls
        private TextBlock _txtSettingsRam;
        private CheckBox _chkEcoMode;
        private ComboBox _cmbTargetFps;
        private CheckBox _chkLivePreview;
        private CheckBox _chkAutostart;
        private ComboBox _cmbIdleTimeout;
        private CheckBox _chkTray;

        // System tray
        private System.Windows.Forms.NotifyIcon _trayIcon;
        private bool _forceExit = false;

        // Refined studio palette (inspired by Wootility v5 & Linear Dark)
        private static readonly SolidColorBrush BgApp = BrushHex("#0B0C0E");
        private static readonly SolidColorBrush BgSidebar = BrushHex("#101216");
        private static readonly SolidColorBrush BgSurface = BrushHex("#15181E");
        private static readonly SolidColorBrush BgElevated = BrushHex("#1C2028");
        private static readonly SolidColorBrush BgInput = BrushHex("#0E1014");
        private static readonly SolidColorBrush BorderSubtle = BrushHex("#242933");
        private static readonly SolidColorBrush BorderFocus = BrushHex("#384050");
        private static readonly SolidColorBrush BrandAccent = BrushHex("#E53935");
        private static readonly SolidColorBrush BrandAccentSoft = BrushHex("#2A1618");
        private static readonly SolidColorBrush StatusOnline = BrushHex("#10B981");
        private static readonly SolidColorBrush StatusWarn = BrushHex("#F59E0B");
        private static readonly SolidColorBrush TextPrimary = BrushHex("#F1F3F5");
        private static readonly SolidColorBrush TextSecondary = BrushHex("#949BA6");
        private static readonly SolidColorBrush TextMuted = BrushHex("#5C6370");

        private static readonly FontFamily FontSans = new FontFamily("Segoe UI");
        private static readonly FontFamily FontMono = new FontFamily("Consolas");

        private static SolidColorBrush BrushHex(string hex)
        {
            var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
            b.Freeze();
            return b;
        }

        private static readonly HashSet<string> ModifierKeys = new HashSet<string>
        {
            "esc", "tab", "caps", "lshift", "lctrl", "win", "lalt", "ralt",
            "fn", "menu", "rctrl", "rshift", "enter", "backspace", "backslash"
        };

        public StudioWindow(bool startMinimized)
        {
            _baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!Directory.Exists(Path.Combine(_baseDir, "templates")))
            {
                var parentInfo = Directory.GetParent(_baseDir.TrimEnd('\\', '/'));
                string parent = parentInfo != null ? parentInfo.FullName : null;
                if (parent != null && Directory.Exists(Path.Combine(parent, "templates")))
                    _baseDir = parent;
            }

            _driver = new K617HardwareDriver();
            _macroEngine = new NativeMacroEngine(_baseDir);
            _fxEngine = new RgbEffectsEngine(_driver);

            _macroEngine.OnPhysicalKeyPressed += (vk, kName) =>
            {
                _fxEngine.NotifyKeyPress(vk);
                if (_isBindingMacroTrigger)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _isBindingMacroTrigger = false;
                        _txtMacroTrigger.Text = kName.ToUpperInvariant();
                        _btnBindTrigger.Content = "Tuş Ata";
                    }));
                }
            };

            LoadSettingsFromDisk();

            Title = "FizzCtl — Redragon K617 Fizz Studio";
            Width = 1280;
            Height = 840;
            MinWidth = 1040;
            MinHeight = 720;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = false;
            Background = BgApp;
            FontFamily = FontSans;

            LoadCustomThemeResources();

            string icoPath = Path.Combine(_baseDir, "icon.ico");
            if (File.Exists(icoPath))
            {
                try { Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(icoPath)); } catch { }
            }

            BuildUserInterface();
            InitSystemTray(icoPath);

            var uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(85) };
            int tickCounter = 0;
            uiTimer.Tick += (s, e) =>
            {
                if (WindowState == WindowState.Minimized || !IsVisible) return;

                if (_livePreviewEnabled)
                {
                    UpdateKeyboardPreview();
                }

                tickCounter++;
                if (tickCounter % 15 == 0)
                {
                    if (_fxEngine.EcoMode && tickCounter % 120 == 0)
                        NativeMethods.TrimMemory();

                    double ram = NativeMethods.GetWorkingSetMB();
                    if (_txtSidebarRam != null) _txtSidebarRam.Text = string.Format("{0:0.0} MB", ram);
                    if (_txtSidebarFps != null) _txtSidebarFps.Text = string.Format("{0:0} FPS", _fxEngine.ActualFps);
                    if (_txtSettingsRam != null) _txtSettingsRam.Text = string.Format("{0:0.0} MB", ram);
                    if (_txtConnStatus != null)
                    {
                        _txtConnStatus.Text = _driver.IsConnected ? "K617 Fizz Bağlı" : "Aygıt Bekleniyor";
                        _connDot.Fill = _driver.IsConnected ? StatusOnline : StatusWarn;
                    }
                }
            };
            uiTimer.Start();

            MouseLeftButtonUp += (s, e) => { _isPaintingDrag = false; };

            if (startMinimized)
            {
                Loaded += (s, e) => { Hide(); NativeMethods.TrimMemory(); };
            }
            else
            {
                Loaded += (s, e) => NativeMethods.TrimMemory();
            }
        }

        private void LoadCustomThemeResources()
        {
            const string xaml = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>

  <!-- Slim Modern ScrollBar -->
  <Style TargetType='{x:Type ScrollBar}'>
    <Setter Property='Width' Value='6'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type ScrollBar}'>
          <Grid Background='Transparent'>
            <Track Name='PART_Track' IsDirectionReversed='true'>
              <Track.Thumb>
                <Thumb>
                  <Thumb.Template>
                    <ControlTemplate TargetType='{x:Type Thumb}'>
                      <Border Background='#2A303C' CornerRadius='3' Margin='1,0,1,0'/>
                    </ControlTemplate>
                  </Thumb.Template>
                </Thumb>
              </Track.Thumb>
            </Track>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Modern iOS/Win11 Pill Toggle Switch for CheckBox -->
  <Style x:Key='ModernSwitch' TargetType='{x:Type CheckBox}'>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Foreground' Value='#F1F3F5'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type CheckBox}'>
          <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
            <Border x:Name='Track' Width='38' Height='20' CornerRadius='10' Background='#222630' BorderBrush='#323846' BorderThickness='1'>
              <Border x:Name='Thumb' Width='14' Height='14' CornerRadius='7' Background='#949BA6' HorizontalAlignment='Left' Margin='2,0,0,0'/>
            </Border>
            <ContentPresenter VerticalAlignment='Center' Margin='8,0,0,0'/>
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='Track' Property='Background' Value='#E53935'/>
              <Setter TargetName='Track' Property='BorderBrush' Value='#E53935'/>
              <Setter TargetName='Thumb' Property='Background' Value='#FFFFFF'/>
              <Setter TargetName='Thumb' Property='HorizontalAlignment' Value='Right'/>
              <Setter TargetName='Thumb' Property='Margin' Value='0,0,2,0'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Modern Precision Slider -->
  <Style TargetType='{x:Type Slider}'>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type Slider}'>
          <Grid VerticalAlignment='Center' Height='20'>
            <Border Height='4' CornerRadius='2' Background='#222630' VerticalAlignment='Center'/>
            <Track x:Name='PART_Track'>
              <Track.DecreaseRepeatButton>
                <RepeatButton Command='Slider.DecreaseLarge'>
                  <RepeatButton.Template>
                    <ControlTemplate TargetType='{x:Type RepeatButton}'>
                      <Border Height='4' CornerRadius='2' Background='#E53935'/>
                    </ControlTemplate>
                  </RepeatButton.Template>
                </RepeatButton>
              </Track.DecreaseRepeatButton>
              <Track.IncreaseRepeatButton>
                <RepeatButton Command='Slider.IncreaseLarge'>
                  <RepeatButton.Template>
                    <ControlTemplate TargetType='{x:Type RepeatButton}'>
                      <Border Height='4' Background='Transparent'/>
                    </ControlTemplate>
                  </RepeatButton.Template>
                </RepeatButton>
              </Track.IncreaseRepeatButton>
              <Track.Thumb>
                <Thumb>
                  <Thumb.Template>
                    <ControlTemplate TargetType='{x:Type Thumb}'>
                      <Border Width='14' Height='14' CornerRadius='7' Background='#F1F3F5' BorderBrush='#E53935' BorderThickness='2'/>
                    </ControlTemplate>
                  </Thumb.Template>
                </Thumb>
              </Track.Thumb>
            </Track>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Clean Dark Button (No default Aero blue glow) -->
  <Style x:Key='StudioButton' TargetType='{x:Type Button}'>
    <Setter Property='Background' Value='#1C2028'/>
    <Setter Property='Foreground' Value='#F1F3F5'/>
    <Setter Property='BorderBrush' Value='#2B313D'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='12,6'/>
    <Setter Property='FontSize' Value='12'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type Button}'>
          <Border x:Name='Bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='6' Padding='{TemplateBinding Padding}'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='Bd' Property='BorderBrush' Value='#485164'/>
            </Trigger>
            <Trigger Property='IsPressed' Value='True'>
              <Setter TargetName='Bd' Property='Opacity' Value='0.85'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Dark ComboBox & ComboBoxItem -->
  <Style TargetType='{x:Type ComboBoxItem}'>
    <Setter Property='Background' Value='#15181E'/>
    <Setter Property='Foreground' Value='#F1F3F5'/>
    <Setter Property='Padding' Value='10,6'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type ComboBoxItem}'>
          <Border x:Name='Bd' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'>
            <ContentPresenter/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsHighlighted' Value='True'>
              <Setter TargetName='Bd' Property='Background' Value='#252A34'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='{x:Type ComboBox}'>
    <Setter Property='Background' Value='#0E1014'/>
    <Setter Property='Foreground' Value='#F1F3F5'/>
    <Setter Property='BorderBrush' Value='#242933'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='10,6'/>
    <Setter Property='FontSize' Value='12'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type ComboBox}'>
          <Grid>
            <ToggleButton x:Name='ToggleButton' Focusable='false'
                          IsChecked='{Binding Path=IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'
                          ClickMode='Press'>
              <ToggleButton.Template>
                <ControlTemplate TargetType='{x:Type ToggleButton}'>
                  <Border x:Name='Bd' Background='#0E1014' BorderBrush='#242933' BorderThickness='1' CornerRadius='6'>
                    <Grid>
                      <Grid.ColumnDefinitions>
                        <ColumnDefinition/>
                        <ColumnDefinition Width='26'/>
                      </Grid.ColumnDefinitions>
                      <Path Grid.Column='1' HorizontalAlignment='Center' VerticalAlignment='Center' Data='M 0 0 L 4 4 L 8 0 Z' Fill='#949BA6'/>
                    </Grid>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property='IsMouseOver' Value='True'>
                      <Setter TargetName='Bd' Property='BorderBrush' Value='#384050'/>
                    </Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </ToggleButton.Template>
            </ToggleButton>
            <ContentPresenter x:Name='ContentSite' IsHitTestVisible='False'
                              Content='{TemplateBinding SelectionBoxItem}'
                              Margin='10,6,28,6' VerticalAlignment='Center' HorizontalAlignment='Left'/>
            <Popup x:Name='Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False' PopupAnimation='Slide'>
              <Border x:Name='DropDownBorder' Background='#15181E' BorderBrush='#2B313D' BorderThickness='1' CornerRadius='6' Margin='0,4,0,0'
                      MinWidth='{TemplateBinding ActualWidth}' MaxHeight='240'>
                <ScrollViewer Margin='2' SnapsToDevicePixels='True'>
                  <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Contained'/>
                </ScrollViewer>
              </Border>
            </Popup>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>";
            try
            {
                var dict = (ResourceDictionary)XamlReader.Parse(xaml);
                Resources.MergedDictionaries.Add(dict);
            }
            catch { }
        }

        private void showToast(string msg)
        {
            if (_txtToast == null || _toastBorder == null) return;
            _txtToast.Text = msg;
            _toastBorder.Visibility = Visibility.Visible;
            if (_toastTimer == null)
            {
                _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2200) };
                _toastTimer.Tick += (s, e) =>
                {
                    _toastBorder.Visibility = Visibility.Collapsed;
                    _toastTimer.Stop();
                };
            }
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void InitSystemTray(string icoPath)
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "FizzCtl — Redragon K617 Fizz Studio",
                Visible = true
            };
            if (File.Exists(icoPath))
            {
                try { _trayIcon.Icon = new System.Drawing.Icon(icoPath); }
                catch { _trayIcon.Icon = System.Drawing.SystemIcons.Application; }
            }
            else
            {
                _trayIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("FizzCtl Stüdyoyu Aç", null, (s, e) => RestoreFromTray());
            menu.Items.Add("Belleği Temizle (RAM Trim)", null, (s, e) => NativeMethods.TrimMemory());
            menu.Items.Add("-");
            menu.Items.Add("Çıkış", null, (s, e) => ExitApplication());
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitApplication()
        {
            _forceExit = true;
            SaveSettingsToDisk();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            _fxEngine.Dispose();
            _macroEngine.Dispose();
            _driver.Dispose();
            Application.Current.Shutdown();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_forceExit && _minimizeToTray)
            {
                e.Cancel = true;
                Hide();
                NativeMethods.TrimMemory();
                return;
            }
            base.OnClosing(e);
        }

        // =====================================================================
        // MODERN SPLIT-PANE STUDIO SHELL (LEFT SIDEBAR + RIGHT WORKSPACE)
        // =====================================================================
        private void BuildUserInterface()
        {
            var outerBorder = new Border
            {
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(1),
                Background = BgApp
            };

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) }); // Sleek window titlebar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Main split shell

            rootGrid.Children.Add(BuildWindowTitleBar());

            var shellGrid = new Grid();
            shellGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) }); // Left Sidebar
            shellGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Main Stage & Panels

            var sidebar = BuildLeftSidebar();
            Grid.SetColumn(sidebar, 0);
            shellGrid.Children.Add(sidebar);

            var workspaceGrid = new Grid { Margin = new Thickness(18, 14, 18, 16) };
            workspaceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Keyboard Stage
            workspaceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) }); // Gap
            workspaceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Active Module

            var kbStage = BuildKeyboardStage();
            Grid.SetRow(kbStage, 0);
            workspaceGrid.Children.Add(kbStage);

            var tabHost = new Grid();
            _tabPanels["effects"] = BuildEffectsTab();
            _tabPanels["paint"] = BuildPaintTab();
            _tabPanels["macros"] = BuildMacrosTab();
            _tabPanels["firmware"] = BuildFirmwareTab();
            _tabPanels["settings"] = BuildSettingsTab();

            foreach (var kv in _tabPanels)
            {
                kv.Value.Visibility = (kv.Key == "effects") ? Visibility.Visible : Visibility.Collapsed;
                tabHost.Children.Add(kv.Value);
            }
            Grid.SetRow(tabHost, 2);
            workspaceGrid.Children.Add(tabHost);

            // Toast notification overlay
            _toastBorder = new Border
            {
                Background = BgElevated,
                BorderBrush = BrandAccent,
                BorderThickness = new Thickness(3, 1, 1, 1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(14, 8, 14, 8),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Visibility = Visibility.Collapsed
            };
            _txtToast = MakeText("", 11.5, TextPrimary, FontWeights.SemiBold);
            _toastBorder.Child = _txtToast;
            Grid.SetRow(_toastBorder, 2);
            workspaceGrid.Children.Add(_toastBorder);

            Grid.SetColumn(workspaceGrid, 1);
            shellGrid.Children.Add(workspaceGrid);

            Grid.SetRow(shellGrid, 1);
            rootGrid.Children.Add(shellGrid);

            outerBorder.Child = rootGrid;
            Content = outerBorder;
        }

        private UIElement BuildWindowTitleBar()
        {
            var bar = new Border
            {
                Background = BgSidebar,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 0, 10, 0)
            };
            bar.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                    WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
                else
                    DragMove();
            };

            var dock = new DockPanel { VerticalAlignment = VerticalAlignment.Center };

            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var accentPill = new Border
            {
                Width = 8, Height = 16, CornerRadius = new CornerRadius(2),
                Background = BrandAccent, Margin = new Thickness(0, 0, 10, 0)
            };
            left.Children.Add(accentPill);
            left.Children.Add(MakeText("FizzCtl", 13.5, TextPrimary, FontWeights.Bold));
            left.Children.Add(MakeText("  —  Redragon K617 Fizz Driver & Lighting Studio", 12, TextSecondary));
            dock.Children.Add(left);

            var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            right.Children.Add(MakeWinControlBtn("—", "Küçült", () => WindowState = WindowState.Minimized, false));
            right.Children.Add(MakeWinControlBtn("▢", "Tam Ekran", () => WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized, false));
            right.Children.Add(MakeWinControlBtn("✕", "Kapat", () =>
            {
                if (_minimizeToTray) { Hide(); NativeMethods.TrimMemory(); }
                else ExitApplication();
            }, true));

            dock.Children.Add(right);
            bar.Child = dock;
            return bar;
        }

        private Button MakeWinControlBtn(string symbol, string tip, Action action, bool isClose)
        {
            var btn = new Button
            {
                Style = (Style)Resources["StudioButton"],
                Content = symbol,
                ToolTip = tip,
                Width = 32,
                Height = 26,
                Padding = new Thickness(0),
                Margin = new Thickness(3, 0, 0, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = isClose ? BrushHex("#F87171") : TextSecondary
            };
            btn.Click += (s, e) => action();
            return btn;
        }

        private UIElement BuildLeftSidebar()
        {
            var border = new Border
            {
                Background = BgSidebar,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(12, 16, 12, 16)
            };

            var dock = new DockPanel();

            // Top Device Card
            var devCard = new Border
            {
                Background = BgSurface,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 18)
            };
            var devStack = new StackPanel();
            devStack.Children.Add(MakeText("K617 FIZZ 60%", 12.5, TextPrimary, FontWeights.Bold));
            devStack.Children.Add(MakeText("Sinowealth SH68F90A • USB HID", 10.5, TextMuted));

            var connRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            _connDot = new Ellipse
            {
                Width = 7, Height = 7,
                Fill = _driver.IsConnected ? StatusOnline : StatusWarn,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            connRow.Children.Add(_connDot);
            _txtConnStatus = MakeText(_driver.IsConnected ? "K617 Fizz Bağlı" : "Aygıt Bekleniyor", 11, TextSecondary, FontWeights.SemiBold);
            connRow.Children.Add(_txtConnStatus);
            devStack.Children.Add(connRow);
            devCard.Child = devStack;
            DockPanel.SetDock(devCard, Dock.Top);
            dock.Children.Add(devCard);

            // Navigation Section
            var navStack = new StackPanel();
            var navLabel = MakeText("MODÜLLER", 10, TextMuted, FontWeights.Bold);
            navLabel.Margin = new Thickness(8, 0, 0, 6);
            navStack.Children.Add(navLabel);

            navStack.Children.Add(MakeNavButton("effects", "Aydınlatma Efektleri"));
            navStack.Children.Add(MakeNavButton("paint", "Tuş Boyama & Profiller"));
            navStack.Children.Add(MakeNavButton("macros", "Makro Stüdyosu"));
            navStack.Children.Add(MakeNavButton("firmware", "Donanım Hafızası"));
            navStack.Children.Add(MakeNavButton("settings", "Ayarlar & Performans"));

            DockPanel.SetDock(navStack, Dock.Top);
            dock.Children.Add(navStack);

            // Bottom Resource Telemetry Box
            var telCard = new Border
            {
                Background = BgApp,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(12, 10, 12, 10),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            var telStack = new StackPanel();
            var telGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            telGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            telGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var ramCell = new StackPanel();
            ramCell.Children.Add(MakeText("BELLEK", 9.5, TextMuted, FontWeights.Bold));
            _txtSidebarRam = MakeText("4.2 MB", 12.5, StatusOnline, FontWeights.Bold);
            _txtSidebarRam.FontFamily = FontMono;
            ramCell.Children.Add(_txtSidebarRam);
            Grid.SetColumn(ramCell, 0);
            telGrid.Children.Add(ramCell);

            var fpsCell = new StackPanel();
            fpsCell.Children.Add(MakeText("YENİLEME", 9.5, TextMuted, FontWeights.Bold));
            _txtSidebarFps = MakeText("35 FPS", 12.5, TextPrimary, FontWeights.Bold);
            _txtSidebarFps.FontFamily = FontMono;
            fpsCell.Children.Add(_txtSidebarFps);
            Grid.SetColumn(fpsCell, 1);
            telGrid.Children.Add(fpsCell);

            telStack.Children.Add(telGrid);

            var btnTrim = MakeActionBtn("Belleği Temizle", false, () =>
            {
                NativeMethods.TrimMemory();
                double r = NativeMethods.GetWorkingSetMB();
                if (_txtSidebarRam != null) _txtSidebarRam.Text = string.Format("{0:0.0} MB", r);
                if (_txtSettingsRam != null) _txtSettingsRam.Text = string.Format("{0:0.0} MB", r);
                showToast(string.Format("Bellek optimize edildi ({0:0.0} MB)", r));
            });
            btnTrim.Margin = new Thickness(0);
            telStack.Children.Add(btnTrim);

            telCard.Child = telStack;
            DockPanel.SetDock(telCard, Dock.Bottom);
            dock.Children.Add(telCard);

            border.Child = dock;
            return border;
        }

        private Button MakeNavButton(string tabKey, string title)
        {
            bool active = (tabKey == _activeTab);
            var btn = new Button
            {
                Style = (Style)Resources["StudioButton"],
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 4),
                Background = active ? BgElevated : Brushes.Transparent,
                Foreground = active ? TextPrimary : TextSecondary,
                BorderBrush = active ? BrandAccent : Brushes.Transparent,
                BorderThickness = new Thickness(active ? 3 : 1, 0, 0, 0),
                Content = title
            };
            _navButtons[tabKey] = btn;
            btn.Click += (s, e) => SwitchTab(tabKey);
            return btn;
        }

        private void SwitchTab(string tabKey)
        {
            _activeTab = tabKey;
            foreach (var kv in _tabPanels)
                kv.Value.Visibility = (kv.Key == tabKey) ? Visibility.Visible : Visibility.Collapsed;

            foreach (var kv in _navButtons)
            {
                bool act = (kv.Key == tabKey);
                kv.Value.Background = act ? BgElevated : Brushes.Transparent;
                kv.Value.Foreground = act ? TextPrimary : TextSecondary;
                kv.Value.BorderBrush = act ? BrandAccent : Brushes.Transparent;
                kv.Value.BorderThickness = new Thickness(act ? 3 : 1, 0, 0, 0);
            }

            if (tabKey == "paint")
            {
                _fxEngine.CurrentMode = "per_key";
                _fxEngine.MarkDirty();
                if (_txtActiveModeBadge != null) _txtActiveModeBadge.Text = "Özel Tuş Haritası";
            }
            else if (tabKey == "macros")
            {
                RefreshMacroListUI();
            }
        }

        // =====================================================================
        // REALISTIC 3D SCULPTED 61-KEY MECHANICAL KEYBOARD STAGE
        // =====================================================================
        private UIElement BuildKeyboardStage()
        {
            var stageCard = new Border
            {
                Background = BgSurface,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18, 12, 18, 14)
            };

            var stack = new StackPanel();

            // Stage Header with Quick Key Group Selectors
            var topDock = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var leftMeta = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            leftMeta.Children.Add(MakeText("61-Tuş ANSI Mekanik Klavye", 12, TextSecondary, FontWeights.SemiBold));
            leftMeta.Children.Add(MakeText("   •   ", 12, TextMuted));
            _txtActiveModeBadge = MakeText("Gökkuşağı Dalgası", 12, TextPrimary, FontWeights.Bold);
            leftMeta.Children.Add(_txtActiveModeBadge);
            topDock.Children.Add(leftMeta);

            var rightGroups = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            rightGroups.Children.Add(MakeText("Hızlı Boya: ", 11, TextMuted, FontWeights.SemiBold));
            rightGroups.Children.Add(MakeQuickGroupBtn("WASD", new[] { "w", "a", "s", "d" }));
            rightGroups.Children.Add(MakeQuickGroupBtn("QWER", new[] { "q", "w", "e", "r", "d", "f" }));
            rightGroups.Children.Add(MakeQuickGroupBtn("1-0", new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" }));
            rightGroups.Children.Add(MakeQuickGroupBtn("Modifiye", ModifierKeys.ToArray()));
            topDock.Children.Add(rightGroups);

            stack.Children.Add(topDock);

            // Anodized Aluminum Bezel & Recessed Switch Plate
            var caseBezel = new Border
            {
                Background = BrushHex("#181B22"),
                BorderBrush = BrushHex("#2C323E"),
                BorderThickness = new Thickness(1, 1, 1, 3),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 10, 10, 8),
                MaxWidth = 960,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var switchPlate = new Border
            {
                Background = BrushHex("#08090C"),
                BorderBrush = BrushHex("#111318"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6)
            };

            var rowsStack = new StackPanel();
            var keys = K617HardwareDriver.Keys;

            for (int r = 0; r < 5; r++)
            {
                var rowGrid = new Grid { Height = 42, Margin = new Thickness(0, r == 0 ? 0 : 4, 0, 0) };
                int colCursor = 0;
                for (int i = 0; i < keys.Length; i++)
                {
                    if (keys[i].Row != r) continue;
                    int kIndex = i;
                    var k = keys[i];
                    bool isMod = ModifierKeys.Contains(k.Id);

                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(k.Width, GridUnitType.Star) });

                    var rimBrush = new SolidColorBrush(Color.FromRgb(45, 52, 64));
                    var ledBrush = new SolidColorBrush(Color.FromRgb(229, 57, 53));
                    var legendBrush = new SolidColorBrush(Color.FromRgb(240, 242, 245));

                    _keyRimBrushes[kIndex] = rimBrush;
                    _keyLedBrushes[kIndex] = ledBrush;
                    _keyLegendBrushes[kIndex] = legendBrush;

                    // Outer Keycap Skirt
                    var outerSkirt = new Border
                    {
                        Background = isMod ? BrushHex("#1C1F26") : BrushHex("#232730"),
                        BorderBrush = rimBrush,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(5),
                        Margin = new Thickness(2, 0, 2, 0),
                        Padding = new Thickness(3, 2, 3, 5),
                        Cursor = Cursors.Hand
                    };

                    // Inner Sculpted Top Surface
                    var topCap = new Border
                    {
                        Background = isMod ? BrushHex("#15181E") : BrushHex("#191C23"),
                        CornerRadius = new CornerRadius(3.5),
                        Padding = new Thickness(5, 3, 4, 2)
                    };

                    var capGrid = new Grid();

                    // Top-Left Key Legend
                    var lbl = new TextBlock
                    {
                        Text = k.Id == "space" ? "" : k.Label,
                        Foreground = legendBrush,
                        FontFamily = FontMono,
                        FontSize = isMod ? 9.5 : 10.5,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    capGrid.Children.Add(lbl);

                    // Bottom RGB Switch Diffuser Bar
                    var ledBar = new Border
                    {
                        Height = 2.5,
                        Width = 16,
                        CornerRadius = new CornerRadius(1.2),
                        Background = ledBrush,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, 0, 1)
                    };
                    capGrid.Children.Add(ledBar);

                    topCap.Child = capGrid;
                    outerSkirt.Child = topCap;

                    outerSkirt.MouseLeftButtonDown += (s, e) =>
                    {
                        _isPaintingDrag = true;
                        PaintSingleKey(kIndex);
                    };
                    outerSkirt.MouseEnter += (s, e) =>
                    {
                        if (_isPaintingDrag && e.LeftButton == MouseButtonState.Pressed)
                            PaintSingleKey(kIndex);
                    };

                    Grid.SetColumn(outerSkirt, colCursor++);
                    rowGrid.Children.Add(outerSkirt);

                    _keyOuterBorders[kIndex] = outerSkirt;
                    _keyTopCaps[kIndex] = topCap;
                    _keyLedDots[kIndex] = ledBar;
                    _keyLegends[kIndex] = lbl;
                }
                rowsStack.Children.Add(rowGrid);
            }

            switchPlate.Child = rowsStack;
            caseBezel.Child = switchPlate;
            stack.Children.Add(caseBezel);
            stageCard.Child = stack;
            return stageCard;
        }

        private Button MakeQuickGroupBtn(string label, string[] keyIds)
        {
            var btn = new Button
            {
                Style = (Style)Resources["StudioButton"],
                Content = label,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(4, 0, 0, 0),
                FontSize = 10.5
            };
            btn.Click += (s, e) =>
            {
                if (_activeTab != "paint") SwitchTab("paint");
                _fxEngine.CurrentMode = "per_key";
                foreach (string id in keyIds)
                {
                    int idx;
                    if (K617HardwareDriver.KeyIdToIndex.TryGetValue(id, out idx))
                    {
                        _fxEngine.PaintedR[idx] = _brushColor.R;
                        _fxEngine.PaintedG[idx] = _brushColor.G;
                        _fxEngine.PaintedB[idx] = _brushColor.B;
                        UpdateSingleKeyBrush(idx, _brushColor.R, _brushColor.G, _brushColor.B);
                    }
                }
                _fxEngine.MarkDirty();
                showToast(label + " grubu boyandı");
            };
            return btn;
        }

        private void PaintSingleKey(int kIndex)
        {
            if (_activeTab != "paint") SwitchTab("paint");
            _fxEngine.CurrentMode = "per_key";
            _fxEngine.PaintedR[kIndex] = _brushColor.R;
            _fxEngine.PaintedG[kIndex] = _brushColor.G;
            _fxEngine.PaintedB[kIndex] = _brushColor.B;
            _fxEngine.MarkDirty();
            UpdateSingleKeyBrush(kIndex, _brushColor.R, _brushColor.G, _brushColor.B);
        }

        private void UpdateSingleKeyBrush(int i, byte r, byte g, byte b)
        {
            int packed = (r << 16) | (g << 8) | b;
            if (_lastKeyPackedRgb[i] == packed) return;
            _lastKeyPackedRgb[i] = packed;

            if (packed == 0)
            {
                _keyRimBrushes[i].Color = Color.FromRgb(32, 36, 44);
                _keyLedBrushes[i].Color = Color.FromRgb(25, 28, 35);
                _keyLegendBrushes[i].Color = Color.FromRgb(90, 98, 112);
            }
            else
            {
                _keyRimBrushes[i].Color = Color.FromArgb(180, r, g, b);
                _keyLedBrushes[i].Color = Color.FromRgb(r, g, b);
                byte lr = (byte)Math.Min(255, r + 75);
                byte lg = (byte)Math.Min(255, g + 75);
                byte lb = (byte)Math.Min(255, b + 75);
                _keyLegendBrushes[i].Color = Color.FromRgb(lr, lg, lb);
            }
        }

        private void UpdateKeyboardPreview()
        {
            for (int i = 0; i < 61; i++)
            {
                UpdateSingleKeyBrush(i, _fxEngine.KeyR[i], _fxEngine.KeyG[i], _fxEngine.KeyB[i]);
            }
        }

        // =====================================================================
        // TAB 1: LIGHTING EFFECTS + RIGHT DOCKED PARAMETER INSPECTOR
        // =====================================================================
        private UIElement BuildEffectsTab()
        {
            var splitGrid = new Grid();
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) });

            // Left: Filter Bar + Scrollable Effects Grid
            var leftGrid = new Grid();
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var toolbar = new Border
            {
                Background = BgSurface,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(12, 8, 12, 8)
            };
            var filterDock = new DockPanel();
            var chipsPanel = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };

            var cats = new[]
            {
                new { Key = "all", Label = "Tümü (23)" },
                new { Key = "spectrum", Label = "Spektrum" },
                new { Key = "reactive", Label = "Tepkisel" },
                new { Key = "audio", Label = "Ses & Müzik" },
                new { Key = "thematic", Label = "Atmosfer" }
            };

            var chipBtns = new List<Button>();
            foreach (var c in cats)
            {
                string catKey = c.Key;
                var cBtn = MakeActionBtn(c.Label, catKey == "all", () => { });
                cBtn.Margin = new Thickness(0, 0, 5, 0);
                cBtn.Padding = new Thickness(10, 5, 10, 5);
                chipBtns.Add(cBtn);
                cBtn.Click += (s, e) =>
                {
                    _categoryFilter = catKey;
                    foreach (var b in chipBtns)
                    {
                        b.Background = BgElevated;
                        b.BorderBrush = BorderSubtle;
                    }
                    cBtn.Background = BrandAccent;
                    cBtn.BorderBrush = BrandAccent;
                    FilterEffectsGrid();
                };
                chipsPanel.Children.Add(cBtn);
            }
            filterDock.Children.Add(chipsPanel);

            var searchBox = MakeInputBox("", 190);
            searchBox.HorizontalAlignment = HorizontalAlignment.Right;
            searchBox.ToolTip = "Efekt ara...";
            searchBox.TextChanged += (s, e) =>
            {
                _searchQuery = (searchBox.Text ?? "").Trim().ToLowerInvariant();
                FilterEffectsGrid();
            };
            filterDock.Children.Add(searchBox);
            toolbar.Child = filterDock;
            Grid.SetRow(toolbar, 0);
            leftGrid.Children.Add(toolbar);

            var cardsScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _effectsCardsGrid = new WrapPanel();

            var modeDefs = new[]
            {
                new { Id = "rainbow_wave", Cat = "spectrum", Tag = "Spektrum", Name = "Gökkuşağı Dalgası", Desc = "Yöne göre kesintisiz spektrum akışı" },
                new { Id = "rainbow_spiral", Cat = "spectrum", Tag = "Spektrum", Name = "Spiral Çark", Desc = "Merkezden dışa dönen radyal döngü" },
                new { Id = "color_cycle", Cat = "spectrum", Tag = "Spektrum", Name = "Senkron Renk Çarkı", Desc = "Tüm tuşlarda eşzamanlı renk geçişi" },
                new { Id = "gradient_sweep", Cat = "spectrum", Tag = "Spektrum", Name = "Üç Renk Süpürme", Desc = "Seçili renk kanalları arasında geçiş" },
                new { Id = "breathing", Cat = "spectrum", Tag = "Spektrum", Name = "Nefes Alma (Pulse)", Desc = "İki renk tonu arasında yumuşak nabız" },
                new { Id = "static", Cat = "spectrum", Tag = "Eko Mod", Name = "Sabit Tek Renk", Desc = "Sıfır arka plan trafiği, sabit ışık" },

                new { Id = "reactive_ripple", Cat = "reactive", Tag = "Tepkisel", Name = "Tepkisel Dalga", Desc = "Basılan tuştan yayılan ışık halkası" },
                new { Id = "reactive_single", Cat = "reactive", Tag = "Tepkisel", Name = "Bas-Sön (Fade)", Desc = "Basılan tuş parlar ve iz bırakarak söner" },
                new { Id = "reactive_line", Cat = "reactive", Tag = "Tepkisel", Name = "Crosshair Eksen", Desc = "Basılan tuşun satır ve sütununu yakar" },
                new { Id = "reactive_explosion", Cat = "reactive", Tag = "Tepkisel", Name = "Kıvılcım Saçılması", Desc = "Yazım hızına duyarlı kıvılcım dağılımı" },

                new { Id = "audio_bars", Cat = "audio", Tag = "Ses DSP", Name = "14-Bant Ekolayzır", Desc = "Sistem sesine duyarlı frekans sütunları" },
                new { Id = "audio_pulse", Cat = "audio", Tag = "Ses DSP", Name = "Bas & Ritim Vuruşu", Desc = "Bas vuruşlarına senkronize parlama" },
                new { Id = "audio_wave", Cat = "audio", Tag = "Ses DSP", Name = "Radyal Ses Dalgası", Desc = "Ses şiddetine göre merkezden yayılan halka" },

                new { Id = "matrix", Cat = "thematic", Tag = "Atmosfer", Name = "Dijital Yağmur", Desc = "Dikey süzülen yeşil veri akışı" },
                new { Id = "cyberpunk_glitch", Cat = "thematic", Tag = "Atmosfer", Name = "Frekans Glitch", Desc = "Yüksek kontrastlı dinamik sinyal flaşları" },
                new { Id = "fire", Cat = "thematic", Tag = "Atmosfer", Name = "Termal Kor & Alev", Desc = "Alt satırdan yükselen sıcak alev modeli" },
                new { Id = "police_lights", Cat = "thematic", Tag = "Atmosfer", Name = "Strobe Flaşör", Desc = "Sol ve sağ yarı arasında çift fazlı çakar" },
                new { Id = "aurora", Cat = "thematic", Tag = "Atmosfer", Name = "Kuzey Işıkları", Desc = "Yumuşak zümrüt ve turkuaz geçişler" },
                new { Id = "ocean_tide", Cat = "thematic", Tag = "Atmosfer", Name = "Derin Gelgit", Desc = "Lacivert ve camgöbeği dalga katmanları" },
                new { Id = "starlight", Cat = "thematic", Tag = "Atmosfer", Name = "Yıldız Alanı", Desc = "Rastgele tuşlarda yumuşak parıldama" },
                new { Id = "heartbeat", Cat = "thematic", Tag = "Atmosfer", Name = "Kardiyak Nabız", Desc = "Yatay eksende ilerleyen EKG darbesi" },
                new { Id = "radar", Cat = "thematic", Tag = "Atmosfer", Name = "Polar Radar", Desc = "360 derece dönen tarama huzmesi" },
                new { Id = "lava_lamp", Cat = "thematic", Tag = "Atmosfer", Name = "Metaball Plazma", Desc = "Birleşip ayrılan yoğunluk alanları" }
            };

            foreach (var m in modeDefs)
            {
                string modeId = m.Id;
                string modeTitle = m.Name;
                bool isCurrent = (modeId == _fxEngine.CurrentMode);
                var card = new Border
                {
                    Width = 212,
                    Height = 82,
                    Margin = new Thickness(0, 0, 8, 8),
                    Padding = new Thickness(12, 10, 12, 10),
                    CornerRadius = new CornerRadius(7),
                    Background = isCurrent ? BgElevated : BgSurface,
                    BorderBrush = isCurrent ? BrandAccent : BorderSubtle,
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    Tag = m.Cat + "|" + m.Name.ToLowerInvariant() + " " + m.Desc.ToLowerInvariant()
                };

                var cStack = new StackPanel();
                var topRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                topRow.Children.Add(MakeText(m.Name, 12.5, TextPrimary, FontWeights.Bold));
                var catBadge = MakeText(m.Tag, 9.5, TextMuted, FontWeights.SemiBold);
                catBadge.HorizontalAlignment = HorizontalAlignment.Right;
                topRow.Children.Add(catBadge);
                cStack.Children.Add(topRow);

                var descTxt = MakeText(m.Desc, 11, TextSecondary);
                descTxt.TextWrapping = TextWrapping.Wrap;
                cStack.Children.Add(descTxt);
                card.Child = cStack;

                card.MouseLeftButtonDown += (s, e) =>
                {
                    foreach (var cb in _effectCardBorders)
                    {
                        cb.Background = BgSurface;
                        cb.BorderBrush = BorderSubtle;
                    }
                    card.Background = BgElevated;
                    card.BorderBrush = BrandAccent;
                    _fxEngine.CurrentMode = modeId;
                    _fxEngine.MarkDirty();
                    if (_txtActiveModeBadge != null) _txtActiveModeBadge.Text = modeTitle;
                    SaveSettingsToDisk();
                };

                _effectCardBorders.Add(card);
                _effectsCardsGrid.Children.Add(card);
            }
            cardsScroll.Content = _effectsCardsGrid;
            Grid.SetRow(cardsScroll, 2);
            leftGrid.Children.Add(cardsScroll);

            Grid.SetColumn(leftGrid, 0);
            splitGrid.Children.Add(leftGrid);

            // Right: Docked Parameter Inspector
            var inspector = new Border
            {
                Background = BgSurface,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(16, 14, 16, 14)
            };
            var inspStack = new StackPanel();
            inspStack.Children.Add(MakeText("EFEKT PARAMETRELERİ", 10.5, TextMuted, FontWeights.Bold));

            inspStack.Children.Add(MakeSliderControl("Parlaklık", 5, 100, _fxEngine.Brightness * 100, "%", val =>
            {
                _fxEngine.Brightness = val / 100.0;
                _fxEngine.MarkDirty();
            }));

            inspStack.Children.Add(MakeSliderControl("Efekt Hızı", 20, 300, _fxEngine.Speed * 100, "%", val =>
            {
                _fxEngine.Speed = val / 100.0;
            }));

            inspStack.Children.Add(MakeSliderControl("Ses Hassasiyeti", 5, 40, _fxEngine.AudioSensitivity * 10, "%", val =>
            {
                _fxEngine.AudioSensitivity = val / 10.0;
            }));

            var dirLabel = MakeText("Hareket Yönü", 11.5, TextSecondary, FontWeights.SemiBold);
            dirLabel.Margin = new Thickness(0, 10, 0, 6);
            inspStack.Children.Add(dirLabel);

            var dirGrid = new UniformGrid { Rows = 1, Columns = 4 };
            var dirBtns = new List<Button>();
            foreach (var d in new[] { new { K = "left", L = "Sol" }, new { K = "right", L = "Sağ" }, new { K = "up", L = "Üst" }, new { K = "down", L = "Alt" } })
            {
                string dk = d.K;
                var b = MakeActionBtn(d.L, _fxEngine.Direction == dk, () => { });
                b.Margin = new Thickness(0, 0, 4, 0);
                b.Padding = new Thickness(0, 6, 0, 6);
                dirBtns.Add(b);
                b.Click += (s, e) =>
                {
                    _fxEngine.Direction = dk;
                    foreach (var db in dirBtns) { db.Background = BgElevated; db.BorderBrush = BorderSubtle; }
                    b.Background = BrandAccent;
                    b.BorderBrush = BrandAccent;
                };
                dirGrid.Children.Add(b);
            }
            inspStack.Children.Add(dirGrid);

            var colHeader = MakeText("Birincil Efekt Rengi", 11.5, TextSecondary, FontWeights.SemiBold);
            colHeader.Margin = new Thickness(0, 16, 0, 8);
            inspStack.Children.Add(colHeader);

            var colWrap = new WrapPanel();
            var presetColors = new[]
            {
                Color.FromRgb(229, 57, 53), Color.FromRgb(245, 158, 11), Color.FromRgb(16, 185, 129),
                Color.FromRgb(0, 210, 255), Color.FromRgb(59, 130, 246), Color.FromRgb(139, 92, 246),
                Color.FromRgb(236, 72, 153), Color.FromRgb(255, 255, 255)
            };
            foreach (var pc in presetColors)
            {
                Color cCopy = pc;
                var sw = new Border
                {
                    Width = 24, Height = 24, CornerRadius = new CornerRadius(5),
                    Background = new SolidColorBrush(cCopy),
                    BorderBrush = BorderFocus, BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 6, 6), Cursor = Cursors.Hand
                };
                sw.MouseLeftButtonDown += (s, e) =>
                {
                    _fxEngine.PrimaryR = cCopy.R;
                    _fxEngine.PrimaryG = cCopy.G;
                    _fxEngine.PrimaryB = cCopy.B;
                    _fxEngine.MarkDirty();
                };
                colWrap.Children.Add(sw);
            }
            inspStack.Children.Add(colWrap);

            inspector.Child = inspStack;
            Grid.SetColumn(inspector, 2);
            splitGrid.Children.Add(inspector);

            return splitGrid;
        }

        private void FilterEffectsGrid()
        {
            foreach (var card in _effectCardBorders)
            {
                string tag = Convert.ToString(card.Tag ?? "");
                string[] parts = tag.Split('|');
                string cat = parts.Length > 0 ? parts[0] : "";
                string text = parts.Length > 1 ? parts[1] : "";

                bool catMatch = (_categoryFilter == "all" || cat == _categoryFilter);
                bool searchMatch = string.IsNullOrEmpty(_searchQuery) || text.Contains(_searchQuery);
                card.Visibility = (catMatch && searchMatch) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private UIElement MakeSliderControl(string label, double min, double max, double initial, string suffix, Action<double> onChange)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            head.Children.Add(MakeText(label, 11.5, TextSecondary, FontWeights.SemiBold));
            var valTxt = MakeText(((int)initial) + suffix, 11.5, TextPrimary, FontWeights.Bold);
            valTxt.FontFamily = FontMono;
            valTxt.HorizontalAlignment = HorizontalAlignment.Right;
            head.Children.Add(valTxt);
            sp.Children.Add(head);

            var slider = new Slider
            {
                Minimum = min, Maximum = max, Value = initial
            };
            slider.ValueChanged += (s, e) =>
            {
                valTxt.Text = ((int)slider.Value) + suffix;
                onChange(slider.Value);
            };
            sp.Children.Add(slider);
            return sp;
        }

        // =====================================================================
        // TAB 2: PER-KEY PAINT & GAME PROFILES
        // =====================================================================
        private UIElement BuildPaintTab()
        {
            var stack = new StackPanel();

            var bar = new Border
            {
                Background = BgSurface,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 14)
            };

            var dock = new DockPanel();
            var swatchWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            swatchWrap.Children.Add(MakeText("Fırça Rengi:  ", 12, TextSecondary, FontWeights.SemiBold));

            var colors = new[]
            {
                Color.FromRgb(229, 57, 53), Color.FromRgb(245, 158, 11), Color.FromRgb(16, 185, 129),
                Color.FromRgb(0, 210, 255), Color.FromRgb(59, 130, 246), Color.FromRgb(139, 92, 246),
                Color.FromRgb(236, 72, 153), Color.FromRgb(255, 255, 255), Color.FromRgb(0, 0, 0)
            };

            foreach (var c in colors)
            {
                Color cCopy = c;
                bool isOff = (cCopy.R == 0 && cCopy.G == 0 && cCopy.B == 0);
                var circle = new Border
                {
                    Width = 26, Height = 26, CornerRadius = new CornerRadius(5),
                    Background = new SolidColorBrush(cCopy),
                    BorderBrush = isOff ? BorderFocus : Brushes.White,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 7, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = isOff ? "LED Kapat (Silgi)" : "Fırça Rengi"
                };
                circle.MouseLeftButtonDown += (s, e) => { _brushColor = cCopy; };
                swatchWrap.Children.Add(circle);
            }
            dock.Children.Add(swatchWrap);

            var btnGroup = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            _txtProfileSaveName = MakeInputBox("Ozel_Profil", 130);
            _txtProfileSaveName.Margin = new Thickness(0, 0, 6, 0);
            btnGroup.Children.Add(_txtProfileSaveName);

            btnGroup.Children.Add(MakeActionBtn("Kaydet", false, SavePaintedProfileToDisk));
            btnGroup.Children.Add(MakeActionBtn("Tümünü Doldur", true, () =>
            {
                for (int i = 0; i < 61; i++)
                {
                    _fxEngine.PaintedR[i] = _brushColor.R;
                    _fxEngine.PaintedG[i] = _brushColor.G;
                    _fxEngine.PaintedB[i] = _brushColor.B;
                }
                _fxEngine.CurrentMode = "per_key";
                _fxEngine.MarkDirty();
                showToast("Tüm tuşlar dolduruldu");
            }));
            btnGroup.Children.Add(MakeActionBtn("Temizle", false, () =>
            {
                for (int i = 0; i < 61; i++)
                {
                    _fxEngine.PaintedR[i] = 0;
                    _fxEngine.PaintedG[i] = 0;
                    _fxEngine.PaintedB[i] = 0;
                }
                _fxEngine.CurrentMode = "per_key";
                _fxEngine.MarkDirty();
                showToast("Tüm tuş ışıkları kapatıldı");
            }));
            dock.Children.Add(btnGroup);
            bar.Child = dock;
            stack.Children.Add(bar);

            stack.Children.Add(MakeText("HAZIR OYUN VE TEMA PROFİLLERİ", 10.5, TextMuted, FontWeights.Bold));

            var presetsWrap = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            var presets = new[]
            {
                new { File = "valorant_fps", Title = "Valorant Taktik FPS", Sub = "WASD, Yetenekler (C/Q/E/X) ve Silahlar", Accent = "#FF4655" },
                new { File = "cs2_competitive", Title = "Counter-Strike 2", Sub = "Kehribar WASD ve Bomba/Teçhizat Yuvaları", Accent = "#F59E0B" },
                new { File = "lol_moba", Title = "League of Legends MOBA", Sub = "QWER Yetenekler, D/F Büyüler ve Eşyalar", Accent = "#0EA5E9" },
                new { File = "turkish_flag", Title = "Türk Bayrağı (Ay Yıldız)", Sub = "Al Kırmızı Zemin Üzerine Beyaz Hilal", Accent = "#E30A17" },
                new { File = "cyberpunk_neon", Title = "Night City 2077", Sub = "Yüksek Kontrast Sarı, Siyan ve Macenta", Accent = "#FCEE09" },
                new { File = "synthwave_80s", Title = "Outrun Synthwave", Sub = "Kademeli Viyole, Fuşya ve Turkuaz", Accent = "#EC4899" },
                new { File = "sunset_vibes", Title = "Horizon Termal", Sub = "Sıcak Kızıl, Bakır ve Altın Geçiş", Accent = "#EA580C" },
                new { File = "minecraft_adventure", Title = "Sandbox Craft", Sub = "Biyom Yeşili ve Elmas Mavisi Düzeni", Accent = "#22C55E" }
            };

            foreach (var p in presets)
            {
                string fName = p.File;
                string pTitle = p.Title;
                var tile = new Border
                {
                    Width = 235, Height = 68, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(14, 10, 14, 10),
                    Background = BgSurface, BorderBrush = BorderSubtle, BorderThickness = new Thickness(3, 1, 1, 1),
                    CornerRadius = new CornerRadius(7), Cursor = Cursors.Hand
                };
                tile.BorderBrush = BrushHex(p.Accent);
                var sp = new StackPanel();
                sp.Children.Add(MakeText(p.Title, 12.5, TextPrimary, FontWeights.Bold));
                var sub = MakeText(p.Sub, 11, TextSecondary);
                sub.Margin = new Thickness(0, 3, 0, 0);
                sp.Children.Add(sub);
                tile.Child = sp;
                tile.MouseLeftButtonDown += (s, e) =>
                {
                    LoadProfileFromFile(fName);
                    if (_txtActiveModeBadge != null) _txtActiveModeBadge.Text = "Profil: " + pTitle;
                    showToast(pTitle + " profili yüklendi");
                };
                presetsWrap.Children.Add(tile);
            }
            stack.Children.Add(presetsWrap);

            return stack;
        }

        private void SavePaintedProfileToDisk()
        {
            try
            {
                string name = (_txtProfileSaveName.Text ?? "Ozel_Profil").Trim();
                if (string.IsNullOrEmpty(name)) name = "Ozel_Profil";
                var cleanChars = name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray();
                string fileName = (cleanChars.Length > 0 ? new string(cleanChars) : "Ozel_Profil") + ".json";
                string pDir = Path.Combine(_baseDir, "profiles");
                Directory.CreateDirectory(pDir);

                var colorsDict = new Dictionary<string, int[]>();
                var keys = K617HardwareDriver.Keys;
                for (int i = 0; i < keys.Length; i++)
                {
                    colorsDict[keys[i].Id] = new int[] { _fxEngine.PaintedR[i], _fxEngine.PaintedG[i], _fxEngine.PaintedB[i] };
                }
                var payload = new Dictionary<string, object>
                {
                    { "name", name },
                    { "colors", colorsDict }
                };
                var ser = new JavaScriptSerializer();
                File.WriteAllText(Path.Combine(pDir, fileName), ser.Serialize(payload), Encoding.UTF8);
                showToast("Profil kaydedildi: " + name);
            }
            catch { }
        }

        private void LoadProfileFromFile(string profileName)
        {
            string path = Path.Combine(_baseDir, "profiles", profileName + ".json");
            if (!File.Exists(path)) return;
            try
            {
                var ser = new JavaScriptSerializer();
                var dict = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
                if (dict != null && dict.ContainsKey("colors"))
                {
                    var colorsObj = dict["colors"] as Dictionary<string, object>;
                    if (colorsObj != null)
                    {
                        foreach (var kv in colorsObj)
                        {
                            int kIdx;
                            if (K617HardwareDriver.KeyIdToIndex.TryGetValue(kv.Key, out kIdx))
                            {
                                var arr = kv.Value as System.Collections.ArrayList;
                                if (arr != null && arr.Count >= 3)
                                {
                                    _fxEngine.PaintedR[kIdx] = Convert.ToByte(arr[0]);
                                    _fxEngine.PaintedG[kIdx] = Convert.ToByte(arr[1]);
                                    _fxEngine.PaintedB[kIdx] = Convert.ToByte(arr[2]);
                                }
                            }
                        }
                        _fxEngine.CurrentMode = "per_key";
                        _fxEngine.MarkDirty();
                    }
                }
            }
            catch { }
        }

        // =====================================================================
        // TAB 3: MACRO STUDIO (WITH INLINE COMPOSER)
        // =====================================================================
        private UIElement BuildMacrosTab()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left: Saved Macros
            var leftCard = new Border
            {
                Background = BgSurface, BorderBrush = BorderSubtle, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7), Padding = new Thickness(16)
            };
            var leftDock = new DockPanel();
            var leftHead = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            leftHead.Children.Add(MakeText("Kayıtlı Makrolar", 13, TextPrimary, FontWeights.Bold));

            var chkGlobal = new CheckBox
            {
                Style = (Style)Resources["ModernSwitch"],
                Content = "Aktif",
                IsChecked = _macroEngine.GlobalEnabled,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            chkGlobal.Checked += (s, e) => { _macroEngine.GlobalEnabled = true; };
            chkGlobal.Unchecked += (s, e) => { _macroEngine.GlobalEnabled = false; };
            leftHead.Children.Add(chkGlobal);
            DockPanel.SetDock(leftHead, Dock.Top);
            leftDock.Children.Add(leftHead);

            var listScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _macroListPanel = new StackPanel();
            listScroll.Content = _macroListPanel;
            leftDock.Children.Add(listScroll);
            leftCard.Child = leftDock;
            Grid.SetColumn(leftCard, 0);
            grid.Children.Add(leftCard);

            // Right: Macro Editor
            var rightCard = new Border
            {
                Background = BgSurface, BorderBrush = BorderSubtle, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7), Padding = new Thickness(16)
            };
            var rStack = new StackPanel();

            var rHead = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            rHead.Children.Add(MakeText("Makro Düzenleyici", 13, TextPrimary, FontWeights.Bold));
            var rHeadBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            _btnRecordMacro = MakeActionBtn("Canlı Kayıt Başlat", false, () =>
            {
                if (!_macroEngine.IsRecording)
                {
                    _macroEngine.StartRecording();
                    _btnRecordMacro.Content = "Kaydı Durdur";
                    _btnRecordMacro.Background = BrandAccent;
                    showToast("Canlı tuş kaydı başladı");
                }
                else
                {
                    var rec = _macroEngine.StopRecording();
                    _btnRecordMacro.Content = "Canlı Kayıt Başlat";
                    _btnRecordMacro.Background = BgElevated;
                    if (rec.Count > 0)
                    {
                        _editingSteps = rec;
                        RefreshMacroStepsUI();
                        showToast(rec.Count + " adım kaydedildi");
                    }
                }
            });
            rHeadBtns.Children.Add(_btnRecordMacro);
            rHeadBtns.Children.Add(MakeActionBtn("Temizle", false, () =>
            {
                _editingSteps.Clear();
                RefreshMacroStepsUI();
            }));
            rHead.Children.Add(rHeadBtns);
            rStack.Children.Add(rHead);

            var metaWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };

            var nameSp = new StackPanel { Width = 200, Margin = new Thickness(0, 0, 12, 8) };
            nameSp.Children.Add(MakeText("Makro Adı", 11, TextSecondary, FontWeights.SemiBold));
            _txtMacroName = MakeInputBox("CS2 Hızlı Silah Değişimi", 200);
            _txtMacroName.Margin = new Thickness(0, 4, 0, 0);
            nameSp.Children.Add(_txtMacroName);
            metaWrap.Children.Add(nameSp);

            var trigSp = new StackPanel { Width = 175, Margin = new Thickness(0, 0, 12, 8) };
            trigSp.Children.Add(MakeText("Tetikleyici Tuş", 11, TextSecondary, FontWeights.SemiBold));
            var trigRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            _txtMacroTrigger = MakeInputBox("F6", 75);
            _txtMacroTrigger.FontFamily = FontMono;
            _txtMacroTrigger.FontWeight = FontWeights.Bold;
            _txtMacroTrigger.IsReadOnly = true;
            trigRow.Children.Add(_txtMacroTrigger);
            _btnBindTrigger = MakeActionBtn("Tuş Ata", false, () =>
            {
                _isBindingMacroTrigger = true;
                _btnBindTrigger.Content = "Basın...";
            });
            trigRow.Children.Add(_btnBindTrigger);
            trigSp.Children.Add(trigRow);
            metaWrap.Children.Add(trigSp);

            var repSp = new StackPanel { Width = 200, Margin = new Thickness(0, 0, 12, 8) };
            repSp.Children.Add(MakeText("Çalışma Modu", 11, TextSecondary, FontWeights.SemiBold));
            _cmbMacroRepeat = new ComboBox { Margin = new Thickness(0, 4, 0, 0) };
            _cmbMacroRepeat.Items.Add(new ComboBoxItem { Content = "Tek Sefer (Once)", Tag = "once", IsSelected = true });
            _cmbMacroRepeat.Items.Add(new ComboBoxItem { Content = "Belirli Sayıda Tekrar", Tag = "repeat_n" });
            _cmbMacroRepeat.Items.Add(new ComboBoxItem { Content = "Aç / Kapat Döngü", Tag = "toggle_loop" });
            repSp.Children.Add(_cmbMacroRepeat);
            metaWrap.Children.Add(repSp);

            var cntSp = new StackPanel { Width = 80, Margin = new Thickness(0, 0, 0, 8) };
            cntSp.Children.Add(MakeText("Tekrar", 11, TextSecondary, FontWeights.SemiBold));
            _txtMacroRepeatCount = MakeInputBox("1", 80);
            _txtMacroRepeatCount.Margin = new Thickness(0, 4, 0, 0);
            cntSp.Children.Add(_txtMacroRepeatCount);
            metaWrap.Children.Add(cntSp);

            rStack.Children.Add(metaWrap);

            // Inline Step Composer Bar
            var composerBorder = new Border
            {
                Background = BgInput, BorderBrush = BorderSubtle, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 0, 0, 10)
            };
            var compWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };

            _txtStepKeyInput = MakeInputBox("q", 55);
            compWrap.Children.Add(_txtStepKeyInput);
            compWrap.Children.Add(MakeActionBtn("+ Tuş", false, () =>
            {
                string k = (_txtStepKeyInput.Text ?? "q").Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(k)) { _editingSteps.Add(new MacroStepItem { type = "key_press", value = k }); RefreshMacroStepsUI(); }
            }));

            _txtStepDelayInput = MakeInputBox("40", 55);
            _txtStepDelayInput.Margin = new Thickness(10, 0, 0, 0);
            compWrap.Children.Add(_txtStepDelayInput);
            compWrap.Children.Add(MakeActionBtn("+ ms Bekle", false, () =>
            {
                int ms = 40;
                int.TryParse(_txtStepDelayInput.Text, out ms);
                _editingSteps.Add(new MacroStepItem { type = "delay", value = Math.Max(1, ms) });
                RefreshMacroStepsUI();
            }));

            _cmbStepMouseInput = new ComboBox { Width = 92, Margin = new Thickness(10, 0, 0, 0) };
            _cmbStepMouseInput.Items.Add(new ComboBoxItem { Content = "Sol Tık", Tag = "left", IsSelected = true });
            _cmbStepMouseInput.Items.Add(new ComboBoxItem { Content = "Sağ Tık", Tag = "right" });
            _cmbStepMouseInput.Items.Add(new ComboBoxItem { Content = "Orta Tık", Tag = "middle" });
            compWrap.Children.Add(_cmbStepMouseInput);
            compWrap.Children.Add(MakeActionBtn("+ Fare", false, () =>
            {
                var item = _cmbStepMouseInput.SelectedItem as ComboBoxItem;
                string mb = (item != null && item.Tag != null) ? item.Tag.ToString() : "left";
                _editingSteps.Add(new MacroStepItem { type = "mouse_click", value = mb });
                RefreshMacroStepsUI();
            }));

            _txtStepTextInput = MakeInputBox("GG", 75);
            _txtStepTextInput.Margin = new Thickness(10, 0, 0, 0);
            compWrap.Children.Add(_txtStepTextInput);
            compWrap.Children.Add(MakeActionBtn("+ Metin", false, () =>
            {
                if (!string.IsNullOrEmpty(_txtStepTextInput.Text))
                {
                    _editingSteps.Add(new MacroStepItem { type = "text", value = _txtStepTextInput.Text });
                    RefreshMacroStepsUI();
                }
            }));

            composerBorder.Child = compWrap;
            rStack.Children.Add(composerBorder);

            var stepsBorder = new Border
            {
                Background = BgInput, BorderBrush = BorderSubtle, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(8), Height = 135, Margin = new Thickness(0, 0, 0, 10)
            };
            var stepsScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _macroStepsPanel = new StackPanel();
            stepsScroll.Content = _macroStepsPanel;
            stepsBorder.Child = stepsScroll;
            rStack.Children.Add(stepsBorder);

            var btnSaveMacro = MakeActionBtn("Makroyu Kaydet ve Etkinleştir", true, SaveCurrentEditedMacro);
            btnSaveMacro.Margin = new Thickness(0);
            btnSaveMacro.Padding = new Thickness(16, 9, 16, 9);
            rStack.Children.Add(btnSaveMacro);

            rightCard.Child = rStack;
            Grid.SetColumn(rightCard, 2);
            grid.Children.Add(rightCard);

            _editingSteps = new List<MacroStepItem>
            {
                new MacroStepItem { type = "key_press", value = "3" },
                new MacroStepItem { type = "delay", value = 45 },
                new MacroStepItem { type = "key_press", value = "1" }
            };
            RefreshMacroListUI();
            RefreshMacroStepsUI();

            return grid;
        }

        private void RefreshMacroListUI()
        {
            if (_macroListPanel == null) return;
            _macroListPanel.Children.Clear();
            foreach (var m in _macroEngine.Macros)
            {
                MacroDefinition mRef = m;
                var row = new Border
                {
                    Background = BgElevated, BorderBrush = BorderSubtle, BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 6)
                };
                var dock = new DockPanel();
                var left = new StackPanel();
                left.Children.Add(MakeText("[" + (mRef.trigger ?? "F6").ToUpperInvariant() + "]  " + mRef.name, 12, TextPrimary, FontWeights.Bold));
                left.Children.Add(MakeText((mRef.steps != null ? mRef.steps.Count : 0) + " adım • " + mRef.repeat_mode, 10.5, TextSecondary));
                dock.Children.Add(left);

                var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                var btnEdit = MakeActionBtn("Düzenle", false, () =>
                {
                    _editingMacroId = mRef.id;
                    _txtMacroName.Text = mRef.name;
                    _txtMacroTrigger.Text = (mRef.trigger ?? "f6").ToUpperInvariant();
                    _editingSteps = mRef.steps != null ? new List<MacroStepItem>(mRef.steps) : new List<MacroStepItem>();
                    RefreshMacroStepsUI();
                });
                btnEdit.Padding = new Thickness(8, 4, 8, 4);
                right.Children.Add(btnEdit);

                var btnDel = MakeActionBtn("Sil", false, () =>
                {
                    _macroEngine.Macros.Remove(mRef);
                    _macroEngine.Save();
                    RefreshMacroListUI();
                    showToast("Makro silindi");
                });
                btnDel.Padding = new Thickness(8, 4, 8, 4);
                right.Children.Add(btnDel);

                dock.Children.Add(right);
                row.Child = dock;
                _macroListPanel.Children.Add(row);
            }
        }

        private void RefreshMacroStepsUI()
        {
            if (_macroStepsPanel == null) return;
            _macroStepsPanel.Children.Clear();
            for (int i = 0; i < _editingSteps.Count; i++)
            {
                int idx = i;
                var st = _editingSteps[i];
                var r = new Border
                {
                    Background = BgSurface, BorderBrush = BorderSubtle, BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 4)
                };
                var dock = new DockPanel();
                var lbl = MakeText(string.Format("{0:00}   [{1}]   {2}", idx + 1, st.type.ToUpperInvariant(), st.value), 11, TextPrimary, FontWeights.SemiBold);
                lbl.FontFamily = FontMono;
                dock.Children.Add(lbl);

                var delBtn = MakeActionBtn("Sil", false, () =>
                {
                    _editingSteps.RemoveAt(idx);
                    RefreshMacroStepsUI();
                });
                delBtn.Padding = new Thickness(7, 2, 7, 2);
                delBtn.HorizontalAlignment = HorizontalAlignment.Right;
                dock.Children.Add(delBtn);

                r.Child = dock;
                _macroStepsPanel.Children.Add(r);
            }
        }

        private void SaveCurrentEditedMacro()
        {
            string rMode = "once";
            var selItem = _cmbMacroRepeat.SelectedItem as ComboBoxItem;
            if (selItem != null && selItem.Tag != null) rMode = selItem.Tag.ToString();
            int rCount = 1;
            int.TryParse(_txtMacroRepeatCount.Text, out rCount);

            var m = new MacroDefinition
            {
                id = string.IsNullOrEmpty(_editingMacroId) ? ("macro_" + Environment.TickCount) : _editingMacroId,
                name = string.IsNullOrWhiteSpace(_txtMacroName.Text) ? "Özel Makro" : _txtMacroName.Text,
                trigger = (_txtMacroTrigger.Text ?? "f6").Trim().ToLowerInvariant(),
                repeat_mode = rMode,
                repeat_count = Math.Max(1, rCount),
                enabled = true,
                steps = new List<MacroStepItem>(_editingSteps)
            };

            int existingIdx = _macroEngine.Macros.FindIndex(x => x.id == m.id);
            if (existingIdx >= 0) _macroEngine.Macros[existingIdx] = m;
            else _macroEngine.Macros.Add(m);

            _editingMacroId = null;
            _macroEngine.Save();
            RefreshMacroListUI();
            showToast("Makro kaydedildi ve etkinleştirildi: " + m.name);
        }

        // =====================================================================
        // TAB 4: HARDWARE FIRMWARE MODES (ONBOARD MCU)
        // =====================================================================
        private UIElement BuildFirmwareTab()
        {
            var stack = new StackPanel();
            stack.Children.Add(MakeText("KLAVYE DAHİLİ HAFIZA MODLARI (SINOWEALTH SH68F90A EEPROM)", 10.5, TextMuted, FontWeights.Bold));
            var sub = MakeText("Bu modlar doğrudan klavyenin kendi mikrodenetleyicisine yazılır; uygulama kapalıyken bile çalışmaya devam eder.", 11.5, TextSecondary);
            sub.Margin = new Thickness(0, 4, 0, 12);
            stack.Children.Add(sub);

            var wrap = new WrapPanel();
            var fwModes = new[]
            {
                new { Id = "fw-rainbow", Name = "Donanım Gökkuşağı", Desc = "Fabrika çıkışı dahili spektrum akışı" },
                new { Id = "fw-snake", Name = "Sürekli Tarama (Snake)", Desc = "Satırlar boyunca ilerleyen tekil iz" },
                new { Id = "fw-sine-wave", Name = "Harmonik Sinüs", Desc = "Donanımsal sinüs dalga simülasyonu" },
                new { Id = "fw-star-twinkle", Name = "Yıldız Parlaması", Desc = "Çip üzerinde çalışan rastgele pırıltı" },
                new { Id = "fw-rainbow-blossom", Name = "Merkezden Açılma", Desc = "Merkezden dışa açılan spektrum halkası" },
                new { Id = "fw-waterfall", Name = "Dikey Şelale", Desc = "Üstten alta doğru sürekli ışık akışı" },
                new { Id = "fw-wheel", Name = "Dönen Spektrum Çarkı", Desc = "Klavye ekseninde dönen renk çarkı" },
                new { Id = "fw-static", Name = "Kalıcı Sabit Renk", Desc = "Donanım hafızasında saklanan tek renk" }
            };

            foreach (var fm in fwModes)
            {
                string fwId = fm.Id;
                string fwTitle = fm.Name;
                var card = new Border
                {
                    Width = 235, Height = 76, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(14, 10, 14, 10),
                    Background = BgSurface, BorderBrush = BorderSubtle, BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(7), Cursor = Cursors.Hand
                };
                var sp = new StackPanel();
                sp.Children.Add(MakeText(fm.Name, 12.5, TextPrimary, FontWeights.Bold));
                var d = MakeText(fm.Desc, 11, TextSecondary);
                d.Margin = new Thickness(0, 4, 0, 0);
                sp.Children.Add(d);
                card.Child = sp;

                card.MouseLeftButtonDown += (s, e) =>
                {
                    _fxEngine.CurrentMode = "firmware";
                    _driver.SendFirmwareEffect(_baseDir, fwId, _fxEngine.PrimaryR, _fxEngine.PrimaryG, _fxEngine.PrimaryB, 3, 4);
                    if (_txtActiveModeBadge != null) _txtActiveModeBadge.Text = "Donanım: " + fwTitle;
                    showToast("Donanım hafızasına yazıldı: " + fwTitle);
                };
                wrap.Children.Add(card);
            }
            stack.Children.Add(wrap);
            return stack;
        }

        // =====================================================================
        // TAB 5: SETTINGS & PERFORMANCE
        // =====================================================================
        private UIElement BuildSettingsTab()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftCard = new Border
            {
                Background = BgSurface, BorderBrush = BorderSubtle, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7), Padding = new Thickness(18)
            };
            var lStack = new StackPanel();
            lStack.Children.Add(MakeText("PERFORMANS VE BELLEK MİMARİSİ", 10.5, TextMuted, FontWeights.Bold));

            _chkEcoMode = new CheckBox
            {
                Style = (Style)Resources["ModernSwitch"],
                Content = "Ultra Düşük RAM / Eko Modu (Otomatik Working Set Temizleyici)",
                IsChecked = _fxEngine.EcoMode,
                Margin = new Thickness(0, 14, 0, 12)
            };
            _chkEcoMode.Checked += (s, e) => { _fxEngine.EcoMode = true; NativeMethods.TrimMemory(); SaveSettingsToDisk(); };
            _chkEcoMode.Unchecked += (s, e) => { _fxEngine.EcoMode = false; SaveSettingsToDisk(); };
            lStack.Children.Add(_chkEcoMode);

            _chkLivePreview = new CheckBox
            {
                Style = (Style)Resources["ModernSwitch"],
                Content = "Canlı Klavye Arayüz Önizlemesi",
                IsChecked = _livePreviewEnabled,
                Margin = new Thickness(0, 0, 0, 16)
            };
            _chkLivePreview.Checked += (s, e) => { _livePreviewEnabled = true; SaveSettingsToDisk(); };
            _chkLivePreview.Unchecked += (s, e) => { _livePreviewEnabled = false; SaveSettingsToDisk(); };
            lStack.Children.Add(_chkLivePreview);

            var fpsRow = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
            fpsRow.Children.Add(MakeText("Sürücü Yenileme Hızı (Target FPS)", 12, TextSecondary, FontWeights.SemiBold));
            _cmbTargetFps = new ComboBox { Width = 210, HorizontalAlignment = HorizontalAlignment.Right };
            _cmbTargetFps.Items.Add(new ComboBoxItem { Content = "15 FPS (Maksimum Tasarruf)", Tag = 15 });
            _cmbTargetFps.Items.Add(new ComboBoxItem { Content = "25 FPS (Düşük CPU)", Tag = 25 });
            _cmbTargetFps.Items.Add(new ComboBoxItem { Content = "35 FPS (Dengeli Standart)", Tag = 35, IsSelected = true });
            _cmbTargetFps.Items.Add(new ComboBoxItem { Content = "60 FPS (Maksimum Akıcılık)", Tag = 60 });
            _cmbTargetFps.SelectionChanged += (s, e) =>
            {
                var item = _cmbTargetFps.SelectedItem as ComboBoxItem;
                if (item != null && item.Tag != null)
                {
                    _fxEngine.TargetFps = Convert.ToInt32(item.Tag);
                    SaveSettingsToDisk();
                }
            };
            fpsRow.Children.Add(_cmbTargetFps);
            lStack.Children.Add(fpsRow);

            var ramBox = new Border
            {
                Background = BgInput, BorderBrush = BorderSubtle, BorderThickness = new Thickness(3, 1, 1, 1),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(14, 12, 14, 12)
            };
            ramBox.BorderBrush = StatusOnline;
            var ramDock = new DockPanel();
            var ramInfo = new StackPanel();
            ramInfo.Children.Add(MakeText("ANLIK WORKING SET BELLEĞİ", 10, TextMuted, FontWeights.Bold));
            _txtSettingsRam = MakeText(NativeMethods.GetWorkingSetMB() + " MB", 18, StatusOnline, FontWeights.Bold);
            _txtSettingsRam.FontFamily = FontMono;
            ramInfo.Children.Add(_txtSettingsRam);
            ramDock.Children.Add(ramInfo);

            var btnTrim = MakeActionBtn("Şimdi Optimize Et", true, () =>
            {
                NativeMethods.TrimMemory();
                double r = NativeMethods.GetWorkingSetMB();
                _txtSettingsRam.Text = string.Format("{0:0.0} MB", r);
                if (_txtSidebarRam != null) _txtSidebarRam.Text = string.Format("{0:0.0} MB", r);
                showToast(string.Format("Bellek optimize edildi ({0:0.0} MB)", r));
            });
            btnTrim.HorizontalAlignment = HorizontalAlignment.Right;
            btnTrim.VerticalAlignment = VerticalAlignment.Center;
            ramDock.Children.Add(btnTrim);
            ramBox.Child = ramDock;
            lStack.Children.Add(ramBox);

            leftCard.Child = lStack;
            Grid.SetColumn(leftCard, 0);
            grid.Children.Add(leftCard);

            // Right Card: System & Power
            var rightCard = new Border
            {
                Background = BgSurface, BorderBrush = BorderSubtle, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7), Padding = new Thickness(18)
            };
            var rStack = new StackPanel();
            rStack.Children.Add(MakeText("SİSTEM VE GÜÇ YÖNETİMİ", 10.5, TextMuted, FontWeights.Bold));

            _chkAutostart = new CheckBox
            {
                Style = (Style)Resources["ModernSwitch"],
                Content = "Windows Başlangıcında Otomatik Çalıştır",
                IsChecked = IsAutostartRegistered(),
                Margin = new Thickness(0, 14, 0, 12)
            };
            _chkAutostart.Checked += (s, e) => SetAutostartRegistry(true);
            _chkAutostart.Unchecked += (s, e) => SetAutostartRegistry(false);
            rStack.Children.Add(_chkAutostart);

            _chkTray = new CheckBox
            {
                Style = (Style)Resources["ModernSwitch"],
                Content = "Pencere Kapatıldığında Sistem Tepsisine Küçült",
                IsChecked = _minimizeToTray,
                Margin = new Thickness(0, 0, 0, 16)
            };
            _chkTray.Checked += (s, e) => { _minimizeToTray = true; SaveSettingsToDisk(); };
            _chkTray.Unchecked += (s, e) => { _minimizeToTray = false; SaveSettingsToDisk(); };
            rStack.Children.Add(_chkTray);

            var idleRow = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
            idleRow.Children.Add(MakeText("Boşta (Idle) LED Uyku Zamanlayıcısı", 12, TextSecondary, FontWeights.SemiBold));
            _cmbIdleTimeout = new ComboBox { Width = 190, HorizontalAlignment = HorizontalAlignment.Right };
            _cmbIdleTimeout.Items.Add(new ComboBoxItem { Content = "Kapalı (Sürekli Açık)", Tag = 0 });
            _cmbIdleTimeout.Items.Add(new ComboBoxItem { Content = "5 Dakika Sonra", Tag = 5 });
            _cmbIdleTimeout.Items.Add(new ComboBoxItem { Content = "15 Dakika Sonra", Tag = 15, IsSelected = true });
            _cmbIdleTimeout.Items.Add(new ComboBoxItem { Content = "30 Dakika Sonra", Tag = 30 });
            _cmbIdleTimeout.SelectionChanged += (s, e) =>
            {
                var item = _cmbIdleTimeout.SelectedItem as ComboBoxItem;
                if (item != null && item.Tag != null)
                {
                    _fxEngine.IdleTimeoutMinutes = Convert.ToInt32(item.Tag);
                    SaveSettingsToDisk();
                }
            };
            idleRow.Children.Add(_cmbIdleTimeout);
            rStack.Children.Add(idleRow);

            var btnExitFull = MakeActionBtn("Uygulamayı Tamamen Kapat", false, ExitApplication);
            btnExitFull.HorizontalAlignment = HorizontalAlignment.Left;
            btnExitFull.Margin = new Thickness(0);
            rStack.Children.Add(btnExitFull);

            rightCard.Child = rStack;
            Grid.SetColumn(rightCard, 2);
            grid.Children.Add(rightCard);

            return grid;
        }

        private void LoadSettingsFromDisk()
        {
            string sFile = Path.Combine(_baseDir, "settings.json");
            if (!File.Exists(sFile)) return;
            try
            {
                var ser = new JavaScriptSerializer();
                var d = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(sFile, Encoding.UTF8));
                if (d == null) return;
                if (d.ContainsKey("last_mode")) _fxEngine.CurrentMode = Convert.ToString(d["last_mode"]);
                if (d.ContainsKey("brightness")) _fxEngine.Brightness = Convert.ToDouble(d["brightness"]);
                if (d.ContainsKey("speed")) _fxEngine.Speed = Convert.ToDouble(d["speed"]);
                if (d.ContainsKey("target_fps")) _fxEngine.TargetFps = Convert.ToInt32(d["target_fps"]);
                if (d.ContainsKey("eco_mode")) _fxEngine.EcoMode = Convert.ToBoolean(d["eco_mode"]);
                if (d.ContainsKey("ui_live_preview")) _livePreviewEnabled = Convert.ToBoolean(d["ui_live_preview"]);
                if (d.ContainsKey("minimize_to_tray")) _minimizeToTray = Convert.ToBoolean(d["minimize_to_tray"]);
                if (d.ContainsKey("idle_timeout_minutes")) _fxEngine.IdleTimeoutMinutes = Convert.ToInt32(d["idle_timeout_minutes"]);
            }
            catch { }
        }

        private void SaveSettingsToDisk()
        {
            try
            {
                string sFile = Path.Combine(_baseDir, "settings.json");
                var dict = new Dictionary<string, object>
                {
                    {"target_fps", _fxEngine.TargetFps},
                    {"eco_mode", _fxEngine.EcoMode},
                    {"ui_live_preview", _livePreviewEnabled},
                    {"idle_timeout_minutes", _fxEngine.IdleTimeoutMinutes},
                    {"autostart", IsAutostartRegistered()},
                    {"minimize_to_tray", _minimizeToTray},
                    {"macros_enabled", _macroEngine.GlobalEnabled},
                    {"last_mode", _fxEngine.CurrentMode},
                    {"brightness", _fxEngine.Brightness},
                    {"speed", _fxEngine.Speed},
                    {"primary_color", new int[] { _fxEngine.PrimaryR, _fxEngine.PrimaryG, _fxEngine.PrimaryB }},
                    {"secondary_color", new int[] { _fxEngine.SecondaryR, _fxEngine.SecondaryG, _fxEngine.SecondaryB }},
                    {"tertiary_color", new int[] { _fxEngine.TertiaryR, _fxEngine.TertiaryG, _fxEngine.TertiaryB }},
                    {"direction", _fxEngine.Direction},
                    {"audio_sensitivity", _fxEngine.AudioSensitivity}
                };
                var ser = new JavaScriptSerializer();
                File.WriteAllText(sFile, ser.Serialize(dict), Encoding.UTF8);
            }
            catch { }
        }

        private bool IsAutostartRegistered()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key != null && key.GetValue("RedragonK617RGBStudio") != null;
                }
            }
            catch { return false; }
        }

        private void SetAutostartRegistry(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    if (enable)
                    {
                        string exePath = Process.GetCurrentProcess().MainModule.FileName;
                        key.SetValue("RedragonK617RGBStudio", "\"" + exePath + "\" --minimized");
                    }
                    else
                    {
                        key.DeleteValue("RedragonK617RGBStudio", false);
                    }
                }
            }
            catch { }
        }

        private TextBox MakeInputBox(string text, double width)
        {
            return new TextBox
            {
                Text = text,
                Width = width,
                Padding = new Thickness(8, 5, 8, 5),
                Background = BgInput,
                Foreground = TextPrimary,
                CaretBrush = TextPrimary,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(1),
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        private static TextBlock MakeText(string text, double size, Brush color, FontWeight? weight = null)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = color,
                FontWeight = weight ?? FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private Button MakeActionBtn(string text, bool primary, Action onClick)
        {
            var btn = new Button
            {
                Style = (Style)Resources["StudioButton"],
                Content = text,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(4, 0, 0, 0),
                Background = primary ? BrandAccent : BgElevated,
                Foreground = Brushes.White,
                BorderBrush = primary ? BrandAccent : BorderSubtle,
                BorderThickness = new Thickness(1)
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            bool createdNew;
            using (var mutex = new Mutex(true, "RedragonK617RGBStudio_SingleInstanceMutex", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("FizzCtl zaten çalışıyor. Sistem tepsisini (saatin yanını) kontrol edin.", "FizzCtl — Redragon K617 Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                bool minimized = args != null && args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
                var app = new Application();
                app.Run(new StudioWindow(minimized));
            }
        }
    }
}
