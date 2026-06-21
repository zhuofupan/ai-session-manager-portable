using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace AiSessionManagerPortable
{
    public static class Program
    {
        public const string AppVersion = "2026.06.21.07";
        public const string AppAuthor = "Joff Pan";
        public const string GitHubUrl = "https://github.com/zhuofupan/ai-session-manager-portable";

        [STAThread]
        public static void Main()
        {
            try { NativeMethods.SetCurrentProcessExplicitAppUserModelID("JoffPan.AiSessionManagerPortable.Wpf"); } catch { }
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            var root = AppDomain.CurrentDomain.BaseDirectory;
            app.Run(new MainWindow(root));
        }
    }

    internal static class NativeMethods
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SetCurrentProcessExplicitAppUserModelID(string appID);
    }

    public sealed class AppConfig
    {
        public string CodexHome = "";
        public string CcSwitchHome = "";
        public string CodexExe = "";
        public string DefaultSourceProvider = "__all__";
        public string DefaultTargetProvider = "custom";
        public string DefaultCcSwitchNode = "";
        public string LaunchModel = "";
        public string LaunchReasoningEffort = "";
        public string DirectoryFilter = "";
        public string UiLanguage = "zh-CN";
        public string UiTheme = "summer_ocean_breeze";
        public bool IncludeArchived = false;
        public bool FastModeOnLaunch = false;
        public bool ApprovalNeverOnLaunch = true;
        public bool LoadChatOnLaunch = true;
        public bool UsePowerShellTerminal = false;
        public bool TurnCompletePopup = true;
        public bool DisableAppsOnFast = true;
        public int Limit = 50;
        public int ConversationFontSize = 14;
    }

    public sealed class ProviderItem
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public override string ToString() { return Label; }
    }

    public sealed class SessionRow
    {
        public bool Selected { get; set; }
        public string Updated { get; set; }
        public string Provider { get; set; }
        public bool Archived { get; set; }
        public long UpdatedMs { get; set; }
        public string Id { get; set; }
        public string Cwd { get; set; }
        public string Title { get; set; }
        public string PreviewText { get; set; }
        public string FilePath { get; set; }
        public string Source { get; set; }
        public string DisplayNumber { get; set; }
        public Brush Accent { get; set; }
        public Brush AccentSoft { get; set; }
        public string Meta
        {
            get
            {
                var provider = String.IsNullOrWhiteSpace(Provider) ? "unknown" : Provider;
                return Updated + "  |  " + provider + (Archived ? "  |  archived" : "");
            }
        }
        public string CwdShort
        {
            get
            {
                if (String.IsNullOrWhiteSpace(Cwd)) return "未记录项目目录";
                return Cwd;
            }
        }
        public string ProjectGroup
        {
            get
            {
                if (String.IsNullOrWhiteSpace(Cwd)) return "未记录项目目录";
                try
                {
                    var trimmed = Cwd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var name = Path.GetFileName(trimmed);
                    return String.IsNullOrWhiteSpace(name) ? Cwd : name;
                }
                catch { return Cwd; }
            }
        }
        public string ProjectPath
        {
            get { return String.IsNullOrWhiteSpace(Cwd) ? "未记录项目目录" : Cwd; }
        }
    }

    public sealed class CcSwitchNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string HistoryProvider { get; set; }
        public string ModelProvider { get; set; }
        public string Model { get; set; }
        public string ReasoningEffort { get; set; }
        public string BaseUrl { get; set; }
        public string WireApi { get; set; }
        public string ProviderName { get; set; }
        public string AuthMode { get; set; }
        public string ApiKey { get; set; }
        public bool IsCurrent { get; set; }
        public override string ToString()
        {
            if (String.IsNullOrWhiteSpace(Name)) return Id ?? "";
            return IsCurrent ? Name + "  (当前)" : Name;
        }
    }

    public sealed class ConversationEntry
    {
        public string Role { get; set; }
        public string Text { get; set; }
    }

    internal sealed class UserMessageSegment
    {
        public bool IsUserText { get; set; }
        public string Text { get; set; }
    }

    internal sealed class DetailNavItem
    {
        public FrameworkContentElement Target { get; set; }
        public string Preview { get; set; }
    }

    internal sealed class RefreshDataSnapshot
    {
        public List<Dictionary<string, object>> ThreadRows { get; set; }
        public List<CcSwitchNode> Nodes { get; set; }
    }

    internal sealed class ThemePalette
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public Color Primary { get; set; }
        public Color PrimaryHover { get; set; }
        public Color PrimaryPressed { get; set; }
        public Color Ink { get; set; }
        public Color Muted { get; set; }
        public Color Accent { get; set; }
        public Color Surface { get; set; }
        public Color AppBackground { get; set; }
        public Color Border { get; set; }
        public Color SoftHover { get; set; }
        public Color SoftPressed { get; set; }
        public Color UserBackground { get; set; }
        public Color AssistantAccent { get; set; }
        public Color AssistantBackground { get; set; }
        public Color ToolAccent { get; set; }
        public Color ToolBackground { get; set; }
        public Color SystemAccent { get; set; }
        public Color SystemBackground { get; set; }
        public Color FolderBackground { get; set; }
        public Color FolderBorder { get; set; }
        public Color FolderIcon { get; set; }
        public Color FolderCount { get; set; }
        public Color Success { get; set; }
        public Color MutedSmall { get; set; }
    }

    public sealed class MainWindow : Window
    {
        private readonly string _rootDir;
        private readonly string _configPath;
        private readonly string _sqlitePath;
        private readonly string _cliScriptPath;
        private readonly string _diagnosticLogPath;
        private AppConfig _config;
        private string _stateDb = "";
        private string _ccSwitchDb = "";

        private readonly List<SessionRow> _allRows = new List<SessionRow>();
        private readonly List<SessionRow> _filteredRows = new List<SessionRow>();
        private readonly List<CcSwitchNode> _ccSwitchNodes = new List<CcSwitchNode>();
        private readonly ObservableCollection<SessionRow> _pageRows = new ObservableCollection<SessionRow>();
        private int _pageIndex = 0;
        private int _pageSize = 30;
        private bool _initialLoadComplete = false;
        private bool _suppressConfigSave = false;
        private bool _suppressUiEvents = false;

        private ComboBox _sourceCombo;
        private ComboBox _targetCombo;
        private ComboBox _folderCombo;
        private ComboBox _pageSizeCombo;
        private ComboBox _ccSwitchCombo;
        private ComboBox _modelCombo;
        private ComboBox _reasoningCombo;
        private TextBox _searchBox;
        private TextBox _fontSizeBox;
        private RichTextBox _detailBox;
        private StackPanel _detailNav;
        private Border _detailNavHitBox;
        private Grid _detailHost;
        private Popup _detailNavPopup;
        private CheckBox _includeArchivedBox;
        private CheckBox _loadChatBox;
        private CheckBox _fastBox;
        private CheckBox _fullAccessBox;
        private CheckBox _usePowerShellBox;
        private CheckBox _turnPopupBox;
        private ListBox _sessionList;
        private TextBlock _statusText;
        private TextBlock _countText;
        private TextBlock _pathText;
        private Button _prevButton;
        private Button _nextButton;
        private SessionRow _detailRow;
        private readonly List<DetailNavItem> _detailNavTargets = new List<DetailNavItem>();
        private readonly List<int> _detailNavVisibleIndexes = new List<int>();
        private const int MaxDetailNavigationButtons = 20;
        private const int InitialSessionLoadLimit = 60;
        private const int SessionLoadChunkSize = 120;
        private const int FullSessionLoadLimit = 1000;
        private const double DetailNavigationPreviewWidth = 320.0;
        private const double DetailNavigationPreviewGap = 10.0;
        private int _refreshGeneration = 0;
        private int _detailNavPreviewHideToken = 0;
        private int _detailNavPreviewShowToken = 0;
        private bool _detailNavPreviewShowPending = false;
        private int _pendingDetailNavPreviewIndex = -1;
        private List<int> _pendingDetailNavPreviewIndexes = new List<int>();
        private UIElement _pendingDetailNavPreviewTarget = null;
        private int _lastLoggedDetailNavPointerIndex = -1;
        private bool _lastLoggedDetailNavPopupOpen = false;
        private int _activeDetailNavPreviewIndex = -1;
        private string _activeDetailNavPreviewSignature = "";
        private bool _ccSwitchUnifyCodexHistory = false;
        private string _ccSwitchOfficialHistoryProvider = "";
        private string _ccSwitchThirdPartyHistoryProvider = "";
        private bool _fullRowsLoading = false;

        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue, RecursionLimit = 100 };
        private static readonly ThemePalette[] ThemePalettes = BuildThemePalettes();
        private static ThemePalette _activeTheme = ThemePalettes[0];

        public MainWindow(string rootDir)
        {
            var startupSw = Stopwatch.StartNew();
            _rootDir = rootDir;
            _configPath = Path.Combine(_rootDir, "ai-session-manager-config.json");
            _sqlitePath = Path.Combine(_rootDir, "bin", "sqlite3.exe");
            _cliScriptPath = Path.Combine(_rootDir, "tools", "ai-session-manager.ps1");
            _diagnosticLogPath = Path.Combine(GetAppStateDirectory(), "wpf-diagnostic.log");
            _config = LoadConfig();
            _activeTheme = ResolveTheme(_config.UiTheme);
            WriteDiagnostic("Startup config loaded elapsedMs=" + startupSw.ElapsedMilliseconds + " root='" + _rootDir + "' theme='" + _activeTheme.Name + "'.");
            _stateDb = ResolveStateDb();
            _ccSwitchDb = ResolveCcSwitchDb();
            RefreshCcSwitchHistorySettings();
            WriteDiagnostic("Startup paths resolved elapsedMs=" + startupSw.ElapsedMilliseconds + " stateDb='" + _stateDb + "' ccSwitchDb='" + _ccSwitchDb + "'.");

            Title = L("AI 会话管理器 Portable", "AI Session Manager Portable");
            Width = 1360;
            Height = 860;
            MinWidth = 1120;
            MinHeight = 720;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = AppBackgroundBrush();
            FontFamily = new FontFamily("Microsoft YaHei UI");
            FontSize = 14;
            SetIcon();
            Content = BuildLayout();
            WriteDiagnostic("Startup layout built elapsedMs=" + startupSw.ElapsedMilliseconds + ".");

            Loaded += delegate
            {
                WriteDiagnostic("Startup Loaded event elapsedMs=" + startupSw.ElapsedMilliseconds + ".");
                Dispatcher.BeginInvoke(new Action(delegate { RefreshAll(); }), System.Windows.Threading.DispatcherPriority.ContextIdle);
            };
        }

        private void SetIcon()
        {
            var png = Path.Combine(_rootDir, "assets", "ai-session-manager-icon.png");
            var ico = Path.Combine(_rootDir, "assets", "ai-session-manager.ico");
            try
            {
                if (File.Exists(png))
                {
                    Icon = LoadImageSource(png);
                    WriteDiagnostic("SetIcon source=png path='" + png + "'.");
                    return;
                }
                if (File.Exists(ico))
                {
                    Icon = LoadLargestIconFrame(ico);
                    WriteDiagnostic("SetIcon source=ico path='" + ico + "'.");
                }
            }
            catch (Exception ex)
            {
                WriteDiagnostic("SetIcon failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private UIElement BuildLayout()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = BuildHeader();
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var body = new Grid { Margin = new Thickness(18, 0, 18, 12) };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(body, 1);
            root.Children.Add(body);

            var left = BuildSessionPanel();
            Grid.SetColumn(left, 0);
            body.Children.Add(left);

            var right = BuildDetailPanel();
            Grid.SetColumn(right, 1);
            body.Children.Add(right);

            var status = BuildStatusBar();
            Grid.SetRow(status, 2);
            root.Children.Add(status);

            return root;
        }

        private UIElement BuildHeader()
        {
            var outer = new StackPanel { Margin = new Thickness(18, 14, 18, 10) };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            outer.Children.Add(grid);

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var iconBox = new Border
            {
                Width = 52,
                Height = 52,
                CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent,
                Margin = new Thickness(0, 0, 14, 0)
            };
            iconBox.Child = BuildHeaderIcon();
            titleStack.Children.Add(iconBox);
            var textStack = new StackPanel();
            textStack.Children.Add(new TextBlock
            {
                Text = L("AI 会话管理器", "AI Session Manager"),
                FontSize = 25,
                FontWeight = FontWeights.Bold,
                Foreground = InkBrush()
            });
            textStack.Children.Add(new TextBlock
            {
                Text = L("本地 Codex 会话浏览、派生和启动", "Browse, derive, and launch local Codex sessions"),
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = MutedBrush()
            });
            titleStack.Children.Add(textStack);
            Grid.SetColumn(titleStack, 0);
            grid.Children.Add(titleStack);

            var meta = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            meta.Children.Add(new TextBlock
            {
                Text = "v" + Program.AppVersion + "  |  " + L("作者 ", "by ") + Program.AppAuthor + "  |",
                Foreground = MutedBrush(),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            meta.Children.Add(MakeLinkButton("GitHub", delegate { OpenPath(Program.GitHubUrl); }));
            meta.Children.Add(new TextBlock { Text = "|", Foreground = MutedBrush(), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) });
            meta.Children.Add(MakeLinkButton(IsEnglish() ? "中文" : "English", delegate { ToggleLanguage(); }));
            Grid.SetColumn(meta, 1);
            grid.Children.Add(meta);

            outer.Children.Add(BuildCommandBar());
            return outer;
        }

        private UIElement BuildHeaderIcon()
        {
            var icon = Path.Combine(_rootDir, "assets", "ai-session-manager-icon.png");
            if (File.Exists(icon))
            {
                try
                {
                    return new Image
                    {
                        Source = LoadImageSource(icon),
                        Width = 52,
                        Height = 52,
                        Stretch = Stretch.UniformToFill
                    };
                }
                catch { }
            }
            return new Border
            {
                Background = PrimaryBrush(),
                CornerRadius = new CornerRadius(8),
                Child = new TextBlock
                {
                    Text = "AI",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 20,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        private static ImageSource LoadImageSource(string path)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static ImageSource LoadLargestIconFrame(string path)
        {
            var decoder = new IconBitmapDecoder(new Uri(path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames
                .OrderByDescending(f => f.PixelWidth * f.PixelHeight)
                .FirstOrDefault();
            if (frame == null) return LoadImageSource(path);
            frame.Freeze();
            return frame;
        }

        private UIElement BuildStatusBar()
        {
            var border = new Border
            {
                Background = SurfaceBrush(),
                BorderBrush = ThemeBorderBrush(),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(18, 8, 18, 8)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            _statusText = new TextBlock
            {
                Foreground = InkBrush(),
                VerticalAlignment = VerticalAlignment.Center,
                Text = L("准备就绪", "Ready")
            };
            _pathText = new TextBlock
            {
                Foreground = MutedBrush(),
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_statusText, 0);
            Grid.SetColumn(_pathText, 1);
            grid.Children.Add(_statusText);
            grid.Children.Add(_pathText);
            border.Child = grid;
            return border;
        }

        private UIElement BuildCommandBar()
        {
            var bar = new Border
            {
                Background = SurfaceBrush(),
                BorderBrush = ThemeBorderBrush(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 7, 8, 7),
                Margin = new Thickness(0, 14, 0, 0)
            };

            var commandGrid = new Grid();
            commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            commandGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var primary = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            primary.Children.Add(MakeCommandButton("\uE72C", L("刷新", "Refresh"), true, delegate { RefreshAll(); }));
            primary.Children.Add(MakeCommandButton("\uE762", L("全选", "Select all"), false, delegate { SetAllPageSelected(true); }));
            primary.Children.Add(MakeCommandButton("\uE74D", L("清空", "Clear"), false, delegate { SetAllPageSelected(false); }));
            primary.Children.Add(MakeCommandButton("\uE8AB", L("交换", "Swap"), false, delegate { SwapProviders(); }));
            primary.Children.Add(MakeToolbarSeparator());
            primary.Children.Add(MakeToolbarLabel(L("派生", "Derive")));
            primary.Children.Add(MakeCommandButton("\uE8C8", L("派生选中", "Selected"), false, async delegate { await CloneSelectedAsync(); }));
            primary.Children.Add(MakeCommandButton("\uE8EF", L("派生全部", "All"), false, async delegate { await SyncAllAsync(); }));
            primary.Children.Add(MakeCommandButton("\uE8AB", L("双向派生", "Two-way"), false, async delegate { await MirrorAsync(); }));
            Grid.SetColumn(primary, 0);
            commandGrid.Children.Add(primary);

            var support = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            support.Children.Add(MakeThemeMenuButton());
            support.Children.Add(MakeCommandButton("\uE713", L("配置", "Config"), false, delegate { EnsureAndOpenConfig(); }));
            support.Children.Add(MakeCommandButton("\uE897", L("帮助", "Help"), false, delegate { ShowHelpWindow(); }));
            support.Children.Add(MakeCommandButton("\uE895", L("更新", "Update"), false, async delegate { await CheckUpdateAsync(); }));
            Grid.SetColumn(support, 2);
            commandGrid.Children.Add(support);

            bar.Child = commandGrid;
            return bar;
        }

        private UIElement BuildSessionPanel()
        {
            var border = PanelBorder();
            border.Margin = new Thickness(0, 0, 14, 0);
            var dock = new DockPanel();
            border.Child = dock;

            var filters = new StackPanel { Margin = new Thickness(12, 10, 12, 8) };
            DockPanel.SetDock(filters, Dock.Top);
            dock.Children.Add(filters);

            var accountGrid = new Grid { Margin = new Thickness(0, 0, 0, 7) };
            accountGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            accountGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            accountGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            accountGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filters.Children.Add(accountGrid);

            _sourceCombo = MakeCombo();
            _targetCombo = MakeCombo();
            _sourceCombo.MinWidth = 124;
            _targetCombo.MinWidth = 124;
            AddInlineControl(accountGrid, L("源账号", "Source"), _sourceCombo, 0);
            AddInlineControl(accountGrid, L("目标账号", "Target"), _targetCombo, 2);
            _sourceCombo.SelectionChanged += delegate { if (_suppressUiEvents) return; ApplyFilters(true); SaveConfigWithDetectedInfo(false); };
            _targetCombo.SelectionChanged += delegate { if (_suppressUiEvents) return; SaveConfigWithDetectedInfo(false); };

            _folderCombo = MakeCombo();
            _folderCombo.Margin = new Thickness(0);
            _folderCombo.ItemTemplate = BuildFolderComboItemTemplate();
            _folderCombo.ItemContainerStyle = BuildFolderComboItemContainerStyle();
            _folderCombo.MaxDropDownHeight = 360;
            filters.Children.Add(InlineLabeled(L("项目目录", "Project"), _folderCombo, new Thickness(0, 0, 0, 7)));
            _folderCombo.SelectionChanged += delegate { if (_suppressUiEvents) return; ApplyFilters(true); SaveConfigWithDetectedInfo(false); };

            _searchBox = new TextBox { Height = 30, Padding = new Thickness(9, 4, 9, 4), BorderBrush = ThemeBorderBrush(), Background = SurfaceBrush(), Foreground = InkBrush() };
            _searchBox.TextChanged += delegate { if (_suppressUiEvents) return; ApplyFilters(true); };
            filters.Children.Add(InlineLabeled(L("搜索", "Search"), _searchBox, new Thickness(0, 0, 0, 7)));

            var optionRow = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            optionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            optionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            optionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            optionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            optionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            optionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            optionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _includeArchivedBox = new CheckBox { Content = L("显示归档", "Archived"), VerticalAlignment = VerticalAlignment.Center, IsChecked = _config.IncludeArchived };
            _includeArchivedBox.Checked += delegate { if (_suppressUiEvents) return; SaveConfigWithDetectedInfo(false); RefreshAll(); };
            _includeArchivedBox.Unchecked += delegate { if (_suppressUiEvents) return; SaveConfigWithDetectedInfo(false); RefreshAll(); };
            Grid.SetColumn(_includeArchivedBox, 0);
            optionRow.Children.Add(_includeArchivedBox);
            _pageSizeCombo = MakeCombo();
            _pageSizeCombo.Width = 78;
            foreach (var item in new[] { L("30 条", "30 rows"), L("50 条", "50 rows"), L("100 条", "100 rows") }) _pageSizeCombo.Items.Add(item);
            if (_config.Limit >= 100) _pageSizeCombo.SelectedIndex = 2;
            else if (_config.Limit >= 50) _pageSizeCombo.SelectedIndex = 1;
            else _pageSizeCombo.SelectedIndex = 0;
            _pageSizeCombo.SelectionChanged += delegate { if (_suppressUiEvents) return; _pageSize = ParsePageSize(); ApplyFilters(true); SaveConfigWithDetectedInfo(false); };
            var pageLabel = new TextBlock { Text = L("每页", "Per page"), Margin = new Thickness(10, 3, 4, 0), Foreground = MutedBrush(), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(pageLabel, 1);
            optionRow.Children.Add(pageLabel);
            Grid.SetColumn(_pageSizeCombo, 2);
            optionRow.Children.Add(_pageSizeCombo);

            _prevButton = MakePagerButton(L("上一页", "Prev"), delegate { MovePage(-1); });
            Grid.SetColumn(_prevButton, 3);
            optionRow.Children.Add(_prevButton);

            _countText = new TextBlock
            {
                Margin = new Thickness(6, 3, 4, 0),
                Foreground = MutedBrush(),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                MinWidth = 82,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_countText, 4);
            optionRow.Children.Add(_countText);

            _nextButton = MakePagerButton(L("下一页", "Next"), delegate { MovePage(1); });
            Grid.SetColumn(_nextButton, 5);
            optionRow.Children.Add(_nextButton);
            filters.Children.Add(optionRow);

            _sessionList = new ListBox
            {
                ItemsSource = _pageRows,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Margin = new Thickness(12, 0, 12, 0),
                ItemTemplate = BuildSessionTemplate(),
                ItemContainerStyle = BuildSessionItemContainerStyle()
            };
            _sessionList.GroupStyle.Add(BuildSessionGroupStyle());
            ConfigureSessionListGrouping();
            _sessionList.SelectionChanged += delegate { ShowSelectedDetail(); };
            dock.Children.Add(_sessionList);

            return border;
        }

        private UIElement BuildDetailPanel()
        {
            var border = PanelBorder();
            var dock = new DockPanel();
            border.Child = dock;

            var top = new Grid { Margin = new Thickness(18, 16, 18, 12) };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            DockPanel.SetDock(top, Dock.Top);
            dock.Children.Add(top);

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock { Text = L("会话详情", "Session Details"), FontSize = 21, FontWeight = FontWeights.Bold, Foreground = InkBrush() });
            titleStack.Children.Add(new TextBlock { Text = L("查看 transcript、打开文件夹、派生或启动 Codex", "View transcript, open folders, derive, or launch Codex"), Margin = new Thickness(0, 4, 0, 0), Foreground = MutedBrush() });
            Grid.SetColumn(titleStack, 0);
            top.Children.Add(titleStack);

            var actionStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            actionStack.Children.Add(MakeCommandButton("\uE8A5", L("打开会话", "Open session"), false, delegate { OpenSelectedFile(); }));
            actionStack.Children.Add(MakeCommandButton("\uE838", L("打开目录", "Open folder"), false, delegate { OpenSelectedFolder(); }));
            actionStack.Children.Add(MakeCommandButton("\uE8C8", L("复制 ID", "Copy ID"), false, delegate { CopySelectedId(); }));
            Grid.SetColumn(actionStack, 1);
            top.Children.Add(actionStack);

            var controls = new Grid { Margin = new Thickness(18, 0, 18, 12) };
            controls.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            controls.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DockPanel.SetDock(controls, Dock.Top);
            dock.Children.Add(controls);

            var settingsRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(settingsRow, 0);
            controls.Children.Add(settingsRow);
            settingsRow.Children.Add(MakeToolbarLabel(L("终端", "Terminal")));
            settingsRow.Children.Add(MakeInlineLabel(L("账号", "Account")));
            _ccSwitchCombo = MakeCombo();
            _ccSwitchCombo.Width = 190;
            _ccSwitchCombo.Margin = new Thickness(0, 0, 10, 8);
            _ccSwitchCombo.SelectionChanged += delegate { if (_suppressUiEvents) return; SaveConfigWithDetectedInfo(false); };
            settingsRow.Children.Add(_ccSwitchCombo);

            settingsRow.Children.Add(MakeInlineLabel(L("模型", "Model")));
            _modelCombo = MakeCombo();
            _modelCombo.Width = 126;
            _modelCombo.Margin = new Thickness(0, 0, 10, 8);
            foreach (var item in new[] { "default", "gpt-5.5", "gpt-5", "gpt-4.1" }) _modelCombo.Items.Add(item);
            _modelCombo.SelectedItem = String.IsNullOrWhiteSpace(_config.LaunchModel) ? "default" : _config.LaunchModel;
            _modelCombo.SelectionChanged += delegate { if (_suppressUiEvents) return; SaveConfigWithDetectedInfo(false); };
            settingsRow.Children.Add(_modelCombo);

            settingsRow.Children.Add(MakeInlineLabel(L("推理", "Reasoning")));
            _reasoningCombo = MakeCombo();
            _reasoningCombo.Width = 108;
            _reasoningCombo.Margin = new Thickness(0, 0, 10, 8);
            foreach (var item in new[] { "default", "minimal", "low", "medium", "high" }) _reasoningCombo.Items.Add(item);
            _reasoningCombo.SelectedItem = String.IsNullOrWhiteSpace(_config.LaunchReasoningEffort) ? "default" : _config.LaunchReasoningEffort;
            _reasoningCombo.SelectionChanged += delegate { if (_suppressUiEvents) return; SaveConfigWithDetectedInfo(false); };
            settingsRow.Children.Add(_reasoningCombo);

            var optionsRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            optionsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            optionsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(optionsRow, 1);
            controls.Children.Add(optionsRow);

            var optionActions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Left };
            Grid.SetColumn(optionActions, 0);
            optionsRow.Children.Add(optionActions);
            optionActions.Children.Add(MakeToolbarLabel(L("选项", "Options")));
            _loadChatBox = new CheckBox { Content = L("+ 会话", "+ Session"), Margin = new Thickness(0, 7, 16, 0), IsChecked = _config.LoadChatOnLaunch };
            _fastBox = new CheckBox { Content = "Fast", Margin = new Thickness(0, 7, 16, 0), IsChecked = _config.FastModeOnLaunch };
            _fullAccessBox = new CheckBox { Content = L("完全访问", "Full access"), Margin = new Thickness(0, 7, 16, 0), IsChecked = _config.ApprovalNeverOnLaunch };
            _usePowerShellBox = new CheckBox { Content = "PowerShell", Margin = new Thickness(0, 7, 16, 0), IsChecked = _config.UsePowerShellTerminal };
            _turnPopupBox = new CheckBox { Content = L("弹窗提醒", "Popup"), Margin = new Thickness(0, 7, 16, 0), IsChecked = _config.TurnCompletePopup };
            _loadChatBox.Checked += delegate { if (_suppressUiEvents) return; SaveConfigWithDetectedInfo(false); };
            _loadChatBox.Unchecked += delegate { if (_suppressUiEvents) return; SaveConfigWithDetectedInfo(false); };
            _fastBox.Checked += delegate { if (_suppressUiEvents) return; SaveConfigWithDetectedInfo(false); };
            _fastBox.Unchecked += delegate { if (_suppressUiEvents) return; SaveConfigWithDetectedInfo(false); };
            _fullAccessBox.Checked += delegate { if (_suppressUiEvents) return; SaveConfigWithDetectedInfo(false); };
            _fullAccessBox.Unchecked += delegate { if (_suppressUiEvents) return; SaveConfigWithDetectedInfo(false); };
            _usePowerShellBox.Checked += delegate { if (_suppressUiEvents) return; SaveConfigWithDetectedInfo(false); };
            _usePowerShellBox.Unchecked += delegate { if (_suppressUiEvents) return; SaveConfigWithDetectedInfo(false); };
            _turnPopupBox.Checked += delegate { if (_suppressUiEvents) return; SaveConfigWithDetectedInfo(false); };
            _turnPopupBox.Unchecked += delegate { if (_suppressUiEvents) return; SaveConfigWithDetectedInfo(false); };
            optionActions.Children.Add(_loadChatBox);
            optionActions.Children.Add(_fastBox);
            optionActions.Children.Add(_fullAccessBox);
            optionActions.Children.Add(_usePowerShellBox);
            var launchButton = MakeCommandButton("\uE756", L("启动终端", "Launch"), true, delegate { LaunchTerminal(); });
            launchButton.Margin = new Thickness(0, -2, 5, 0);
            launchButton.MinWidth = 88;
            launchButton.Padding = new Thickness(12, 7, 12, 7);
            optionActions.Children.Add(launchButton);

            var rowEndGroup = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(16, 0, 0, 0) };
            Grid.SetColumn(rowEndGroup, 1);
            optionsRow.Children.Add(rowEndGroup);
            rowEndGroup.Children.Add(_turnPopupBox);
            var fontSizeGroup = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            rowEndGroup.Children.Add(fontSizeGroup);
            fontSizeGroup.Children.Add(MakeInlineLabel(L("字号", "Font")));
            _fontSizeBox = new TextBox
            {
                Width = 46,
                Height = 30,
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(7, 4, 7, 4),
                Text = GetConversationFontSize().ToString(),
                BorderBrush = ThemeBorderBrush(),
                Background = SurfaceBrush(),
                Foreground = InkBrush(),
                ToolTip = L("会话内容字号，范围 10-24", "Conversation font size, range 10-24")
            };
            _fontSizeBox.LostFocus += delegate { ApplyFontSizeInput(); };
            _fontSizeBox.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) ApplyFontSizeInput(); };
            fontSizeGroup.Children.Add(_fontSizeBox);

            var detailHost = new Grid { Margin = new Thickness(18, 0, 18, 14) };
            _detailHost = detailHost;
            _detailBox = new RichTextBox
            {
                Margin = new Thickness(0),
                IsReadOnly = true,
                IsDocumentEnabled = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = GetConversationFontSize(),
                BorderThickness = new Thickness(1),
                BorderBrush = ThemeBorderBrush(),
                Background = SurfaceBrush(),
                Padding = new Thickness(12)
            };
            _detailBox.Document = CreateDetailDocument();
            detailHost.Children.Add(_detailBox);
            _detailNavHitBox = new Border
            {
                Width = 46,
                Padding = new Thickness(8, 14, 8, 14),
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _detailNavHitBox.MouseEnter += delegate(object sender, MouseEventArgs e)
            {
                _detailNavPreviewHideToken++;
                ShowNavigationPreviewFromPointer(e);
            };
            _detailNavHitBox.MouseMove += delegate(object sender, MouseEventArgs e) { ShowNavigationPreviewFromPointer(e); };
            _detailNavHitBox.MouseLeave += delegate { ScheduleNavigationPreviewHide(); };
            _detailNav = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0),
                Width = 30,
                Background = Brushes.Transparent
            };
            _detailNav.MouseEnter += delegate { _detailNavPreviewHideToken++; };
            _detailNavHitBox.Child = _detailNav;
            Panel.SetZIndex(_detailNavHitBox, 2);
            detailHost.Children.Add(_detailNavHitBox);
            _detailNavPopup = new Popup
            {
                Placement = PlacementMode.Relative,
                StaysOpen = true,
                AllowsTransparency = true
            };
            dock.Children.Add(detailHost);

            return border;
        }

        private Border PanelBorder()
        {
            return new Border
            {
                Background = SurfaceBrush(),
                CornerRadius = new CornerRadius(8),
                BorderBrush = ThemeBorderBrush(),
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 12,
                    ShadowDepth = 1,
                    Opacity = 0.08,
                    Color = Color.FromRgb(0x1D, 0x35, 0x57)
                }
            };
        }

        private DataTemplate BuildSessionTemplate()
        {
            var template = new DataTemplate(typeof(SessionRow));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 8));
            border.SetValue(Border.PaddingProperty, new Thickness(10));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.BorderBrushProperty, ThemeBorderBrush());
            border.SetBinding(Border.BackgroundProperty, new Binding("AccentSoft"));

            var dock = new FrameworkElementFactory(typeof(DockPanel));
            dock.SetValue(FrameworkElement.MarginProperty, new Thickness(0));

            var accent = new FrameworkElementFactory(typeof(Border));
            accent.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            accent.SetBinding(Border.BackgroundProperty, new Binding("Accent"));
            accent.SetValue(FrameworkElement.WidthProperty, 4.0);
            accent.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 10, 2));
            accent.SetValue(DockPanel.DockProperty, Dock.Left);
            dock.AppendChild(accent);

            var stack = new FrameworkElementFactory(typeof(StackPanel));
            var rowTop = new FrameworkElementFactory(typeof(DockPanel));
            var number = new FrameworkElementFactory(typeof(TextBlock));
            number.SetBinding(TextBlock.TextProperty, new Binding("DisplayNumber"));
            number.SetValue(TextBlock.ForegroundProperty, AccentBrush());
            number.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            number.SetValue(TextBlock.FontSizeProperty, 13.0);
            number.SetValue(FrameworkElement.MinWidthProperty, 22.0);
            number.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
            number.SetValue(DockPanel.DockProperty, Dock.Left);
            rowTop.AppendChild(number);
            var check = new FrameworkElementFactory(typeof(CheckBox));
            check.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Selected") { Mode = BindingMode.TwoWay });
            check.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 1, 8, 0));
            check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
            check.SetValue(DockPanel.DockProperty, Dock.Left);
            rowTop.AppendChild(check);
            var title = new FrameworkElementFactory(typeof(TextBlock));
            title.SetBinding(TextBlock.TextProperty, new Binding("Title"));
            title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            title.SetValue(TextBlock.FontSizeProperty, 14.5);
            title.SetValue(TextBlock.ForegroundProperty, InkBrush());
            title.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            rowTop.AppendChild(title);
            stack.AppendChild(rowTop);

            var meta = new FrameworkElementFactory(typeof(TextBlock));
            meta.SetBinding(TextBlock.TextProperty, new Binding("Meta"));
            meta.SetValue(TextBlock.MarginProperty, new Thickness(0, 6, 0, 0));
            meta.SetValue(TextBlock.ForegroundProperty, MutedBrush());
            meta.SetValue(TextBlock.FontSizeProperty, 12.5);
            stack.AppendChild(meta);

            var cwd = new FrameworkElementFactory(typeof(TextBlock));
            cwd.SetBinding(TextBlock.TextProperty, new Binding("CwdShort"));
            cwd.SetValue(TextBlock.MarginProperty, new Thickness(0, 4, 0, 0));
            cwd.SetValue(TextBlock.ForegroundProperty, SteelBrush());
            cwd.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            cwd.SetValue(TextBlock.FontSizeProperty, 12.0);
            cwd.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            stack.AppendChild(cwd);

            dock.AppendChild(stack);
            border.AppendChild(dock);
            template.VisualTree = border;
            return template;
        }

        private Style BuildSessionItemContainerStyle()
        {
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));

            var template = new ControlTemplate(typeof(ListBoxItem));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "ItemBorder";
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            border.AppendChild(presenter);
            template.VisualTree = border;

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, SoftHoverBrush(), "ItemBorder"));
            template.Triggers.Add(hover);

            var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Border.BackgroundProperty, SoftPressedBrush(), "ItemBorder"));
            template.Triggers.Add(selected);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private GroupStyle BuildSessionGroupStyle()
        {
            var groupStyle = new GroupStyle();
            var headerTemplate = new DataTemplate();
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.MarginProperty, new Thickness(0, 4, 0, 5));
            border.SetValue(Border.PaddingProperty, new Thickness(8, 6, 8, 6));
            border.SetValue(Border.BackgroundProperty, FolderBackgroundBrush());
            border.SetValue(Border.BorderBrushProperty, FolderBorderBrush());
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            border.SetBinding(FrameworkElement.ToolTipProperty, new Binding("Items[0].ProjectPath"));

            var dock = new FrameworkElementFactory(typeof(DockPanel));
            var icon = new FrameworkElementFactory(typeof(TextBlock));
            icon.SetValue(TextBlock.TextProperty, "\uE8B7");
            icon.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
            icon.SetValue(TextBlock.ForegroundProperty, FolderIconBrush());
            icon.SetValue(TextBlock.FontSizeProperty, 14.0);
            icon.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 7, 0));
            icon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            icon.SetValue(DockPanel.DockProperty, Dock.Left);
            dock.AppendChild(icon);

            var count = new FrameworkElementFactory(typeof(TextBlock));
            count.SetBinding(TextBlock.TextProperty, new Binding("ItemCount") { StringFormat = IsEnglish() ? "{0} items" : "{0} 条" });
            count.SetValue(TextBlock.ForegroundProperty, FolderCountBrush());
            count.SetValue(TextBlock.FontSizeProperty, 11.5);
            count.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 1, 0, 0));
            count.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            count.SetValue(DockPanel.DockProperty, Dock.Right);
            dock.AppendChild(count);

            var name = new FrameworkElementFactory(typeof(TextBlock));
            name.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            name.SetValue(TextBlock.ForegroundProperty, InkBrush());
            name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            name.SetValue(TextBlock.FontSizeProperty, 13.0);
            name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            name.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            dock.AppendChild(name);

            border.AppendChild(dock);
            headerTemplate.VisualTree = border;
            groupStyle.HeaderTemplate = headerTemplate;

            var containerStyle = new Style(typeof(GroupItem));
            var containerTemplate = new ControlTemplate(typeof(GroupItem));
            var expander = new FrameworkElementFactory(typeof(Expander));
            expander.SetValue(Expander.IsExpandedProperty, true);
            expander.SetValue(Control.BackgroundProperty, Brushes.Transparent);
            expander.SetBinding(HeaderedContentControl.HeaderProperty, new Binding());
            expander.SetValue(HeaderedContentControl.HeaderTemplateProperty, headerTemplate);
            var presenter = new FrameworkElementFactory(typeof(ItemsPresenter));
            presenter.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 4));
            expander.AppendChild(presenter);
            containerTemplate.VisualTree = expander;
            containerStyle.Setters.Add(new Setter(Control.TemplateProperty, containerTemplate));
            groupStyle.ContainerStyle = containerStyle;
            return groupStyle;
        }

        private void ConfigureSessionListGrouping()
        {
            try
            {
                var view = CollectionViewSource.GetDefaultView(_pageRows);
                if (view == null) return;
                view.GroupDescriptions.Clear();
                view.GroupDescriptions.Add(new PropertyGroupDescription("ProjectGroup"));
            }
            catch { }
        }

        private void AddLabeledControl(Grid grid, string label, System.Windows.Controls.Control control, int column)
        {
            var host = new StackPanel { Margin = column == 0 ? new Thickness(0, 0, 8, 0) : new Thickness(8, 0, 0, 0) };
            host.Children.Add(new TextBlock { Text = label, Foreground = MutedBrush(), Margin = new Thickness(0, 0, 0, 6), FontWeight = FontWeights.SemiBold });
            host.Children.Add(control);
            Grid.SetColumn(host, column);
            grid.Children.Add(host);
        }

        private void AddInlineControl(Grid grid, string label, System.Windows.Controls.Control control, int labelColumn)
        {
            var text = new TextBlock
            {
                Text = label,
                Foreground = MutedBrush(),
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = labelColumn == 0 ? new Thickness(0, 0, 6, 0) : new Thickness(10, 0, 6, 0)
            };
            Grid.SetColumn(text, labelColumn);
            Grid.SetColumn(control, labelColumn + 1);
            grid.Children.Add(text);
            grid.Children.Add(control);
        }

        private FrameworkElement InlineLabeled(string label, System.Windows.Controls.Control control, Thickness margin)
        {
            var grid = new Grid { Margin = margin };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var text = new TextBlock
            {
                Text = label,
                Foreground = MutedBrush(),
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(text, 0);
            Grid.SetColumn(control, 1);
            grid.Children.Add(text);
            grid.Children.Add(control);
            return grid;
        }

        private FrameworkElement Labeled(string label, System.Windows.Controls.Control control)
        {
            var host = new StackPanel();
            host.Children.Add(new TextBlock { Text = label, Foreground = MutedBrush(), Margin = new Thickness(0, 0, 0, 6), FontWeight = FontWeights.SemiBold });
            host.Children.Add(control);
            return host;
        }

        private TextBlock MakeInlineLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = MutedBrush(),
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 8)
            };
        }

        private ComboBox MakeCombo()
        {
            return new ComboBox
            {
                MinHeight = 32,
                Padding = new Thickness(8, 4, 8, 4),
                BorderBrush = ThemeBorderBrush(),
                Background = SurfaceBrush(),
                Foreground = InkBrush()
            };
        }

        private DataTemplate BuildFolderComboItemTemplate()
        {
            var template = new DataTemplate();
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new Binding("."));
            text.SetBinding(FrameworkElement.ToolTipProperty, new Binding("."));
            text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
            text.SetValue(FrameworkElement.WidthProperty, 300.0);
            text.SetValue(TextBlock.ForegroundProperty, InkBrush());
            template.VisualTree = text;
            return template;
        }

        private Style BuildFolderComboItemContainerStyle()
        {
            var style = new Style(typeof(ComboBoxItem));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 4, 6, 4)));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            return style;
        }

        private Border MakeToolbarSeparator()
        {
            return new Border
            {
                Width = 1,
                Height = 24,
                Background = ThemeBorderBrush(),
                Margin = new Thickness(10, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private TextBlock MakeToolbarLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = MutedBrush(),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
        }

        private Button MakeCommandButton(string icon, string text, bool primary, RoutedEventHandler handler)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(new TextBlock
            {
                Text = icon,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = text,
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
                TextWrapping = TextWrapping.NoWrap
            });
            var b = new Button
            {
                Content = panel,
                Margin = new Thickness(0, 0, 5, 0),
                Padding = new Thickness(9, 7, 9, 7),
                MinWidth = primary ? 58 : 54,
                MinHeight = 34,
                Background = primary ? PrimaryBrush() : SurfaceBrush(),
                BorderBrush = primary ? PrimaryBrush() : ThemeBorderBrush(),
                Foreground = primary ? Brushes.White : InkBrush(),
                Cursor = Cursors.Hand,
                ToolTip = text
            };
            ApplyButtonChrome(b);
            AttachClickFeedback(b, text, handler);
            return b;
        }

        private Button MakeIconButton(string icon, string tooltip, RoutedEventHandler handler)
        {
            var glyph = new TextBlock
            {
                Text = icon,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var b = new Button
            {
                Content = glyph,
                Width = 34,
                Height = 34,
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(0),
                Background = SurfaceBrush(),
                BorderBrush = ThemeBorderBrush(),
                Foreground = InkBrush(),
                Cursor = Cursors.Hand,
                ToolTip = tooltip
            };
            ApplyButtonChrome(b);
            AttachClickFeedback(b, tooltip, handler);
            return b;
        }

        private Button MakeThemeMenuButton()
        {
            var button = MakeCommandButton("\uE790", L("皮肤", "Theme"), false, delegate { });
            button.Click += delegate
            {
                var menu = BuildThemeMenu();
                menu.PlacementTarget = button;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            };
            return button;
        }

        private ContextMenu BuildThemeMenu()
        {
            var menu = new ContextMenu
            {
                Background = SurfaceBrush(),
                BorderBrush = ThemeBorderBrush(),
                Padding = new Thickness(4)
            };
            foreach (var theme in ThemePalettes)
            {
                var item = new MenuItem
                {
                    Header = theme.Name,
                    IsCheckable = true,
                    IsChecked = String.Equals(CurrentTheme().Key, theme.Key, StringComparison.OrdinalIgnoreCase),
                    Icon = BuildThemeSwatch(theme),
                    Foreground = InkBrush(),
                    Padding = new Thickness(8, 5, 12, 5)
                };
                var key = theme.Key;
                item.Click += delegate { ApplyTheme(key); };
                menu.Items.Add(item);
            }
            return menu;
        }

        private UIElement BuildThemeSwatch(ThemePalette theme)
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Width = 50, Height = 14 };
            foreach (var color in new[] { theme.Primary, theme.Muted, theme.Accent, theme.AppBackground, theme.Border })
            {
                stack.Children.Add(new Border
                {
                    Width = 10,
                    Height = 14,
                    Background = ColorBrush(color)
                });
            }
            return stack;
        }

        private void ApplyTheme(string key)
        {
            var theme = ResolveTheme(key);
            if (theme == null) return;
            var current = CurrentTheme();
            if (String.Equals(current.Key, theme.Key, StringComparison.OrdinalIgnoreCase)) return;

            var selectedId = GetActiveDetailRow() == null ? "" : GetActiveDetailRow().Id;
            _config.UiTheme = theme.Key;
            _activeTheme = theme;
            SaveConfigWithDetectedInfo(true);
            RebuildLayoutForTheme(selectedId);
            SetStatus("已切换皮肤：" + theme.Name);
        }

        private void RebuildLayoutForTheme(string selectedId)
        {
            if (_detailNavPopup != null) _detailNavPopup.IsOpen = false;
            var oldSuppress = _suppressConfigSave;
            _suppressConfigSave = true;
            Title = L("AI 会话管理器 Portable", "AI Session Manager Portable");
            Background = AppBackgroundBrush();
            Content = BuildLayout();
            RunWithoutUiEvents(delegate
            {
                PopulateFilters();
                PopulateCcSwitchCombo();
            });
            _suppressConfigSave = oldSuppress;
            ApplyFilters(false);
            if (!String.IsNullOrWhiteSpace(selectedId)) SelectSessionById(selectedId);
            UpdatePathText();
        }

        private bool IsEnglish()
        {
            return !String.IsNullOrWhiteSpace(_config.UiLanguage) &&
                _config.UiLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        }

        private string L(string zh, string en)
        {
            return IsEnglish() ? en : zh;
        }

        private string AllAccountsLabel()
        {
            return L("全部账号", "All accounts");
        }

        private string AllFoldersLabel()
        {
            return L("全部目录", "All projects");
        }

        private string NoSwitchLabel()
        {
            return L("不切换", "No switch");
        }

        private void ToggleLanguage()
        {
            var selectedId = GetActiveDetailRow() == null ? "" : GetActiveDetailRow().Id;
            _config.UiLanguage = IsEnglish() ? "zh-CN" : "en-US";
            SaveConfigWithDetectedInfo(true);
            RebuildLayoutForTheme(selectedId);
            SetStatus(IsEnglish() ? "Language switched to English." : "已切换为中文。");
        }

        private void RunWithoutUiEvents(Action action)
        {
            var oldSuppress = _suppressUiEvents;
            _suppressUiEvents = true;
            try
            {
                if (action != null) action();
            }
            finally
            {
                _suppressUiEvents = oldSuppress;
            }
        }

        private void SelectSessionById(string id)
        {
            if (String.IsNullOrWhiteSpace(id) || _sessionList == null) return;
            var index = _filteredRows.FindIndex(r => String.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return;
            _pageIndex = Math.Max(0, index / Math.Max(1, _pageSize));
            RenderPage(false);
            for (var i = 0; i < _pageRows.Count; i++)
            {
                if (String.Equals(_pageRows[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    _sessionList.SelectedIndex = i;
                    _sessionList.ScrollIntoView(_pageRows[i]);
                    return;
                }
            }
        }

        private Border MakeCommandGroup(string label, UIElement[] items)
        {
            var border = new Border
            {
                Background = SoftHoverBrush(),
                BorderBrush = ThemeBorderBrush(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 10, 8)
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = MutedBrush(),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            foreach (var item in items) stack.Children.Add(item);
            border.Child = stack;
            return border;
        }

        private Button MakeToolButton(string icon, string text, bool primary, RoutedEventHandler handler)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(new TextBlock
            {
                Text = icon,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = text,
                Margin = new Thickness(7, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            var b = new Button
            {
                Content = panel,
                Margin = new Thickness(2, 0, 2, 0),
                Padding = new Thickness(10, 6, 10, 6),
                MinHeight = 32,
                Background = primary ? PrimaryBrush() : SurfaceBrush(),
                BorderBrush = primary ? PrimaryBrush() : ThemeBorderBrush(),
                Foreground = primary ? Brushes.White : InkBrush(),
                Cursor = Cursors.Hand,
                ToolTip = text
            };
            ApplyButtonChrome(b);
            AttachClickFeedback(b, text, handler);
            return b;
        }

        private Button MakeButton(string text, Brush background, RoutedEventHandler handler)
        {
            var primary = IsPrimaryBrush(background);
            var b = new Button
            {
                Content = text,
                Margin = new Thickness(5, 0, 0, 0),
                Padding = new Thickness(12, 7, 12, 7),
                MinHeight = 32,
                Background = background,
                BorderBrush = primary ? background : ThemeBorderBrush(),
                Foreground = primary ? Brushes.White : InkBrush(),
                Cursor = Cursors.Hand
            };
            ApplyButtonChrome(b);
            AttachClickFeedback(b, text, handler);
            return b;
        }

        private Button MakePagerButton(string text, RoutedEventHandler handler)
        {
            var b = new Button
            {
                Content = text,
                Margin = new Thickness(2, 0, 0, 0),
                Padding = new Thickness(5, 5, 5, 5),
                MinWidth = 42,
                MinHeight = 30,
                FontSize = 12.5,
                Background = SurfaceBrush(),
                BorderBrush = ThemeBorderBrush(),
                Foreground = InkBrush(),
                Cursor = Cursors.Hand,
                ToolTip = text
            };
            ApplyButtonChrome(b);
            AttachClickFeedback(b, text, handler);
            return b;
        }

        private Button MakeLinkButton(string text, RoutedEventHandler handler)
        {
            var label = new TextBlock
            {
                Text = text,
                Foreground = PrimaryBrush(),
                FontWeight = FontWeights.SemiBold,
                TextDecorations = null
            };
            var b = new Button
            {
                Content = label,
                Padding = new Thickness(2, 1, 2, 1),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                FocusVisualStyle = null
            };
            var template = new ControlTemplate(typeof(Button));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            template.VisualTree = presenter;
            b.Template = template;
            b.MouseEnter += delegate
            {
                label.Foreground = AccentBrush();
                label.TextDecorations = TextDecorations.Underline;
            };
            b.MouseLeave += delegate
            {
                label.Foreground = PrimaryBrush();
                label.TextDecorations = null;
            };
            b.Click += delegate(object sender, RoutedEventArgs e)
            {
                try { handler(sender, e); }
                catch (Exception ex)
                {
                    SetStatus("打开链接失败：" + ex.Message);
                    System.Windows.MessageBox.Show(ex.Message, "打开链接失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            return b;
        }

        private void AttachClickFeedback(Button button, string actionText, RoutedEventHandler handler)
        {
            button.Click += delegate(object sender, RoutedEventArgs e)
            {
                if (!String.IsNullOrWhiteSpace(actionText)) SetStatus(L("已触发：", "Triggered: ") + actionText);
                handler(sender, e);
            };
        }

        private void ApplyButtonChrome(Button button)
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "ChromeBorder";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetValue(UIElement.RenderTransformProperty, new TranslateTransform(0, 0));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            border.AppendChild(presenter);
            template.VisualTree = border;

            var primary = IsPrimaryBrush(button.Background);
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, primary ? PrimaryHoverBrush() : SoftHoverBrush(), "ChromeBorder"));
            hover.Setters.Add(new Setter(Border.BorderBrushProperty, primary ? PrimaryHoverBrush() : AquaBrush(), "ChromeBorder"));
            template.Triggers.Add(hover);

            var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(Border.BackgroundProperty, primary ? PrimaryPressedBrush() : SoftPressedBrush(), "ChromeBorder"));
            pressed.Setters.Add(new Setter(Border.BorderBrushProperty, SteelBrush(), "ChromeBorder"));
            pressed.Setters.Add(new Setter(UIElement.RenderTransformProperty, new TranslateTransform(0, 1), "ChromeBorder"));
            template.Triggers.Add(pressed);

            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.46, "ChromeBorder"));
            template.Triggers.Add(disabled);

            button.Template = template;
            button.BorderThickness = new Thickness(1);
            button.SnapsToDevicePixels = true;
        }

        private static ThemePalette CurrentTheme() { return _activeTheme ?? ThemePalettes[0]; }
        private static Brush PrimaryBrush() { return ColorBrush(CurrentTheme().Primary); }
        private static Brush PrimaryHoverBrush() { return ColorBrush(CurrentTheme().PrimaryHover); }
        private static Brush PrimaryPressedBrush() { return ColorBrush(CurrentTheme().PrimaryPressed); }
        private static Brush InkBrush() { return ColorBrush(CurrentTheme().Ink); }
        private static Brush SteelBrush() { return ColorBrush(CurrentTheme().Muted); }
        private static Brush AquaBrush() { return ColorBrush(CurrentTheme().Border); }
        private static Brush AccentBrush() { return ColorBrush(CurrentTheme().Accent); }
        private static Brush SurfaceBrush() { return ColorBrush(CurrentTheme().Surface); }
        private static Brush AppBackgroundBrush() { return ColorBrush(CurrentTheme().AppBackground); }
        private static Brush ThemeBorderBrush() { return ColorBrush(CurrentTheme().Border); }
        private static Brush MutedBrush() { return ColorBrush(CurrentTheme().Muted); }
        private static Brush SoftHoverBrush() { return ColorBrush(CurrentTheme().SoftHover); }
        private static Brush SoftPressedBrush() { return ColorBrush(CurrentTheme().SoftPressed); }
        private static Brush FolderBackgroundBrush() { return ColorBrush(CurrentTheme().FolderBackground); }
        private static Brush FolderBorderBrush() { return ColorBrush(CurrentTheme().FolderBorder); }
        private static Brush FolderIconBrush() { return ColorBrush(CurrentTheme().FolderIcon); }
        private static Brush FolderCountBrush() { return ColorBrush(CurrentTheme().FolderCount); }
        private static Brush SuccessBrush() { return ColorBrush(CurrentTheme().Success); }
        private static Brush MutedSmallBrush() { return ColorBrush(CurrentTheme().MutedSmall); }
        private static SolidColorBrush ColorBrush(Color color) { return new SolidColorBrush(color); }
        private static SolidColorBrush ColorBrush(byte r, byte g, byte b) { return new SolidColorBrush(Color.FromRgb(r, g, b)); }
        private static bool IsPrimaryBrush(Brush brush)
        {
            var solid = brush as SolidColorBrush;
            return solid != null && solid.Color == CurrentTheme().Primary;
        }

        private static ThemePalette ResolveTheme(string key)
        {
            if (!String.IsNullOrWhiteSpace(key))
            {
                foreach (var theme in ThemePalettes)
                    if (String.Equals(theme.Key, key, StringComparison.OrdinalIgnoreCase)) return theme;
            }
            return ThemePalettes[0];
        }

        private static Color C(byte r, byte g, byte b) { return Color.FromRgb(r, g, b); }

        private static ThemePalette[] BuildThemePalettes()
        {
            return new[]
            {
                new ThemePalette
                {
                    Key = "summer_ocean_breeze",
                    Name = "Summer Ocean Breeze",
                    Primary = C(0x1D, 0x35, 0x57),
                    PrimaryHover = C(0x27, 0x47, 0x66),
                    PrimaryPressed = C(0x14, 0x27, 0x3F),
                    Ink = C(0x1D, 0x35, 0x57),
                    Muted = C(0x45, 0x7B, 0x9D),
                    Accent = C(0xE6, 0x39, 0x46),
                    Surface = C(0xFD, 0xFF, 0xFC),
                    AppBackground = C(0xF1, 0xFA, 0xEE),
                    Border = C(0xA8, 0xDA, 0xDC),
                    SoftHover = C(0xF1, 0xFA, 0xEE),
                    SoftPressed = C(0xE4, 0xF3, 0xF4),
                    UserBackground = C(0xE4, 0xF3, 0xF4),
                    AssistantAccent = C(0x45, 0x7B, 0x9D),
                    AssistantBackground = C(0xEB, 0xF6, 0xF7),
                    ToolAccent = C(0xE6, 0x39, 0x46),
                    ToolBackground = C(0xFB, 0xEA, 0xEC),
                    SystemAccent = C(0x1D, 0x35, 0x57),
                    SystemBackground = C(0xF1, 0xFA, 0xEE),
                    FolderBackground = C(0xFB, 0xEA, 0xEC),
                    FolderBorder = C(0xE6, 0x39, 0x46),
                    FolderIcon = C(0xE6, 0x39, 0x46),
                    FolderCount = C(0x1D, 0x35, 0x57),
                    Success = C(0x45, 0x7B, 0x9D),
                    MutedSmall = C(0x72, 0x8D, 0xA6)
                },
                new ThemePalette
                {
                    Key = "ocean_blue_serenity",
                    Name = "Ocean Blue Serenity",
                    Primary = C(0x03, 0x04, 0x5E),
                    PrimaryHover = C(0x02, 0x3E, 0x8A),
                    PrimaryPressed = C(0x02, 0x02, 0x42),
                    Ink = C(0x03, 0x04, 0x5E),
                    Muted = C(0x00, 0x77, 0xB6),
                    Accent = C(0x00, 0xB4, 0xD8),
                    Surface = C(0xFE, 0xFF, 0xFF),
                    AppBackground = C(0xCA, 0xF0, 0xF8),
                    Border = C(0x90, 0xE0, 0xEF),
                    SoftHover = C(0xCA, 0xF0, 0xF8),
                    SoftPressed = C(0xAD, 0xE8, 0xF4),
                    UserBackground = C(0xAD, 0xE8, 0xF4),
                    AssistantAccent = C(0x00, 0x77, 0xB6),
                    AssistantBackground = C(0xE5, 0xFA, 0xFD),
                    ToolAccent = C(0x00, 0x96, 0xC7),
                    ToolBackground = C(0xEA, 0xF8, 0xFB),
                    SystemAccent = C(0x02, 0x3E, 0x8A),
                    SystemBackground = C(0xE7, 0xF1, 0xF7),
                    FolderBackground = C(0xE7, 0xF1, 0xF7),
                    FolderBorder = C(0x00, 0x96, 0xC7),
                    FolderIcon = C(0x00, 0x77, 0xB6),
                    FolderCount = C(0x03, 0x04, 0x5E),
                    Success = C(0x00, 0x96, 0xC7),
                    MutedSmall = C(0x4D, 0x86, 0xA1)
                },
                new ThemePalette
                {
                    Key = "pastel_dreamland",
                    Name = "Pastel Dreamland",
                    Primary = C(0x5E, 0x54, 0x78),
                    PrimaryHover = C(0x73, 0x64, 0x8F),
                    PrimaryPressed = C(0x47, 0x3E, 0x5C),
                    Ink = C(0x4B, 0x44, 0x60),
                    Muted = C(0x8B, 0xA7, 0xC7),
                    Accent = C(0xF3, 0x9A, 0xC1),
                    Surface = C(0xFF, 0xFD, 0xFF),
                    AppBackground = C(0xF7, 0xED, 0xF8),
                    Border = C(0xCD, 0xB4, 0xDB),
                    SoftHover = C(0xF7, 0xED, 0xF8),
                    SoftPressed = C(0xEA, 0xDD, 0xF0),
                    UserBackground = C(0xEA, 0xF4, 0xFF),
                    AssistantAccent = C(0x8B, 0xA7, 0xC7),
                    AssistantBackground = C(0xEA, 0xF4, 0xFF),
                    ToolAccent = C(0xF3, 0x9A, 0xC1),
                    ToolBackground = C(0xFE, 0xEC, 0xF4),
                    SystemAccent = C(0x5E, 0x54, 0x78),
                    SystemBackground = C(0xF7, 0xED, 0xF8),
                    FolderBackground = C(0xFE, 0xEC, 0xF4),
                    FolderBorder = C(0xF3, 0x9A, 0xC1),
                    FolderIcon = C(0xF3, 0x9A, 0xC1),
                    FolderCount = C(0x5E, 0x54, 0x78),
                    Success = C(0x8B, 0xA7, 0xC7),
                    MutedSmall = C(0x91, 0x91, 0xAD)
                },
                new ThemePalette
                {
                    Key = "fresh_greens",
                    Name = "Fresh Greens",
                    Primary = C(0x38, 0x66, 0x41),
                    PrimaryHover = C(0x4F, 0x7B, 0x4C),
                    PrimaryPressed = C(0x29, 0x4C, 0x30),
                    Ink = C(0x38, 0x66, 0x41),
                    Muted = C(0x6A, 0x99, 0x4E),
                    Accent = C(0xBC, 0x47, 0x49),
                    Surface = C(0xFF, 0xFE, 0xF8),
                    AppBackground = C(0xF2, 0xE8, 0xCF),
                    Border = C(0xA7, 0xC9, 0x57),
                    SoftHover = C(0xF2, 0xE8, 0xCF),
                    SoftPressed = C(0xE3, 0xED, 0xC5),
                    UserBackground = C(0xE3, 0xED, 0xC5),
                    AssistantAccent = C(0x6A, 0x99, 0x4E),
                    AssistantBackground = C(0xEB, 0xF2, 0xDC),
                    ToolAccent = C(0xBC, 0x47, 0x49),
                    ToolBackground = C(0xF7, 0xE4, 0xDD),
                    SystemAccent = C(0x38, 0x66, 0x41),
                    SystemBackground = C(0xF2, 0xE8, 0xCF),
                    FolderBackground = C(0xF7, 0xE4, 0xDD),
                    FolderBorder = C(0xBC, 0x47, 0x49),
                    FolderIcon = C(0xBC, 0x47, 0x49),
                    FolderCount = C(0x38, 0x66, 0x41),
                    Success = C(0x6A, 0x99, 0x4E),
                    MutedSmall = C(0x76, 0x86, 0x61)
                },
                new ThemePalette
                {
                    Key = "deep_sea",
                    Name = "Deep Sea",
                    Primary = C(0x0D, 0x1B, 0x2A),
                    PrimaryHover = C(0x1B, 0x26, 0x3B),
                    PrimaryPressed = C(0x07, 0x12, 0x1D),
                    Ink = C(0x0D, 0x1B, 0x2A),
                    Muted = C(0x41, 0x5A, 0x77),
                    Accent = C(0x45, 0x7B, 0x9D),
                    Surface = C(0xFD, 0xFE, 0xFF),
                    AppBackground = C(0xEC, 0xF0, 0xF4),
                    Border = C(0xC5, 0xD0, 0xDA),
                    SoftHover = C(0xEC, 0xF0, 0xF4),
                    SoftPressed = C(0xE0, 0xE7, 0xEE),
                    UserBackground = C(0xE0, 0xE7, 0xEE),
                    AssistantAccent = C(0x41, 0x5A, 0x77),
                    AssistantBackground = C(0xEC, 0xF0, 0xF4),
                    ToolAccent = C(0x5A, 0x55, 0x72),
                    ToolBackground = C(0xF1, 0xF0, 0xF6),
                    SystemAccent = C(0x7A, 0x5D, 0x3A),
                    SystemBackground = C(0xF8, 0xF1, 0xE8),
                    FolderBackground = C(0xF8, 0xF1, 0xE8),
                    FolderBorder = C(0xC8, 0xA0, 0x68),
                    FolderIcon = C(0x7A, 0x5D, 0x3A),
                    FolderCount = C(0x5B, 0x43, 0x28),
                    Success = C(0x2A, 0x9D, 0x8F),
                    MutedSmall = C(0x77, 0x8D, 0xA6)
                }
            };
        }

        private static string GetAppStateDirectory()
        {
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = String.IsNullOrWhiteSpace(roaming)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".local-state")
                : Path.Combine(roaming, "ai-session-manager-portable");
            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { }
            return dir;
        }

        private void WriteDiagnostic(string text)
        {
            try
            {
                var line = "[" + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz") + "] " + (text ?? "") + Environment.NewLine;
                File.AppendAllText(_diagnosticLogPath, line, Encoding.UTF8);
            }
            catch { }
        }

        private AppConfig LoadConfig()
        {
            var cfg = new AppConfig();
            if (!File.Exists(_configPath)) return cfg;
            try
            {
                var dict = _json.DeserializeObject(File.ReadAllText(_configPath, Encoding.UTF8)) as Dictionary<string, object>;
                if (dict == null) return cfg;
                cfg.CodexHome = GetString(dict, "codexHome");
                cfg.CcSwitchHome = GetString(dict, "ccSwitchHome");
                cfg.CodexExe = GetString(dict, "codexExe");
                cfg.DefaultSourceProvider = DefaultString(GetString(dict, "defaultSourceProvider"), "__all__");
                cfg.DefaultTargetProvider = DefaultString(GetString(dict, "defaultTargetProvider"), "custom");
                cfg.DefaultCcSwitchNode = GetString(dict, "defaultCcSwitchNode");
                cfg.LaunchModel = GetString(dict, "launchModel");
                cfg.LaunchReasoningEffort = GetString(dict, "launchReasoningEffort");
                cfg.DirectoryFilter = GetString(dict, "directoryFilter");
                cfg.UiLanguage = DefaultString(GetString(dict, "uiLanguage"), "zh-CN");
                cfg.UiTheme = DefaultString(GetString(dict, "uiTheme"), "summer_ocean_breeze");
                cfg.IncludeArchived = GetBool(dict, "includeArchived", false);
                cfg.FastModeOnLaunch = GetBool(dict, "fastModeOnLaunch", false);
                cfg.ApprovalNeverOnLaunch = GetBool(dict, "approvalNeverOnLaunch", true);
                cfg.LoadChatOnLaunch = GetBool(dict, "loadChatOnLaunch", true);
                cfg.UsePowerShellTerminal = GetBool(dict, "usePowerShellTerminal", false);
                cfg.TurnCompletePopup = GetBool(dict, "turnCompletePopup", true);
                cfg.DisableAppsOnFast = GetBool(dict, "disableAppsOnFast", true);
                cfg.Limit = GetInt(dict, "limit", 50);
                cfg.ConversationFontSize = Math.Max(10, Math.Min(24, GetInt(dict, "conversationFontSize", 14)));
            }
            catch { }
            return cfg;
        }

        private string ResolveStateDb()
        {
            var home = GetCodexHome();
            return String.IsNullOrWhiteSpace(home) ? "" : Path.Combine(home, "state_5.sqlite");
        }

        private string GetCodexHome()
        {
            return GetCodexHome(false);
        }

        private string GetCodexHome(bool allowDeepSearch)
        {
            var candidates = new List<string>();
            if (!String.IsNullOrWhiteSpace(_config.CodexHome)) candidates.Add(_config.CodexHome);
            var env = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (!String.IsNullOrWhiteSpace(env)) candidates.Add(env);
            var user = Environment.GetEnvironmentVariable("USERPROFILE");
            if (!String.IsNullOrWhiteSpace(user)) candidates.Add(Path.Combine(user, ".codex"));
            candidates.Add(Path.Combine(_rootDir, ".codex"));
            var parent = Directory.GetParent(_rootDir);
            if (parent != null) candidates.Add(Path.Combine(parent.FullName, ".codex"));
            foreach (var c in candidates)
            {
                try
                {
                    var path = allowDeepSearch ? ResolveCodexHomeFromSelection(c) : ResolveCodexHomeFromSelectionFast(c);
                    if (File.Exists(Path.Combine(path, "state_5.sqlite"))) return path;
                }
                catch { }
            }
            if (allowDeepSearch)
            {
                foreach (var root in GetBoundedSearchRoots())
                {
                    var hit = FindFileBelow(root, "state_5.sqlite", 4);
                    if (!String.IsNullOrWhiteSpace(hit)) return Path.GetDirectoryName(hit);
                }
            }
            return "";
        }

        private string ResolveCcSwitchDb()
        {
            return ResolveCcSwitchDb(false);
        }

        private string ResolveCcSwitchDb(bool allowDeepSearch)
        {
            var candidates = new List<string>();
            if (!String.IsNullOrWhiteSpace(_config.CcSwitchHome)) candidates.Add(Path.Combine(_config.CcSwitchHome, "cc-switch.db"));
            candidates.Add(Path.Combine(_rootDir, "cc-switch.db"));
            var parent = Directory.GetParent(_rootDir);
            if (parent != null)
            {
                candidates.Add(Path.Combine(parent.FullName, "cc-switch.db"));
                candidates.Add(Path.Combine(parent.FullName, "cc-switch", "cc-switch.db"));
            }
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!String.IsNullOrWhiteSpace(local)) candidates.Add(Path.Combine(local, "cc-switch", "cc-switch.db"));
            if (!String.IsNullOrWhiteSpace(roaming)) candidates.Add(Path.Combine(roaming, "cc-switch", "cc-switch.db"));
            foreach (var c in candidates)
            {
                try
                {
                    var resolved = allowDeepSearch ? ResolveCcSwitchDbFromSelection(c) : ResolveCcSwitchDbFromSelectionFast(c);
                    if (!String.IsNullOrWhiteSpace(resolved)) return resolved;
                }
                catch { }
            }
            if (allowDeepSearch)
            {
                foreach (var root in GetBoundedSearchRoots())
                {
                    var hit = FindFileBelow(root, "cc-switch.db", 4);
                    if (!String.IsNullOrWhiteSpace(hit)) return hit;
                }
            }
            return "";
        }

        private List<string> GetBoundedSearchRoots()
        {
            var roots = new List<string>();
            AddExistingDirectory(roots, _rootDir);
            var parent = Directory.GetParent(_rootDir);
            if (parent != null) AddExistingDirectory(roots, parent.FullName);
            AddExistingDirectory(roots, Environment.GetEnvironmentVariable("USERPROFILE"));
            AddExistingDirectory(roots, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            AddExistingDirectory(roots, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            return roots;
        }

        private static void AddExistingDirectory(List<string> roots, string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return;
            try
            {
                path = NormalizePath(path);
                if (Directory.Exists(path) && !roots.Any(r => String.Equals(r, path, StringComparison.OrdinalIgnoreCase))) roots.Add(path);
            }
            catch { }
        }

        private static string FindFileBelow(string root, string fileName, int maxDepth)
        {
            if (String.IsNullOrWhiteSpace(root) || maxDepth < 0) return "";
            try
            {
                var direct = Path.Combine(root, fileName);
                if (File.Exists(direct)) return direct;
                if (maxDepth == 0) return "";
                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    var name = Path.GetFileName(dir);
                    if (String.Equals(name, "node_modules", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(name, ".git", StringComparison.OrdinalIgnoreCase)) continue;
                    var hit = FindFileBelow(dir, fileName, maxDepth - 1);
                    if (!String.IsNullOrWhiteSpace(hit)) return hit;
                }
            }
            catch { }
            return "";
        }

        private static string ResolveCodexHomeFromSelection(string selectedPath)
        {
            if (String.IsNullOrWhiteSpace(selectedPath)) return "";
            var path = NormalizePath(selectedPath);
            if (File.Exists(path) && String.Equals(Path.GetFileName(path), "state_5.sqlite", StringComparison.OrdinalIgnoreCase))
                return Path.GetDirectoryName(path);
            if (File.Exists(path)) path = Path.GetDirectoryName(path);
            if (String.IsNullOrWhiteSpace(path)) return "";

            var directChild = Path.Combine(path, ".codex");
            if (File.Exists(Path.Combine(directChild, "state_5.sqlite"))) return NormalizePath(directChild);

            var current = new DirectoryInfo(path);
            for (var i = 0; current != null && i < 8; i++, current = current.Parent)
            {
                if (File.Exists(Path.Combine(current.FullName, "state_5.sqlite"))) return NormalizePath(current.FullName);
            }

            var hit = FindFileBelow(path, "state_5.sqlite", 5);
            return String.IsNullOrWhiteSpace(hit) ? "" : NormalizePath(Path.GetDirectoryName(hit));
        }

        private static string ResolveCodexHomeFromSelectionFast(string selectedPath)
        {
            if (String.IsNullOrWhiteSpace(selectedPath)) return "";
            var path = NormalizePath(selectedPath);
            if (File.Exists(path) && String.Equals(Path.GetFileName(path), "state_5.sqlite", StringComparison.OrdinalIgnoreCase))
                return Path.GetDirectoryName(path);
            if (File.Exists(path)) path = Path.GetDirectoryName(path);
            if (String.IsNullOrWhiteSpace(path)) return "";

            if (File.Exists(Path.Combine(path, "state_5.sqlite"))) return NormalizePath(path);
            var directChild = Path.Combine(path, ".codex");
            if (File.Exists(Path.Combine(directChild, "state_5.sqlite"))) return NormalizePath(directChild);

            var current = new DirectoryInfo(path);
            for (var i = 0; current != null && i < 4; i++, current = current.Parent)
            {
                if (File.Exists(Path.Combine(current.FullName, "state_5.sqlite"))) return NormalizePath(current.FullName);
            }
            return "";
        }

        private static string ResolveCcSwitchDbFromSelection(string selectedPath)
        {
            if (String.IsNullOrWhiteSpace(selectedPath)) return "";
            var path = NormalizePath(selectedPath);
            if (File.Exists(path))
            {
                var extension = Path.GetExtension(path);
                if (String.Equals(Path.GetFileName(path), "cc-switch.db", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(extension, ".db", StringComparison.OrdinalIgnoreCase)) return path;
                path = Path.GetDirectoryName(path);
            }
            if (String.IsNullOrWhiteSpace(path)) return "";
            var current = new DirectoryInfo(path);
            for (var i = 0; current != null && i < 8; i++, current = current.Parent)
            {
                var candidate = Path.Combine(current.FullName, "cc-switch.db");
                if (File.Exists(candidate)) return NormalizePath(candidate);
            }
            var hit = FindFileBelow(path, "cc-switch.db", 5);
            return String.IsNullOrWhiteSpace(hit) ? "" : NormalizePath(hit);
        }

        private static string ResolveCcSwitchDbFromSelectionFast(string selectedPath)
        {
            if (String.IsNullOrWhiteSpace(selectedPath)) return "";
            var path = NormalizePath(selectedPath);
            if (File.Exists(path))
            {
                var extension = Path.GetExtension(path);
                if (String.Equals(Path.GetFileName(path), "cc-switch.db", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(extension, ".db", StringComparison.OrdinalIgnoreCase)) return path;
                path = Path.GetDirectoryName(path);
            }
            if (String.IsNullOrWhiteSpace(path)) return "";
            var current = new DirectoryInfo(path);
            for (var i = 0; current != null && i < 4; i++, current = current.Parent)
            {
                var candidate = Path.Combine(current.FullName, "cc-switch.db");
                if (File.Exists(candidate)) return NormalizePath(candidate);
            }
            return "";
        }

        private async void RefreshAll()
        {
            var generation = ++_refreshGeneration;
            var sw = Stopwatch.StartNew();
            try
            {
                WriteDiagnostic("RefreshAll start.");
                SetStatus("正在快速加载最近会话...");
                _suppressConfigSave = true;
                _config = LoadConfig();
                _activeTheme = ResolveTheme(_config.UiTheme);
                _stateDb = ResolveStateDb();
                _ccSwitchDb = ResolveCcSwitchDb();
                RefreshCcSwitchHistorySettings();
                WriteDiagnostic("RefreshAll resolved paths elapsedMs=" + sw.ElapsedMilliseconds + " stateDb='" + _stateDb + "' ccSwitchDb='" + _ccSwitchDb + "'.");
                var stateDb = _stateDb;
                var ccSwitchDb = _ccSwitchDb;
                var snapshot = await Task.Run(delegate { return LoadRefreshDataSnapshot(stateDb, ccSwitchDb, InitialSessionLoadLimit, 0, true); });
                if (generation != _refreshGeneration) return;
                WriteDiagnostic("RefreshAll initial snapshot elapsedMs=" + sw.ElapsedMilliseconds + " rows=" + (snapshot.ThreadRows == null ? 0 : snapshot.ThreadRows.Count) + " nodes=" + (snapshot.Nodes == null ? 0 : snapshot.Nodes.Count) + ".");
                var stage = Stopwatch.StartNew();
                ApplyRefreshDataSnapshot(snapshot);
                WriteDiagnostic("RefreshAll apply snapshot stageMs=" + stage.ElapsedMilliseconds + ".");
                stage.Restart();
                RunWithoutUiEvents(delegate
                {
                    PopulateFilters();
                    PopulateCcSwitchCombo();
                });
                WriteDiagnostic("RefreshAll populate controls stageMs=" + stage.ElapsedMilliseconds + ".");
                stage.Restart();
                ApplyFilters(true, false);
                WriteDiagnostic("RefreshAll apply filters/render stageMs=" + stage.ElapsedMilliseconds + " pageRows=" + _pageRows.Count + ".");
                WriteDiagnostic("RefreshAll first render elapsedMs=" + sw.ElapsedMilliseconds + " rows=" + _allRows.Count + " filtered=" + _filteredRows.Count + ".");
                _suppressConfigSave = false;
                _initialLoadComplete = true;
                UpdatePathText();
                stage.Restart();
                SaveConfigWithDetectedInfo(true);
                WriteDiagnostic("RefreshAll save config stageMs=" + stage.ElapsedMilliseconds + ".");
                var loadingMore = snapshot.ThreadRows != null && snapshot.ThreadRows.Count >= InitialSessionLoadLimit;
                SetStatus(loadingMore
                    ? "已加载最近 " + _allRows.Count + " 条会话，正在后台补全..."
                    : "已刷新。本地 Codex 会话 " + _allRows.Count + " 条，cc-switch 节点 " + _ccSwitchNodes.Count + " 个。");
                WriteDiagnostic("RefreshAll completed initial elapsedMs=" + sw.ElapsedMilliseconds + " rows=" + _allRows.Count + " filtered=" + _filteredRows.Count + " ccNodes=" + _ccSwitchNodes.Count + ".");
                if (loadingMore) StartLazySessionLoad(generation, stateDb, InitialSessionLoadLimit);
            }
            catch (Exception ex)
            {
                _suppressConfigSave = false;
                SetStatus("刷新失败：" + ex.Message);
                WriteDiagnostic("RefreshAll failed: " + ex.GetType().FullName + ": " + ex.Message);
                System.Windows.MessageBox.Show(ex.Message, "刷新失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private RefreshDataSnapshot LoadRefreshDataSnapshot(string stateDb, string ccSwitchDb, int limit, int offset, bool includeNodes)
        {
            return new RefreshDataSnapshot
            {
                ThreadRows = LoadThreadRowsFromDb(stateDb, limit, offset),
                Nodes = includeNodes ? LoadCcSwitchNodesFromDb(ccSwitchDb) : new List<CcSwitchNode>()
            };
        }

        private void ApplyRefreshDataSnapshot(RefreshDataSnapshot snapshot)
        {
            _allRows.Clear();
            if (snapshot != null && snapshot.ThreadRows != null)
            {
                foreach (Dictionary<string, object> row in snapshot.ThreadRows)
                    _allRows.Add(MapSessionRow(row));
            }
            _ccSwitchNodes.Clear();
            if (snapshot != null && snapshot.Nodes != null) _ccSwitchNodes.AddRange(snapshot.Nodes);
        }

        private async void StartLazySessionLoad(int generation, string stateDb, int offset)
        {
            if (_fullRowsLoading) return;
            _fullRowsLoading = true;
            try
            {
                var existing = new HashSet<string>(_allRows.Select(r => r.Id ?? ""), StringComparer.OrdinalIgnoreCase);
                var totalAdded = 0;
                while (offset < FullSessionLoadLimit)
                {
                    await Task.Delay(totalAdded == 0 ? 1600 : 700);
                    if (generation != _refreshGeneration) return;
                    var chunkOffset = offset;
                    var sw = Stopwatch.StartNew();
                    var rows = await Task.Run(delegate { return LoadThreadRowsFromDb(stateDb, SessionLoadChunkSize, chunkOffset); });
                    if (generation != _refreshGeneration) return;
                    if (rows == null || rows.Count == 0) break;

                    var added = 0;
                    foreach (Dictionary<string, object> row in rows)
                    {
                        var mapped = MapSessionRow(row);
                        if (existing.Add(mapped.Id ?? ""))
                        {
                            _allRows.Add(mapped);
                            added++;
                        }
                    }
                    totalAdded += added;
                    if (added > 0)
                    {
                        SetStatus("后台补全会话：" + _allRows.Count + " 条...");
                    }
                    WriteDiagnostic("Lazy session chunk offset=" + chunkOffset + " fetched=" + rows.Count + " added=" + added + " elapsedMs=" + sw.ElapsedMilliseconds + " total=" + _allRows.Count + ".");
                    offset += rows.Count;
                    if (rows.Count < SessionLoadChunkSize) break;
                }
                if (totalAdded > 0 && generation == _refreshGeneration)
                {
                    var selected = GetActiveDetailRow();
                    var selectedId = selected == null ? "" : selected.Id;
                    var renderSw = Stopwatch.StartNew();
                    RunWithoutUiEvents(delegate { PopulateFilters(); });
                    ApplyFilters(false, false);
                    if (!String.IsNullOrWhiteSpace(selectedId)) SelectSessionById(selectedId);
                    WriteDiagnostic("Lazy session final render added=" + totalAdded + " stageMs=" + renderSw.ElapsedMilliseconds + " filtered=" + _filteredRows.Count + " pageRows=" + _pageRows.Count + ".");
                }
                SetStatus("已补全。本地 Codex 会话 " + _allRows.Count + " 条，cc-switch 节点 " + _ccSwitchNodes.Count + " 个。");
                WriteDiagnostic("Lazy session load completed rows=" + _allRows.Count + ".");
            }
            catch (Exception ex)
            {
                WriteDiagnostic("Lazy session load failed: " + ex.GetType().FullName + ": " + ex.Message);
            }
            finally
            {
                _fullRowsLoading = false;
            }
        }

        private void LoadRows()
        {
            _allRows.Clear();
            foreach (Dictionary<string, object> row in LoadThreadRowsFromDb(_stateDb, FullSessionLoadLimit, 0))
                _allRows.Add(MapSessionRow(row));
            WriteDiagnostic("LoadRows mapped rows=" + _allRows.Count + ".");
        }

        private List<Dictionary<string, object>> LoadThreadRowsFromDb(string stateDb, int limit, int offset)
        {
            if (String.IsNullOrWhiteSpace(stateDb) || !File.Exists(stateDb))
                throw new InvalidOperationException("找不到 Codex state_5.sqlite，请在配置文件中设置 codexHome。");
            if (!File.Exists(_sqlitePath))
                throw new InvalidOperationException("找不到 bin\\sqlite3.exe。");

            limit = Math.Max(1, Math.Min(5000, limit));
            offset = Math.Max(0, offset);
            var sql = "SELECT id, model_provider, cwd, title, preview, first_user_message, archived, updated_at_ms, rollout_path FROM threads ORDER BY updated_at_ms DESC, id DESC LIMIT " + limit + " OFFSET " + offset + ";";
            var sw = Stopwatch.StartNew();
            var rows = QueryJson(stateDb, sql);
            WriteDiagnostic("LoadThreadRowsFromDb limit=" + limit + " offset=" + offset + " rows=" + rows.Count + " elapsedMs=" + sw.ElapsedMilliseconds + ".");
            var list = new List<Dictionary<string, object>>();
            foreach (Dictionary<string, object> row in rows) list.Add(row);
            return list;
        }

        private SessionRow MapSessionRow(Dictionary<string, object> row)
        {
            var provider = GetString(row, "model_provider");
            var archived = GetBool(row, "archived", false);
            var previewText = FirstNonEmpty(GetString(row, "first_user_message"), GetString(row, "preview"), GetString(row, "title"));
            var title = FirstNonEmpty(GetString(row, "title"), GetString(row, "preview"), GetString(row, "first_user_message"), GetString(row, "id"));
            var updatedMs = GetLong(row, "updated_at_ms", 0);
            var session = new SessionRow
            {
                Selected = false,
                Updated = UnixMsToLocalText(updatedMs),
                UpdatedMs = updatedMs,
                Provider = ProviderLabel(provider),
                Archived = archived,
                Id = GetString(row, "id"),
                Cwd = NormalizePath(GetString(row, "cwd")),
                Title = Shorten(title, 130),
                PreviewText = previewText,
                FilePath = NormalizePath(GetString(row, "rollout_path")),
                Source = "codex threads"
            };
            ApplyAccent(session, provider, archived);
            return session;
        }

        private void PopulateFilters()
        {
            var oldSource = SelectedProviderValue(_sourceCombo);
            var oldTarget = SelectedProviderValue(_targetCombo);
            var oldFolder = _folderCombo.SelectedItem as string;

            _sourceCombo.Items.Clear();
            _targetCombo.Items.Clear();
            var providers = _allRows.Select(r => r.Provider)
                .Concat(_ccSwitchNodes.Select(n => ProviderLabel(n.HistoryProvider)))
                .Where(s => !String.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();
            _sourceCombo.Items.Add(new ProviderItem { Label = AllAccountsLabel(), Value = "__all__" });
            foreach (var p in providers)
            {
                _sourceCombo.Items.Add(new ProviderItem { Label = p, Value = ProviderValue(p) });
                _targetCombo.Items.Add(new ProviderItem { Label = p, Value = ProviderValue(p) });
            }
            SelectProvider(_sourceCombo, DefaultString(oldSource, _config.DefaultSourceProvider));
            SelectProvider(_targetCombo, DefaultString(oldTarget, _config.DefaultTargetProvider));
            if (_targetCombo.SelectedIndex < 0 && _targetCombo.Items.Count > 0) _targetCombo.SelectedIndex = 0;

            _folderCombo.Items.Clear();
            _folderCombo.Items.Add(AllFoldersLabel());
            foreach (var cwd in _allRows.Select(r => r.Cwd).Where(s => !String.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s))
                _folderCombo.Items.Add(cwd);
            if (!String.IsNullOrWhiteSpace(oldFolder) && _folderCombo.Items.Contains(oldFolder)) _folderCombo.SelectedItem = oldFolder;
            else if (!String.IsNullOrWhiteSpace(_config.DirectoryFilter) && _folderCombo.Items.Contains(_config.DirectoryFilter)) _folderCombo.SelectedItem = _config.DirectoryFilter;
            else _folderCombo.SelectedIndex = 0;
        }

        private void LoadCcSwitchNodes()
        {
            _ccSwitchNodes.Clear();
            _ccSwitchNodes.AddRange(LoadCcSwitchNodesFromDb(_ccSwitchDb));
            WriteDiagnostic("LoadCcSwitchNodes mapped nodes=" + _ccSwitchNodes.Count + ".");
        }

        private List<CcSwitchNode> LoadCcSwitchNodesFromDb(string ccSwitchDb)
        {
            var nodes = new List<CcSwitchNode>();
            if (String.IsNullOrWhiteSpace(ccSwitchDb) || !File.Exists(ccSwitchDb)) return nodes;
            try
            {
                var columns = QueryJson(ccSwitchDb, "PRAGMA table_info(providers);");
                if (columns.Count == 0) return nodes;
                var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Dictionary<string, object> col in columns)
                {
                    var name = GetString(col, "name");
                    if (!String.IsNullOrWhiteSpace(name)) columnNames.Add(name);
                }
                if (!columnNames.Contains("id") || !columnNames.Contains("name") || !columnNames.Contains("settings_config") || !columnNames.Contains("app_type")) return nodes;
                var currentSelect = columnNames.Contains("is_current") ? "is_current" : "0 AS is_current";
                var sort = columnNames.Contains("sort_index") ? "is_current DESC, sort_index ASC, name ASC" : "is_current DESC, name ASC";
                var rows = QueryJson(ccSwitchDb, "SELECT id, name, settings_config, " + currentSelect + " FROM providers WHERE app_type = 'codex' ORDER BY " + sort + ";");
                foreach (Dictionary<string, object> row in rows)
                {
                    var node = new CcSwitchNode
                    {
                        Id = GetString(row, "id"),
                        Name = GetString(row, "name"),
                        HistoryProvider = InferHistoryProvider(row),
                        ModelProvider = InferNodeConfigValue(row, "model_provider"),
                        Model = InferNodeConfigValue(row, "model"),
                        ReasoningEffort = FirstNonEmpty(InferNodeConfigValue(row, "model_reasoning_effort"), InferNodeConfigValue(row, "reasoning_effort")),
                        BaseUrl = InferNodeConfigValue(row, "base_url"),
                        WireApi = InferNodeConfigValue(row, "wire_api"),
                        ProviderName = InferNodeConfigValue(row, "name"),
                        AuthMode = InferNodeAuthValue(row, "auth_mode"),
                        ApiKey = InferNodeAuthValue(row, "OPENAI_API_KEY"),
                        IsCurrent = GetBool(row, "is_current", false)
                    };
                    nodes.Add(node);
                }
            }
            catch (Exception ex)
            {
                WriteDiagnostic("LoadCcSwitchNodes failed: " + ex.GetType().FullName + ": " + ex.Message);
            }
            return nodes;
        }

        private void RefreshCcSwitchHistorySettings()
        {
            _ccSwitchUnifyCodexHistory = false;
            _ccSwitchOfficialHistoryProvider = "";
            _ccSwitchThirdPartyHistoryProvider = "";
            try
            {
                var dir = String.IsNullOrWhiteSpace(_ccSwitchDb) ? "" : Path.GetDirectoryName(_ccSwitchDb);
                var settingsPath = String.IsNullOrWhiteSpace(dir) ? "" : Path.Combine(dir, "settings.json");
                if (String.IsNullOrWhiteSpace(settingsPath) || !File.Exists(settingsPath)) return;
                var settings = _json.DeserializeObject(File.ReadAllText(settingsPath, Encoding.UTF8)) as Dictionary<string, object>;
                _ccSwitchUnifyCodexHistory = GetBool(settings, "unifyCodexSessionHistory", false);
                var migrations = GetDict(settings, "localMigrations");
                var official = GetDict(migrations, "codexOfficialHistoryUnifyV1");
                var thirdParty = GetDict(migrations, "codexThirdPartyHistoryProviderBucketV1");
                _ccSwitchOfficialHistoryProvider = GetString(official, "targetProviderId");
                _ccSwitchThirdPartyHistoryProvider = GetString(thirdParty, "targetProviderId");
                WriteDiagnostic("CcSwitchHistorySettings unify=" + _ccSwitchUnifyCodexHistory +
                    " officialTarget='" + _ccSwitchOfficialHistoryProvider +
                    "' thirdPartyTarget='" + _ccSwitchThirdPartyHistoryProvider + "'.");
            }
            catch (Exception ex)
            {
                WriteDiagnostic("CcSwitchHistorySettings failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void PopulateCcSwitchCombo()
        {
            if (_ccSwitchCombo == null) return;
            var previous = SelectedCcSwitchNodeKey();
            _ccSwitchCombo.Items.Clear();
            _ccSwitchCombo.Items.Add(NoSwitchLabel());
            foreach (var node in _ccSwitchNodes) _ccSwitchCombo.Items.Add(node);
            SelectCcSwitchNode(DefaultString(previous, _config.DefaultCcSwitchNode));
            if (_ccSwitchCombo.SelectedIndex < 0) _ccSwitchCombo.SelectedIndex = 0;
        }

        private string InferHistoryProvider(Dictionary<string, object> row)
        {
            var id = GetString(row, "id");
            var name = GetString(row, "name");
            var normalizedName = (name ?? "").ToLowerInvariant().Replace(" ", "");
            if (String.Equals(id, "codex-official", StringComparison.OrdinalIgnoreCase) || normalizedName.Contains("openaiofficial")) return "openai";
            if (normalizedName.Contains("anyrouter")) return "custom";
            if (normalizedName.Contains("rightcode")) return "rightcode";
            try
            {
                var settingsText = GetString(row, "settings_config");
                var settings = _json.DeserializeObject(settingsText) as Dictionary<string, object>;
                var config = settings == null ? "" : GetString(settings, "config");
                var modelProvider = GetTomlStringValue(config, "model_provider");
                if (!String.IsNullOrWhiteSpace(modelProvider)) return modelProvider;
            }
            catch { }
            return "";
        }

        private string InferNodeConfigValue(Dictionary<string, object> row, string key)
        {
            try
            {
                var settingsText = GetString(row, "settings_config");
                var settings = _json.DeserializeObject(settingsText) as Dictionary<string, object>;
                var config = settings == null ? "" : GetString(settings, "config");
                return GetTomlStringValue(config, key);
            }
            catch { return ""; }
        }

        private string InferNodeAuthValue(Dictionary<string, object> row, string key)
        {
            try
            {
                var settingsText = GetString(row, "settings_config");
                var settings = _json.DeserializeObject(settingsText) as Dictionary<string, object>;
                var auth = settings == null ? null : GetDict(settings, "auth");
                return auth == null ? "" : GetString(auth, key);
            }
            catch { return ""; }
        }

        private static string GetTomlStringValue(string text, string name)
        {
            if (String.IsNullOrWhiteSpace(text) || String.IsNullOrWhiteSpace(name)) return "";
            var pattern = "(?m)^\\s*" + Regex.Escape(name) + "\\s*=\\s*\"(?<value>[^\"]*)\"";
            var match = Regex.Match(text, pattern);
            return match.Success ? match.Groups["value"].Value : "";
        }

        private string SelectedCcSwitchNodeKey()
        {
            if (_ccSwitchCombo == null || _ccSwitchCombo.SelectedItem == null) return "";
            var node = _ccSwitchCombo.SelectedItem as CcSwitchNode;
            if (node != null) return !String.IsNullOrWhiteSpace(node.Name) ? node.Name : node.Id;
            return _ccSwitchCombo.SelectedItem.ToString();
        }

        private void SelectCcSwitchNode(string value)
        {
            if (_ccSwitchCombo == null || String.IsNullOrWhiteSpace(value)) return;
            foreach (var item in _ccSwitchCombo.Items)
            {
                var node = item as CcSwitchNode;
                if (node != null &&
                    (String.Equals(node.Id, value, StringComparison.OrdinalIgnoreCase) ||
                     String.Equals(node.Name, value, StringComparison.OrdinalIgnoreCase)))
                {
                    _ccSwitchCombo.SelectedItem = item;
                    return;
                }
                if (node == null && String.Equals(Convert.ToString(item), value, StringComparison.OrdinalIgnoreCase))
                {
                    _ccSwitchCombo.SelectedItem = item;
                    return;
                }
            }
        }

        private void UpdatePathText()
        {
            if (_pathText == null) return;
            _pathText.Inlines.Clear();
            _pathText.ToolTip = "点击可重新选择 Codex 或 cc-switch 数据库";

            _pathText.Inlines.Add(MakeStatusLink(
                String.IsNullOrWhiteSpace(_stateDb) ? "加载Codex\\state_5.sqlite" : "Codex: " + _stateDb,
                delegate { SelectCodexHome(); }));
            _pathText.Inlines.Add(new Run("    |    ") { Foreground = MutedBrush() });
            _pathText.Inlines.Add(MakeStatusLink(
                String.IsNullOrWhiteSpace(_ccSwitchDb) ? "加载cc-switch\\cc-switch.db" : "cc-switch: " + _ccSwitchDb,
                delegate { SelectCcSwitchDb(); }));
        }

        private Hyperlink MakeStatusLink(string text, RoutedEventHandler handler)
        {
            var link = new Hyperlink(new Run(text ?? ""))
            {
                Foreground = MutedBrush(),
                TextDecorations = null,
                Cursor = Cursors.Hand
            };
            link.MouseEnter += delegate
            {
                link.Foreground = PrimaryBrush();
                link.TextDecorations = TextDecorations.Underline;
            };
            link.MouseLeave += delegate
            {
                link.Foreground = MutedBrush();
                link.TextDecorations = null;
            };
            link.Click += handler;
            return link;
        }

        private void SaveConfigWithDetectedInfo(bool createIfMissing)
        {
            try
            {
                if (_suppressConfigSave) return;
                if (!createIfMissing && !_initialLoadComplete) return;
                if (!createIfMissing && !File.Exists(_configPath)) return;
                var dict = new Dictionary<string, object>();
                if (File.Exists(_configPath))
                {
                    try { dict = _json.DeserializeObject(File.ReadAllText(_configPath, Encoding.UTF8)) as Dictionary<string, object> ?? dict; }
                    catch { dict = new Dictionary<string, object>(); }
                }

                if (!dict.ContainsKey("_help")) dict["_help"] = BuildConfigHelp();
                dict["version"] = Program.AppVersion;
                dict["lastSavedAt"] = DateTimeOffset.Now.ToString("o");
                dict["codexHome"] = String.IsNullOrWhiteSpace(_stateDb) ? _config.CodexHome : Path.GetDirectoryName(_stateDb);
                dict["ccSwitchHome"] = String.IsNullOrWhiteSpace(_ccSwitchDb) ? _config.CcSwitchHome : Path.GetDirectoryName(_ccSwitchDb);
                dict["codexExe"] = _config.CodexExe ?? "";
                dict["defaultSourceProvider"] = SelectedProviderValue(_sourceCombo);
                dict["defaultTargetProvider"] = SelectedProviderValue(_targetCombo);
                dict["defaultCcSwitchNode"] = SelectedCcSwitchNodeKey();
                dict["launchModel"] = SelectedComboText(_modelCombo, "default");
                dict["launchReasoningEffort"] = SelectedComboText(_reasoningCombo, "default");
                dict["directoryFilter"] = SelectedFolderFilter();
                dict["limit"] = ParsePageSize();
                dict["conversationFontSize"] = GetConversationFontSize();
                dict["includeArchived"] = _includeArchivedBox != null && _includeArchivedBox.IsChecked == true;
                dict["disableAppsOnFast"] = _config.DisableAppsOnFast;
                dict["fastModeOnLaunch"] = _fastBox != null && _fastBox.IsChecked == true;
                dict["turnCompletePopup"] = _turnPopupBox == null ? _config.TurnCompletePopup : _turnPopupBox.IsChecked == true;
                dict["usePowerShellTerminal"] = _usePowerShellBox != null && _usePowerShellBox.IsChecked == true;
                dict["approvalNeverOnLaunch"] = _fullAccessBox != null && _fullAccessBox.IsChecked == true;
                dict["loadChatOnLaunch"] = _loadChatBox != null && _loadChatBox.IsChecked == true;
                dict["uiLanguage"] = _config.UiLanguage;
                dict["uiTheme"] = _config.UiTheme;
                dict["knownCodexHistoryProviders"] = BuildKnownProviders();
                dict["knownCcSwitchNodes"] = BuildKnownCcSwitchNodes();

                var json = _json.Serialize(dict);
                File.WriteAllText(_configPath, PrettyJson(json), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                SetStatus("保存配置失败：" + ex.Message);
            }
        }

        private Dictionary<string, object> BuildConfigHelp()
        {
            var help = new Dictionary<string, object>();
            help["howToUse"] = "这个文件是本机配置。点击软件里的【软件配置文件】会打开它；保存后软件会自动刷新。";
            help["codexHome"] = "包含 state_5.sqlite 和 sessions 文件夹的 .codex 目录。";
            help["ccSwitchHome"] = "cc-switch.db 所在目录。";
            help["codexExe"] = "codex.exe 的完整路径；如果 PATH 已经能找到 codex.exe，可以留空。";
            help["uiTheme"] = "界面皮肤。菜单显示为 Summer Ocean Breeze、Ocean Blue Serenity、Pastel Dreamland、Fresh Greens、Deep Sea；配置值可选：summer_ocean_breeze、ocean_blue_serenity、pastel_dreamland、fresh_greens、deep_sea。";
            help["security"] = "不要在这里写 API key、token、auth.json、config.toml 或 state_5.sqlite 内容。";
            return help;
        }

        private ArrayList BuildKnownProviders()
        {
            var arr = new ArrayList();
            if (_sourceCombo == null) return arr;
            foreach (var item in _sourceCombo.Items)
            {
                var p = item as ProviderItem;
                var label = p == null ? Convert.ToString(item) : p.Label;
                var value = p == null ? Convert.ToString(item) : p.Value;
                var dict = new Dictionary<string, object>();
                dict["label"] = label;
                dict["value"] = value;
                arr.Add(dict);
            }
            return arr;
        }

        private ArrayList BuildKnownCcSwitchNodes()
        {
            var arr = new ArrayList();
            foreach (var node in _ccSwitchNodes)
            {
                var dict = new Dictionary<string, object>();
                dict["id"] = node.Id;
                dict["name"] = node.Name;
                dict["historyProvider"] = node.HistoryProvider;
                dict["model"] = node.Model;
                dict["reasoningEffort"] = node.ReasoningEffort;
                dict["isCurrent"] = node.IsCurrent;
                arr.Add(dict);
            }
            return arr;
        }

        private string SelectedFolderFilter()
        {
            var folder = _folderCombo == null ? "" : _folderCombo.SelectedItem as string;
            return String.IsNullOrWhiteSpace(folder) || folder == AllFoldersLabel() ? "" : folder;
        }

        private static string SelectedComboText(ComboBox combo, string fallback)
        {
            if (combo == null || combo.SelectedItem == null) return fallback;
            var value = combo.SelectedItem.ToString();
            return String.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string PrettyJson(string json)
        {
            if (String.IsNullOrWhiteSpace(json)) return "{}";
            var sb = new StringBuilder();
            var indent = 0;
            var quoted = false;
            for (var i = 0; i < json.Length; i++)
            {
                var ch = json[i];
                if (ch == '"' && (i == 0 || json[i - 1] != '\\')) quoted = !quoted;
                if (!quoted && (ch == '{' || ch == '['))
                {
                    sb.Append(ch);
                    sb.AppendLine();
                    indent++;
                    sb.Append(new string(' ', indent * 2));
                }
                else if (!quoted && (ch == '}' || ch == ']'))
                {
                    sb.AppendLine();
                    indent--;
                    sb.Append(new string(' ', Math.Max(0, indent) * 2));
                    sb.Append(ch);
                }
                else if (!quoted && ch == ',')
                {
                    sb.Append(ch);
                    sb.AppendLine();
                    sb.Append(new string(' ', indent * 2));
                }
                else if (!quoted && ch == ':')
                {
                    sb.Append(": ");
                }
                else
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }

        private void ApplyFilters(bool resetPage)
        {
            ApplyFilters(resetPage, true);
        }

        private void ApplyFilters(bool resetPage, bool autoSelect)
        {
            if (resetPage) _pageIndex = 0;
            _pageSize = ParsePageSize();
            _filteredRows.Clear();
            var provider = SelectedProviderValue(_sourceCombo);
            var folder = _folderCombo.SelectedItem as string;
            var search = (_searchBox.Text ?? "").Trim();
            var includeArchived = _includeArchivedBox.IsChecked == true;
            foreach (var row in _allRows)
            {
                if (!includeArchived && row.Archived) continue;
                if (!String.IsNullOrWhiteSpace(provider) && provider != "__all__" && !String.Equals(ProviderValue(row.Provider), provider, StringComparison.OrdinalIgnoreCase)) continue;
                if (!String.IsNullOrWhiteSpace(folder) && folder != AllFoldersLabel() && !String.Equals(row.Cwd, folder, StringComparison.OrdinalIgnoreCase)) continue;
                if (!String.IsNullOrWhiteSpace(search))
                {
                    var hay = (row.Title + "\n" + row.Id + "\n" + row.Cwd + "\n" + row.Provider).ToLowerInvariant();
                    if (!hay.Contains(search.ToLowerInvariant())) continue;
                }
                _filteredRows.Add(row);
            }
            SortFilteredRowsByProject();
            RenderPage(autoSelect);
        }

        private void SortFilteredRowsByProject()
        {
            var ordered = _filteredRows
                .GroupBy(row => ProjectSortKey(row), StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    Latest = group.Max(row => row.UpdatedMs),
                    Name = group.First().ProjectGroup,
                    Rows = group
                        .OrderByDescending(row => row.UpdatedMs)
                        .ThenBy(row => row.Title ?? "", StringComparer.OrdinalIgnoreCase)
                        .ThenBy(row => row.Id ?? "", StringComparer.OrdinalIgnoreCase)
                        .ToList()
                })
                .OrderByDescending(group => group.Latest)
                .ThenBy(group => group.Name ?? "", StringComparer.OrdinalIgnoreCase)
                .SelectMany(group => group.Rows)
                .ToList();
            _filteredRows.Clear();
            _filteredRows.AddRange(ordered);
        }

        private static string ProjectSortKey(SessionRow row)
        {
            if (row == null || String.IsNullOrWhiteSpace(row.Cwd)) return "__missing_project__";
            return NormalizePath(row.Cwd).ToLowerInvariant();
        }

        private void RenderPage()
        {
            RenderPage(true);
        }

        private void RenderPage(bool autoSelect)
        {
            _pageRows.Clear();
            var pageCount = Math.Max(1, (int)Math.Ceiling(_filteredRows.Count / (double)_pageSize));
            if (_pageIndex >= pageCount) _pageIndex = pageCount - 1;
            if (_pageIndex < 0) _pageIndex = 0;
            var start = _pageIndex * _pageSize;
            var offset = 0;
            foreach (var row in _filteredRows.Skip(start).Take(_pageSize))
            {
                row.DisplayNumber = (start + offset + 1).ToString();
                _pageRows.Add(row);
                offset++;
            }
            _countText.Text = IsEnglish() ? "Page " + (_pageIndex + 1) + " / " + pageCount : "第 " + (_pageIndex + 1) + " / " + pageCount + " 页";
            _countText.ToolTip = _filteredRows.Count + " / " + _allRows.Count + (IsEnglish() ? " items" : " 条");
            _prevButton.IsEnabled = _pageIndex > 0;
            _nextButton.IsEnabled = _pageIndex < pageCount - 1;
            if (_pageRows.Count > 0 && autoSelect) _sessionList.SelectedIndex = 0;
            else if (_pageRows.Count > 0)
            {
                _sessionList.SelectedIndex = -1;
                SetDetailPlain("请选择左侧会话查看详情。");
            }
            else SetDetailPlain("没有匹配的会话。");
        }

        private void MovePage(int delta)
        {
            _pageIndex += delta;
            RenderPage();
        }

        private void SetAllPageSelected(bool selected)
        {
            foreach (var row in _pageRows) row.Selected = selected;
            _sessionList.Items.Refresh();
            SetStatus(selected ? "已勾选当前页。" : "已清空当前页勾选。");
        }

        private void SwapProviders()
        {
            var source = SelectedProviderValue(_sourceCombo);
            var target = SelectedProviderValue(_targetCombo);
            if (!String.IsNullOrWhiteSpace(target)) SelectProvider(_sourceCombo, target);
            if (!String.IsNullOrWhiteSpace(source)) SelectProvider(_targetCombo, source);
            SaveConfigWithDetectedInfo(false);
            ApplyFilters(true);
        }

        private void SelectCodexHome()
        {
            try
            {
                var selected = SelectFolderLikePath("加载codex账号：请选择包含 state_5.sqlite 的 .codex 文件夹", GetCodexHome());
                if (String.IsNullOrWhiteSpace(selected)) return;
                var resolved = ResolveCodexHomeFromSelection(selected);
                if (String.IsNullOrWhiteSpace(resolved)) throw new InvalidOperationException("没有在所选目录或其父目录中找到 state_5.sqlite。");
                _config.CodexHome = resolved;
                _stateDb = Path.Combine(resolved, "state_5.sqlite");
                RefreshAll();
                SetStatus("已加载 Codex 账号目录：" + resolved);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "加载codex账号失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectCcSwitchDb()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.Title = "请选择 cc-switch.db 文件";
                dialog.Filter = "cc-switch database (cc-switch.db)|cc-switch.db|SQLite database (*.db)|*.db|All files (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.RestoreDirectory = true;
                if (!String.IsNullOrWhiteSpace(_ccSwitchDb) && File.Exists(_ccSwitchDb)) dialog.InitialDirectory = Path.GetDirectoryName(_ccSwitchDb);
                if (dialog.ShowDialog(this) != true) return;
                var resolved = ResolveCcSwitchDbFromSelection(dialog.FileName);
                if (String.IsNullOrWhiteSpace(resolved)) throw new InvalidOperationException("没有找到可识别的 cc-switch.db 或 .db 文件。");
                _config.CcSwitchHome = Path.GetDirectoryName(resolved);
                _ccSwitchDb = resolved;
                RefreshCcSwitchHistorySettings();
                LoadCcSwitchNodes();
                PopulateCcSwitchCombo();
                SaveConfigWithDetectedInfo(true);
                UpdatePathText();
                SetStatus("已加载 cc-switch.db 文件：" + resolved + "，Codex 节点 " + _ccSwitchNodes.Count + " 个。");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "加载 cc-switch.db 失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string SelectFolderLikePath(string title, string initialDirectory)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Title = title;
            dialog.CheckFileExists = false;
            dialog.CheckPathExists = true;
            dialog.ValidateNames = false;
            dialog.RestoreDirectory = true;
            dialog.FileName = "选择此文件夹";
            dialog.Filter = "文件夹|*.folder";
            if (!String.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory)) dialog.InitialDirectory = initialDirectory;
            if (dialog.ShowDialog(this) != true) return "";
            var selected = NormalizePath(dialog.FileName);
            if (Directory.Exists(selected)) return selected;
            var parent = Path.GetDirectoryName(selected);
            return Directory.Exists(parent) ? parent : "";
        }

        private void OpenSessionDirectory()
        {
            var row = GetActiveDetailRow();
            if (row != null && File.Exists(row.FilePath))
            {
                OpenPath(Path.GetDirectoryName(row.FilePath));
                return;
            }
            var home = GetCodexHome();
            var sessions = String.IsNullOrWhiteSpace(home) ? "" : Path.Combine(home, "sessions");
            OpenPath(Directory.Exists(sessions) ? sessions : home);
        }

        private void EnsureAndOpenConfig()
        {
            if (!File.Exists(_configPath)) SaveConfigWithDetectedInfo(true);
            OpenPath(_configPath);
        }

        private void ShowHelpWindow()
        {
            try { System.Windows.Clipboard.SetText("state_5.sqlite\r\ncc-switch.db"); } catch { }
            var win = new Window
            {
                Title = "帮助",
                Owner = this,
                Width = 820,
                Height = 640,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = AppBackgroundBrush(),
                FontFamily = new FontFamily("Microsoft YaHei UI")
            };
            var text = new TextBox
            {
                Text = BuildHelpText(),
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(22),
                FontSize = 14,
                Background = SurfaceBrush(),
                Foreground = InkBrush()
            };
            win.Content = text;
            win.ShowDialog();
        }

        private string BuildHelpText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("AI 会话管理器");
            sb.AppendLine("版本：" + Program.AppVersion);
            sb.AppendLine("作者：" + Program.AppAuthor);
            sb.AppendLine("GitHub：" + Program.GitHubUrl);
            sb.AppendLine();
            sb.AppendLine("已复制 state_5.sqlite 和 cc-switch.db 到剪贴板，可直接粘贴到 Everything 搜索。");
            sb.AppendLine();
            sb.AppendLine("【加载codex账号】");
            sb.AppendLine("请选择包含 state_5.sqlite 的 .codex 文件夹；如果误选 sessions 或子目录，软件会自动向上查找。");
            sb.AppendLine();
            sb.AppendLine("【cc-switch.db 文件】");
            sb.AppendLine("点击【加载cc-switch.db文件】选择 cc-switch.db，软件会读取 Any Router、RightCode、OpenAI Official 等 Codex 节点。");
            sb.AppendLine();
            sb.AppendLine("【启动终端】");
            sb.AppendLine("勾选【+ 会话】时执行 codex resume <thread-id>；取消时在当前目录新建会话。");
            sb.AppendLine("勾选【Fast】时追加 service_tier=fast；勾选【完全访问】时追加 Codex bypass 参数。");
            sb.AppendLine("【PowerShell】决定启动终端用 PowerShell 还是 CMD。");
            sb.AppendLine();
            sb.AppendLine("【配置文件】");
            sb.AppendLine("软件会自动生成 ai-session-manager-config.json，并写入检测到的 Codex 账号、cc-switch 节点和默认选项。不要写 API key、token、auth.json 或 config.toml 内容。");
            return sb.ToString();
        }

        private void ShowSessionHistoryWindow()
        {
            var win = new Window
            {
                Title = "完整会话历史 - " + _filteredRows.Count + " / " + _allRows.Count + " 条",
                Owner = this,
                Width = 1120,
                Height = 720,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = AppBackgroundBrush()
            };
            var grid = new Grid { Margin = new Thickness(14) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(220) });
            var table = new DataGrid
            {
                ItemsSource = _filteredRows.ToList(),
                AutoGenerateColumns = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Background = SurfaceBrush(),
                BorderBrush = ThemeBorderBrush()
            };
            table.Columns.Add(new DataGridTextColumn { Header = "时间", Binding = new Binding("Updated"), Width = 130 });
            table.Columns.Add(new DataGridTextColumn { Header = "账号", Binding = new Binding("Provider"), Width = 100 });
            table.Columns.Add(new DataGridTextColumn { Header = "会话", Binding = new Binding("Title"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            table.Columns.Add(new DataGridTextColumn { Header = "目录", Binding = new Binding("Cwd"), Width = 260 });
            table.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new Binding("Id"), Width = 260 });
            var detail = new TextBox
            {
                Margin = new Thickness(0, 10, 0, 0),
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12.5,
                Background = SurfaceBrush(),
                Foreground = InkBrush(),
                BorderBrush = ThemeBorderBrush()
            };
            table.SelectionChanged += delegate
            {
                var row = table.SelectedItem as SessionRow;
                detail.Text = row == null ? "" : BuildDetailText(row);
            };
            Grid.SetRow(table, 0);
            Grid.SetRow(detail, 1);
            grid.Children.Add(table);
            grid.Children.Add(detail);
            win.Content = grid;
            if (_filteredRows.Count > 0) table.SelectedIndex = 0;
            win.ShowDialog();
        }

        private async Task CheckUpdateAsync()
        {
            await RunBusyAsync("正在检查更新...", delegate
            {
                if (Directory.Exists(Path.Combine(_rootDir, ".git")))
                {
                    var status = RunProcess("git.exe", "status --porcelain", _rootDir, 20000).Output.Trim();
                    if (String.IsNullOrWhiteSpace(status))
                    {
                        var output = RunProcess("git.exe", "pull --ff-only origin main", _rootDir, 120000).Output.Trim();
                        Dispatcher.Invoke(new Action(delegate { System.Windows.MessageBox.Show(output, "检查更新", MessageBoxButton.OK, MessageBoxImage.Information); }));
                        return;
                    }
                }
                Dispatcher.Invoke(new Action(delegate { OpenPath(Program.GitHubUrl); }));
            });
        }

        private int ParsePageSize()
        {
            if (_pageSizeCombo == null || _pageSizeCombo.SelectedItem == null) return 30;
            var m = Regex.Match(_pageSizeCombo.SelectedItem.ToString(), "\\d+");
            return m.Success ? Math.Max(10, Math.Min(200, Int32.Parse(m.Value))) : 30;
        }

        private void ShowSelectedDetail()
        {
            var row = _sessionList.SelectedItem as SessionRow;
            if (row == null) return;
            WriteDiagnostic("ShowSelectedDetail id='" + row.Id + "' title='" + ShortenForLog(row.Title, 80) + "'.");
            RenderDetail(row);
            _detailBox.ScrollToHome();
        }

        private void RenderDetail(SessionRow row)
        {
            _detailRow = row;
            var doc = CreateDetailDocument();
            _detailNavTargets.Clear();

            AddSessionMetaBlock(doc, row);

            var resolvedPath = ResolveConversationPath(row);
            var entries = ReadConversationEntries(resolvedPath, 0);
            WriteDiagnostic("RenderDetail id='" + row.Id + "' entries=" + entries.Count + " path='" + resolvedPath + "'.");
            if (entries.Count == 0)
            {
                var fallbackPreview = BuildSessionFallbackPreview(row);
                if (!String.IsNullOrWhiteSpace(fallbackPreview))
                    AddMessageBlock(doc, "会话预览", fallbackPreview, SteelBrush(), SoftPressedBrush());
                AddMessageBlock(doc, "提示", BuildMissingConversationMessage(row, resolvedPath), AccentBrush(), ColorBrush(CurrentTheme().SystemBackground));
            }
            else
            {
                var qaCount = entries.Count(e => IsUserEntry(e) && HasVisibleUserMessageText(e.Text ?? ""));
                AddSectionCaption(doc, "问答次数  " + qaCount + " 轮");
                var leadingGroup = new List<ConversationEntry>();
                var responseGroup = new List<ConversationEntry>();
                var seenUser = false;
                foreach (var entry in entries)
                {
                    if (IsUserEntry(entry))
                    {
                        if (seenUser && responseGroup.Count > 0)
                        {
                            AddResponseGroupBlock(doc, responseGroup);
                            responseGroup.Clear();
                        }
                        seenUser = true;
                        var preview = BuildNavigationPreview(entry);
                        var target = AddMessageBlock(doc, entry.Role, entry.Text, RoleBrush(entry.Role), RoleBackground(entry.Role), true);
                        if (!String.IsNullOrWhiteSpace(preview))
                            _detailNavTargets.Add(new DetailNavItem { Target = target, Preview = preview });
                    }
                    else
                    {
                        if (seenUser) responseGroup.Add(entry);
                        else leadingGroup.Add(entry);
                    }
                }
                if (responseGroup.Count > 0)
                {
                    AddResponseGroupBlock(doc, responseGroup);
                }
                if (leadingGroup.Count > 0)
                {
                    AddResponseGroupBlock(doc, leadingGroup, seenUser ? "前置记录" : "会话记录");
                }
            }
            _detailBox.Document = doc;
            WriteDiagnostic("RenderDetail navTargets=" + _detailNavTargets.Count + " id='" + row.Id + "'.");
            UpdateDetailNavigation();
        }

        private SessionRow GetActiveDetailRow()
        {
            if (_detailRow != null) return _detailRow;
            return _sessionList == null ? null : _sessionList.SelectedItem as SessionRow;
        }

        private string BuildSessionFallbackPreview(SessionRow row)
        {
            if (row == null) return "";
            return FirstNonEmpty(row.PreviewText, row.Title, row.Id);
        }

        private string BuildDetailText(SessionRow row)
        {
            var sb = new StringBuilder();
            sb.AppendLine("会话：" + row.Title);
            sb.AppendLine("时间：" + row.Updated);
            sb.AppendLine("账号：" + row.Provider);
            sb.AppendLine("状态：" + (row.Archived ? "archived" : "active"));
            sb.AppendLine("ID：" + row.Id);
            if (!String.IsNullOrWhiteSpace(row.Cwd)) sb.AppendLine("项目目录：" + row.Cwd);
            var resolvedPath = ResolveConversationPath(row);
            if (!String.IsNullOrWhiteSpace(resolvedPath)) sb.AppendLine("文件：" + resolvedPath);
            sb.AppendLine("来源：" + row.Source);
            sb.AppendLine();
            sb.AppendLine(new string('=', 72));
            sb.AppendLine();
            var entries = ReadConversationEntries(resolvedPath, 0);
            if (entries.Count == 0)
            {
                sb.AppendLine(BuildMissingConversationMessage(row, resolvedPath));
                return sb.ToString();
            }
            foreach (var entry in entries)
            {
                sb.AppendLine(NormalizeRoleLabel(entry.Role));
                sb.AppendLine(entry.Text);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private void SetDetailPlain(string text)
        {
            _detailRow = null;
            var doc = CreateDetailDocument();
            doc.Blocks.Add(new Paragraph(new Run(text ?? "")));
            _detailNavTargets.Clear();
            UpdateDetailNavigation();
            if (_detailBox != null) _detailBox.Document = doc;
        }

        private FlowDocument CreateDetailDocument()
        {
            return new FlowDocument
            {
                PagePadding = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = GetConversationFontSize(),
                Foreground = InkBrush()
            };
        }

        private int GetConversationFontSize()
        {
            return Math.Max(10, Math.Min(24, _config == null ? 14 : _config.ConversationFontSize));
        }

        private void ApplyFontSizeInput()
        {
            if (_fontSizeBox == null) return;
            int value;
            if (!Int32.TryParse((_fontSizeBox.Text ?? "").Trim(), out value)) value = GetConversationFontSize();
            value = Math.Max(10, Math.Min(24, value));
            _config.ConversationFontSize = value;
            _fontSizeBox.Text = value.ToString();
            SaveConfigWithDetectedInfo(false);
            var row = GetActiveDetailRow();
            if (row != null) RenderDetail(row);
            SetStatus("会话字号已设为 " + value + "。");
        }

        private void AddMetaLine(Paragraph paragraph, string label, string value, Brush valueBrush)
        {
            AddMetaLine(paragraph, label, value, valueBrush, false);
        }

        private void AddMetaLine(Paragraph paragraph, string label, string value, Brush valueBrush, bool smallMuted)
        {
            var size = smallMuted ? Math.Max(11, GetConversationFontSize() - 1) : GetConversationFontSize();
            paragraph.Inlines.Add(new Run(label + "：") { Foreground = MutedBrush(), FontWeight = FontWeights.SemiBold, FontSize = size });
            paragraph.Inlines.Add(new Run(value ?? "") { Foreground = smallMuted ? MutedBrush() : valueBrush, FontSize = size });
            paragraph.Inlines.Add(new LineBreak());
        }

        private void AddSessionMetaBlock(FlowDocument doc, SessionRow row)
        {
            var section = new Section
            {
                Background = SoftHoverBrush(),
                BorderBrush = AquaBrush(),
                BorderThickness = new Thickness(3, 0, 0, 0),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var meta = new Paragraph { Margin = new Thickness(0), LineHeight = 19 };
            AddMetaLine(meta, "会话", row.Title, InkBrush());
            AddMetaLine(meta, "时间", row.Updated, SteelBrush());
            AddMetaLine(meta, "账号", row.Provider, AccentBrush());
            AddMetaLine(meta, "状态", row.Archived ? "archived" : "active", row.Archived ? MutedBrush() : SuccessBrush());
            AddMetaLine(meta, "ID", row.Id, SteelBrush());
            if (!String.IsNullOrWhiteSpace(row.Cwd)) AddMetaLine(meta, "项目目录", row.Cwd, SteelBrush());
            if (!String.IsNullOrWhiteSpace(row.FilePath)) AddMetaLine(meta, "文件", row.FilePath, File.Exists(row.FilePath) ? SteelBrush() : AccentBrush());
            AddMetaLine(meta, "来源", row.Source, SteelBrush());
            section.Blocks.Add(meta);
            doc.Blocks.Add(section);
        }

        private void AddSectionCaption(FlowDocument doc, string text)
        {
            var p = new Paragraph(new Run(text))
            {
                Margin = new Thickness(0, 6, 0, 10),
                Foreground = MutedBrush(),
                FontWeight = FontWeights.SemiBold
            };
            doc.Blocks.Add(p);
        }

        private FrameworkContentElement AddMessageBlock(FlowDocument doc, string role, string text, Brush accent, Brush background, bool userRequest = false)
        {
            var hasUserText = userRequest && HasVisibleUserMessageText(text ?? "");
            var autoUserMetadata = userRequest && !hasUserText && IsAutoGeneratedUserMetadataOnly(text ?? "");
            var emphasizedUser = userRequest && hasUserText;
            var section = new Section
            {
                Background = emphasizedUser ? SoftHoverBrush() : (autoUserMetadata ? ColorBrush(CurrentTheme().SoftHover) : background),
                BorderBrush = emphasizedUser ? accent : (autoUserMetadata ? ThemeBorderBrush() : accent),
                BorderThickness = emphasizedUser ? new Thickness(0, 0, 3, 0) : new Thickness(3, 0, 0, 0),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 10)
            };
            section.Blocks.Add(new Paragraph(new Run(autoUserMetadata ? "自动信息" : (emphasizedUser ? "用户" : NormalizeRoleLabel(role))))
            {
                Foreground = autoUserMetadata ? MutedBrush() : accent,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            });

            if (userRequest) AddUserRequestParagraphs(section, text ?? "");
            else section.Blocks.Add(CreateSelectableParagraph(text ?? "", InkBrush(), new Thickness(0)));

            doc.Blocks.Add(section);
            return section;
        }

        private void AddUserRequestParagraphs(Section section, string text)
        {
            var marker = "## My request for Codex:";
            var index = (text ?? "").IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                var segments = BuildUserMessageSegments(text ?? "");
                if (segments.Count == 0)
                {
                    section.Blocks.Add(CreateMutedSmallParagraph(text ?? "", new Thickness(0)));
                    return;
                }
                foreach (var segment in segments)
                {
                    var segmentText = (segment.Text ?? "").Trim();
                    if (String.IsNullOrWhiteSpace(segmentText)) continue;
                    section.Blocks.Add(segment.IsUserText
                        ? CreateUserRequestParagraph(segmentText)
                        : CreateMutedSmallParagraph(segmentText, new Thickness(0, 0, 0, 8)));
                }
                return;
            }

            var before = text.Substring(0, index).Trim();
            var request = text.Substring(index + marker.Length).Trim();
            if (!String.IsNullOrWhiteSpace(before))
            {
                section.Blocks.Add(CreateMutedSmallParagraph(before, new Thickness(0, 0, 0, 8)));
            }

            AddUserRequestContentBlocks(section, request);
        }

        private static List<UserMessageSegment> BuildUserMessageSegments(string text)
        {
            var segments = new List<UserMessageSegment>();
            if (String.IsNullOrWhiteSpace(text)) return segments;
            var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = normalized.Split('\n');
            var inEnvironment = false;
            var inImage = false;
            var inFiles = false;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (inEnvironment)
                {
                    AppendUserMessageSegment(segments, false, line);
                    if (trimmed.IndexOf("</environment_context>", StringComparison.OrdinalIgnoreCase) >= 0) inEnvironment = false;
                    continue;
                }
                if (trimmed.StartsWith("<environment_context>", StringComparison.OrdinalIgnoreCase))
                {
                    AppendUserMessageSegment(segments, false, line);
                    if (trimmed.IndexOf("</environment_context>", StringComparison.OrdinalIgnoreCase) < 0) inEnvironment = true;
                    continue;
                }
                if (inImage)
                {
                    AppendUserMessageSegment(segments, false, line);
                    if (trimmed.IndexOf("</image>", StringComparison.OrdinalIgnoreCase) >= 0) inImage = false;
                    continue;
                }
                if (Regex.IsMatch(trimmed, @"^<image\b", RegexOptions.IgnoreCase))
                {
                    AppendUserMessageSegment(segments, false, line);
                    if (trimmed.IndexOf("</image>", StringComparison.OrdinalIgnoreCase) < 0) inImage = true;
                    continue;
                }
                if (trimmed.StartsWith("# Files mentioned by the user:", StringComparison.OrdinalIgnoreCase))
                {
                    inFiles = true;
                    AppendUserMessageSegment(segments, false, line);
                    continue;
                }
                if (inFiles)
                {
                    if (trimmed.Length == 0 || trimmed.StartsWith("## ", StringComparison.Ordinal))
                    {
                        AppendUserMessageSegment(segments, false, line);
                        continue;
                    }
                    inFiles = false;
                }
                AppendUserMessageSegment(segments, true, line);
            }
            return segments.Where(s => !String.IsNullOrWhiteSpace(s.Text)).ToList();
        }

        private static void AppendUserMessageSegment(List<UserMessageSegment> segments, bool isUserText, string line)
        {
            if (segments.Count == 0 || segments[segments.Count - 1].IsUserText != isUserText)
            {
                segments.Add(new UserMessageSegment { IsUserText = isUserText, Text = line + Environment.NewLine });
                return;
            }
            segments[segments.Count - 1].Text += line + Environment.NewLine;
        }

        private bool HasVisibleUserMessageText(string text)
        {
            var marker = "## My request for Codex:";
            var index = (text ?? "").IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) return !String.IsNullOrWhiteSpace(CleanUserPreviewText(text.Substring(index + marker.Length)));
            return BuildUserMessageSegments(text ?? "").Any(s => s.IsUserText && !String.IsNullOrWhiteSpace(CleanUserPreviewText(s.Text)));
        }

        private void AddUserRequestContentBlocks(Section section, string text)
        {
            text = text ?? "";
            var imagePattern = new Regex(@"<image\b[\s\S]*?</image>", RegexOptions.IgnoreCase);
            var position = 0;
            var addedRequest = false;
            foreach (Match match in imagePattern.Matches(text))
            {
                var userText = text.Substring(position, match.Index - position).Trim();
                if (!String.IsNullOrWhiteSpace(userText))
                {
                    section.Blocks.Add(CreateUserRequestParagraph(userText));
                    addedRequest = true;
                }
                section.Blocks.Add(CreateMutedSmallParagraph(match.Value.Trim(), new Thickness(0, addedRequest ? 6 : 0, 0, 6)));
                position = match.Index + match.Length;
            }

            var tail = text.Substring(position).Trim();
            if (!String.IsNullOrWhiteSpace(tail))
            {
                section.Blocks.Add(CreateUserRequestParagraph(tail));
                addedRequest = true;
            }
            if (!addedRequest && String.IsNullOrWhiteSpace(text))
                section.Blocks.Add(CreateUserRequestParagraph(""));
        }

        private Paragraph CreateUserRequestParagraph(string text)
        {
            return new Paragraph(new Run(text ?? ""))
            {
                Background = ColorBrush(CurrentTheme().UserBackground),
                BorderBrush = AquaBrush(),
                BorderThickness = new Thickness(1),
                Foreground = InkBrush(),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0),
                Padding = new Thickness(8, 6, 8, 6)
            };
        }

        private Paragraph CreateSelectableParagraph(string text, Brush foreground, Thickness margin)
        {
            return CreateSelectableParagraph(text, foreground, margin, false);
        }

        private Paragraph CreateSelectableParagraph(string text, Brush foreground, Thickness margin, bool small)
        {
            return new Paragraph(new Run(text ?? ""))
            {
                Foreground = foreground,
                Margin = margin,
                FontSize = small ? Math.Max(11, GetConversationFontSize() - 1) : GetConversationFontSize(),
                LineHeight = (small ? Math.Max(11, GetConversationFontSize() - 1) : GetConversationFontSize()) + 6
            };
        }

        private Paragraph CreateMutedSmallParagraph(string text, Thickness margin)
        {
            var size = Math.Max(10, GetConversationFontSize() - 2);
            return new Paragraph(new Run(text ?? ""))
            {
                Foreground = MutedSmallBrush(),
                Margin = margin,
                FontSize = size,
                LineHeight = size + 5
            };
        }

        private static bool IsAutoGeneratedUserMetadataOnly(string text)
        {
            var trimmed = (text ?? "").TrimStart();
            return trimmed.StartsWith("<environment_context>", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("# Files mentioned by the user:", StringComparison.OrdinalIgnoreCase);
        }

        private FrameworkContentElement AddResponseGroupBlock(FlowDocument doc, List<ConversationEntry> entries)
        {
            return AddResponseGroupBlock(doc, entries, "回复");
        }

        private FrameworkContentElement AddResponseGroupBlock(FlowDocument doc, List<ConversationEntry> entries, string titlePrefix)
        {
            var section = new Section
            {
                Background = ColorBrush(CurrentTheme().SoftHover),
                BorderBrush = ThemeBorderBrush(),
                BorderThickness = new Thickness(3, 0, 0, 0),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var headerPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var toggle = new Button
            {
                Content = "展开",
                Padding = new Thickness(10, 4, 10, 4),
                MinWidth = 54,
                MinHeight = 28,
                Margin = new Thickness(8, 0, 0, 0),
                Background = SurfaceBrush(),
                BorderBrush = AquaBrush(),
                Foreground = InkBrush(),
                Cursor = Cursors.Hand,
                FontWeight = FontWeights.SemiBold,
                Focusable = true,
                ToolTip = "展开或折叠这组回复"
            };
            ApplyButtonChrome(toggle);
            DockPanel.SetDock(toggle, Dock.Right);
            headerPanel.Children.Add(toggle);
            headerPanel.Children.Add(new TextBlock
            {
                Text = BuildResponseGroupTitle(entries, titlePrefix),
                Foreground = SteelBrush(),
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
            section.Blocks.Add(new BlockUIContainer(headerPanel) { Margin = new Thickness(0) });

            var bodySection = new Section { Margin = new Thickness(0) };
            section.Blocks.Add(bodySection);
            var expanded = false;
            RenderResponseGroupBody(bodySection, entries, expanded);
            Action toggleResponse = delegate
            {
                expanded = !expanded;
                toggle.Content = expanded ? "折叠" : "展开";
                RenderResponseGroupBody(bodySection, entries, expanded);
            };
            toggle.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                toggleResponse();
                e.Handled = true;
            };
            toggle.Click += delegate { toggleResponse(); };
            toggle.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter || e.Key == Key.Space)
                {
                    toggleResponse();
                    e.Handled = true;
                }
            };

            doc.Blocks.Add(section);
            return section;
        }

        private string BuildResponseGroupTitle(List<ConversationEntry> entries, string titlePrefix)
        {
            var contextCount = entries.Count(e => IsContextRole(e.Role));
            var assistantCount = entries.Count(e => String.Equals(e.Role, "assistant", StringComparison.OrdinalIgnoreCase));
            var toolCount = entries.Count(e => String.Equals(e.Role, "tool", StringComparison.OrdinalIgnoreCase));
            var otherCount = entries.Count - contextCount - assistantCount - toolCount;
            var parts = new List<string>();
            if (contextCount > 0) parts.Add("自动信息 " + contextCount);
            if (assistantCount > 0) parts.Add("助手 " + assistantCount);
            if (toolCount > 0) parts.Add("工具 " + toolCount);
            if (otherCount > 0) parts.Add("补充 " + otherCount);
            return (String.IsNullOrWhiteSpace(titlePrefix) ? "回复" : titlePrefix) + "：" + String.Join(" / ", parts.ToArray());
        }

        private void RenderResponseGroupBody(Section bodySection, List<ConversationEntry> entries, bool expanded)
        {
            bodySection.Blocks.Clear();
            var count = expanded ? entries.Count : Math.Min(entries.Count, 3);
            for (var i = 0; i < count; i++)
            {
                AddResponseEntryBlock(bodySection, entries[i], !expanded);
            }
            if (!expanded && entries.Count > count)
            {
                bodySection.Blocks.Add(new Paragraph(new Run("已折叠 " + (entries.Count - count) + " 项，点击右侧【展开】查看全部。"))
                {
                    Foreground = MutedBrush(),
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }
        }

        private void AddResponseEntryBlock(Section parent, ConversationEntry entry, bool preview)
        {
            var role = entry == null ? "" : entry.Role;
            var isContext = IsContextRole(role);
            var accent = isContext ? SteelBrush() : RoleBrush(role);
            var background = isContext ? ColorBrush(CurrentTheme().SoftHover) : RoleBackground(role);
            var section = new Section
            {
                Background = background,
                BorderBrush = accent,
                BorderThickness = new Thickness(3, 0, 0, 0),
                Padding = new Thickness(9, 6, 9, 6),
                Margin = new Thickness(0, 0, 0, 8)
            };
            section.Blocks.Add(new Paragraph(new Run(isContext ? "自动信息" : NormalizeRoleLabel(role)))
            {
                Foreground = accent,
                FontWeight = FontWeights.Bold,
                FontSize = isContext ? Math.Max(11, GetConversationFontSize() - 1) : GetConversationFontSize(),
                Margin = new Thickness(0, 0, 0, 4)
            });
            var text = entry == null ? "" : entry.Text ?? "";
            if (preview && text.Length > 520) text = text.Substring(0, 520).TrimEnd() + "...";
            section.Blocks.Add(CreateSelectableParagraph(text, isContext ? MutedBrush() : InkBrush(), new Thickness(0), isContext));
            parent.Blocks.Add(section);
        }

        private static bool IsContextRole(string role)
        {
            role = (role ?? "").ToLowerInvariant();
            return role == "developer" || role == "system" || String.IsNullOrWhiteSpace(role);
        }

        private static bool IsUserEntry(ConversationEntry entry)
        {
            return entry != null && String.Equals(entry.Role, "user", StringComparison.OrdinalIgnoreCase);
        }

        private string BuildResponseGroupText(List<ConversationEntry> entries, bool preview)
        {
            var sb = new StringBuilder();
            foreach (var entry in entries)
            {
                var text = entry.Text ?? "";
                if (preview && text.Length > 420) text = text.Substring(0, 420).TrimEnd() + "...";
                sb.AppendLine(NormalizeRoleLabel(entry.Role));
                sb.AppendLine(text);
                sb.AppendLine();
                if (preview && sb.Length > 900)
                {
                    sb.AppendLine("...");
                    break;
                }
            }
            return sb.ToString().TrimEnd();
        }

        private void UpdateDetailNavigation()
        {
            if (_detailNav == null) return;
            if (_detailNavPopup != null) _detailNavPopup.IsOpen = false;
            _activeDetailNavPreviewIndex = -1;
            _activeDetailNavPreviewSignature = "";
            _detailNavPreviewShowPending = false;
            _pendingDetailNavPreviewIndex = -1;
            _pendingDetailNavPreviewIndexes = new List<int>();
            _pendingDetailNavPreviewTarget = null;
            _lastLoggedDetailNavPointerIndex = -1;
            _lastLoggedDetailNavPopupOpen = false;
            _detailNavPreviewHideToken++;
            _detailNavPreviewShowToken++;
            _detailNav.Children.Clear();
            _detailNavVisibleIndexes.Clear();
            var count = _detailNavTargets.Count;
            WriteDiagnostic("UpdateDetailNavigation targets=" + count + ".");
            if (count <= 1) return;

            var indexes = BuildNavigationIndexes(count, MaxDetailNavigationButtons);
            _detailNavVisibleIndexes.AddRange(indexes);
            foreach (var index in indexes)
            {
                var item = _detailNavTargets[index];
                var button = MakeDetailNavigationButton(index + 1, count, index, indexes, delegate
                {
                    item.Target.BringIntoView();
                    if (_detailBox != null) _detailBox.Focus();
                    SetStatus("已跳转到会话内容第 " + (index + 1) + " / " + count + " 条。");
                });
                _detailNav.Children.Add(button);
            }
        }

        private static List<int> BuildNavigationIndexes(int count, int maxCount)
        {
            var indexes = new List<int>();
            if (count <= 0 || maxCount <= 0) return indexes;
            if (count <= maxCount)
            {
                for (var i = 0; i < count; i++) indexes.Add(i);
                return indexes;
            }

            var seen = new HashSet<int>();
            for (var i = 0; i < maxCount; i++)
            {
                var index = (int)Math.Round(i * (count - 1) / (double)(maxCount - 1));
                if (seen.Add(index)) indexes.Add(index);
            }
            return indexes;
        }

        private Button MakeDetailNavigationButton(int number, int total, int activeIndex, List<int> visibleIndexes, RoutedEventHandler handler)
        {
            var b = new Button
            {
                Content = "",
                Width = 30,
                Height = 14,
                Margin = new Thickness(0),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = SteelBrush(),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Cursor = Cursors.Hand
            };
            ApplyDetailNavigationButtonChrome(b);
            AttachClickFeedback(b, "", handler);
            return b;
        }

        private void ShowNavigationPreviewFromPointer(MouseEventArgs e)
        {
            if (_detailNav == null || _detailNavVisibleIndexes.Count == 0) return;
            var navHeight = _detailNav.ActualHeight;
            if (navHeight <= 0) return;
            var position = e.GetPosition(_detailNav);
            var slotHeight = Math.Max(1.0, navHeight / Math.Max(1, _detailNavVisibleIndexes.Count));
            var y = Math.Max(0, Math.Min(navHeight - 0.1, position.Y));
            var slot = Math.Max(0, Math.Min(_detailNavVisibleIndexes.Count - 1, (int)Math.Floor(y / slotHeight)));
            var activeIndex = _detailNavVisibleIndexes[slot];
            var target = _detailNavHitBox ?? (UIElement)_detailNav;
            var popupOpen = _detailNavPopup != null && _detailNavPopup.IsOpen;
            if (activeIndex != _lastLoggedDetailNavPointerIndex || popupOpen != _lastLoggedDetailNavPopupOpen)
            {
                WriteDiagnostic("NavPreview pointer rawY=" + Math.Round(position.Y) +
                    " clampedY=" + Math.Round(y) +
                    " navHeight=" + Math.Round(navHeight) +
                    " slot=" + slot +
                    " activeIndex=" + activeIndex +
                    " popupOpen=" + popupOpen + ".");
                _lastLoggedDetailNavPointerIndex = activeIndex;
                _lastLoggedDetailNavPopupOpen = popupOpen;
            }
            if (_detailNavPopup != null && _detailNavPopup.IsOpen) ShowNavigationPreviewPopup(target, activeIndex, _detailNavVisibleIndexes);
            else QueueNavigationPreviewShow(target, activeIndex, _detailNavVisibleIndexes);
        }

        private void ApplyDetailNavigationButtonChrome(Button button)
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "ChromeBorder";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(0));
            border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
            template.VisualTree = border;

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BorderBrushProperty, PrimaryBrush(), "ChromeBorder"));
            hover.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 2), "ChromeBorder"));
            template.Triggers.Add(hover);

            var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(Border.BorderBrushProperty, AccentBrush(), "ChromeBorder"));
            template.Triggers.Add(pressed);

            button.Template = template;
            button.SnapsToDevicePixels = true;
        }

        private void ShowNavigationPreviewPopup(UIElement target, int activeIndex, List<int> visibleIndexes)
        {
            if (_detailNavPopup == null) return;
            _detailNavPreviewHideToken++;
            var signature = BuildNavigationPreviewSignature(visibleIndexes);
            if (_detailNavPopup.IsOpen &&
                _activeDetailNavPreviewIndex == activeIndex &&
                String.Equals(_activeDetailNavPreviewSignature, signature, StringComparison.Ordinal))
            {
                return;
            }
            _activeDetailNavPreviewIndex = activeIndex;
            _activeDetailNavPreviewSignature = signature;
            var panel = BuildNavigationPreviewPanel(activeIndex, visibleIndexes);
            panel.MouseEnter += delegate { _detailNavPreviewHideToken++; };
            panel.MouseLeave += delegate { ScheduleNavigationPreviewHide(); };
            _detailNavPopup.Child = panel;
            _detailNavPopup.Placement = PlacementMode.Relative;
            _detailNavPopup.PlacementTarget = (_detailNavHitBox as UIElement) ?? (_detailNav as UIElement) ?? target;
            _detailNavPopup.HorizontalOffset = -(DetailNavigationPreviewWidth + DetailNavigationPreviewGap);
            _detailNavPopup.VerticalOffset = CalculateNavigationPreviewVerticalOffset(panel);
            _detailNavPopup.IsOpen = true;
        }

        private async void QueueNavigationPreviewShow(UIElement target, int activeIndex, List<int> visibleIndexes)
        {
            if (_detailNavPopup == null) return;
            _detailNavPreviewHideToken++;
            _pendingDetailNavPreviewTarget = target;
            _pendingDetailNavPreviewIndex = activeIndex;
            _pendingDetailNavPreviewIndexes = visibleIndexes == null ? new List<int>() : new List<int>(visibleIndexes);
            WriteDiagnostic("NavPreview queued activeIndex=" + activeIndex +
                " visible=" + BuildNavigationPreviewSignature(_pendingDetailNavPreviewIndexes) +
                " pending=" + _detailNavPreviewShowPending + ".");
            if (_detailNavPreviewShowPending) return;
            _detailNavPreviewShowPending = true;
            var token = ++_detailNavPreviewShowToken;
            await Task.Delay(100);
            _detailNavPreviewShowPending = false;
            if (token != _detailNavPreviewShowToken) return;
            if (!IsPointerOverNavigationPreviewArea())
            {
                WriteDiagnostic("NavPreview show canceled: pointer left before delay completed.");
                return;
            }
            if (_pendingDetailNavPreviewIndex < 0 || _pendingDetailNavPreviewIndexes == null || _pendingDetailNavPreviewIndexes.Count == 0)
            {
                WriteDiagnostic("NavPreview show canceled: no pending index.");
                return;
            }
            WriteDiagnostic("NavPreview show after delay activeIndex=" + _pendingDetailNavPreviewIndex + ".");
            ShowNavigationPreviewPopup(
                _pendingDetailNavPreviewTarget ?? (_detailNavHitBox as UIElement) ?? (_detailNav as UIElement),
                _pendingDetailNavPreviewIndex,
                _pendingDetailNavPreviewIndexes);
        }

        private static string BuildNavigationPreviewSignature(List<int> visibleIndexes)
        {
            if (visibleIndexes == null || visibleIndexes.Count == 0) return "";
            return String.Join(",", visibleIndexes.Select(i => i.ToString()).ToArray());
        }

        private Point CalculateFixedNavigationPopupScreenLocation(UIElement fallbackTarget, FrameworkElement panel, int activeIndex)
        {
            var nav = (_detailNav as FrameworkElement) ?? (fallbackTarget as FrameworkElement);
            if (nav == null) return new Point(0, 0);
            try
            {
                var host = _detailHost as FrameworkElement;
                nav.UpdateLayout();
                if (host != null) host.UpdateLayout();
                var popupHeight = MeasureElementHeight(panel, DetailNavigationPreviewWidth, 120.0);
                var navHeight = MeasureElementHeight(nav, 18.0, 8.0);
                Point navTopLeft;
                double anchorCenterY;
                if (host != null && host.ActualWidth > 0 && host.ActualHeight > 0)
                {
                    var hostTopLeft = ToDeviceIndependentScreenPoint(host.PointToScreen(new Point(0, 0)));
                    var rightMargin = 23.0;
                    if (_detailNavHitBox != null) rightMargin = _detailNavHitBox.Margin.Right + _detailNavHitBox.Padding.Right;
                    else if (_detailNav != null) rightMargin = _detailNav.Margin.Right;
                    var navWidth = Math.Max(18.0, nav.ActualWidth);
                    navTopLeft = new Point(hostTopLeft.X + host.ActualWidth - rightMargin - navWidth, hostTopLeft.Y + (host.ActualHeight - navHeight) / 2.0);
                    anchorCenterY = hostTopLeft.Y + host.ActualHeight / 2.0;
                }
                else
                {
                    navTopLeft = ToDeviceIndependentScreenPoint(nav.PointToScreen(new Point(0, 0)));
                    anchorCenterY = navTopLeft.Y + navHeight / 2.0;
                }
                var x = navTopLeft.X - DetailNavigationPreviewWidth - DetailNavigationPreviewGap;
                var y = anchorCenterY - popupHeight / 2.0;
                var result = new Point(Math.Round(Math.Max(0, x)), Math.Round(Math.Max(0, y)));
                LogNavigationPreviewGeometry(fallbackTarget, panel, activeIndex, navTopLeft, navHeight, popupHeight, result);
                return result;
            }
            catch (Exception ex)
            {
                WriteDiagnostic("NavPreview geometry failed: " + ex.GetType().Name + ": " + ex.Message);
                return new Point(0, 0);
            }
        }

        private Point ToDeviceIndependentScreenPoint(Point screenPoint)
        {
            try
            {
                var source = PresentationSource.FromVisual(this);
                if (source != null && source.CompositionTarget != null)
                    return source.CompositionTarget.TransformFromDevice.Transform(screenPoint);
            }
            catch { }
            return screenPoint;
        }

        private double CalculateNavigationPreviewVerticalOffset(FrameworkElement panel)
        {
            try
            {
                var target = (_detailNavHitBox as FrameworkElement) ?? (_detailNav as FrameworkElement);
                if (target == null) return 0;
                target.UpdateLayout();
                var popupHeight = MeasureElementHeight(panel, DetailNavigationPreviewWidth, 120.0);
                var targetHeight = Math.Max(1.0, target.ActualHeight);
                var offset = Math.Round((targetHeight - popupHeight) / 2.0);
                WriteDiagnostic("NavPreview placement mode=RelativeLeft targetSize=" +
                    Math.Round(target.ActualWidth) + "x" + Math.Round(targetHeight) +
                    " popupHeight=" + Math.Round(popupHeight) +
                    " horizontalOffset=" + (-(DetailNavigationPreviewWidth + DetailNavigationPreviewGap)) +
                    " verticalOffset=" + offset + ".");
                return offset;
            }
            catch (Exception ex)
            {
                WriteDiagnostic("NavPreview placement failed: " + ex.GetType().Name + ": " + ex.Message);
                return 0;
            }
        }

        private void LogNavigationPreviewGeometry(UIElement fallbackTarget, FrameworkElement panel, int activeIndex, Point navTopLeft, double navHeight, double popupHeight, Point result)
        {
            try
            {
                var target = fallbackTarget as FrameworkElement;
                var targetSize = target == null ? "n/a" : Math.Round(target.ActualWidth) + "x" + Math.Round(target.ActualHeight);
                var navWidth = _detailNav == null ? 0 : _detailNav.ActualWidth;
                var panelDesired = panel == null ? new Size(0, 0) : panel.DesiredSize;
                WriteDiagnostic("NavPreview geometry activeIndex=" + activeIndex +
                    " navTopLeft=" + Math.Round(navTopLeft.X) + "," + Math.Round(navTopLeft.Y) +
                    " navSize=" + Math.Round(navWidth) + "x" + Math.Round(navHeight) +
                    " popupHeight=" + Math.Round(popupHeight) +
                    " panelDesired=" + Math.Round(panelDesired.Width) + "x" + Math.Round(panelDesired.Height) +
                    " targetSize=" + targetSize +
                    " final=" + result.X + "," + result.Y + ".");
            }
            catch { }
        }

        private static double MeasureElementHeight(FrameworkElement element, double width, double fallback)
        {
            if (element == null) return fallback;
            try
            {
                element.UpdateLayout();
                var height = element.ActualHeight;
                if (height <= 0)
                {
                    element.Measure(new Size(width, Double.PositiveInfinity));
                    height = element.DesiredSize.Height;
                }
                return height > 0 ? height : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private async void ScheduleNavigationPreviewHide()
        {
            _detailNavPreviewShowToken++;
            _detailNavPreviewShowPending = false;
            _pendingDetailNavPreviewIndex = -1;
            _pendingDetailNavPreviewIndexes = new List<int>();
            _pendingDetailNavPreviewTarget = null;
            var token = ++_detailNavPreviewHideToken;
            WriteDiagnostic("NavPreview hide scheduled token=" + token + ".");
            await Task.Delay(260);
            if (token != _detailNavPreviewHideToken) return;
            if (_detailNavPopup == null || !_detailNavPopup.IsOpen) return;
            var child = _detailNavPopup.Child as UIElement;
            var overPopup = child != null && child.IsMouseOver;
            var overHitBox = _detailNavHitBox != null && _detailNavHitBox.IsMouseOver;
            var overNav = _detailNav != null && _detailNav.IsMouseOver;
            if (!overPopup && !overHitBox && !overNav)
            {
                _detailNavPopup.IsOpen = false;
                _activeDetailNavPreviewIndex = -1;
                _activeDetailNavPreviewSignature = "";
                _lastLoggedDetailNavPointerIndex = -1;
                _lastLoggedDetailNavPopupOpen = false;
                WriteDiagnostic("NavPreview hidden token=" + token + ".");
            }
        }

        private bool IsPointerOverNavigationPreviewArea()
        {
            var child = _detailNavPopup == null ? null : _detailNavPopup.Child as UIElement;
            return (_detailNavHitBox != null && _detailNavHitBox.IsMouseOver) ||
                (_detailNav != null && _detailNav.IsMouseOver) ||
                (child != null && child.IsMouseOver);
        }

        private Border BuildNavigationPreviewPanel(int activeIndex, List<int> visibleIndexes)
        {
            var list = new StackPanel { Width = DetailNavigationPreviewWidth - 28 };
            Border activeItem = null;
            foreach (var index in visibleIndexes)
            {
                if (index < 0 || index >= _detailNavTargets.Count) continue;
                var active = index == activeIndex;
                var button = new Button
                {
                    Content = new TextBlock
                    {
                        Text = ShortenPreview(_detailNavTargets[index].Preview, 34),
                        Foreground = InkBrush(),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    },
                    Margin = new Thickness(8, 5, 8, 5)
                };
                button.Background = active ? SoftPressedBrush() : Brushes.Transparent;
                button.BorderThickness = new Thickness(0);
                button.Padding = new Thickness(0);
                button.HorizontalContentAlignment = HorizontalAlignment.Left;
                button.Cursor = Cursors.Hand;
                button.MouseEnter += delegate { button.Background = SoftPressedBrush(); };
                button.MouseLeave += delegate { button.Background = active ? SoftPressedBrush() : Brushes.Transparent; };
                var jumpIndex = index;
                button.Click += delegate
                {
                    if (jumpIndex >= 0 && jumpIndex < _detailNavTargets.Count)
                    {
                        _detailNavTargets[jumpIndex].Target.BringIntoView();
                        if (_detailBox != null) _detailBox.Focus();
                    }
                    if (_detailNavPopup != null) _detailNavPopup.IsOpen = false;
                    _activeDetailNavPreviewIndex = -1;
                    _activeDetailNavPreviewSignature = "";
                    _detailNavPreviewHideToken++;
                };
                var item = new Border
                {
                    CornerRadius = new CornerRadius(7),
                    Background = button.Background,
                    Child = button
                };
                if (active) activeItem = item;
                list.Children.Add(item);
            }

            var scroll = new ScrollViewer
            {
                Content = list,
                MaxHeight = 420,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(8)
            };
            if (activeItem != null)
            {
                scroll.Loaded += delegate
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        CenterNavigationPreviewItem(scroll, list, activeItem);
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                };
            }
            return new Border
            {
                Width = DetailNavigationPreviewWidth,
                Child = scroll,
                Background = SurfaceBrush(),
                BorderBrush = ThemeBorderBrush(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(0)
            };
        }

        private void CenterNavigationPreviewItem(ScrollViewer scroll, FrameworkElement container, FrameworkElement item)
        {
            if (scroll == null || container == null || item == null) return;
            try
            {
                scroll.UpdateLayout();
                container.UpdateLayout();
                item.UpdateLayout();
                var point = item.TransformToAncestor(container).Transform(new Point(0, 0));
                var viewport = scroll.ViewportHeight > 0 ? scroll.ViewportHeight : Math.Min(scroll.ActualHeight, scroll.MaxHeight);
                if (viewport <= 0) viewport = 120.0;
                var itemHeight = item.ActualHeight > 0 ? item.ActualHeight : 28.0;
                var offset = Math.Max(0, point.Y - Math.Max(0, (viewport - itemHeight) / 2.0));
                scroll.ScrollToVerticalOffset(offset);
                WriteDiagnostic("NavPreview scroll activeY=" + Math.Round(point.Y) +
                    " itemHeight=" + Math.Round(itemHeight) +
                    " viewport=" + Math.Round(viewport) +
                    " offset=" + Math.Round(offset) + ".");
            }
            catch (Exception ex)
            {
                WriteDiagnostic("NavPreview scroll failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private string BuildNavigationPreview(ConversationEntry entry)
        {
            if (entry == null) return "";
            var text = entry.Text ?? "";
            var marker = "## My request for Codex:";
            var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) return CleanUserPreviewText(text.Substring(index + marker.Length));
            var preview = String.Join(Environment.NewLine, BuildUserMessageSegments(text)
                .Where(s => s.IsUserText)
                .Select(s => s.Text)
                .ToArray());
            return CleanUserPreviewText(preview);
        }

        private static string CleanUserPreviewText(string text)
        {
            var preview = text ?? "";
            preview = Regex.Replace(preview, @"<image\b[\s\S]*?</image>", "", RegexOptions.IgnoreCase).Trim();
            preview = Regex.Replace(preview, @"<environment_context>[\s\S]*?</environment_context>", "", RegexOptions.IgnoreCase).Trim();
            preview = Regex.Replace(preview, @"# Files mentioned by the user:[\s\S]*?(?=## My request for Codex:|$)", "", RegexOptions.IgnoreCase).Trim();
            preview = Regex.Replace(preview, @"## My request for Codex:", "", RegexOptions.IgnoreCase).Trim();
            preview = Regex.Replace(preview, @"\s+", " ").Trim();
            return preview;
        }

        private static string ShortenPreview(string text, int maxLength)
        {
            text = String.IsNullOrWhiteSpace(text) ? "(空请求)" : text.Trim();
            if (text.Length <= maxLength) return text;
            return text.Substring(0, Math.Max(1, maxLength - 1)) + "...";
        }

        private List<ConversationEntry> ReadConversationEntries(string path, int maxLines)
        {
            var entries = new List<ConversationEntry>();
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return entries;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(fs, Encoding.UTF8))
                {
                    string line;
                    var count = 0;
                    while ((line = reader.ReadLine()) != null && (maxLines <= 0 || count < maxLines))
                    {
                        count++;
                        if (String.IsNullOrWhiteSpace(line)) continue;
                        Dictionary<string, object> obj = null;
                        try { obj = _json.DeserializeObject(line) as Dictionary<string, object>; } catch { }
                        if (obj == null) continue;
                        var role = GuessRole(obj);
                        var text = ExtractReadableText(obj);
                        if (String.IsNullOrWhiteSpace(role) || String.IsNullOrWhiteSpace(text)) continue;
                        if (ShouldHideConversationEntry(role, text)) continue;
                        text = text.Trim();
                        if (text.Length > 6000) text = text.Substring(0, 6000) + Environment.NewLine + "...";
                        if (IsDuplicateConversationEntry(entries, role, text)) continue;
                        entries.Add(new ConversationEntry { Role = role, Text = text });
                    }
                }
            }
            catch { }
            return entries;
        }

        private static bool IsDuplicateConversationEntry(List<ConversationEntry> entries, string role, string text)
        {
            if (entries == null || entries.Count == 0) return false;
            var last = entries[entries.Count - 1];
            if (last == null) return false;
            if (!String.Equals(last.Role ?? "", role ?? "", StringComparison.OrdinalIgnoreCase)) return false;
            if (String.Equals(role ?? "", "user", StringComparison.OrdinalIgnoreCase))
            {
                var lastUser = BuildUserDuplicateSignature(last.Text);
                var currentUser = BuildUserDuplicateSignature(text);
                if (!String.IsNullOrWhiteSpace(lastUser) && !String.IsNullOrWhiteSpace(currentUser))
                    return String.Equals(lastUser, currentUser, StringComparison.Ordinal);
            }
            return String.Equals(NormalizeConversationText(last.Text), NormalizeConversationText(text), StringComparison.Ordinal);
        }

        private static string BuildUserDuplicateSignature(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return "";
            var marker = "## My request for Codex:";
            var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            var visible = index >= 0
                ? CleanUserPreviewText(text.Substring(index + marker.Length))
                : String.Join(Environment.NewLine, BuildUserMessageSegments(text)
                    .Where(s => s.IsUserText)
                    .Select(s => CleanUserPreviewText(s.Text))
                    .ToArray());
            return NormalizeConversationText(visible);
        }

        private static string NormalizeConversationText(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return "";
            return Regex.Replace(text.Trim(), @"\s+", " ");
        }

        private string ResolveConversationPath(SessionRow row)
        {
            if (row == null) return "";
            if (!String.IsNullOrWhiteSpace(row.FilePath) && File.Exists(row.FilePath)) return row.FilePath;
            if (String.IsNullOrWhiteSpace(row.Id)) return row == null ? "" : row.FilePath;

            var roots = new List<string>();
            var home = GetCodexHome();
            if (!String.IsNullOrWhiteSpace(home)) AddExistingDirectory(roots, Path.Combine(home, "sessions"));
            if (!String.IsNullOrWhiteSpace(row.FilePath))
            {
                try
                {
                    var dir = Path.GetDirectoryName(row.FilePath);
                    AddExistingDirectory(roots, dir);
                    var parent = Directory.GetParent(dir ?? "");
                    for (var i = 0; parent != null && i < 4; i++, parent = parent.Parent)
                        AddExistingDirectory(roots, parent.FullName);
                }
                catch { }
            }

            foreach (var root in roots)
            {
                var hit = FindRolloutFile(root, row.Id, 5);
                if (!String.IsNullOrWhiteSpace(hit))
                {
                    row.FilePath = hit;
                    return hit;
                }
            }
            return row.FilePath ?? "";
        }

        private static string FindRolloutFile(string root, string id, int maxDepth)
        {
            if (String.IsNullOrWhiteSpace(root) || String.IsNullOrWhiteSpace(id) || maxDepth < 0 || !Directory.Exists(root)) return "";
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "rollout-*" + id + ".jsonl")) return NormalizePath(file);
                foreach (var file in Directory.EnumerateFiles(root, "rollout-*" + id + ".json")) return NormalizePath(file);
                if (maxDepth == 0) return "";
                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    var hit = FindRolloutFile(dir, id, maxDepth - 1);
                    if (!String.IsNullOrWhiteSpace(hit)) return hit;
                }
            }
            catch { }
            return "";
        }

        private string BuildMissingConversationMessage(SessionRow row, string resolvedPath)
        {
            var path = resolvedPath;
            if (String.IsNullOrWhiteSpace(path)) path = row == null ? "" : row.FilePath;
            if (String.IsNullOrWhiteSpace(path)) return "没有找到这条会话对应的 rollout 文件。";
            if (!File.Exists(path)) return "数据库记录的 rollout 文件不存在：" + path + Environment.NewLine + "已尝试在当前 Codex sessions 目录按会话 ID 搜索。";
            return "已找到文件，但没有解析到可显示的用户/助手文本：" + path + Environment.NewLine + "这通常是 Codex jsonl 格式变更，或该文件只包含工具/元数据事件。";
        }

        private static Brush RoleBrush(string role)
        {
            role = (role ?? "").ToLowerInvariant();
            if (role == "user") return AccentBrush();
            if (role == "assistant") return ColorBrush(CurrentTheme().AssistantAccent);
            if (role == "tool") return ColorBrush(CurrentTheme().ToolAccent);
            if (role == "system") return ColorBrush(CurrentTheme().SystemAccent);
            return SteelBrush();
        }

        private static Brush RoleBackground(string role)
        {
            role = (role ?? "").ToLowerInvariant();
            if (role == "user") return SoftHoverBrush();
            if (role == "assistant") return ColorBrush(CurrentTheme().AssistantBackground);
            if (role == "tool") return ColorBrush(CurrentTheme().ToolBackground);
            if (role == "system") return ColorBrush(CurrentTheme().SystemBackground);
            return SoftHoverBrush();
        }

        private static string NormalizeRoleLabel(string role)
        {
            role = (role ?? "").ToLowerInvariant();
            if (role == "user") return "用户";
            if (role == "assistant") return "助手";
            if (role == "tool") return "工具";
            if (role == "system") return "系统";
            return String.IsNullOrWhiteSpace(role) ? "消息" : role;
        }

        private string GuessRole(Dictionary<string, object> obj)
        {
            var type = GetString(obj, "type");
            if (type == "user" || type == "assistant") return type;
            if (type == "response_item")
            {
                var payload = GetDict(obj, "payload");
                var role = payload == null ? "" : GetString(payload, "role");
                if (!String.IsNullOrWhiteSpace(role)) return role;
                var payloadType = payload == null ? "" : GetString(payload, "type");
                if (payloadType == "function_call" || payloadType == "function_call_output") return "tool";
            }
            if (type == "event_msg")
            {
                var payload = GetDict(obj, "payload");
                var payloadType = payload == null ? "" : GetString(payload, "type");
                if (payloadType == "user_message") return "user";
                if (payloadType == "agent_message") return "assistant";
                if (payloadType == "agent_reasoning" || payloadType == "agent_reasoning_delta") return "assistant";
                if (payloadType == "exec_command" || payloadType == "exec_output" || payloadType == "tool_output") return "tool";
            }
            return "";
        }

        private static bool ShouldHideConversationEntry(string role, string text)
        {
            role = (role ?? "").ToLowerInvariant();
            if (role == "developer" || role == "system") return true;
            if (String.IsNullOrWhiteSpace(text)) return true;
            return false;
        }

        private string ExtractReadableText(object value)
        {
            if (value == null) return "";
            var s = value as string;
            if (s != null) return s;
            var dict = value as Dictionary<string, object>;
            if (dict != null)
            {
                foreach (var key in new[] { "text", "content", "message", "prompt", "payload", "output", "arguments" })
                {
                    if (dict.ContainsKey(key))
                    {
                        var text = ExtractReadableText(dict[key]);
                        if (!String.IsNullOrWhiteSpace(text)) return text;
                    }
                }
                if (dict.ContainsKey("message"))
                {
                    var text = ExtractReadableText(dict["message"]);
                    if (!String.IsNullOrWhiteSpace(text)) return text;
                }
                return "";
            }
            var list = value as ArrayList;
            if (list != null)
            {
                var parts = new List<string>();
                foreach (var item in list)
                {
                    var text = ExtractReadableText(item);
                    if (!String.IsNullOrWhiteSpace(text)) parts.Add(text);
                }
                return String.Join(Environment.NewLine, parts.ToArray());
            }
            var array = value as object[];
            if (array != null)
            {
                var parts = new List<string>();
                foreach (var item in array)
                {
                    var text = ExtractReadableText(item);
                    if (!String.IsNullOrWhiteSpace(text)) parts.Add(text);
                }
                return String.Join(Environment.NewLine, parts.ToArray());
            }
            return "";
        }

        private async Task CloneSelectedAsync()
        {
            var rows = _allRows.Where(r => r.Selected).ToList();
            if (rows.Count == 0)
            {
                System.Windows.MessageBox.Show("请先勾选要派生的会话。", "没有选择", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var target = SelectedProviderValue(_targetCombo);
            if (String.IsNullOrWhiteSpace(target) || target == "__all__")
            {
                System.Windows.MessageBox.Show("请选择目标账号。", "目标账号不完整", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var options = BuildDeriveOptions(target);
            await RunBusyAsync("正在派生 " + rows.Count + " 条会话...", delegate
            {
                foreach (var row in rows)
                    RunPowerShellScript(_cliScriptPath, "clone -Id " + QuoteArg(row.Id) + " -To " + QuoteArg(target) + options);
            });
            RefreshAll();
        }

        private async Task SyncAllAsync()
        {
            var source = SelectedProviderValue(_sourceCombo);
            var target = SelectedProviderValue(_targetCombo);
            if (String.IsNullOrWhiteSpace(source) || source == "__all__" || String.IsNullOrWhiteSpace(target) || target == "__all__")
            {
                System.Windows.MessageBox.Show("派生全部需要选择明确的源账号和目标账号。", "账号不完整", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var options = BuildDeriveOptions(target);
            await RunBusyAsync("正在派生全部...", delegate { RunPowerShellScript(_cliScriptPath, "sync -From " + QuoteArg(source) + " -To " + QuoteArg(target) + options); });
            RefreshAll();
        }

        private async Task MirrorAsync()
        {
            var source = SelectedProviderValue(_sourceCombo);
            var target = SelectedProviderValue(_targetCombo);
            if (String.IsNullOrWhiteSpace(source) || source == "__all__" || String.IsNullOrWhiteSpace(target) || target == "__all__")
            {
                System.Windows.MessageBox.Show("双向派生需要选择两个明确账号。", "账号不完整", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var options = BuildDeriveOptions(target);
            await RunBusyAsync("正在双向派生...", delegate { RunPowerShellScript(_cliScriptPath, "mirror -Providers " + QuoteArg(source + "," + target) + options); });
            RefreshAll();
        }

        private string BuildDeriveOptions(string targetProvider)
        {
            var model = SelectedComboText(_modelCombo, "default");
            var reasoning = SelectedComboText(_reasoningCombo, "default");
            if (model == "default" || String.IsNullOrWhiteSpace(model))
            {
                var node = FindNodeForHistoryProvider(targetProvider);
                if (node != null) model = node.Model;
            }
            if (reasoning == "default" || String.IsNullOrWhiteSpace(reasoning))
            {
                var node = FindNodeForHistoryProvider(targetProvider);
                if (node != null) reasoning = node.ReasoningEffort;
            }
            var args = "";
            if (!String.IsNullOrWhiteSpace(model) && model != "default") args += " -TargetModel " + QuoteArg(model);
            if (!String.IsNullOrWhiteSpace(reasoning) && reasoning != "default") args += " -TargetReasoningEffort " + QuoteArg(reasoning);
            if (!String.Equals(targetProvider, "openai", StringComparison.OrdinalIgnoreCase)) args += " -SanitizeForProxy";
            return args;
        }

        private CcSwitchNode FindNodeForHistoryProvider(string provider)
        {
            if (String.IsNullOrWhiteSpace(provider)) return null;
            var selected = _ccSwitchCombo == null ? null : _ccSwitchCombo.SelectedItem as CcSwitchNode;
            if (selected != null && String.Equals(selected.HistoryProvider, provider, StringComparison.OrdinalIgnoreCase)) return selected;
            return _ccSwitchNodes.FirstOrDefault(n => String.Equals(n.HistoryProvider, provider, StringComparison.OrdinalIgnoreCase));
        }

        private CcSwitchNode GetSelectedLaunchNode()
        {
            return _ccSwitchCombo == null ? null : _ccSwitchCombo.SelectedItem as CcSwitchNode;
        }

        private void ApplyCcSwitchLaunchNode(List<string> args, CcSwitchNode node)
        {
            if (args == null || node == null) return;
            var provider = FirstNonEmpty(node.ModelProvider, node.HistoryProvider);
            if (!String.IsNullOrWhiteSpace(provider)) AddCodexConfigArg(args, "model_provider", TomlString(provider));
            if (!String.IsNullOrWhiteSpace(node.AuthMode)) AddCodexConfigArg(args, "preferred_auth_method", TomlString(node.AuthMode));
            else if (!String.IsNullOrWhiteSpace(node.ApiKey)) AddCodexConfigArg(args, "preferred_auth_method", TomlString("apikey"));
            if (!String.IsNullOrWhiteSpace(provider) && !String.IsNullOrWhiteSpace(node.ProviderName))
                AddCodexConfigArg(args, "model_providers." + provider + ".name", TomlString(node.ProviderName));
            if (!String.IsNullOrWhiteSpace(provider) && !String.IsNullOrWhiteSpace(node.BaseUrl))
                AddCodexConfigArg(args, "model_providers." + provider + ".base_url", TomlString(node.BaseUrl));
            if (!String.IsNullOrWhiteSpace(provider) && !String.IsNullOrWhiteSpace(node.WireApi))
                AddCodexConfigArg(args, "model_providers." + provider + ".wire_api", TomlString(node.WireApi));
            if (!String.IsNullOrWhiteSpace(provider) && !String.IsNullOrWhiteSpace(node.ApiKey))
                AddCodexConfigArg(args, "model_providers." + provider + ".requires_openai_auth", "true");
        }

        private static void AddCodexConfigArg(List<string> args, string key, string value)
        {
            if (String.IsNullOrWhiteSpace(key) || String.IsNullOrWhiteSpace(value)) return;
            args.Add("-c");
            args.Add(key + "=" + value);
        }

        private static string TomlString(string value)
        {
            value = value ?? "";
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private Dictionary<string, string> BuildLaunchEnvironment(CcSwitchNode node)
        {
            var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (node != null && !String.IsNullOrWhiteSpace(node.ApiKey))
                env["OPENAI_API_KEY"] = node.ApiKey;
            return env;
        }

        private string BuildLaunchProviderInfo(CcSwitchNode node)
        {
            if (node == null) return L("启动节点：不切换（使用当前 Codex 配置）", "Launch node: no switch (use current Codex config)");
            var lines = new List<string>();
            lines.Add(L("启动节点：", "Launch node: ") + node.ToString());
            lines.Add(L("历史账号：", "History account: ") + DefaultString(node.HistoryProvider, IsOfficialCodexNode(node) ? "openai" : "custom") +
                "  |  Codex provider: " + GetDisplayCodexProvider(node));
            if (!String.IsNullOrWhiteSpace(node.BaseUrl)) lines.Add("Base URL：" + node.BaseUrl);
            if (String.Equals(node.AuthMode, "chatgpt", StringComparison.OrdinalIgnoreCase)) lines.Add(L("认证：ChatGPT 登录", "Auth: ChatGPT login"));
            return String.Join(Environment.NewLine, lines.ToArray());
        }

        private string GetDisplayCodexProvider(CcSwitchNode node)
        {
            if (node == null) return "custom";
            if (!String.IsNullOrWhiteSpace(node.ModelProvider)) return node.ModelProvider;
            if (IsOfficialCodexNode(node))
            {
                if (_ccSwitchUnifyCodexHistory) return DefaultString(_ccSwitchOfficialHistoryProvider, "custom");
                return "official";
            }
            if (_ccSwitchUnifyCodexHistory) return DefaultString(_ccSwitchThirdPartyHistoryProvider, "custom");
            return DefaultString(node.HistoryProvider, "custom");
        }

        private static bool IsOfficialCodexNode(CcSwitchNode node)
        {
            if (node == null) return false;
            var id = node.Id ?? "";
            var name = (node.Name ?? "").ToLowerInvariant().Replace(" ", "");
            return String.Equals(id, "codex-official", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("openaiofficial") ||
                String.Equals(node.AuthMode, "chatgpt", StringComparison.OrdinalIgnoreCase);
        }

        private async Task RunBusyAsync(string message, Action action)
        {
            SetStatus(message);
            IsEnabled = false;
            try
            {
                await Task.Run(action);
                SetStatus("完成。");
            }
            catch (Exception ex)
            {
                SetStatus("失败：" + ex.Message);
                System.Windows.MessageBox.Show(ex.Message, "操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private void LaunchTerminal()
        {
            var row = GetActiveDetailRow();
            var cwd = row != null && !String.IsNullOrWhiteSpace(row.Cwd) ? row.Cwd : _rootDir;
            var codex = ResolveCodexExecutable();
            try { if (!String.IsNullOrWhiteSpace(codex) && File.Exists(codex)) _config.CodexExe = NormalizePath(codex); } catch { }
            var launchNode = GetSelectedLaunchNode();
            var baseArgs = new List<string>();
            baseArgs.Add("--disable");
            baseArgs.Add("apps");
            ApplyCcSwitchLaunchNode(baseArgs, launchNode);
            if (_fullAccessBox.IsChecked == true) baseArgs.Add("--dangerously-bypass-approvals-and-sandbox");
            if (_fastBox.IsChecked == true) { baseArgs.Add("-c"); baseArgs.Add("service_tier=fast"); }
            var model = SelectedComboText(_modelCombo, "default");
            var reasoning = SelectedComboText(_reasoningCombo, "default");
            if (!String.IsNullOrWhiteSpace(model) && model != "default") { baseArgs.Add("-m"); baseArgs.Add(model); }
            if (!String.IsNullOrWhiteSpace(reasoning) && reasoning != "default") { baseArgs.Add("-c"); baseArgs.Add("model_reasoning_effort=" + reasoning); }
            var args = new List<string>(baseArgs);
            var fallbackArgs = new List<string>(baseArgs);
            var resumeSession = row != null && _loadChatBox.IsChecked == true && !String.IsNullOrWhiteSpace(row.Id);
            if (resumeSession)
            {
                args.Clear();
                args.Add("resume");
                args.AddRange(baseArgs);
                args.Add(row.Id);
            }
            var codexHome = ResolveCodexHomeForSession(row);
            var launchNotice = "";
            string sessionCliVersion;
            string codexCliVersion;
            if (resumeSession && IsSessionNewerThanCodex(row, codex, out sessionCliVersion, out codexCliVersion))
            {
                resumeSession = false;
                args = new List<string>(fallbackArgs);
                launchNotice = "当前 Codex CLI " + codexCliVersion + " 低于会话版本 " + sessionCliVersion + "，已改为新开 Codex，避免 resume 失败。";
            }
            SaveConfigWithDetectedInfo(false);
            CleanInvalidCodexServiceTier();
            var launchEnvironment = BuildLaunchEnvironment(launchNode);
            var launchProviderInfo = BuildLaunchProviderInfo(launchNode);
            if (_usePowerShellBox != null && _usePowerShellBox.IsChecked == true)
            {
                var scriptPath = WritePowerShellLaunchScript(cwd, codex, args, codexHome, resumeSession, fallbackArgs, launchNotice, launchProviderInfo);
                StartTerminalProcess("powershell.exe", "-NoExit -NoLogo -NoProfile -ExecutionPolicy Bypass -File " + QuoteArg(scriptPath), launchEnvironment);
            }
            else
            {
                var scriptPath = WriteCmdLaunchScript(cwd, codex, args, codexHome, resumeSession, fallbackArgs, launchNotice, launchProviderInfo);
                StartTerminalProcess("cmd.exe", "/d /k call " + QuoteCmdArg(scriptPath), launchEnvironment);
            }
        }

        private string ResolveCodexHomeForSession(SessionRow row)
        {
            if (row != null)
            {
                var resolvedPath = ResolveConversationPath(row);
                var home = TryGetCodexHomeFromPath(resolvedPath);
                if (!String.IsNullOrWhiteSpace(home)) return home;
                home = TryGetCodexHomeFromPath(row.FilePath);
                if (!String.IsNullOrWhiteSpace(home)) return home;
            }
            return GetCodexHome();
        }

        private static string TryGetCodexHomeFromPath(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return "";
            try
            {
                var current = File.Exists(path) ? Directory.GetParent(path) : new DirectoryInfo(path);
                while (current != null)
                {
                    if (String.Equals(current.Name, ".codex", StringComparison.OrdinalIgnoreCase))
                        return NormalizePath(current.FullName);
                    current = current.Parent;
                }
            }
            catch { }
            return "";
        }

        private bool IsSessionNewerThanCodex(SessionRow row, string codex, out string sessionVersion, out string codexVersion)
        {
            sessionVersion = GetSessionCliVersion(row);
            codexVersion = GetCodexExecutableVersion(codex);
            if (String.IsNullOrWhiteSpace(sessionVersion) || String.IsNullOrWhiteSpace(codexVersion)) return false;
            return CompareLooseVersions(sessionVersion, codexVersion) > 0;
        }

        private string GetSessionCliVersion(SessionRow row)
        {
            if (row == null) return "";
            var path = ResolveConversationPath(row);
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "";
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(fs, Encoding.UTF8))
                {
                    for (var i = 0; i < 30; i++)
                    {
                        var line = reader.ReadLine();
                        if (line == null) break;
                        if (line.IndexOf("\"session_meta\"", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var obj = _json.DeserializeObject(line) as Dictionary<string, object>;
                        var payload = obj == null ? null : GetDict(obj, "payload");
                        var version = payload == null ? "" : GetString(payload, "cli_version");
                        if (!String.IsNullOrWhiteSpace(version)) return version;
                    }
                }
            }
            catch { }
            return "";
        }

        private string GetCodexExecutableVersion(string codex)
        {
            try
            {
                var result = RunProcess(codex, "--version", _rootDir, 5000);
                var match = Regex.Match(result.Output ?? "", @"\d+\.\d+\.\d+(?:[-+][A-Za-z0-9\.\-]+)?");
                return match.Success ? match.Value : "";
            }
            catch { return ""; }
        }

        private static int CompareLooseVersions(string left, string right)
        {
            var a = ParseVersionParts(left);
            var b = ParseVersionParts(right);
            for (var i = 0; i < 3; i++)
            {
                if (a[i] != b[i]) return a[i].CompareTo(b[i]);
            }
            return 0;
        }

        private static int[] ParseVersionParts(string value)
        {
            var match = Regex.Match(value ?? "", @"(\d+)\.(\d+)\.(\d+)");
            if (!match.Success) return new[] { 0, 0, 0 };
            return new[] { Int32.Parse(match.Groups[1].Value), Int32.Parse(match.Groups[2].Value), Int32.Parse(match.Groups[3].Value) };
        }

        private string ResolveCodexExecutable()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var candidates = new List<string>();
            AddCandidate(candidates, _config.CodexExe);
            AddCandidate(candidates, GetConfiguredCodexCliPath());
            if (!String.IsNullOrWhiteSpace(local))
            {
                var binDir = Path.Combine(local, "OpenAI", "Codex", "bin");
                foreach (var path in FindCodexExecutables(binDir)) AddCandidate(candidates, path);
            }

            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in pathEnv.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    AddCandidate(candidates, Path.Combine(dir.Trim('"'), "codex.exe"));
                }
                catch { }
            }
            foreach (var dir in pathEnv.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    AddCandidate(candidates, Path.Combine(dir.Trim('"'), "codex.cmd"));
                    AddCandidate(candidates, Path.Combine(dir.Trim('"'), "codex.bat"));
                }
                catch { }
            }

            var best = ChooseBestCodexExecutable(candidates);
            if (!String.IsNullOrWhiteSpace(best)) return best;
            return "codex";
        }

        private string ChooseBestCodexExecutable(List<string> candidates)
        {
            string bestPath = "";
            string bestVersion = "";
            foreach (var candidate in candidates)
            {
                try
                {
                    if (String.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate)) continue;
                    var normalized = NormalizePath(candidate);
                    var version = GetCodexExecutableVersion(normalized);
                    if (String.IsNullOrWhiteSpace(bestPath))
                    {
                        bestPath = normalized;
                        bestVersion = version;
                        continue;
                    }
                    if (!String.IsNullOrWhiteSpace(version) &&
                        (String.IsNullOrWhiteSpace(bestVersion) || CompareLooseVersions(version, bestVersion) > 0))
                    {
                        bestPath = normalized;
                        bestVersion = version;
                    }
                }
                catch { }
            }
            return bestPath;
        }

        private string GetConfiguredCodexCliPath()
        {
            try
            {
                var configPath = Path.Combine(GetCodexHome(), "config.toml");
                if (!File.Exists(configPath)) return "";
                var text = File.ReadAllText(configPath, Encoding.UTF8);
                var match = Regex.Match(text, @"(?m)^\s*CODEX_CLI_PATH\s*=\s*['""]([^'""]+)['""]");
                return match.Success ? match.Groups[1].Value : "";
            }
            catch { return ""; }
        }

        private static IEnumerable<string> FindCodexExecutables(string root)
        {
            if (String.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) yield break;
            List<FileInfo> files;
            try
            {
                files = Directory.GetFiles(root, "codex.exe", SearchOption.AllDirectories)
                    .Select(p => new FileInfo(p))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .ToList();
            }
            catch { yield break; }
            foreach (var file in files) yield return file.FullName;
        }

        private static void AddCandidate(List<string> candidates, string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return;
            try
            {
                var normalized = NormalizePath(path);
                if (!candidates.Any(p => String.Equals(p, normalized, StringComparison.OrdinalIgnoreCase)))
                    candidates.Add(normalized);
            }
            catch { }
        }

        private string WriteCmdLaunchScript(string cwd, string codex, List<string> args, string codexHome, bool allowResumeFallback, List<string> fallbackArgs, string launchNotice, string launchProviderInfo)
        {
            var scriptPath = Path.Combine(GetAppStateDirectory(), "launch-codex.cmd");
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("chcp 65001 >nul");
            sb.AppendLine("cd /d " + QuoteCmdArg(cwd));
            if (!String.IsNullOrWhiteSpace(codexHome)) sb.AppendLine("set " + QuoteCmdSet("CODEX_HOME", codexHome));
            sb.AppendLine("echo(工作目录: " + EscapeCmdEcho(cwd));
            if (!String.IsNullOrWhiteSpace(codexHome)) sb.AppendLine("echo(CODEX_HOME: " + EscapeCmdEcho(codexHome));
            foreach (var line in SplitLines(launchProviderInfo)) sb.AppendLine("echo(" + EscapeCmdEcho(line));
            if (!String.IsNullOrWhiteSpace(launchNotice)) sb.AppendLine("echo(" + EscapeCmdEcho(launchNotice));
            sb.AppendLine("echo(命令: " + EscapeCmdEcho(BuildCommandPreview(codex, args)));
            sb.AppendLine("echo(");
            sb.AppendLine("echo(正在启动 Codex...");
            sb.AppendLine(BuildCmdLaunchCommand(codex, args));
            if (allowResumeFallback)
            {
                sb.AppendLine("if errorlevel 1 (");
                sb.AppendLine("  echo(");
                sb.AppendLine("  echo(恢复会话失败，正在改为新开 Codex...");
                sb.AppendLine("  echo(命令: " + EscapeCmdEcho(BuildCommandPreview(codex, fallbackArgs)));
                sb.AppendLine("  " + BuildCmdLaunchCommand(codex, fallbackArgs));
                sb.AppendLine(")");
            }
            sb.AppendLine("echo(");
            sb.AppendLine("echo(Codex 已退出，窗口保持打开以便查看输出。");
            File.WriteAllText(scriptPath, sb.ToString(), new UTF8Encoding(false));
            return scriptPath;
        }

        private string WritePowerShellLaunchScript(string cwd, string codex, List<string> args, string codexHome, bool allowResumeFallback, List<string> fallbackArgs, string launchNotice, string launchProviderInfo)
        {
            var scriptPath = Path.Combine(GetAppStateDirectory(), "launch-codex.ps1");
            var sb = new StringBuilder();
            sb.AppendLine("$OutputEncoding = [Console]::OutputEncoding = [System.Text.Encoding]::UTF8");
            sb.AppendLine("Set-Location -LiteralPath " + QuotePowerShell(cwd));
            if (!String.IsNullOrWhiteSpace(codexHome)) sb.AppendLine("$env:CODEX_HOME = " + QuotePowerShell(codexHome));
            sb.AppendLine("Write-Host ('工作目录: ' + (Get-Location).Path)");
            if (!String.IsNullOrWhiteSpace(codexHome)) sb.AppendLine("Write-Host ('CODEX_HOME: ' + $env:CODEX_HOME)");
            foreach (var line in SplitLines(launchProviderInfo)) sb.AppendLine("Write-Host " + QuotePowerShell(line));
            if (!String.IsNullOrWhiteSpace(launchNotice)) sb.AppendLine("Write-Host " + QuotePowerShell(launchNotice));
            sb.AppendLine("Write-Host " + QuotePowerShell("命令: " + BuildCommandPreview(codex, args)));
            sb.AppendLine("Write-Host ''");
            sb.AppendLine("Write-Host '正在启动 Codex...'");
            var command = "& " + QuotePowerShell(codex);
            foreach (var arg in args) command += " " + QuotePowerShell(arg);
            sb.AppendLine(command);
            if (allowResumeFallback)
            {
                sb.AppendLine("if ($LASTEXITCODE -ne 0) {");
                sb.AppendLine("  Write-Host ''");
                sb.AppendLine("  Write-Host '恢复会话失败，正在改为新开 Codex...'");
                sb.AppendLine("  Write-Host " + QuotePowerShell("命令: " + BuildCommandPreview(codex, fallbackArgs)));
                var fallbackCommand = "& " + QuotePowerShell(codex);
                foreach (var arg in fallbackArgs) fallbackCommand += " " + QuotePowerShell(arg);
                sb.AppendLine("  " + fallbackCommand);
                sb.AppendLine("}");
            }
            sb.AppendLine("Write-Host ''");
            sb.AppendLine("Write-Host 'Codex 已退出，窗口保持打开以便查看输出。'");
            File.WriteAllText(scriptPath, sb.ToString(), new UTF8Encoding(true));
            return scriptPath;
        }

        private void StartTerminalProcess(string fileName, string arguments, Dictionary<string, string> environment)
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = false
            };
            if (environment != null)
            {
                foreach (var kv in environment)
                {
                    if (!String.IsNullOrWhiteSpace(kv.Key) && kv.Value != null)
                        psi.EnvironmentVariables[kv.Key] = kv.Value;
                }
            }
            Process.Start(psi);
        }

        private static IEnumerable<string> SplitLines(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) yield break;
            foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                if (!String.IsNullOrWhiteSpace(line)) yield return line;
        }

        private static string BuildCmdLaunchCommand(string codex, List<string> args)
        {
            var extension = "";
            try { extension = Path.GetExtension(codex) ?? ""; } catch { }
            var needsCall = String.IsNullOrWhiteSpace(extension) ||
                String.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase);
            var command = (needsCall ? "call " : "") + QuoteCmdArg(codex);
            foreach (var arg in args) command += " " + QuoteCmdArg(arg);
            return command;
        }

        private static string BuildCommandPreview(string codex, List<string> args)
        {
            var command = codex ?? "";
            foreach (var arg in args) command += " " + arg;
            return command;
        }

        private static string QuoteCmdSet(string name, string value)
        {
            return "\"" + (name ?? "").Replace("\"", "") + "=" + (value ?? "").Replace("\"", "\"\"") + "\"";
        }

        private static string EscapeCmdEcho(string value)
        {
            value = value ?? "";
            return value
                .Replace("^", "^^")
                .Replace("&", "^&")
                .Replace("|", "^|")
                .Replace("<", "^<")
                .Replace(">", "^>");
        }

        private void CleanInvalidCodexServiceTier()
        {
            try
            {
                var codexHome = GetCodexHome();
                if (String.IsNullOrWhiteSpace(codexHome)) return;
                var configPath = Path.Combine(codexHome, "config.toml");
                if (!File.Exists(configPath)) return;
                var text = File.ReadAllText(configPath, Encoding.UTF8);
                var cleaned = Regex.Replace(
                    text,
                    @"(?m)^\s*service_tier\s*=\s*[""']default[""']\s*(?:\r?\n)?",
                    "");
                if (!String.Equals(text, cleaned, StringComparison.Ordinal))
                {
                    File.WriteAllText(configPath, cleaned, Encoding.UTF8);
                    SetStatus("已修正 Codex config.toml 中无效的 service_tier=default。");
                }
            }
            catch (Exception ex)
            {
                SetStatus("修正 Codex service_tier 失败：" + ex.Message);
            }
        }

        private void OpenSelectedFile()
        {
            var row = GetActiveDetailRow();
            if (row != null && File.Exists(row.FilePath)) Process.Start(new ProcessStartInfo(row.FilePath) { UseShellExecute = true });
        }

        private void OpenSelectedFolder()
        {
            var row = GetActiveDetailRow();
            var path = row == null ? "" : row.FilePath;
            if (File.Exists(path)) Process.Start("explorer.exe", "/select," + QuoteArg(path));
            else if (row != null && Directory.Exists(row.Cwd)) Process.Start(new ProcessStartInfo(row.Cwd) { UseShellExecute = true });
        }

        private void CopySelectedId()
        {
            var row = GetActiveDetailRow();
            if (row != null && !String.IsNullOrWhiteSpace(row.Id)) System.Windows.Clipboard.SetText(row.Id);
        }

        private void OpenPath(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return;
            if (Regex.IsMatch(path, "^https?://", RegexOptions.IgnoreCase))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return;
            }
            if (File.Exists(path) || Directory.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private void RunPowerShellScript(string script, string args)
        {
            var output = RunProcess("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File " + QuoteArg(script) + " " + args, _rootDir, 120000);
            if (output.ExitCode != 0) throw new InvalidOperationException(output.Output);
        }

        private ArrayList QueryJson(string db, string sql)
        {
            var sw = Stopwatch.StartNew();
            var output = RunSqliteJson(db, sql, 30000);
            if (output.ExitCode != 0) throw new InvalidOperationException(output.Output);
            var text = (output.Output ?? "").Trim();
            WriteDiagnostic("QueryJson db='" + db + "' outputLength=" + text.Length + " elapsedMs=" + sw.ElapsedMilliseconds + " sql='" + ShortenForLog(sql, 180) + "'.");
            if (String.IsNullOrWhiteSpace(text)) return new ArrayList();
            var parsed = _json.DeserializeObject(text);
            var arr = parsed as ArrayList;
            if (arr != null) return arr;
            var objects = parsed as object[];
            if (objects != null)
            {
                var list = new ArrayList();
                foreach (var item in objects) list.Add(item);
                return list;
            }
            return new ArrayList();
        }

        private ProcessResult RunSqliteJson(string db, string sql, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            var psi = new ProcessStartInfo(_sqlitePath, "-json " + QuoteArg(db))
            {
                WorkingDirectory = _rootDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using (var p = Process.Start(psi))
            {
                p.StandardInput.WriteLine(sql);
                p.StandardInput.Close();
                var stdout = p.StandardOutput.ReadToEnd();
                var stderr = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    throw new TimeoutException("sqlite3 执行超时。");
                }
                WriteDiagnostic("RunSqliteJson db='" + db + "' exit=" + p.ExitCode + " elapsedMs=" + sw.ElapsedMilliseconds + " sql='" + ShortenForLog(sql, 180) + "'.");
                return new ProcessResult { ExitCode = p.ExitCode, Output = stdout + stderr };
            }
        }

        private ProcessResult RunProcess(string file, string args, string cwd, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            var psi = new ProcessStartInfo(file, args)
            {
                WorkingDirectory = cwd,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using (var p = Process.Start(psi))
            {
                var stdout = p.StandardOutput.ReadToEnd();
                var stderr = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    throw new TimeoutException(file + " 执行超时。");
                }
                WriteDiagnostic("RunProcess file='" + file + "' exit=" + p.ExitCode + " elapsedMs=" + sw.ElapsedMilliseconds + " args='" + ShortenForLog(args, 180) + "'.");
                return new ProcessResult { ExitCode = p.ExitCode, Output = stdout + stderr };
            }
        }

        private static string ShortenForLog(string text, int maxLength)
        {
            text = (text ?? "").Replace("\r", " ").Replace("\n", " ");
            if (text.Length <= maxLength) return text;
            return text.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }

        private void SetStatus(string text)
        {
            if (_statusText != null) _statusText.Text = text;
        }

        private static void ApplyAccent(SessionRow row, string provider, bool archived)
        {
            if (archived)
            {
                row.Accent = MutedSmallBrush();
                row.AccentSoft = SoftHoverBrush();
            }
            else if (String.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase))
            {
                row.Accent = ColorBrush(CurrentTheme().AssistantAccent);
                row.AccentSoft = ColorBrush(CurrentTheme().AssistantBackground);
            }
            else if (String.Equals(provider, "rightcode", StringComparison.OrdinalIgnoreCase))
            {
                row.Accent = ColorBrush(CurrentTheme().ToolAccent);
                row.AccentSoft = ColorBrush(CurrentTheme().ToolBackground);
            }
            else
            {
                row.Accent = SteelBrush();
                row.AccentSoft = SoftHoverBrush();
            }
        }

        private static string ProviderLabel(string value)
        {
            if (String.Equals(value, "openai", StringComparison.OrdinalIgnoreCase)) return "OpenAI";
            return String.IsNullOrWhiteSpace(value) ? "" : value;
        }

        private static string ProviderValue(string label)
        {
            if (String.Equals(label, "OpenAI", StringComparison.OrdinalIgnoreCase)) return "openai";
            return (label ?? "").Trim();
        }

        private static void SelectProvider(ComboBox combo, string value)
        {
            if (combo == null) return;
            foreach (var item in combo.Items)
            {
                var p = item as ProviderItem;
                if (p != null && String.Equals(p.Value, value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private static string SelectedProviderValue(ComboBox combo)
        {
            if (combo == null || combo.SelectedItem == null) return "";
            var item = combo.SelectedItem as ProviderItem;
            return item == null ? combo.SelectedItem.ToString() : item.Value;
        }

        private static string NormalizePath(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return "";
            return path.Replace(@"\\?\", "").TrimEnd('\\');
        }

        private static string UnixMsToLocalText(long ms)
        {
            if (ms <= 0) return "";
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddMilliseconds(ms).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        private static string Shorten(string text, int max)
        {
            if (String.IsNullOrWhiteSpace(text)) return "";
            var clean = Regex.Replace(text, "\\s+", " ").Trim();
            return clean.Length <= max ? clean : clean.Substring(0, max - 1) + "...";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var v in values) if (!String.IsNullOrWhiteSpace(v)) return v;
            return "";
        }

        private static string DefaultString(string value, string fallback)
        {
            return String.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return "";
            return Convert.ToString(dict[key]);
        }

        private static Dictionary<string, object> GetDict(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key)) return null;
            return dict[key] as Dictionary<string, object>;
        }

        private static bool GetBool(Dictionary<string, object> dict, string key, bool fallback)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return fallback;
            try { return Convert.ToBoolean(dict[key]); } catch { return fallback; }
        }

        private static int GetInt(Dictionary<string, object> dict, string key, int fallback)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return fallback;
            try { return Convert.ToInt32(dict[key]); } catch { return fallback; }
        }

        private static long GetLong(Dictionary<string, object> dict, string key, long fallback)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return fallback;
            try { return Convert.ToInt64(dict[key]); } catch { return fallback; }
        }

        private static string QuoteArg(string value)
        {
            if (value == null) value = "";
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string QuoteCmdArg(string value)
        {
            if (value == null) value = "";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string QuotePowerShell(string value)
        {
            if (value == null) value = "";
            return "'" + value.Replace("'", "''") + "'";
        }
    }

    internal sealed class ProcessResult
    {
        public int ExitCode;
        public string Output;
    }
}
