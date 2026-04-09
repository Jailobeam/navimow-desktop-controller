using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace NavimowDesktopController
{
    internal sealed class MainForm : Form
    {
        private readonly NavimowApiClient apiClient;
        private readonly SessionStore sessionStore;
        private readonly OverlayMapStore overlayMapStore;
        private readonly List<NavimowDevice> devices;
        private readonly List<PointF> pathPoints;
        private DateTime lastMovementUtc;

        private SessionData session;
        private MqttWsClient mqttClient;
        private Dictionary<string, object> lastDeviceInfo;
        private Dictionary<string, object> lastStatus;
        private Dictionary<string, object> lastEvent;
        private Dictionary<string, object> lastAttributes;
        private Dictionary<string, object> lastLocation;
        private Dictionary<string, object> lastMqttInfo;

        private Button loginButton;
        private TextBox authorizationCodeTextBox;
        private Button tokenButton;
        private Button deleteTokenButton;
        private CheckBox themeModeCheckBox;
        private Label connectionStatusLabel;
        private Button getDevicesButton;
        private ComboBox deviceComboBox;
        private Button showStatusButton;
        private Button showMqttInfoButton;
        private CheckBox disableMqttCheckBox;
        private Button loadMapImageButton;
        private Button clearMapImageButton;
        private CheckBox editMapImageCheckBox;
        private Label overlayScaleLabel;
        private TrackBar overlayScaleTrackBar;
        private Label overlayRotationLabel;
        private TrackBar overlayRotationTrackBar;
        private Button startButton;
        private Button stopButton;
        private Button pauseButton;
        private Button resumeButton;
        private Button dockButton;
        private Label selectedDeviceLabel;
        private Label deviceStateLabel;
        private Label batteryLabel;
        private Label positionLabel;
        private Label errorLabel;
        private Label timestampLabel;
        private Label mqttStatusLabel;
        private Panel mapPanel;
        private Panel sidebarPanel;
        private Panel mapHostPanel;
        private Panel sidebarHostPanel;
        private TableLayoutPanel headerPanel;
        private TableLayoutPanel contentGridPanel;
        private TableLayoutPanel rightContentGridPanel;
        private TableLayoutPanel overlayHeaderPanel;
        private TableLayoutPanel overlayScaleHostPanel;
        private TableLayoutPanel overlayRotationHostPanel;
        private TableLayoutPanel commandButtonsGrid;
        private ThemedTabControl mainTabControl;
        private FlowLayoutPanel tabHeaderPanel;
        private Panel tabContentBorderPanel;
        private Panel tabContentHostPanel;
        private Panel allValuesPanel;
        private TreeView deviceTreeView;
        private TreeView statusTreeView;
        private TreeView eventTreeView;
        private TreeView attributesTreeView;
        private TreeView locationTreeView;
        private TreeView mqttInfoTreeView;
        private DarkTextView rawJsonTextView;
        private DarkTextView logTextView;
        private Timer refreshTimer;
        private string lastMqttFailureSignature;
        private DateTime lastDetailRefreshUtc;
        private static readonly TimeSpan DetailRefreshInterval = TimeSpan.FromSeconds(10);
        private Image overlayMapImage;
        private PointF overlayCenterWorld;
        private float overlayBaseWidthWorld;
        private float overlayBaseHeightWorld;
        private float overlayRotationDegrees;
        private bool overlayDragging;
        private Point overlayDragStartScreen;
        private bool mapProjectionValid;
        private float mapProjectionBaseScale;
        private float mapProjectionScale;
        private float mapProjectionOffsetX;
        private float mapProjectionOffsetY;
        private float mapProjectionMinX;
        private float mapProjectionMinY;
        private float mapViewZoom = 1F;
        private PointF mapViewPanScreen = PointF.Empty;
        private bool mapViewPanning;
        private Point mapViewPanStartScreen;
        private bool isDarkMode = true;
        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<Control> tabContentPanels = new List<Control>();
        private Button activeTabButton;
        private readonly List<AllValuesRow> allValuesRows = new List<AllValuesRow>();
        private int allValuesScrollOffset;
        private bool allValuesScrollbarDragging;
        private int allValuesScrollbarDragOffsetY;
        private static readonly Color AppBackgroundColor = Color.FromArgb(243, 246, 240);
        private static readonly Color SurfaceColor = Color.FromArgb(255, 255, 252);
        private static readonly Color SidebarColor = Color.FromArgb(234, 239, 230);
        private static readonly Color BorderColor = Color.FromArgb(206, 214, 203);
        private static readonly Color TextColor = Color.FromArgb(41, 52, 46);
        private static readonly Color MutedTextColor = Color.FromArgb(104, 115, 108);
        private static readonly Color AccentColor = Color.FromArgb(59, 123, 100);
        private static readonly Color AccentSoftColor = Color.FromArgb(222, 236, 228);
        private static readonly Color DangerColor = Color.FromArgb(183, 88, 74);

        private Color CurrentAppBackgroundColor { get { return this.isDarkMode ? Color.FromArgb(24, 29, 31) : AppBackgroundColor; } }
        private Color CurrentSurfaceColor { get { return this.isDarkMode ? Color.FromArgb(35, 41, 44) : SurfaceColor; } }
        private Color CurrentSidebarColor { get { return this.isDarkMode ? Color.FromArgb(29, 35, 38) : SidebarColor; } }
        private Color CurrentBorderColor { get { return this.isDarkMode ? Color.FromArgb(31, 36, 39) : BorderColor; } }
        private Color CurrentTextColor { get { return this.isDarkMode ? Color.FromArgb(236, 241, 238) : TextColor; } }
        private Color CurrentMutedTextColor { get { return this.isDarkMode ? Color.FromArgb(163, 173, 168) : MutedTextColor; } }
        private Color CurrentAccentColor { get { return this.isDarkMode ? Color.FromArgb(88, 176, 140) : AccentColor; } }
        private Color CurrentAccentSoftColor { get { return this.isDarkMode ? Color.FromArgb(49, 72, 64) : AccentSoftColor; } }
        private Color CurrentDangerColor { get { return this.isDarkMode ? Color.FromArgb(227, 140, 123) : DangerColor; } }
        private Color CurrentSuccessColor { get { return this.isDarkMode ? Color.FromArgb(146, 215, 176) : Color.FromArgb(45, 128, 87); } }
        private Color CurrentErrorColor { get { return this.isDarkMode ? Color.FromArgb(255, 166, 152) : Color.FromArgb(181, 73, 59); } }
        private Color CurrentMapPathColor { get { return this.isDarkMode ? Color.FromArgb(120, 171, 255) : Color.FromArgb(44, 111, 231); } }
        private Color CurrentMapMessageColor { get { return this.isDarkMode ? Color.FromArgb(173, 181, 188) : Color.FromArgb(124, 133, 138); } }
        private Color CurrentMarkerStartColor { get { return this.isDarkMode ? Color.FromArgb(255, 110, 96) : Color.FromArgb(235, 73, 61); } }
        private Color CurrentMarkerEndColor { get { return this.isDarkMode ? Color.FromArgb(115, 214, 132) : Color.FromArgb(46, 179, 85); } }
        private Color CurrentTableHeaderColor { get { return this.isDarkMode ? Color.FromArgb(44, 52, 56) : Color.FromArgb(241, 245, 240); } }
        private Color CurrentTableAlternateRowColor { get { return this.isDarkMode ? Color.FromArgb(39, 46, 50) : Color.FromArgb(248, 250, 246); } }
        private Color CurrentTableSelectionColor { get { return this.isDarkMode ? Color.FromArgb(56, 92, 78) : Color.FromArgb(214, 234, 223); } }
        private Color CurrentTableGridColor { get { return this.isDarkMode ? Color.FromArgb(43, 49, 53) : Color.FromArgb(214, 220, 214); } }

        [DllImport("dwmapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        private sealed class AllValuesRow
        {
            public string Path { get; set; }
            public string Label { get; set; }
            public string Value { get; set; }
        }

        public MainForm()
        {
            this.apiClient = new NavimowApiClient();
            this.sessionStore = new SessionStore();
            this.overlayMapStore = new OverlayMapStore();
            this.devices = new List<NavimowDevice>();
            this.pathPoints = new List<PointF>();
            this.lastMovementUtc = DateTime.MinValue;

            this.Text = "Navimow Controller";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(1180, 760);
            this.ClientSize = new Size(1320, 860);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.ForeColor = this.CurrentTextColor;
            this.BackColor = this.CurrentAppBackgroundColor;
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            this.lastDetailRefreshUtc = DateTime.MinValue;

            this.BuildLayout();
            this.WireEvents();
            this.LoadOverlayState();

            this.session = this.sessionStore.Load();
            if (this.session != null && !string.IsNullOrWhiteSpace(this.session.AccessToken))
            {
                this.connectionStatusLabel.Text = "Gespeicherter Token geladen.";
                this.connectionStatusLabel.ForeColor = this.CurrentSuccessColor;
            }
            else
            {
                this.connectionStatusLabel.Text = "Noch kein Token vorhanden.";
                this.connectionStatusLabel.ForeColor = this.CurrentErrorColor;
            }

            this.refreshTimer = new Timer();
            this.refreshTimer.Interval = 15000;
            this.refreshTimer.Tick += async (sender, args) =>
            {
                if (this.GetSelectedDeviceId() != null)
                {
                    await this.RefreshStatusAsync(false);
                }
            };
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (this.refreshTimer != null)
            {
                this.refreshTimer.Stop();
                this.refreshTimer.Dispose();
            }

            this.StopMqttClient();
            this.SaveOverlayState();
            this.DisposeOverlayImage();

            base.OnFormClosed(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            this.ApplyWindowTheme();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = this.CurrentAppBackgroundColor;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.Controls.Add(root);

            this.headerPanel = new TableLayoutPanel();
            this.headerPanel.Dock = DockStyle.Fill;
            this.headerPanel.BackColor = this.CurrentSurfaceColor;
            this.headerPanel.Padding = new Padding(14, 8, 14, 6);
            this.headerPanel.ColumnCount = 7;
            this.headerPanel.RowCount = 2;
            this.headerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            this.headerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            this.headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 182F));
            this.headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126F));
            this.headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            this.headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            this.headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
            this.headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 286F));
            root.Controls.Add(this.headerPanel, 0, 0);

            this.loginButton = new Button();
            this.loginButton.Text = "Login bei Navimow";
            this.loginButton.Dock = DockStyle.Fill;
            this.loginButton.Margin = new Padding(0, 0, 10, 0);
            this.headerPanel.Controls.Add(this.loginButton, 0, 0);

            var authLabel = new Label();
            authLabel.Text = "Authorization Code:";
            authLabel.Dock = DockStyle.Fill;
            authLabel.TextAlign = ContentAlignment.MiddleLeft;
            authLabel.Margin = new Padding(0, 0, 10, 0);
            this.headerPanel.Controls.Add(authLabel, 1, 0);

            this.authorizationCodeTextBox = new TextBox();
            this.authorizationCodeTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            this.authorizationCodeTextBox.Margin = new Padding(0, 5, 10, 5);
            this.headerPanel.Controls.Add(this.authorizationCodeTextBox, 2, 0);

            this.tokenButton = new Button();
            this.tokenButton.Text = "Token abrufen";
            this.tokenButton.Dock = DockStyle.Fill;
            this.tokenButton.Margin = new Padding(0, 0, 10, 0);
            this.headerPanel.Controls.Add(this.tokenButton, 3, 0);

            this.deleteTokenButton = new Button();
            this.deleteTokenButton.Text = "Token löschen";
            this.deleteTokenButton.Dock = DockStyle.Fill;
            this.deleteTokenButton.Margin = new Padding(0);
            this.headerPanel.Controls.Add(this.deleteTokenButton, 4, 0);

            this.themeModeCheckBox = new CheckBox();
            this.themeModeCheckBox.Text = "Dunkelmodus";
            this.themeModeCheckBox.AutoSize = true;
            this.themeModeCheckBox.Checked = true;
            this.themeModeCheckBox.Dock = DockStyle.Left;
            this.themeModeCheckBox.Margin = new Padding(0, 2, 0, 0);
            this.headerPanel.Controls.Add(this.themeModeCheckBox, 0, 1);
            this.headerPanel.SetColumnSpan(this.themeModeCheckBox, 2);

            this.connectionStatusLabel = new Label();
            this.connectionStatusLabel.Dock = DockStyle.Fill;
            this.connectionStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.connectionStatusLabel.AutoEllipsis = true;
            this.connectionStatusLabel.Margin = new Padding(0, 2, 0, 0);
            this.headerPanel.Controls.Add(this.connectionStatusLabel, 3, 1);
            this.headerPanel.SetColumnSpan(this.connectionStatusLabel, 2);

            this.overlayHeaderPanel = new TableLayoutPanel();
            this.overlayHeaderPanel.Dock = DockStyle.Fill;
            this.overlayHeaderPanel.Margin = new Padding(6, 0, 0, 0);
            this.overlayHeaderPanel.BackColor = this.CurrentSurfaceColor;
            this.overlayHeaderPanel.RowCount = 2;
            this.overlayHeaderPanel.ColumnCount = 4;
            this.overlayHeaderPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            this.overlayHeaderPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            this.overlayHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            this.overlayHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            this.overlayHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            this.overlayHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            this.headerPanel.Controls.Add(this.overlayHeaderPanel, 5, 0);
            this.headerPanel.SetColumnSpan(this.overlayHeaderPanel, 2);
            this.headerPanel.SetRowSpan(this.overlayHeaderPanel, 2);

            this.contentGridPanel = new TableLayoutPanel();
            this.contentGridPanel.Dock = DockStyle.Fill;
            this.contentGridPanel.BackColor = this.CurrentAppBackgroundColor;
            this.contentGridPanel.Padding = new Padding(12, 10, 12, 12);
            this.contentGridPanel.ColumnCount = 2;
            this.contentGridPanel.RowCount = 1;
            this.contentGridPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320F));
            this.contentGridPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.contentGridPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(this.contentGridPanel, 0, 1);

            this.sidebarHostPanel = new Panel();
            this.sidebarHostPanel.Dock = DockStyle.Fill;
            this.sidebarHostPanel.BackColor = this.CurrentSidebarColor;
            this.sidebarHostPanel.Margin = new Padding(0, 0, 14, 0);
            this.contentGridPanel.Controls.Add(this.sidebarHostPanel, 0, 0);

            this.sidebarPanel = new Panel();
            this.sidebarPanel.Dock = DockStyle.Fill;
            this.sidebarPanel.AutoScroll = true;
            this.sidebarPanel.BackColor = this.CurrentSidebarColor;
            this.sidebarHostPanel.Controls.Add(this.sidebarPanel);

            this.getDevicesButton = new Button();
            this.getDevicesButton.Text = "Geräte abrufen";
            this.getDevicesButton.Location = new Point(0, 12);
            this.getDevicesButton.Size = new Size(300, 34);
            this.sidebarPanel.Controls.Add(this.getDevicesButton);

            this.deviceComboBox = new ComboBox();
            this.deviceComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this.deviceComboBox.Location = new Point(0, 56);
            this.deviceComboBox.Size = new Size(300, 24);
            this.sidebarPanel.Controls.Add(this.deviceComboBox);

            this.showStatusButton = new Button();
            this.showStatusButton.Text = "Status aktualisieren";
            this.showStatusButton.Location = new Point(0, 100);
            this.showStatusButton.Size = new Size(300, 34);
            this.sidebarPanel.Controls.Add(this.showStatusButton);

            this.showMqttInfoButton = new Button();
            this.showMqttInfoButton.Text = "MQTT Info aktualisieren";
            this.showMqttInfoButton.Location = new Point(0, 144);
            this.showMqttInfoButton.Size = new Size(300, 34);
            this.sidebarPanel.Controls.Add(this.showMqttInfoButton);

            this.disableMqttCheckBox = new CheckBox();
            this.disableMqttCheckBox.Text = "MQTT deaktivieren";
            this.disableMqttCheckBox.AutoSize = true;
            this.disableMqttCheckBox.Location = new Point(0, 188);
            this.sidebarPanel.Controls.Add(this.disableMqttCheckBox);

            this.loadMapImageButton = new Button();
            this.loadMapImageButton.Text = "Bild laden";
            this.loadMapImageButton.Dock = DockStyle.Fill;
            this.loadMapImageButton.Margin = new Padding(0, 0, 8, 0);
            this.loadMapImageButton.MinimumSize = new Size(0, 34);
            this.sidebarPanel.Controls.Add(this.loadMapImageButton);

            this.clearMapImageButton = new Button();
            this.clearMapImageButton.Text = "Bild löschen";
            this.clearMapImageButton.Dock = DockStyle.Fill;
            this.clearMapImageButton.Margin = new Padding(0);
            this.clearMapImageButton.MinimumSize = new Size(0, 34);
            this.sidebarPanel.Controls.Add(this.clearMapImageButton);

            this.editMapImageCheckBox = new CheckBox();
            this.editMapImageCheckBox.Text = "Bild bearbeiten";
            this.editMapImageCheckBox.AutoSize = true;
            this.editMapImageCheckBox.Anchor = AnchorStyles.None;
            this.editMapImageCheckBox.Margin = new Padding(0, 2, 0, 0);
            this.sidebarPanel.Controls.Add(this.editMapImageCheckBox);

            this.overlayScaleLabel = new Label();
            this.overlayScaleLabel.Text = "Skalierung: 100 % (x1.00)";
            this.overlayScaleLabel.AutoSize = false;
            this.overlayScaleLabel.Dock = DockStyle.Fill;
            this.overlayScaleLabel.Margin = new Padding(0);
            this.overlayScaleLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.overlayScaleLabel.Font = new Font(this.Font.FontFamily, 7.5F, FontStyle.Regular);
            this.sidebarPanel.Controls.Add(this.overlayScaleLabel);

            this.overlayScaleTrackBar = new PrecisionTrackBar();
            this.overlayScaleTrackBar.AutoSize = false;
            this.overlayScaleTrackBar.Dock = DockStyle.Fill;
            this.overlayScaleTrackBar.Height = 20;
            this.overlayScaleTrackBar.Margin = new Padding(0);
            this.overlayScaleTrackBar.Minimum = 5;
            this.overlayScaleTrackBar.Maximum = 1000;
            this.overlayScaleTrackBar.TickFrequency = 100;
            this.overlayScaleTrackBar.SmallChange = 1;
            this.overlayScaleTrackBar.LargeChange = 1;
            this.overlayScaleTrackBar.Value = 100;
            this.sidebarPanel.Controls.Add(this.overlayScaleTrackBar);

            this.overlayRotationLabel = new Label();
            this.overlayRotationLabel.Text = "Drehung: 0 Grad";
            this.overlayRotationLabel.AutoSize = false;
            this.overlayRotationLabel.Dock = DockStyle.Fill;
            this.overlayRotationLabel.Margin = new Padding(0);
            this.overlayRotationLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.overlayRotationLabel.Font = new Font(this.Font.FontFamily, 7.5F, FontStyle.Regular);
            this.sidebarPanel.Controls.Add(this.overlayRotationLabel);

            this.overlayRotationTrackBar = new PrecisionTrackBar();
            this.overlayRotationTrackBar.AutoSize = false;
            this.overlayRotationTrackBar.Dock = DockStyle.Fill;
            this.overlayRotationTrackBar.Height = 20;
            this.overlayRotationTrackBar.Margin = new Padding(0);
            this.overlayRotationTrackBar.Minimum = -180;
            this.overlayRotationTrackBar.Maximum = 180;
            this.overlayRotationTrackBar.TickFrequency = 15;
            this.overlayRotationTrackBar.SmallChange = 1;
            this.overlayRotationTrackBar.LargeChange = 1;
            this.overlayRotationTrackBar.Value = 0;
            this.sidebarPanel.Controls.Add(this.overlayRotationTrackBar);

            this.overlayScaleHostPanel = new TableLayoutPanel();
            this.overlayScaleHostPanel.Dock = DockStyle.Fill;
            this.overlayScaleHostPanel.Margin = new Padding(8, 0, 8, 0);
            this.overlayScaleHostPanel.BackColor = this.CurrentSurfaceColor;
            this.overlayScaleHostPanel.RowCount = 2;
            this.overlayScaleHostPanel.ColumnCount = 1;
            this.overlayScaleHostPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));
            this.overlayScaleHostPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            this.overlayScaleHostPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.overlayScaleHostPanel.Controls.Add(this.overlayScaleLabel, 0, 0);
            this.overlayScaleHostPanel.Controls.Add(this.overlayScaleTrackBar, 0, 1);
            this.overlayHeaderPanel.Controls.Add(this.overlayScaleHostPanel, 2, 0);
            this.overlayHeaderPanel.SetRowSpan(this.overlayScaleHostPanel, 2);

            this.overlayRotationHostPanel = new TableLayoutPanel();
            this.overlayRotationHostPanel.Dock = DockStyle.Fill;
            this.overlayRotationHostPanel.Margin = new Padding(0);
            this.overlayRotationHostPanel.BackColor = this.CurrentSurfaceColor;
            this.overlayRotationHostPanel.RowCount = 2;
            this.overlayRotationHostPanel.ColumnCount = 1;
            this.overlayRotationHostPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));
            this.overlayRotationHostPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            this.overlayRotationHostPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.overlayRotationHostPanel.Controls.Add(this.overlayRotationLabel, 0, 0);
            this.overlayRotationHostPanel.Controls.Add(this.overlayRotationTrackBar, 0, 1);
            this.overlayHeaderPanel.Controls.Add(this.overlayRotationHostPanel, 3, 0);
            this.overlayHeaderPanel.SetRowSpan(this.overlayRotationHostPanel, 2);

            this.overlayHeaderPanel.Controls.Add(this.loadMapImageButton, 0, 0);
            this.overlayHeaderPanel.Controls.Add(this.clearMapImageButton, 1, 0);
            this.overlayHeaderPanel.Controls.Add(this.editMapImageCheckBox, 0, 1);
            this.overlayHeaderPanel.SetColumnSpan(this.editMapImageCheckBox, 2);

            var infoHeader = new Label();
            infoHeader.Text = "Übersicht:";
            infoHeader.Font = new Font(this.Font, FontStyle.Bold);
            infoHeader.AutoSize = true;
            infoHeader.Location = new Point(0, 226);
            this.sidebarPanel.Controls.Add(infoHeader);

            this.selectedDeviceLabel = this.CreateInfoLabel("Gerät: -", 252, this.sidebarPanel);
            this.deviceStateLabel = this.CreateInfoLabel("Zustand: -", 276, this.sidebarPanel);
            this.batteryLabel = this.CreateInfoLabel("Akku: -", 300, this.sidebarPanel);
            this.positionLabel = this.CreateInfoLabel("Position: -", 348, this.sidebarPanel);
            this.errorLabel = this.CreateInfoLabel("Fehler: -", 372, this.sidebarPanel);
            this.timestampLabel = this.CreateInfoLabel("Zeit: -", 396, this.sidebarPanel);
            this.mqttStatusLabel = this.CreateInfoLabel("MQTT: -", 420, this.sidebarPanel);
            this.selectedDeviceLabel.Top = 252;
            this.deviceStateLabel.Top = 276;
            this.batteryLabel.Top = 300;
            this.positionLabel.Top = 348;
            this.errorLabel.Top = 372;
            this.timestampLabel.Top = 396;
            this.mqttStatusLabel.Top = 420;
            this.positionLabel.Top = 324;
            this.errorLabel.Top = this.positionLabel.Top + 24;
            this.timestampLabel.Top = this.errorLabel.Top + 24;
            this.mqttStatusLabel.Top = this.timestampLabel.Top + 24;

            var commandHeader = new Label();
            commandHeader.Text = "Kommandos:";
            commandHeader.Font = new Font(this.Font, FontStyle.Bold);
            commandHeader.AutoSize = true;
            commandHeader.Location = new Point(0, 468);
            this.sidebarPanel.Controls.Add(commandHeader);

            this.startButton = this.CreateCommandButton("Start", 0, 498);
            this.stopButton = this.CreateCommandButton("Stop", 76, 498);
            this.pauseButton = this.CreateCommandButton("Pause", 152, 498);
            this.resumeButton = this.CreateCommandButton("Resume", 228, 498);
            this.dockButton = this.CreateCommandButton("Dock", 0, 536);

            this.commandButtonsGrid = new TableLayoutPanel();
            this.commandButtonsGrid.Location = new Point(0, 498);
            this.commandButtonsGrid.Size = new Size(300, 32);
            this.commandButtonsGrid.ColumnCount = 4;
            this.commandButtonsGrid.RowCount = 1;
            this.commandButtonsGrid.Margin = new Padding(0);
            this.commandButtonsGrid.Padding = new Padding(0);
            this.commandButtonsGrid.BackColor = this.CurrentSidebarColor;
            this.commandButtonsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            this.commandButtonsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            this.commandButtonsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            this.commandButtonsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            this.startButton.Dock = DockStyle.Fill;
            this.stopButton.Dock = DockStyle.Fill;
            this.pauseButton.Dock = DockStyle.Fill;
            this.resumeButton.Dock = DockStyle.Fill;
            this.startButton.Margin = new Padding(0, 0, 6, 0);
            this.stopButton.Margin = new Padding(0, 0, 6, 0);
            this.pauseButton.Margin = new Padding(0, 0, 6, 0);
            this.resumeButton.Margin = new Padding(0);
            this.commandButtonsGrid.Controls.Add(this.startButton, 0, 0);
            this.commandButtonsGrid.Controls.Add(this.stopButton, 1, 0);
            this.commandButtonsGrid.Controls.Add(this.pauseButton, 2, 0);
            this.commandButtonsGrid.Controls.Add(this.resumeButton, 3, 0);

            this.dockButton.Location = new Point(0, 540);
            this.dockButton.Size = new Size(144, 30);

            this.sidebarPanel.Controls.Add(this.commandButtonsGrid);
            this.sidebarPanel.Controls.Add(this.dockButton);

            this.rightContentGridPanel = new TableLayoutPanel();
            this.rightContentGridPanel.Dock = DockStyle.Fill;
            this.rightContentGridPanel.Margin = new Padding(0);
            this.rightContentGridPanel.BackColor = this.CurrentAppBackgroundColor;
            this.rightContentGridPanel.ColumnCount = 1;
            this.rightContentGridPanel.RowCount = 2;
            this.rightContentGridPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 330F));
            this.rightContentGridPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.contentGridPanel.Controls.Add(this.rightContentGridPanel, 1, 0);

            var mapHost = new Panel();
            mapHost.Dock = DockStyle.Fill;
            mapHost.Margin = new Padding(0);
            mapHost.Padding = new Padding(0);
            mapHost.BackColor = this.CurrentBorderColor;
            this.rightContentGridPanel.Controls.Add(mapHost, 0, 0);
            this.mapHostPanel = mapHost;

            this.mapPanel = new Panel();
            this.mapPanel.Dock = DockStyle.Fill;
            this.mapPanel.BackColor = this.CurrentSurfaceColor;
            this.mapPanel.Margin = new Padding(0);
            this.mapPanel.TabStop = true;
            this.mapPanel.Paint += this.MapPanel_Paint;
            mapHost.Controls.Add(this.mapPanel);
            EnableDoubleBuffer(this.mapPanel);

            var tabAreaLayout = new TableLayoutPanel();
            tabAreaLayout.Dock = DockStyle.Fill;
            tabAreaLayout.Margin = new Padding(0, 8, 0, 0);
            tabAreaLayout.BackColor = this.CurrentAppBackgroundColor;
            tabAreaLayout.ColumnCount = 1;
            tabAreaLayout.RowCount = 2;
            tabAreaLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            tabAreaLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.rightContentGridPanel.Controls.Add(tabAreaLayout, 0, 1);

            this.tabHeaderPanel = new FlowLayoutPanel();
            this.tabHeaderPanel.Dock = DockStyle.Fill;
            this.tabHeaderPanel.Margin = new Padding(0);
            this.tabHeaderPanel.Padding = new Padding(0);
            this.tabHeaderPanel.WrapContents = false;
            this.tabHeaderPanel.AutoScroll = false;
            this.tabHeaderPanel.FlowDirection = FlowDirection.LeftToRight;
            this.tabHeaderPanel.BackColor = this.CurrentAppBackgroundColor;
            tabAreaLayout.Controls.Add(this.tabHeaderPanel, 0, 0);

            this.tabContentBorderPanel = new Panel();
            this.tabContentBorderPanel.Dock = DockStyle.Fill;
            this.tabContentBorderPanel.Margin = new Padding(0);
            this.tabContentBorderPanel.Padding = new Padding(1);
            this.tabContentBorderPanel.BackColor = this.CurrentBorderColor;
            tabAreaLayout.Controls.Add(this.tabContentBorderPanel, 0, 1);

            this.tabContentHostPanel = new Panel();
            this.tabContentHostPanel.Dock = DockStyle.Fill;
            this.tabContentHostPanel.Margin = new Padding(0);
            this.tabContentHostPanel.Padding = new Padding(0);
            this.tabContentHostPanel.BackColor = this.CurrentSurfaceColor;
            this.tabContentBorderPanel.Controls.Add(this.tabContentHostPanel);

            // Legacy compatibility container kept non-visual to avoid destabilizing existing code paths.
            this.mainTabControl = new ThemedTabControl();


            this.allValuesPanel = new Panel();
            this.allValuesPanel.Dock = DockStyle.Fill;
            this.allValuesPanel.Margin = new Padding(0);
            this.allValuesPanel.BackColor = this.CurrentSurfaceColor;
            this.allValuesPanel.TabStop = true;
            this.allValuesPanel.Paint += this.AllValuesPanel_Paint;
            this.AddCustomTab("Alle Werte", this.allValuesPanel);
            EnableDoubleBuffer(this.allValuesPanel);

            this.deviceTreeView = this.CreateTreeView();
            this.mainTabControl.TabPages.Add(this.CreateTab("Gerät", this.deviceTreeView));

            this.AddCustomTab("Gerät", this.deviceTreeView);

            this.statusTreeView = this.CreateTreeView();
            this.AddCustomTab("Status", this.statusTreeView);

            this.eventTreeView = this.CreateTreeView();
            this.AddCustomTab("MQTT Event", this.eventTreeView);

            this.attributesTreeView = this.CreateTreeView();
            this.AddCustomTab("MQTT Attribute", this.attributesTreeView);

            this.locationTreeView = this.CreateTreeView();
            this.AddCustomTab("MQTT Location", this.locationTreeView);

            this.mqttInfoTreeView = this.CreateTreeView();
            this.AddCustomTab("MQTT Info", this.mqttInfoTreeView);

            this.rawJsonTextView = new DarkTextView();
            this.rawJsonTextView.Dock = DockStyle.Fill;
            this.rawJsonTextView.Font = new Font("Consolas", 10F);
            this.AddCustomTab("Raw JSON", this.rawJsonTextView);

            this.logTextView = new DarkTextView();
            this.logTextView.Dock = DockStyle.Fill;
            this.logTextView.Font = new Font("Consolas", 10F);
            this.logTextView.StickToBottomOnAppend = true;
            this.AddCustomTab("Log", this.logTextView);
            if (this.tabButtons.Count > 0)
            {
                this.SetActiveTab(this.tabButtons[0]);
            }
            this.ApplyTheme();
        }

        private Label CreateInfoLabel(string text, int top, Control parent)
        {
            var label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Location = new Point(12, top);
            label.ForeColor = this.CurrentTextColor;
            parent.Controls.Add(label);
            return label;
        }

        private Button CreateCommandButton(string text, int x, int y)
        {
            var button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(70, 30);
            this.StyleButton(button, false, false);
            return button;
        }

        private TreeView CreateTreeView()
        {
            var treeView = new TreeView();
            treeView.Dock = DockStyle.Fill;
            treeView.HideSelection = false;
            treeView.BackColor = this.CurrentSurfaceColor;
            treeView.ForeColor = this.CurrentTextColor;
            treeView.BorderStyle = BorderStyle.None;
            treeView.LineColor = this.CurrentBorderColor;
            return treeView;
        }

        private TabPage CreateTab(string title, Control control)
        {
            var page = new TabPage(title);
            page.UseVisualStyleBackColor = false;
            page.BackColor = this.CurrentSurfaceColor;
            page.Padding = new Padding(0);

            var contentHost = new Panel();
            contentHost.Dock = DockStyle.Fill;
            contentHost.Margin = new Padding(0);
            contentHost.Padding = new Padding(0);
            contentHost.BackColor = this.CurrentSurfaceColor;

            control.Margin = new Padding(0);
            contentHost.Controls.Add(control);
            page.Controls.Add(contentHost);
            return page;
        }

        private void AddCustomTab(string title, Control control)
        {
            var tabButton = new Button();
            tabButton.Text = title;
            tabButton.Tag = control;
            tabButton.AutoSize = true;
            tabButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tabButton.Padding = new Padding(14, 0, 14, 0);
            tabButton.Height = 30;
            tabButton.Margin = new Padding(0, 0, 4, 0);
            tabButton.FlatStyle = FlatStyle.Flat;
            tabButton.FlatAppearance.BorderSize = 1;
            tabButton.Cursor = Cursors.Hand;
            tabButton.Click += (sender, args) => this.SetActiveTab((Button)sender);
            this.tabHeaderPanel.Controls.Add(tabButton);
            this.tabButtons.Add(tabButton);

            var contentHost = new Panel();
            contentHost.Dock = DockStyle.Fill;
            contentHost.Margin = new Padding(0);
            contentHost.Padding = new Padding(0);
            contentHost.BackColor = this.CurrentSurfaceColor;
            contentHost.Visible = false;
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0);
            contentHost.Controls.Add(control);
            this.tabContentHostPanel.Controls.Add(contentHost);
            this.tabContentPanels.Add(contentHost);
        }

        private void SetActiveTab(Button button)
        {
            if (button == null)
            {
                return;
            }

            this.activeTabButton = button;
            for (int index = 0; index < this.tabButtons.Count; index++)
            {
                var tabButton = this.tabButtons[index];
                var active = tabButton == button;
                this.StyleTabButton(tabButton, active);
                if (index < this.tabContentPanels.Count)
                {
                    this.tabContentPanels[index].Visible = active;
                    if (active)
                    {
                        this.tabContentPanels[index].BringToFront();
                    }
                }
            }
        }

        private void StyleTabButton(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            button.ForeColor = active ? this.CurrentTextColor : this.CurrentMutedTextColor;
            button.BackColor = active ? this.CurrentSurfaceColor : this.CurrentSidebarColor;
            button.FlatAppearance.BorderColor = this.CurrentBorderColor;
            button.FlatAppearance.MouseDownBackColor = active ? this.CurrentSurfaceColor : this.CurrentSidebarColor;
            button.FlatAppearance.MouseOverBackColor = active ? this.CurrentSurfaceColor : this.CurrentSurfaceColor;
        }

        private void ApplyTheme()
        {
            this.BackColor = this.CurrentAppBackgroundColor;
            this.ForeColor = this.CurrentTextColor;
            foreach (Control control in this.Controls)
            {
                var table = control as TableLayoutPanel;
                if (table != null)
                {
                    table.BackColor = this.CurrentAppBackgroundColor;
                }
            }

            if (this.headerPanel != null)
            {
                this.headerPanel.BackColor = this.CurrentSurfaceColor;
                this.ApplyLabelTheme(this.headerPanel);
            }

            if (this.overlayHeaderPanel != null)
            {
                this.overlayHeaderPanel.BackColor = this.CurrentSurfaceColor;
            }

            if (this.overlayScaleHostPanel != null)
            {
                this.overlayScaleHostPanel.BackColor = this.CurrentSurfaceColor;
            }

            if (this.overlayRotationHostPanel != null)
            {
                this.overlayRotationHostPanel.BackColor = this.CurrentSurfaceColor;
            }

            if (this.contentGridPanel != null)
            {
                this.contentGridPanel.BackColor = this.CurrentAppBackgroundColor;
            }

            if (this.sidebarHostPanel != null)
            {
                this.sidebarHostPanel.BackColor = this.CurrentSidebarColor;
            }

            if (this.sidebarPanel != null)
            {
                this.sidebarPanel.BackColor = this.CurrentSidebarColor;
                this.ApplyLabelTheme(this.sidebarPanel);
            }

            if (this.rightContentGridPanel != null)
            {
                this.rightContentGridPanel.BackColor = this.CurrentAppBackgroundColor;
            }

            if (this.tabHeaderPanel != null)
            {
                this.tabHeaderPanel.BackColor = this.CurrentAppBackgroundColor;
            }

            if (this.tabContentBorderPanel != null)
            {
                this.tabContentBorderPanel.BackColor = this.CurrentBorderColor;
            }

            if (this.tabContentHostPanel != null)
            {
                this.tabContentHostPanel.BackColor = this.CurrentSurfaceColor;
            }

            if (this.mapHostPanel != null)
            {
                this.mapHostPanel.BackColor = this.CurrentBorderColor;
            }

            this.StyleButton(this.loginButton, false, false);
            this.StyleButton(this.tokenButton, false, false);
            this.StyleButton(this.getDevicesButton, false, false);
            this.StyleButton(this.showStatusButton, false, false);
            this.StyleButton(this.showMqttInfoButton, false, false);
            this.StyleButton(this.loadMapImageButton, false, false);
            this.StyleButton(this.clearMapImageButton, false, false);
            this.StyleButton(this.startButton, false, false);
            this.StyleButton(this.stopButton, false, false);
            this.StyleButton(this.pauseButton, false, false);
            this.StyleButton(this.resumeButton, false, false);
            this.StyleButton(this.dockButton, false, false);
            this.StyleButton(this.deleteTokenButton, false, false);

            this.StyleInput(this.authorizationCodeTextBox);
            this.StyleComboBox(this.deviceComboBox);

            this.disableMqttCheckBox.ForeColor = this.CurrentTextColor;
            this.disableMqttCheckBox.BackColor = this.CurrentSidebarColor;
            this.editMapImageCheckBox.ForeColor = this.CurrentTextColor;
            this.editMapImageCheckBox.BackColor = this.CurrentSurfaceColor;
            this.themeModeCheckBox.ForeColor = this.CurrentTextColor;
            this.themeModeCheckBox.BackColor = this.CurrentSurfaceColor;
            this.overlayScaleLabel.ForeColor = this.CurrentTextColor;
            this.overlayScaleLabel.BackColor = this.CurrentSurfaceColor;
            this.overlayRotationLabel.ForeColor = this.CurrentTextColor;
            this.overlayRotationLabel.BackColor = this.CurrentSurfaceColor;
            this.overlayScaleTrackBar.BackColor = this.CurrentSurfaceColor;
            this.overlayRotationTrackBar.BackColor = this.CurrentSurfaceColor;
            this.commandButtonsGrid.BackColor = this.CurrentSidebarColor;

            this.allValuesPanel.BackColor = this.CurrentSurfaceColor;
            this.deviceTreeView.BackColor = this.CurrentSurfaceColor;
            this.deviceTreeView.ForeColor = this.CurrentTextColor;
            this.deviceTreeView.BorderStyle = BorderStyle.None;
            this.deviceTreeView.LineColor = this.CurrentBorderColor;
            this.statusTreeView.BackColor = this.CurrentSurfaceColor;
            this.statusTreeView.ForeColor = this.CurrentTextColor;
            this.statusTreeView.BorderStyle = BorderStyle.None;
            this.statusTreeView.LineColor = this.CurrentBorderColor;
            this.eventTreeView.BackColor = this.CurrentSurfaceColor;
            this.eventTreeView.ForeColor = this.CurrentTextColor;
            this.eventTreeView.BorderStyle = BorderStyle.None;
            this.eventTreeView.LineColor = this.CurrentBorderColor;
            this.attributesTreeView.BackColor = this.CurrentSurfaceColor;
            this.attributesTreeView.ForeColor = this.CurrentTextColor;
            this.attributesTreeView.BorderStyle = BorderStyle.None;
            this.attributesTreeView.LineColor = this.CurrentBorderColor;
            this.locationTreeView.BackColor = this.CurrentSurfaceColor;
            this.locationTreeView.ForeColor = this.CurrentTextColor;
            this.locationTreeView.BorderStyle = BorderStyle.None;
            this.locationTreeView.LineColor = this.CurrentBorderColor;
            this.mqttInfoTreeView.BackColor = this.CurrentSurfaceColor;
            this.mqttInfoTreeView.ForeColor = this.CurrentTextColor;
            this.mqttInfoTreeView.BorderStyle = BorderStyle.None;
            this.mqttInfoTreeView.LineColor = this.CurrentBorderColor;

            this.rawJsonTextView.BackColor = this.CurrentSurfaceColor;
            this.rawJsonTextView.ForeColor = this.CurrentTextColor;
            this.rawJsonTextView.ScrollbarTrackColor = this.CurrentSidebarColor;
            this.rawJsonTextView.ScrollbarThumbColor = this.CurrentAccentSoftColor;
            this.rawJsonTextView.BorderColor = this.CurrentTableGridColor;

            this.logTextView.BackColor = this.CurrentSurfaceColor;
            this.logTextView.ForeColor = this.CurrentTextColor;
            this.logTextView.ScrollbarTrackColor = this.CurrentSidebarColor;
            this.logTextView.ScrollbarThumbColor = this.CurrentAccentSoftColor;
            this.logTextView.BorderColor = this.CurrentTableGridColor;

            this.mapPanel.BackColor = this.CurrentSurfaceColor;

            if (this.activeTabButton != null)
            {
                foreach (var tabButton in this.tabButtons)
                {
                    this.StyleTabButton(tabButton, tabButton == this.activeTabButton);
                }
            }

            this.ApplyNativeControlThemes();
            this.ApplyWindowTheme();
            this.ApplyConnectionStatusColor();
            this.allValuesPanel.Invalidate();
            this.mapPanel.Invalidate();
        }

        private void StyleButton(Button button, bool primary, bool danger)
        {
            if (button == null)
            {
                return;
            }

            button.FlatStyle = FlatStyle.Flat;
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(this.CurrentAccentColor, 0.08F);
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(this.CurrentAccentColor, 0.08F);
            button.Font = new Font(this.Font, FontStyle.Regular);
            button.BackColor = this.CurrentAccentColor;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = ControlPaint.Dark(this.CurrentAccentColor, 0.12F);
        }

        private void StyleInput(TextBox textBox)
        {
            if (textBox == null)
            {
                return;
            }

            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.BackColor = this.CurrentSurfaceColor;
            textBox.ForeColor = this.CurrentTextColor;
        }

        private void StyleComboBox(ComboBox comboBox)
        {
            if (comboBox == null)
            {
                return;
            }

            comboBox.FlatStyle = FlatStyle.Flat;
            comboBox.BackColor = this.CurrentSurfaceColor;
            comboBox.ForeColor = this.CurrentTextColor;
        }

        private void MainTabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (this.mainTabControl == null || e.Index < 0 || e.Index >= this.mainTabControl.TabPages.Count)
            {
                return;
            }

            var graphics = e.Graphics;
            var bounds = e.Bounds;
            var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var page = this.mainTabControl.TabPages[e.Index];
            var fillColor = selected ? this.CurrentSurfaceColor : this.CurrentSidebarColor;
            using (var brush = new SolidBrush(fillColor))
            {
                graphics.FillRectangle(brush, bounds);
            }

            using (var borderPen = new Pen(this.CurrentBorderColor))
            {
                graphics.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            }

            if (selected)
            {
                using (var accentBrush = new SolidBrush(this.CurrentAccentColor))
                {
                    graphics.FillRectangle(accentBrush, bounds.X + 1, bounds.Bottom - 3, bounds.Width - 2, 3);
                }
            }

            TextRenderer.DrawText(
                graphics,
                page.Text,
                this.Font,
                bounds,
                selected ? this.CurrentTextColor : this.CurrentMutedTextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void AllValuesPanel_Paint(object sender, PaintEventArgs e)
        {
            var panel = this.allValuesPanel;
            if (panel == null)
            {
                return;
            }

            const int headerHeight = 30;
            const int rowHeight = 24;
            const int scrollbarWidth = 14;

            var width = Math.Max(0, panel.ClientSize.Width);
            var height = Math.Max(0, panel.ClientSize.Height);
            var contentWidth = Math.Max(0, width - scrollbarWidth);
            var pathWidth = Math.Min(260, Math.Max(120, contentWidth / 3));
            var labelWidth = Math.Min(180, Math.Max(110, contentWidth / 5));
            var valueWidth = Math.Max(80, contentWidth - pathWidth - labelWidth);

            var graphics = e.Graphics;
            graphics.Clear(this.CurrentSurfaceColor);

            using (var borderPen = new Pen(this.CurrentTableGridColor))
            using (var headerBrush = new SolidBrush(this.CurrentTableHeaderColor))
            using (var rowBrush = new SolidBrush(this.CurrentSurfaceColor))
            using (var altRowBrush = new SolidBrush(this.CurrentTableAlternateRowColor))
            using (var textBrush = new SolidBrush(this.CurrentTextColor))
            using (var mutedBrush = new SolidBrush(this.CurrentMutedTextColor))
            using (var scrollbarTrackBrush = new SolidBrush(this.CurrentSidebarColor))
            using (var scrollbarThumbBrush = new SolidBrush(this.CurrentAccentSoftColor))
            {
                var headerBounds = new Rectangle(0, 0, contentWidth, headerHeight);
                graphics.FillRectangle(headerBrush, headerBounds);
                graphics.DrawRectangle(borderPen, 0, 0, contentWidth - 1, headerHeight - 1);

                var pathHeader = new Rectangle(0, 0, pathWidth, headerHeight);
                var labelHeader = new Rectangle(pathWidth, 0, labelWidth, headerHeight);
                var valueHeader = new Rectangle(pathWidth + labelWidth, 0, valueWidth, headerHeight);
                this.DrawAllValuesCell(graphics, pathHeader, "Pfad", textBrush, borderPen, true);
                this.DrawAllValuesCell(graphics, labelHeader, "Label", textBrush, borderPen, true);
                this.DrawAllValuesCell(graphics, valueHeader, "Wert", textBrush, borderPen, true);

                var visibleHeight = Math.Max(0, height - headerHeight);
                var visibleRows = Math.Max(1, visibleHeight / rowHeight);
                var maxOffset = Math.Max(0, this.allValuesRows.Count - visibleRows);
                if (this.allValuesScrollOffset > maxOffset)
                {
                    this.allValuesScrollOffset = maxOffset;
                }

                for (int rowIndex = 0; rowIndex < visibleRows; rowIndex++)
                {
                    var dataIndex = this.allValuesScrollOffset + rowIndex;
                    var y = headerHeight + (rowIndex * rowHeight);
                    var rowBounds = new Rectangle(0, y, contentWidth, rowHeight);
                    graphics.FillRectangle(dataIndex % 2 == 0 ? rowBrush : altRowBrush, rowBounds);

                    if (dataIndex < this.allValuesRows.Count)
                    {
                        var row = this.allValuesRows[dataIndex];
                        this.DrawAllValuesCell(graphics, new Rectangle(0, y, pathWidth, rowHeight), row.Path, textBrush, borderPen, false);
                        this.DrawAllValuesCell(graphics, new Rectangle(pathWidth, y, labelWidth, rowHeight), row.Label, textBrush, borderPen, false);
                        this.DrawAllValuesCell(graphics, new Rectangle(pathWidth + labelWidth, y, valueWidth, rowHeight), row.Value, textBrush, borderPen, false);
                    }
                    else
                    {
                        this.DrawAllValuesCell(graphics, new Rectangle(0, y, pathWidth, rowHeight), string.Empty, mutedBrush, borderPen, false);
                        this.DrawAllValuesCell(graphics, new Rectangle(pathWidth, y, labelWidth, rowHeight), string.Empty, mutedBrush, borderPen, false);
                        this.DrawAllValuesCell(graphics, new Rectangle(pathWidth + labelWidth, y, valueWidth, rowHeight), string.Empty, mutedBrush, borderPen, false);
                    }
                }

                var scrollbarBounds = new Rectangle(contentWidth, 0, scrollbarWidth, height);
                graphics.FillRectangle(scrollbarTrackBrush, scrollbarBounds);
                graphics.DrawRectangle(borderPen, scrollbarBounds.X, scrollbarBounds.Y, scrollbarBounds.Width - 1, scrollbarBounds.Height - 1);

                if (this.allValuesRows.Count > visibleRows)
                {
                    var thumbHeight = Math.Max(36, (int)Math.Round((visibleRows / (double)this.allValuesRows.Count) * visibleHeight));
                    var thumbTravel = Math.Max(1, visibleHeight - thumbHeight);
                    var thumbY = headerHeight + (int)Math.Round((this.allValuesScrollOffset / (double)maxOffset) * thumbTravel);
                    var thumbBounds = new Rectangle(contentWidth + 2, thumbY, scrollbarWidth - 4, thumbHeight);
                    graphics.FillRectangle(scrollbarThumbBrush, thumbBounds);
                    graphics.DrawRectangle(borderPen, thumbBounds.X, thumbBounds.Y, thumbBounds.Width - 1, thumbBounds.Height - 1);
                }
            }
        }

        private void DrawAllValuesCell(Graphics graphics, Rectangle bounds, string text, Brush textBrush, Pen borderPen, bool isHeader)
        {
            graphics.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            var textBounds = new Rectangle(bounds.X + 8, bounds.Y + (isHeader ? 0 : 1), Math.Max(0, bounds.Width - 16), Math.Max(0, bounds.Height - 2));
            TextRenderer.DrawText(
                graphics,
                text ?? string.Empty,
                this.Font,
                textBounds,
                isHeader ? this.CurrentTextColor : this.CurrentTextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void AllValuesPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            var delta = e.Delta > 0 ? -3 : 3;
            this.ScrollAllValues(delta);
        }

        private void AllValuesPanel_MouseDown(object sender, MouseEventArgs e)
        {
            Rectangle thumbBounds;
            if (!this.TryGetAllValuesScrollbarThumbBounds(out thumbBounds))
            {
                return;
            }

            if (thumbBounds.Contains(e.Location))
            {
                this.allValuesScrollbarDragging = true;
                this.allValuesScrollbarDragOffsetY = e.Y - thumbBounds.Y;
                this.allValuesPanel.Focus();
                return;
            }

            if (e.X >= thumbBounds.X)
            {
                var delta = e.Y < thumbBounds.Y ? -this.GetAllValuesVisibleRowCount() : this.GetAllValuesVisibleRowCount();
                this.ScrollAllValues(delta);
            }
        }

        private void AllValuesPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (!this.allValuesScrollbarDragging)
            {
                return;
            }

            const int headerHeight = 30;
            var visibleRows = this.GetAllValuesVisibleRowCount();
            var maxOffset = Math.Max(0, this.allValuesRows.Count - visibleRows);
            if (maxOffset <= 0)
            {
                return;
            }

            Rectangle thumbBounds;
            if (!this.TryGetAllValuesScrollbarThumbBounds(out thumbBounds))
            {
                return;
            }

            var trackTop = headerHeight;
            var trackHeight = Math.Max(1, this.allValuesPanel.ClientSize.Height - headerHeight - thumbBounds.Height);
            var thumbTop = Math.Max(trackTop, Math.Min(trackTop + trackHeight, e.Y - this.allValuesScrollbarDragOffsetY));
            var ratio = (thumbTop - trackTop) / (double)trackHeight;
            this.allValuesScrollOffset = Math.Max(0, Math.Min(maxOffset, (int)Math.Round(ratio * maxOffset)));
            this.allValuesPanel.Invalidate();
        }

        private void AllValuesPanel_MouseUp(object sender, MouseEventArgs e)
        {
            this.allValuesScrollbarDragging = false;
        }

        private void ScrollAllValues(int delta)
        {
            var visibleRows = this.GetAllValuesVisibleRowCount();
            var maxOffset = Math.Max(0, this.allValuesRows.Count - visibleRows);
            this.allValuesScrollOffset = Math.Max(0, Math.Min(maxOffset, this.allValuesScrollOffset + delta));
            this.allValuesPanel.Invalidate();
        }

        private int GetAllValuesVisibleRowCount()
        {
            const int headerHeight = 30;
            const int rowHeight = 24;
            return Math.Max(1, (this.allValuesPanel.ClientSize.Height - headerHeight) / rowHeight);
        }

        private bool TryGetAllValuesScrollbarThumbBounds(out Rectangle thumbBounds)
        {
            const int headerHeight = 30;
            const int scrollbarWidth = 14;

            thumbBounds = Rectangle.Empty;
            if (this.allValuesPanel == null)
            {
                return false;
            }

            var visibleRows = this.GetAllValuesVisibleRowCount();
            if (this.allValuesRows.Count <= visibleRows)
            {
                return false;
            }

            var maxOffset = Math.Max(1, this.allValuesRows.Count - visibleRows);
            var visibleHeight = Math.Max(1, this.allValuesPanel.ClientSize.Height - headerHeight);
            var thumbHeight = Math.Max(36, (int)Math.Round((visibleRows / (double)this.allValuesRows.Count) * visibleHeight));
            var thumbTravel = Math.Max(1, visibleHeight - thumbHeight);
            var thumbY = headerHeight + (int)Math.Round((this.allValuesScrollOffset / (double)maxOffset) * thumbTravel);
            thumbBounds = new Rectangle(this.allValuesPanel.ClientSize.Width - scrollbarWidth + 2, thumbY, scrollbarWidth - 4, thumbHeight);
            return true;
        }

        private void ApplyWindowTheme()
        {
            if (!this.IsHandleCreated)
            {
                return;
            }

            try
            {
                var useDarkMode = this.isDarkMode ? 1 : 0;
                DwmSetWindowAttribute(this.Handle, 20, ref useDarkMode, sizeof(int));
                DwmSetWindowAttribute(this.Handle, 19, ref useDarkMode, sizeof(int));
            }
            catch
            {
            }
        }

        private void ApplyNativeControlThemes()
        {
            this.ApplyNativeControlTheme(this.deviceTreeView);
            this.ApplyNativeControlTheme(this.statusTreeView);
            this.ApplyNativeControlTheme(this.eventTreeView);
            this.ApplyNativeControlTheme(this.attributesTreeView);
            this.ApplyNativeControlTheme(this.locationTreeView);
            this.ApplyNativeControlTheme(this.mqttInfoTreeView);
            this.ApplyNativeControlTheme(this.deviceComboBox);
            this.ClearNativeControlTheme(this.mainTabControl);
        }

        private void ApplyNativeControlTheme(Control control)
        {
            if (control == null || !control.IsHandleCreated)
            {
                return;
            }

            try
            {
                SetWindowTheme(control.Handle, this.isDarkMode ? "DarkMode_Explorer" : "Explorer", null);
            }
            catch
            {
            }
        }

        private void ClearNativeControlTheme(Control control)
        {
            if (control == null || !control.IsHandleCreated)
            {
                return;
            }

            try
            {
                SetWindowTheme(control.Handle, string.Empty, string.Empty);
            }
            catch
            {
            }
        }

        private void ApplyLabelTheme(Control root)
        {
            if (root == null)
            {
                return;
            }

            foreach (Control control in root.Controls)
            {
                var label = control as Label;
                if (label != null)
                {
                    label.ForeColor = this.CurrentTextColor;
                    if (label != this.connectionStatusLabel)
                    {
                        label.BackColor = root.BackColor;
                    }
                }

                var checkBox = control as CheckBox;
                if (checkBox != null)
                {
                    checkBox.ForeColor = this.CurrentTextColor;
                    checkBox.BackColor = root.BackColor;
                }

                this.ApplyLabelTheme(control);
            }
        }

        private void HandleThemeModeChanged()
        {
            this.isDarkMode = this.themeModeCheckBox.Checked;
            this.ApplyTheme();
        }

        private void ApplyConnectionStatusColor()
        {
            if (this.connectionStatusLabel == null)
            {
                return;
            }

            var text = this.connectionStatusLabel.Text ?? string.Empty;
            if (text.IndexOf("kein", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("nicht", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("gelöscht", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("fehler", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                this.connectionStatusLabel.ForeColor = this.CurrentErrorColor;
                return;
            }

            if (text.IndexOf("geladen", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("erfolgreich", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("erneuert", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                this.connectionStatusLabel.ForeColor = this.CurrentSuccessColor;
                return;
            }

            this.connectionStatusLabel.ForeColor = this.CurrentMutedTextColor;
        }

        private void WireEvents()
        {
            this.loginButton.Click += (sender, args) => Process.Start(new ProcessStartInfo
            {
                FileName = NavimowApiClient.LoginUrl,
                UseShellExecute = true,
            });

            this.tokenButton.Click += async (sender, args) => await this.FetchTokenAsync();
            this.deleteTokenButton.Click += (sender, args) => this.DeleteTokenWithConfirmation();
            this.getDevicesButton.Click += async (sender, args) => await this.LoadDevicesAsync();
            this.showStatusButton.Click += async (sender, args) => await this.RefreshStatusAsync(true);
            this.showMqttInfoButton.Click += async (sender, args) => await this.ShowMqttInfoAsync();
            this.disableMqttCheckBox.CheckedChanged += async (sender, args) => await this.HandleMqttModeChangedAsync();
            this.loadMapImageButton.Click += (sender, args) => this.LoadMapImage();
            this.clearMapImageButton.Click += (sender, args) => this.ClearMapImage();
            this.editMapImageCheckBox.CheckedChanged += (sender, args) => this.HandleEditMapImageChanged();
            this.themeModeCheckBox.CheckedChanged += (sender, args) => this.HandleThemeModeChanged();
            this.overlayScaleTrackBar.Scroll += (sender, args) => this.HandleOverlayScaleChanged();
            this.overlayRotationTrackBar.Scroll += (sender, args) => this.HandleOverlayRotationChanged();
            this.allValuesPanel.MouseWheel += this.AllValuesPanel_MouseWheel;
            this.allValuesPanel.MouseDown += this.AllValuesPanel_MouseDown;
            this.allValuesPanel.MouseMove += this.AllValuesPanel_MouseMove;
            this.allValuesPanel.MouseUp += this.AllValuesPanel_MouseUp;
            this.allValuesPanel.Resize += (sender, args) => this.allValuesPanel.Invalidate();
            this.deviceComboBox.SelectedIndexChanged += async (sender, args) => await this.HandleDeviceSelectionChangedAsync();
            this.startButton.Click += async (sender, args) => await this.SendCommandAsync("start");
            this.stopButton.Click += async (sender, args) => await this.SendCommandAsync("stop");
            this.pauseButton.Click += async (sender, args) => await this.SendCommandAsync("pause");
            this.resumeButton.Click += async (sender, args) => await this.SendCommandAsync("resume");
            this.dockButton.Click += async (sender, args) => await this.SendCommandAsync("dock");
            this.mapPanel.MouseEnter += (sender, args) => this.mapPanel.Focus();
            this.mapPanel.MouseWheel += this.MapPanel_MouseWheel;
            this.mapPanel.MouseDown += this.MapPanel_MouseDown;
            this.mapPanel.MouseMove += this.MapPanel_MouseMove;
            this.mapPanel.MouseUp += this.MapPanel_MouseUp;
            this.mapPanel.DoubleClick += this.MapPanel_DoubleClick;
        }

        private async System.Threading.Tasks.Task FetchTokenAsync()
        {
            try
            {
                this.SetBusy(true);
                var code = NavimowApiClient.ExtractAuthorizationCode(this.authorizationCodeTextBox.Text);
                if (string.IsNullOrWhiteSpace(code))
                {
                    throw new InvalidOperationException("Bitte den Authorization Code oder die komplette Redirect-URL einfügen.");
                }

                this.session = await this.apiClient.ExchangeCodeForTokenAsync(code);
                this.sessionStore.Save(this.session);
                this.connectionStatusLabel.Text = "Token erfolgreich gespeichert.";
                this.connectionStatusLabel.ForeColor = this.CurrentSuccessColor;
                this.AppendLog("Token erfolgreich abgerufen.");
            }
            catch (Exception ex)
            {
                this.connectionStatusLabel.Text = "Token konnte nicht geladen werden.";
                this.connectionStatusLabel.ForeColor = this.CurrentErrorColor;
                this.AppendLog("Token-Fehler: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Token abrufen", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.SetBusy(false);
            }
        }

        private void DeleteTokenWithConfirmation()
        {
            var result = MessageBox.Show(
                this,
                "Soll der gespeicherte Navimow-Token wirklich gelöscht werden?",
                "Token löschen",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                this.AppendLog("Token-Löschung abgebrochen.");
                return;
            }

            this.DeleteToken();
        }

        private void DeleteToken()
        {
            if (this.refreshTimer != null)
            {
                this.refreshTimer.Stop();
            }

            if (this.mqttClient != null)
            {
                this.mqttClient.PublishReceived -= this.MqttClient_PublishReceived;
                this.mqttClient.Dispose();
                this.mqttClient = null;
            }

            this.sessionStore.Clear();
            this.session = null;
            this.authorizationCodeTextBox.Text = string.Empty;
            this.connectionStatusLabel.Text = "Token gelöscht.";
            this.connectionStatusLabel.ForeColor = this.CurrentErrorColor;
            this.devices.Clear();
            this.deviceComboBox.Items.Clear();
            this.lastDeviceInfo = null;
            this.ResetLiveData();
            this.UpdateVisualizations();
            this.AppendLog("Gespeicherter Token wurde gelöscht.");
        }

        private async System.Threading.Tasks.Task LoadDevicesAsync()
        {
            try
            {
                this.SetBusy(true);
                await this.EnsureValidSessionAsync();
                if (this.session == null)
                {
                    throw new InvalidOperationException("Bitte zuerst ein Token abrufen.");
                }

                var loadedDevices = await this.apiClient.GetDevicesAsync(this.session.AccessToken);
                this.devices.Clear();
                this.devices.AddRange(loadedDevices);
                this.deviceComboBox.Items.Clear();

                foreach (var device in this.devices)
                {
                    this.deviceComboBox.Items.Add(device);
                }

                if (this.deviceComboBox.Items.Count > 0)
                {
                    this.deviceComboBox.SelectedIndex = 0;
                }

                this.AppendLog(loadedDevices.Count + " Gerät(e) gefunden.");
            }
            catch (Exception ex)
            {
                this.AppendLog("Gerätefehler: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Geräte abrufen", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.SetBusy(false);
            }
        }

        private async System.Threading.Tasks.Task HandleDeviceSelectionChangedAsync()
        {
            var device = this.deviceComboBox.SelectedItem as NavimowDevice;
            this.selectedDeviceLabel.Text = "Gerät: " + (device != null ? device.ToString() : "-");
            this.ResetLiveData();
            this.lastMqttFailureSignature = null;
            this.lastDeviceInfo = device != null ? device.Raw : null;
            this.UpdateVisualizations();

            if (device == null || this.session == null)
            {
                return;
            }

            try
            {
                await this.RefreshStatusAsync(true);
                await this.ShowMqttInfoAsync();
                this.refreshTimer.Start();
                this.AppendLog("REST-Polling aktiv (15s). MQTT-Verbindung wird im Hintergrund versucht.");
                await this.StartMqttForSelectedDeviceAsync(device.Id, false);
            }
            catch (Exception ex)
            {
                this.AppendLog("Gerätewechsel: " + this.FormatExceptionDetails(ex));
            }
        }

        private async System.Threading.Tasks.Task RefreshStatusAsync(bool logResponse)
        {
            var deviceId = this.GetSelectedDeviceId();
            if (deviceId == null)
            {
                return;
            }

            try
            {
                await this.EnsureValidSessionAsync();
                var apiStatus = await this.apiClient.GetDeviceStatusAsync(this.session.AccessToken, deviceId);
                this.lastStatus = this.MergeDictionaries(this.lastStatus, apiStatus);
                this.UpdateStatusSummary(this.lastStatus);
                this.UpdateMapFromStatus(this.lastStatus);
                this.UpdateVisualizations();
                this.lastDetailRefreshUtc = DateTime.UtcNow;

                if (logResponse)
                {
                    this.AppendLog("STATUS:" + Environment.NewLine + JsonUtils.ToJson(this.lastStatus));
                }
            }
            catch (Exception ex)
            {
                this.AppendLog("Statusfehler: " + ex.Message);
            }
        }

        private async System.Threading.Tasks.Task ShowMqttInfoAsync()
        {
            try
            {
                await this.EnsureValidSessionAsync();
                var info = await this.apiClient.GetMqttInfoAsync(this.session.AccessToken);
                this.lastMqttInfo = new Dictionary<string, object>
                {
                    { "mqttHost", info.MqttHost },
                    { "mqttUrl", info.MqttUrl },
                    { "userName", info.UserName },
                    { "pwdInfo", info.Password },
                };
                this.UpdateVisualizations();
                this.AppendLog("MQTT INFO aktualisiert.");
            }
            catch (Exception ex)
            {
                this.AppendLog("MQTT-Infofehler: " + ex.Message);
            }
        }

        private async System.Threading.Tasks.Task SendCommandAsync(string commandName)
        {
            var deviceId = this.GetSelectedDeviceId();
            if (deviceId == null)
            {
                return;
            }

            try
            {
                this.SetBusy(true);
                await this.EnsureValidSessionAsync();
                var response = await this.apiClient.SendCommandAsync(this.session.AccessToken, deviceId, commandName);
                this.AppendLog("COMMAND " + commandName.ToUpperInvariant() + ":" + Environment.NewLine + response);
                await System.Threading.Tasks.Task.Delay(5000);
                await this.RefreshStatusAsync(false);
            }
            catch (Exception ex)
            {
                this.AppendLog("Befehlsfehler: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Kommando senden", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.SetBusy(false);
            }
        }

        private async System.Threading.Tasks.Task ConnectMqttAsync(string deviceId)
        {
            await this.EnsureValidSessionAsync();

            this.StopMqttClient();

            MqttConnectionInfo info;
            try
            {
                info = await this.apiClient.GetMqttInfoAsync(this.session.AccessToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("MQTT-Info konnte nicht geladen werden: " + this.FormatExceptionDetails(ex), ex);
            }

            Uri uri;
            string wsPath;
            try
            {
                uri = BuildMqttUri(info, out wsPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("MQTT-URL konnte nicht aufgebaut werden. Host=" + (info.MqttHost ?? "-") + ", Url=" + (info.MqttUrl ?? "-") + ". " + this.FormatExceptionDetails(ex), ex);
            }

            try
            {
                await this.ConnectMqttOfficialStyleAsync(uri, wsPath, info);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("MQTT WebSocket-Verbindung fehlgeschlagen. Endpoint=" + uri + ". " + this.FormatExceptionDetails(ex), ex);
            }

            try
            {
                await this.mqttClient.SubscribeAsync("/downlink/vehicle/" + deviceId + "/realtimeDate/state");
                await this.mqttClient.SubscribeAsync("/downlink/vehicle/" + deviceId + "/realtimeDate/event");
                await this.mqttClient.SubscribeAsync("/downlink/vehicle/" + deviceId + "/realtimeDate/attributes");
                await this.mqttClient.SubscribeAsync("/downlink/vehicle/" + deviceId + "/realtimeDate/location");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("MQTT-Subscription fehlgeschlagen für Gerät " + deviceId + ". " + this.FormatExceptionDetails(ex), ex);
            }

            this.AppendLog("MQTT verbunden für Gerät " + deviceId + ".");
        }

        private async System.Threading.Tasks.Task TryConnectMqttAsync(string deviceId)
        {
            try
            {
                await this.ConnectMqttAsync(deviceId);
                this.lastMqttFailureSignature = null;
                if (this.IsDisposed)
                {
                    return;
                }

                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action<string>(this.SetMqttStatus), "MQTT: Live verbunden");
                }
                else
                {
                    this.SetMqttStatus("MQTT: Live verbunden");
                }
            }
            catch (Exception ex)
            {
                if (this.IsDisposed)
                {
                    return;
                }

                var message = "MQTT momentan nicht verfügbar. REST-Fallback aktiv. " + this.FormatExceptionDetails(ex);
                var shouldLog = this.lastMqttFailureSignature != message;
                this.lastMqttFailureSignature = message;
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action<string>(this.SetMqttStatus), "MQTT: REST-Fallback aktiv");
                    if (shouldLog)
                    {
                        this.BeginInvoke(new Action<string>(this.AppendLog), message);
                    }
                }
                else
                {
                    this.SetMqttStatus("MQTT: REST-Fallback aktiv");
                    if (shouldLog)
                    {
                        this.AppendLog(message);
                    }
                }
            }
        }

        private async System.Threading.Tasks.Task HandleMqttModeChangedAsync()
        {
            this.lastMqttFailureSignature = null;

            if (this.disableMqttCheckBox.Checked)
            {
                this.StopMqttClient();
                this.SetMqttStatus("MQTT: Manuell deaktiviert");
                this.AppendLog("MQTT wurde manuell deaktiviert. REST-Polling bleibt aktiv.");
                return;
            }

            var deviceId = this.GetSelectedDeviceId();
            if (deviceId != null && this.session != null)
            {
                await this.StartMqttForSelectedDeviceAsync(deviceId, true);
            }
            else
            {
                this.SetMqttStatus("MQTT: Bereit");
            }
        }

        private async System.Threading.Tasks.Task StartMqttForSelectedDeviceAsync(string deviceId, bool logManualEnable)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                this.SetMqttStatus("MQTT: -");
                return;
            }

            if (this.disableMqttCheckBox.Checked)
            {
                this.StopMqttClient();
                this.SetMqttStatus("MQTT: Manuell deaktiviert");
                return;
            }

            this.SetMqttStatus("MQTT: Verbinden...");
            if (logManualEnable)
            {
                this.AppendLog("MQTT wurde wieder aktiviert. Verbindungsaufbau läuft im Hintergrund.");
            }

            var ignored = this.TryConnectMqttAsync(deviceId);
            await System.Threading.Tasks.Task.CompletedTask;
        }

        private async System.Threading.Tasks.Task ConnectMqttOfficialStyleAsync(Uri uri, string wsPath, MqttConnectionInfo info)
        {
            this.StopMqttClient();

            this.mqttClient = new MqttWsClient();
            this.mqttClient.PublishReceived += this.MqttClient_PublishReceived;

            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(this.session.AccessToken))
            {
                headers["Authorization"] = "Bearer " + this.session.AccessToken;
            }

            var clientId = BuildMqttClientId(info.UserName);
            this.AppendLog(
                "MQTT Verbindungsversuch (SDK-Stil): Broker=" + uri.Host +
                ", Port=" + uri.Port +
                ", Pfad=" + wsPath +
                ", Benutzer=" + MaskValue(info.UserName) +
                ", AuthorizationHeader=" + (headers.ContainsKey("Authorization") ? "ja" : "nein") +
                ", ClientId=" + clientId);

            try
            {
                await this.mqttClient.ConnectAsync(uri, info.UserName, info.Password, headers, clientId);
                this.AppendLog("MQTT Handshake erfolgreich (SDK-Stil).");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "SDK-Stil MQTT-Verbindung fehlgeschlagen: Broker=" + uri.Host +
                    ", Port=" + uri.Port +
                    ", Pfad=" + wsPath +
                    ", Benutzer=" + MaskValue(info.UserName) +
                    ", AuthorizationHeader=" + (headers.ContainsKey("Authorization") ? "ja" : "nein") +
                    ". " + this.FormatExceptionDetails(ex),
                    ex);
            }
        }

        private void MqttClient_PublishReceived(MqttPublishMessage message)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<MqttPublishMessage>(this.MqttClient_PublishReceived), message);
                return;
            }

            var isLocationTopic = message.Topic.EndsWith("/location", StringComparison.OrdinalIgnoreCase);
            if (!isLocationTopic)
            {
                this.AppendLog("TOPIC : " + message.Topic + Environment.NewLine + "PAYLOAD:" + Environment.NewLine + message.Payload);
            }

            object payloadValue;
            try
            {
                payloadValue = JsonUtils.Parse(message.Payload);
            }
            catch
            {
                return;
            }

            if (message.Topic.EndsWith("/state", StringComparison.OrdinalIgnoreCase))
            {
                var latestState = this.ExtractLatestPayloadObject(payloadValue);
                this.lastStatus = latestState != null
                    ? this.MergeDictionaries(this.lastStatus, latestState)
                    : this.NormalizePayloadForDisplay("state", payloadValue);
                this.UpdateMapFromStatus(latestState ?? this.lastStatus);
                this.RefreshDetailViewsIfDue();
            }
            else if (message.Topic.EndsWith("/event", StringComparison.OrdinalIgnoreCase))
            {
                this.lastEvent = this.NormalizePayloadForDisplay("event", payloadValue);
                this.RefreshDetailViewsIfDue();
            }
            else if (message.Topic.EndsWith("/attributes", StringComparison.OrdinalIgnoreCase))
            {
                this.lastAttributes = this.NormalizePayloadForDisplay("attributes", payloadValue);
                this.RefreshDetailViewsIfDue();
            }
            else if (message.Topic.EndsWith("/location", StringComparison.OrdinalIgnoreCase))
            {
                this.lastLocation = this.NormalizePayloadForDisplay("location", payloadValue);
                var location = this.ExtractLatestPayloadObject(payloadValue);
                if (location != null)
                {
                    this.lastStatus = this.MergeDictionaries(this.lastStatus, location);
                }
                this.UpdateMapFromStatus(location ?? this.lastStatus);
                this.RefreshDetailViewsIfDue();
            }
        }

        private void UpdateStatusSummary(Dictionary<string, object> status)
        {
            if (status == null)
            {
                return;
            }

            this.deviceStateLabel.Text = "Zustand: " + this.GetStateText(status);
            this.batteryLabel.Text = "Akku: " + this.GetBatteryText(status);
            this.positionLabel.Text = "Position: " + this.GetPositionText(status);
            this.timestampLabel.Text = "Zeit: " + this.FormatField(status, "time", "timestamp");
            this.UpdateErrorLabel(status);
        }

        private void UpdateMapFromStatus(Dictionary<string, object> status)
        {
            if (status == null)
            {
                return;
            }

            var x = JsonUtils.GetDouble(status, "postureX");
            var y = JsonUtils.GetDouble(status, "postureY");
            if (x.HasValue && y.HasValue)
            {
                var point = new PointF((float)x.Value, (float)y.Value);
                if (this.pathPoints.Count == 0 || Distance(this.pathPoints[this.pathPoints.Count - 1], point) > 0.01f)
                {
                    this.pathPoints.Add(point);
                    this.lastMovementUtc = DateTime.UtcNow;
                    if (this.pathPoints.Count > 800)
                    {
                        this.pathPoints.RemoveAt(0);
                    }
                }

                this.EnsureOverlayInitialized();
            }

            this.mapPanel.Invalidate();
        }

        private void UpdateVisualizations()
        {
            this.UpdateTreeView(this.deviceTreeView, "Device", this.lastDeviceInfo);
            this.UpdateTreeView(this.statusTreeView, "Status", this.lastStatus);
            this.UpdateTreeView(this.eventTreeView, "Event", this.lastEvent);
            this.UpdateTreeView(this.attributesTreeView, "Attributes", this.lastAttributes);
            this.UpdateTreeView(this.locationTreeView, "Location", this.lastLocation);
            this.UpdateTreeView(this.mqttInfoTreeView, "MQTT", this.lastMqttInfo);
            this.UpdateAllValuesList();
            this.UpdateRawJson();
        }

        private void UpdateTreeView(TreeView treeView, string rootLabel, Dictionary<string, object> data)
        {
            var expandedPaths = new HashSet<string>(StringComparer.Ordinal);
            var selectedPath = treeView.SelectedNode != null ? treeView.SelectedNode.FullPath : null;
            this.CollectExpandedNodePaths(treeView.Nodes, expandedPaths);

            treeView.BeginUpdate();
            treeView.Nodes.Clear();

            var rootNode = new TreeNode(rootLabel);
            if (data == null || data.Count == 0)
            {
                rootNode.Nodes.Add(new TreeNode("Keine Daten vorhanden"));
            }
            else
            {
                foreach (var pair in data.OrderBy(p => p.Key))
                {
                    rootNode.Nodes.Add(this.BuildTreeNode(pair.Key, pair.Value));
                }
            }

            treeView.Nodes.Add(rootNode);
            rootNode.Expand();
            this.RestoreTreeState(treeView.Nodes, expandedPaths, selectedPath);
            treeView.EndUpdate();
        }

        private TreeNode BuildTreeNode(string key, object value)
        {
            var dictionary = value as Dictionary<string, object>;
            if (dictionary != null)
            {
                var node = new TreeNode(FieldCatalog.GetLabel(key) + " [" + key + "]");
                foreach (var pair in dictionary.OrderBy(p => p.Key))
                {
                    node.Nodes.Add(this.BuildTreeNode(pair.Key, pair.Value));
                }

                return node;
            }

            var array = value as object[];
            if (array != null)
            {
                var arrayNode = new TreeNode(FieldCatalog.GetLabel(key) + " [" + key + "] (" + array.Length + ")");
                for (int i = 0; i < array.Length; i++)
                {
                    arrayNode.Nodes.Add(this.BuildTreeNode("[" + i + "]", array[i]));
                }

                return arrayNode;
            }

            var arrayList = value as System.Collections.ArrayList;
            if (arrayList != null)
            {
                var arrayNode = new TreeNode(FieldCatalog.GetLabel(key) + " [" + key + "] (" + arrayList.Count + ")");
                for (int i = 0; i < arrayList.Count; i++)
                {
                    arrayNode.Nodes.Add(this.BuildTreeNode("[" + i + "]", arrayList[i]));
                }

                return arrayNode;
            }

            var text = JsonUtils.ToDisplayString(value);
            var displayValue = FieldCatalog.GetDisplayValue(key, text);
            return new TreeNode(FieldCatalog.GetLabel(key) + " [" + key + "]: " + displayValue);
        }

        private void UpdateAllValuesList()
        {
            this.allValuesRows.Clear();
            this.allValuesScrollOffset = 0;

            var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            this.AddFlattenedSection("device", this.lastDeviceInfo, addedPaths);
            this.AddFlattenedSection("status", this.lastStatus, addedPaths);
            this.AddFlattenedSection("event", this.lastEvent, addedPaths);
            this.AddFlattenedSection("attributes", this.lastAttributes, addedPaths);
            this.AddFlattenedSection("location", this.lastLocation, addedPaths);
            this.AddFlattenedSection("mqtt", this.lastMqttInfo, addedPaths);
            this.AddKnownFieldRows(addedPaths);
            this.allValuesPanel.Invalidate();
        }

        private void AddFlattenedSection(string prefix, Dictionary<string, object> data, HashSet<string> addedPaths)
        {
            if (data == null)
            {
                return;
            }

            foreach (var pair in JsonUtils.FlattenObject(data))
            {
                var fullPath = string.IsNullOrWhiteSpace(pair.Key) ? prefix : prefix + "." + pair.Key;
                var lastSegment = pair.Key;
                var dotIndex = lastSegment.LastIndexOf('.');
                if (dotIndex >= 0 && dotIndex < lastSegment.Length - 1)
                {
                    lastSegment = lastSegment.Substring(dotIndex + 1);
                }

                var bracketIndex = lastSegment.LastIndexOf('[');
                if (bracketIndex > 0)
                {
                    lastSegment = lastSegment.Substring(0, bracketIndex);
                }

                if (string.IsNullOrWhiteSpace(lastSegment))
                {
                    lastSegment = pair.Key;
                }

                var label = FieldCatalog.GetLabel(lastSegment);
                this.allValuesRows.Add(new AllValuesRow
                {
                    Path = fullPath,
                    Label = label,
                    Value = FieldCatalog.GetDisplayValue(lastSegment, pair.Value),
                });
                if (addedPaths != null)
                {
                    addedPaths.Add(fullPath);
                }
            }
        }

        private void UpdateRawJson()
        {
            var sections = new List<string>();

            if (this.lastDeviceInfo != null)
            {
                sections.Add("DEVICE" + Environment.NewLine + JsonUtils.ToJson(this.lastDeviceInfo));
            }

            if (this.lastStatus != null)
            {
                sections.Add("STATUS" + Environment.NewLine + JsonUtils.ToJson(this.lastStatus));
            }

            if (this.lastEvent != null)
            {
                sections.Add("EVENT" + Environment.NewLine + JsonUtils.ToJson(this.lastEvent));
            }

            if (this.lastAttributes != null)
            {
                sections.Add("ATTRIBUTES" + Environment.NewLine + JsonUtils.ToJson(this.lastAttributes));
            }

            if (this.lastLocation != null)
            {
                sections.Add("LOCATION" + Environment.NewLine + JsonUtils.ToJson(this.lastLocation));
            }

            if (this.lastMqttInfo != null)
            {
                sections.Add("MQTT INFO" + Environment.NewLine + JsonUtils.ToJson(this.lastMqttInfo));
            }

            this.rawJsonTextView.TextContent = sections.Count == 0 ? "Noch keine Daten vorhanden." : string.Join(Environment.NewLine + Environment.NewLine, sections.ToArray());
        }

        private void ResetLiveData()
        {
            this.pathPoints.Clear();
            this.lastStatus = null;
            this.lastEvent = null;
            this.lastAttributes = null;
            this.lastLocation = null;
            this.lastMqttInfo = null;
            this.lastDetailRefreshUtc = DateTime.MinValue;
            this.lastMovementUtc = DateTime.MinValue;
            this.deviceStateLabel.Text = "Zustand: -";
            this.batteryLabel.Text = "Akku: -";
            this.positionLabel.Text = "Position: -";
            this.errorLabel.Visible = false;
            this.errorLabel.Text = string.Empty;
            this.timestampLabel.Text = "Zeit: -";
            this.mqttStatusLabel.Text = this.disableMqttCheckBox != null && this.disableMqttCheckBox.Checked ? "MQTT: Manuell deaktiviert" : "MQTT: -";
            this.mapProjectionValid = false;
            this.mapPanel.Invalidate();
        }

        private async System.Threading.Tasks.Task EnsureValidSessionAsync()
        {
            if (this.session == null)
            {
                this.session = this.sessionStore.Load();
            }

            if (this.session == null)
            {
                return;
            }

            var refreshed = await this.apiClient.EnsureValidSessionAsync(this.session);
            if (refreshed != null && refreshed != this.session)
            {
                this.session = refreshed;
                this.sessionStore.Save(this.session);
                this.connectionStatusLabel.Text = "Token automatisch erneuert.";
                this.connectionStatusLabel.ForeColor = this.CurrentSuccessColor;
                this.AppendLog("Token wurde automatisch erneuert.");
            }
        }

        private string GetSelectedDeviceId()
        {
            var device = this.deviceComboBox.SelectedItem as NavimowDevice;
            return device != null ? device.Id : null;
        }

        private void RefreshDetailViewsIfDue()
        {
            if (DateTime.UtcNow - this.lastDetailRefreshUtc < DetailRefreshInterval)
            {
                return;
            }

            this.lastDetailRefreshUtc = DateTime.UtcNow;
            this.UpdateStatusSummary(this.lastStatus);
            this.UpdateVisualizations();
        }

        private string FormatField(Dictionary<string, object> source, params string[] keys)
        {
            foreach (var key in keys)
            {
                var raw = JsonUtils.GetString(source, key);
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    return FieldCatalog.GetDisplayValue(key, raw);
                }
            }

            return "-";
        }

        private string GetStateText(Dictionary<string, object> status)
        {
            string key;
            string raw;
            if (this.TryFindStringValue(status, new[] { "vehicleState", "state", "status" }, out key, out raw))
            {
                var mapped = FieldCatalog.GetDisplayValue(key, raw);
                if (this.IsRecentlyMoving() && mapped.IndexOf("Docked", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "Mowing (live movement)";
                }

                return mapped;
            }

            if (this.IsRecentlyMoving())
            {
                return "Mowing (live movement)";
            }

            return "-";
        }

        private string GetBatteryText(Dictionary<string, object> status)
        {
            var value = this.GetMetricText(status, true, "capacityRemaining", "battery", "batteryLevel", "electricity");
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private void UpdateErrorLabel(Dictionary<string, object> status)
        {
            string key;
            string raw;
            if (!this.TryFindStringValue(status, new[] { "error_message", "errorCode", "error_code" }, out key, out raw))
            {
                this.errorLabel.Visible = false;
                this.errorLabel.Text = string.Empty;
                return;
            }

            var normalized = raw.Trim();
            if (string.IsNullOrWhiteSpace(normalized) ||
                normalized == "-" ||
                normalized == "0" ||
                string.Equals(normalized, "none", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "no error", StringComparison.OrdinalIgnoreCase))
            {
                this.errorLabel.Visible = false;
                this.errorLabel.Text = string.Empty;
                return;
            }

            this.errorLabel.Visible = true;
            this.errorLabel.Text = "Fehler: " + FieldCatalog.GetDisplayValue(key, normalized);
        }

        private string GetMetricText(Dictionary<string, object> status, bool preferPercent, params string[] keys)
        {
            object value;
            string key;
            if (!this.TryFindValue(status, keys, out key, out value))
            {
                return string.Empty;
            }

            string rawValue;
            string unit;
            if (this.TryExtractMetricComponents(value, out rawValue, out unit))
            {
                if (string.Equals(unit, "PERCENTAGE", StringComparison.OrdinalIgnoreCase))
                {
                    unit = "%";
                }

                if (!string.IsNullOrWhiteSpace(unit))
                {
                    return unit == "%" ? rawValue + " %" : rawValue + " " + unit;
                }

                return preferPercent && rawValue.IndexOf('%') < 0 ? rawValue + " %" : rawValue;
            }

            value = this.NormalizeJsonStringValue(value);
            var text = JsonUtils.ToDisplayString(value);
            if (string.IsNullOrWhiteSpace(text) || text == "-")
            {
                return string.Empty;
            }

            return preferPercent && text.IndexOf('%') < 0 ? text + " %" : text;
        }

        private bool TryExtractMetricComponents(object value, out string rawValue, out string unit)
        {
            rawValue = string.Empty;
            unit = string.Empty;

            value = this.NormalizeJsonStringValue(value);

            var dictionary = value as Dictionary<string, object>;
            if (dictionary != null)
            {
                string rawValueKey;
                object rawValueObject;
                if (this.TryFindValue(dictionary, new[] { "rawValue", "value", "batteryLevel", "electricity" }, out rawValueKey, out rawValueObject))
                {
                    rawValue = JsonUtils.ToDisplayString(rawValueObject);
                }

                string unitKey;
                object unitObject;
                if (this.TryFindValue(dictionary, new[] { "unit" }, out unitKey, out unitObject))
                {
                    unit = JsonUtils.ToDisplayString(unitObject);
                }

                return !string.IsNullOrWhiteSpace(rawValue);
            }

            var text = value as string;
            if (string.IsNullOrWhiteSpace(text))
            {
                text = JsonUtils.ToDisplayString(value);
            }

            var rawMatch = Regex.Match(text, "\"rawValue\"\\s*:\\s*\"?(?<value>[^\",}\\]]+)\"?", RegexOptions.IgnoreCase);
            if (rawMatch.Success)
            {
                rawValue = rawMatch.Groups["value"].Value;
            }

            var unitMatch = Regex.Match(text, "\"unit\"\\s*:\\s*\"?(?<value>[^\",}\\]]+)\"?", RegexOptions.IgnoreCase);
            if (unitMatch.Success)
            {
                unit = unitMatch.Groups["value"].Value;
            }

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                var genericNumber = Regex.Match(text, "(?<value>\\d+(?:[\\.,]\\d+)?)");
                if (genericNumber.Success)
                {
                    rawValue = genericNumber.Groups["value"].Value.Replace(',', '.');
                }
            }

            return !string.IsNullOrWhiteSpace(rawValue);
        }

        private bool IsRecentlyMoving()
        {
            return DateTime.UtcNow - this.lastMovementUtc < TimeSpan.FromMinutes(3);
        }

        private bool TryFindStringValue(Dictionary<string, object> source, string[] keys, out string matchedKey, out string value)
        {
            matchedKey = null;
            value = null;

            object rawValue;
            if (!this.TryFindValue(source, keys, out matchedKey, out rawValue))
            {
                return false;
            }

            value = JsonUtils.ToDisplayString(rawValue);
            return !string.IsNullOrWhiteSpace(value);
        }

        private object NormalizeJsonStringValue(object value)
        {
            var text = value as string;
            if (string.IsNullOrWhiteSpace(text))
            {
                return value;
            }

            text = text.Trim();
            if ((!text.StartsWith("{", StringComparison.Ordinal) || !text.EndsWith("}", StringComparison.Ordinal)) &&
                (!text.StartsWith("[", StringComparison.Ordinal) || !text.EndsWith("]", StringComparison.Ordinal)))
            {
                return value;
            }

            try
            {
                return JsonUtils.Parse(text);
            }
            catch
            {
                return value;
            }
        }

        private bool TryFindValue(object source, string[] keys, out string matchedKey, out object value)
        {
            foreach (var key in keys)
            {
                if (this.TryFindValueRecursive(source, key, out value))
                {
                    matchedKey = key;
                    return true;
                }
            }

            matchedKey = null;
            value = null;
            return false;
        }

        private bool TryFindValueRecursive(object source, string searchKey, out object value)
        {
            source = this.NormalizeJsonStringValue(source);

            var dictionary = source as Dictionary<string, object>;
            if (dictionary != null)
            {
                object direct;
                if (dictionary.TryGetValue(searchKey, out direct) && direct != null)
                {
                    value = direct;
                    return true;
                }

                foreach (var pair in dictionary)
                {
                    if (this.TryFindValueRecursive(pair.Value, searchKey, out value))
                    {
                        return true;
                    }
                }
            }

            var array = source as object[];
            if (array != null)
            {
                foreach (var item in array)
                {
                    if (this.TryFindValueRecursive(item, searchKey, out value))
                    {
                        return true;
                    }
                }
            }

            var list = source as System.Collections.ArrayList;
            if (list != null)
            {
                foreach (var item in list)
                {
                    if (this.TryFindValueRecursive(item, searchKey, out value))
                    {
                        return true;
                    }
                }
            }

            value = null;
            return false;
        }

        private string GetPositionText(Dictionary<string, object> status)
        {
            string key;
            object xValue;
            object yValue;
            var x = this.TryFindValue(status, new[] { "postureX" }, out key, out xValue) ? JsonUtils.ToDisplayString(xValue) : string.Empty;
            var y = this.TryFindValue(status, new[] { "postureY" }, out key, out yValue) ? JsonUtils.ToDisplayString(yValue) : string.Empty;
            if (!string.IsNullOrWhiteSpace(x) || !string.IsNullOrWhiteSpace(y))
            {
                return "X=" + x + ", Y=" + y;
            }

            object latValue;
            object lngValue;
            var lat = this.TryFindValue(status, new[] { "latitude", "lat" }, out key, out latValue) ? JsonUtils.ToDisplayString(latValue) : string.Empty;
            var lng = this.TryFindValue(status, new[] { "longitude", "lng" }, out key, out lngValue) ? JsonUtils.ToDisplayString(lngValue) : string.Empty;
            if (!string.IsNullOrWhiteSpace(lat) || !string.IsNullOrWhiteSpace(lng))
            {
                return "Lat=" + lat + ", Lng=" + lng;
            }

            if (this.pathPoints.Count > 0)
            {
                var lastPoint = this.pathPoints[this.pathPoints.Count - 1];
                return "X=" + lastPoint.X.ToString("0.###") + ", Y=" + lastPoint.Y.ToString("0.###");
            }

            return "-";
        }

        private void AddKnownFieldRows(HashSet<string> addedPaths)
        {
            foreach (var key in FieldCatalog.GetKnownKeys().Distinct().OrderBy(k => k))
            {
                var fullPath = "api." + key;
                if (addedPaths.Contains(fullPath))
                {
                    continue;
                }

                object value;
                string matchedKey;
                string display = "-";
                if (this.TryFindValue(this.lastDeviceInfo, new[] { key }, out matchedKey, out value) ||
                    this.TryFindValue(this.lastStatus, new[] { key }, out matchedKey, out value) ||
                    this.TryFindValue(this.lastEvent, new[] { key }, out matchedKey, out value) ||
                    this.TryFindValue(this.lastAttributes, new[] { key }, out matchedKey, out value) ||
                    this.TryFindValue(this.lastLocation, new[] { key }, out matchedKey, out value) ||
                    this.TryFindValue(this.lastMqttInfo, new[] { key }, out matchedKey, out value))
                {
                    display = FieldCatalog.GetDisplayValue(key, JsonUtils.ToDisplayString(value));
                }

                this.allValuesRows.Add(new AllValuesRow
                {
                    Path = fullPath,
                    Label = FieldCatalog.GetLabel(key),
                    Value = display,
                });
            }
        }

        private void AppendLog(string message)
        {
            var block = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine + Environment.NewLine;
            this.logTextView.AppendText(block);
        }

        private void SetMqttStatus(string text)
        {
            this.mqttStatusLabel.Text = text;
        }

        private Dictionary<string, object> NormalizePayloadForDisplay(string rootKey, object payload)
        {
            var dictionary = payload as Dictionary<string, object>;
            if (dictionary != null)
            {
                return dictionary;
            }

            return new Dictionary<string, object>
            {
                { rootKey, payload }
            };
        }

        private Dictionary<string, object> ExtractLatestPayloadObject(object payload)
        {
            var dictionary = payload as Dictionary<string, object>;
            if (dictionary != null)
            {
                return dictionary;
            }

            var array = payload as object[];
            if (array != null)
            {
                for (int i = array.Length - 1; i >= 0; i--)
                {
                    var item = array[i] as Dictionary<string, object>;
                    if (item != null)
                    {
                        return item;
                    }
                }
            }

            var list = payload as System.Collections.ArrayList;
            if (list != null)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var item = list[i] as Dictionary<string, object>;
                    if (item != null)
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        private Dictionary<string, object> MergeDictionaries(Dictionary<string, object> baseValues, Dictionary<string, object> overlayValues)
        {
            var merged = new Dictionary<string, object>();

            if (baseValues != null)
            {
                foreach (var pair in baseValues)
                {
                    merged[pair.Key] = pair.Value;
                }
            }

            if (overlayValues != null)
            {
                foreach (var pair in overlayValues)
                {
                    merged[pair.Key] = pair.Value;
                }
            }

            return merged;
        }

        private void StopMqttClient()
        {
            if (this.mqttClient != null)
            {
                this.mqttClient.PublishReceived -= this.MqttClient_PublishReceived;
                this.mqttClient.Dispose();
                this.mqttClient = null;
            }
        }

        private void CollectExpandedNodePaths(TreeNodeCollection nodes, HashSet<string> paths)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.IsExpanded)
                {
                    paths.Add(node.FullPath);
                }

                this.CollectExpandedNodePaths(node.Nodes, paths);
            }
        }

        private void RestoreTreeState(TreeNodeCollection nodes, HashSet<string> expandedPaths, string selectedPath)
        {
            foreach (TreeNode node in nodes)
            {
                if (expandedPaths.Contains(node.FullPath))
                {
                    node.Expand();
                }

                if (!string.IsNullOrWhiteSpace(selectedPath) && string.Equals(node.FullPath, selectedPath, StringComparison.Ordinal))
                {
                    node.TreeView.SelectedNode = node;
                }

                this.RestoreTreeState(node.Nodes, expandedPaths, selectedPath);
            }
        }

        private static void EnableDoubleBuffer(Control control)
        {
            var property = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (property != null)
            {
                property.SetValue(control, true, null);
            }
        }

        private string FormatExceptionDetails(Exception exception)
        {
            var parts = new List<string>();
            var current = exception;

            while (current != null)
            {
                var typeName = current.GetType().Name;
                var message = current.Message;
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = "(ohne Meldung)";
                }

                parts.Add(typeName + ": " + message);
                current = current.InnerException;
            }

            return string.Join(" | ", parts.ToArray());
        }

        private void SetBusy(bool busy)
        {
            this.UseWaitCursor = busy;
            this.loginButton.Enabled = !busy;
            this.tokenButton.Enabled = !busy;
            this.deleteTokenButton.Enabled = !busy;
            this.getDevicesButton.Enabled = !busy;
            this.showStatusButton.Enabled = !busy;
            this.showMqttInfoButton.Enabled = !busy;
            this.disableMqttCheckBox.Enabled = !busy;
            this.loadMapImageButton.Enabled = !busy;
            this.clearMapImageButton.Enabled = !busy;
            this.editMapImageCheckBox.Enabled = !busy;
            this.overlayScaleTrackBar.Enabled = !busy;
            this.overlayRotationTrackBar.Enabled = !busy;
            this.startButton.Enabled = !busy;
            this.stopButton.Enabled = !busy;
            this.pauseButton.Enabled = !busy;
            this.resumeButton.Enabled = !busy;
            this.dockButton.Enabled = !busy;
        }

        private void MapPanel_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(this.CurrentSurfaceColor);

            using (var borderPen = new Pen(this.CurrentBorderColor, 1))
            {
                e.Graphics.DrawRectangle(borderPen, 0, 0, this.mapPanel.Width - 1, this.mapPanel.Height - 1);
            }

            if (this.pathPoints.Count == 0)
            {
                this.mapProjectionValid = false;
                this.DrawOverlayWithoutPath(e.Graphics);

                using (var font = new Font("Segoe UI", 11F))
                using (var brush = new SolidBrush(this.CurrentMapMessageColor))
                {
                    var message = this.overlayMapImage == null
                        ? "Noch keine Positionsdaten vorhanden."
                        : "Bild geladen. Die Ausrichtung wird aktiv, sobald Positionsdaten vorhanden sind.";
                    e.Graphics.DrawString(message, font, brush, new PointF(18F, 18F));
                }

                return;
            }

            float minX = this.pathPoints.Min(p => p.X);
            float maxX = this.pathPoints.Max(p => p.X);
            float minY = this.pathPoints.Min(p => p.Y);
            float maxY = this.pathPoints.Max(p => p.Y);
            float width = Math.Max(1F, maxX - minX);
            float height = Math.Max(1F, maxY - minY);
            float padding = 32F;
            float availableWidth = Math.Max(1F, this.mapPanel.ClientSize.Width - padding * 2F);
            float availableHeight = Math.Max(1F, this.mapPanel.ClientSize.Height - padding * 2F);
            float scaleX = availableWidth / width;
            float scaleY = availableHeight / height;
            float scale = Math.Max(0.1F, Math.Min(scaleX, scaleY));
            float scaledWidth = width * scale;
            float scaledHeight = height * scale;
            float offsetX = (this.mapPanel.ClientSize.Width - scaledWidth) / 2F;
            float offsetY = (this.mapPanel.ClientSize.Height - scaledHeight) / 2F;
            this.mapProjectionValid = true;
            this.mapProjectionBaseScale = scale;
            this.mapProjectionScale = scale * this.mapViewZoom;
            this.mapProjectionOffsetX = offsetX;
            this.mapProjectionOffsetY = offsetY;
            this.mapProjectionMinX = minX;
            this.mapProjectionMinY = minY;
            this.DrawOverlayWithProjection(e.Graphics);

            var screenPoints = this.pathPoints.Select(this.WorldToScreen).ToArray();

            if (screenPoints.Length >= 2)
            {
                using (var pathPen = new Pen(this.CurrentMapPathColor, 2F))
                {
                    e.Graphics.DrawLines(pathPen, screenPoints);
                }
            }

            using (var startBrush = new SolidBrush(this.CurrentMarkerStartColor))
            using (var startPen = new Pen(ControlPaint.Dark(this.CurrentMarkerStartColor, 0.15F)))
            using (var endBrush = new SolidBrush(this.CurrentMarkerEndColor))
            using (var endPen = new Pen(ControlPaint.Dark(this.CurrentMarkerEndColor, 0.15F)))
            {
                DrawMarker(e.Graphics, startBrush, startPen, screenPoints[0], 10F);
                DrawMarker(e.Graphics, endBrush, endPen, screenPoints[screenPoints.Length - 1], 10F);
            }
        }

        private void LoadMapImage()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Kartenbild laden";
                dialog.Filter = "Bilddateien|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Alle Dateien|*.*";
                dialog.Multiselect = false;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                using (var sourceImage = Image.FromFile(dialog.FileName))
                {
                    this.DisposeOverlayImage();
                    this.overlayMapImage = new Bitmap(sourceImage);
                }

                this.overlayBaseWidthWorld = 0F;
                this.overlayBaseHeightWorld = 0F;
                this.overlayRotationDegrees = this.overlayRotationTrackBar.Value;
                this.EnsureOverlayInitialized();
                this.SaveOverlayState();
                this.AppendLog("Kartenbild geladen: " + Path.GetFileName(dialog.FileName));
                this.mapPanel.Invalidate();
            }
        }

        private void LoadOverlayState()
        {
            var state = this.overlayMapStore.Load();
            if (state == null || state.Image == null)
            {
                this.UpdateOverlayScaleLabel();
                return;
            }

            this.DisposeOverlayImage();
            this.overlayMapImage = state.Image;
            this.overlayBaseWidthWorld = state.BaseWidthWorld;
            this.overlayBaseHeightWorld = state.BaseHeightWorld;
            this.overlayCenterWorld = new PointF(state.CenterX, state.CenterY);
            this.overlayRotationDegrees = state.RotationDegrees;

            var scalePercent = state.ScalePercent <= 0 ? 100 : state.ScalePercent;
            scalePercent = Math.Max(this.overlayScaleTrackBar.Minimum, Math.Min(this.overlayScaleTrackBar.Maximum, scalePercent));
            this.overlayScaleTrackBar.Value = scalePercent;

            var rotation = (int)Math.Round(state.RotationDegrees);
            rotation = Math.Max(this.overlayRotationTrackBar.Minimum, Math.Min(this.overlayRotationTrackBar.Maximum, rotation));
            this.overlayRotationTrackBar.Value = rotation;
            this.overlayRotationLabel.Text = "Drehung: " + rotation + " Grad";
            this.UpdateOverlayScaleLabel();
        }

        private void SaveOverlayState()
        {
            if (this.overlayMapImage == null)
            {
                return;
            }

            this.overlayMapStore.Save(this.overlayMapImage, new OverlayMapState
            {
                ScalePercent = this.overlayScaleTrackBar != null ? this.overlayScaleTrackBar.Value : 100,
                RotationDegrees = this.overlayRotationDegrees,
                CenterX = this.overlayCenterWorld.X,
                CenterY = this.overlayCenterWorld.Y,
                BaseWidthWorld = this.overlayBaseWidthWorld,
                BaseHeightWorld = this.overlayBaseHeightWorld,
            });
        }

        private void ClearMapImage()
        {
            if (this.overlayMapImage == null)
            {
                return;
            }

            this.DisposeOverlayImage();
            this.overlayBaseWidthWorld = 0F;
            this.overlayBaseHeightWorld = 0F;
            this.overlayCenterWorld = PointF.Empty;
            this.overlayRotationDegrees = 0F;
            this.overlayRotationTrackBar.Value = 0;
            this.overlayScaleTrackBar.Value = 100;
            this.overlayRotationLabel.Text = "Drehung: 0 Grad";
            this.UpdateOverlayScaleLabel();
            this.mapPanel.Cursor = Cursors.Default;
            this.overlayMapStore.Clear();
            this.AppendLog("Kartenbild entfernt.");
            this.mapPanel.Invalidate();
        }

        private void HandleEditMapImageChanged()
        {
            this.mapPanel.Cursor = this.editMapImageCheckBox.Checked && this.overlayMapImage != null ? Cursors.SizeAll : Cursors.Default;
            this.mapPanel.Invalidate();
        }

        private void HandleOverlayScaleChanged()
        {
            this.UpdateOverlayScaleLabel();
            this.SaveOverlayState();
            this.mapPanel.Invalidate();
        }

        private void HandleOverlayRotationChanged()
        {
            this.overlayRotationDegrees = this.overlayRotationTrackBar.Value;
            this.overlayRotationLabel.Text = "Drehung: " + this.overlayRotationTrackBar.Value + " Grad";
            this.SaveOverlayState();
            this.mapPanel.Invalidate();
        }

        private void MapPanel_MouseDown(object sender, MouseEventArgs e)
        {
            this.mapPanel.Focus();

            if ((e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle) && this.mapProjectionValid)
            {
                this.mapViewPanning = true;
                this.mapViewPanStartScreen = e.Location;
                this.mapPanel.Cursor = Cursors.Hand;
                return;
            }

            if (!this.CanEditOverlayOnMap() || e.Button != MouseButtons.Left)
            {
                return;
            }

            this.overlayDragging = true;
            this.overlayDragStartScreen = e.Location;
            this.mapPanel.Cursor = Cursors.SizeAll;
        }

        private void MapPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (this.mapViewPanning)
            {
                var panDx = e.X - this.mapViewPanStartScreen.X;
                var panDy = e.Y - this.mapViewPanStartScreen.Y;
                this.mapViewPanStartScreen = e.Location;
                this.mapViewPanScreen = new PointF(this.mapViewPanScreen.X + panDx, this.mapViewPanScreen.Y + panDy);
                this.mapPanel.Invalidate();
                return;
            }

            if (!this.overlayDragging || !this.CanEditOverlayOnMap())
            {
                return;
            }

            if (Math.Abs(this.mapProjectionScale) < 0.0001F)
            {
                return;
            }

            var overlayDx = e.X - this.overlayDragStartScreen.X;
            var overlayDy = e.Y - this.overlayDragStartScreen.Y;
            this.overlayDragStartScreen = e.Location;

            this.overlayCenterWorld = new PointF(
                this.overlayCenterWorld.X + (overlayDx / this.mapProjectionScale),
                this.overlayCenterWorld.Y - (overlayDy / this.mapProjectionScale));

            this.mapPanel.Invalidate();
        }

        private void MapPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (this.mapViewPanning || this.overlayDragging)
            {
                this.SaveOverlayState();
            }

            this.mapViewPanning = false;
            this.overlayDragging = false;
            this.mapPanel.Cursor = this.editMapImageCheckBox.Checked && this.overlayMapImage != null ? Cursors.SizeAll : Cursors.Default;
        }

        private void MapPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            if (!this.mapProjectionValid)
            {
                return;
            }

            var previousZoom = this.mapViewZoom;
            var nextZoom = e.Delta > 0 ? previousZoom * 1.2F : previousZoom / 1.2F;
            nextZoom = Math.Max(0.25F, Math.Min(12F, nextZoom));
            if (Math.Abs(nextZoom - previousZoom) < 0.0001F)
            {
                return;
            }

            var panelCenter = new PointF(this.mapPanel.ClientSize.Width / 2F, this.mapPanel.ClientSize.Height / 2F);
            var relativeX = e.X - panelCenter.X - this.mapViewPanScreen.X;
            var relativeY = e.Y - panelCenter.Y - this.mapViewPanScreen.Y;
            var zoomRatio = nextZoom / previousZoom;
            this.mapViewPanScreen = new PointF(
                e.X - panelCenter.X - (relativeX * zoomRatio),
                e.Y - panelCenter.Y - (relativeY * zoomRatio));
            this.mapViewZoom = nextZoom;
            this.mapProjectionScale = this.mapProjectionBaseScale * this.mapViewZoom;
            this.mapPanel.Invalidate();
        }

        private void MapPanel_DoubleClick(object sender, EventArgs e)
        {
            this.ResetMapView();
        }

        private bool CanEditOverlayOnMap()
        {
            return this.overlayMapImage != null &&
                this.editMapImageCheckBox != null &&
                this.editMapImageCheckBox.Checked &&
                this.mapProjectionValid;
        }

        private void EnsureOverlayInitialized()
        {
            if (this.overlayMapImage == null || this.pathPoints.Count == 0)
            {
                return;
            }

            if (this.overlayBaseWidthWorld > 0F && this.overlayBaseHeightWorld > 0F)
            {
                return;
            }

            float minX = this.pathPoints.Min(p => p.X);
            float maxX = this.pathPoints.Max(p => p.X);
            float minY = this.pathPoints.Min(p => p.Y);
            float maxY = this.pathPoints.Max(p => p.Y);
            float width = Math.Max(1F, maxX - minX);
            float height = Math.Max(1F, maxY - minY);
            float imageAspect = this.overlayMapImage.Width <= 0 || this.overlayMapImage.Height <= 0
                ? 1F
                : (float)this.overlayMapImage.Width / this.overlayMapImage.Height;
            float targetWidth = width * 0.95F;
            float targetHeight = Math.Max(0.5F, targetWidth / Math.Max(0.01F, imageAspect));

            if (targetHeight > height * 0.95F)
            {
                targetHeight = height * 0.95F;
                targetWidth = Math.Max(0.5F, targetHeight * imageAspect);
            }

            this.overlayBaseWidthWorld = Math.Max(0.5F, targetWidth);
            this.overlayBaseHeightWorld = Math.Max(0.5F, targetHeight);
            this.overlayCenterWorld = new PointF(minX + (width / 2F), minY + (height / 2F));
            this.SaveOverlayState();
        }

        private void DrawOverlayWithoutPath(Graphics graphics)
        {
            if (this.overlayMapImage == null)
            {
                return;
            }

            var multiplier = this.GetOverlayScaleMultiplier();
            float maxWidth = this.mapPanel.ClientSize.Width * 0.8F;
            float maxHeight = this.mapPanel.ClientSize.Height * 0.8F;
            float baseScale = Math.Min(
                maxWidth / Math.Max(1F, this.overlayMapImage.Width),
                maxHeight / Math.Max(1F, this.overlayMapImage.Height));
            float width = this.overlayMapImage.Width * baseScale * multiplier;
            float height = this.overlayMapImage.Height * baseScale * multiplier;
            var center = new PointF(this.mapPanel.ClientSize.Width / 2F, this.mapPanel.ClientSize.Height / 2F);
            this.DrawOverlayImage(graphics, center, width, height);
        }

        private void DrawOverlayWithProjection(Graphics graphics)
        {
            if (this.overlayMapImage == null)
            {
                return;
            }

            this.EnsureOverlayInitialized();
            if (this.overlayBaseWidthWorld <= 0F || this.overlayBaseHeightWorld <= 0F)
            {
                return;
            }

            var center = this.WorldToScreen(this.overlayCenterWorld);
            var multiplier = this.GetOverlayScaleMultiplier();
            float width = this.overlayBaseWidthWorld * this.mapProjectionScale * multiplier;
            float height = this.overlayBaseHeightWorld * this.mapProjectionScale * multiplier;
            this.DrawOverlayImage(graphics, center, width, height);
        }

        private void DrawOverlayImage(Graphics graphics, PointF center, float width, float height)
        {
            if (this.overlayMapImage == null || width <= 1F || height <= 1F)
            {
                return;
            }

            var state = graphics.Save();
            graphics.TranslateTransform(center.X, center.Y);
            graphics.RotateTransform(-this.overlayRotationDegrees);
            graphics.DrawImage(this.overlayMapImage, new RectangleF(-width / 2F, -height / 2F, width, height));

            if (this.editMapImageCheckBox != null && this.editMapImageCheckBox.Checked)
            {
                using (var pen = new Pen(this.CurrentDangerColor, 2F))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    graphics.DrawRectangle(pen, -width / 2F, -height / 2F, width, height);
                }
            }

            graphics.Restore(state);
        }

        private PointF WorldToScreen(PointF worldPoint)
        {
            var basePoint = new PointF(
                this.mapProjectionOffsetX + ((worldPoint.X - this.mapProjectionMinX) * this.mapProjectionBaseScale),
                this.mapPanel.ClientSize.Height - this.mapProjectionOffsetY - ((worldPoint.Y - this.mapProjectionMinY) * this.mapProjectionBaseScale));
            var panelCenter = new PointF(this.mapPanel.ClientSize.Width / 2F, this.mapPanel.ClientSize.Height / 2F);
            return new PointF(
                panelCenter.X + ((basePoint.X - panelCenter.X) * this.mapViewZoom) + this.mapViewPanScreen.X,
                panelCenter.Y + ((basePoint.Y - panelCenter.Y) * this.mapViewZoom) + this.mapViewPanScreen.Y);
        }

        private void ResetMapView()
        {
            this.mapViewZoom = 1F;
            this.mapViewPanScreen = PointF.Empty;
            this.mapProjectionScale = this.mapProjectionBaseScale;
            this.mapPanel.Invalidate();
        }

        private void UpdateOverlayScaleLabel()
        {
            if (this.overlayScaleLabel == null)
            {
                return;
            }

            var percent = this.overlayScaleTrackBar == null ? 100 : this.overlayScaleTrackBar.Value;
            this.overlayScaleLabel.Text = "Skalierung: " + percent + " % (x" + this.GetOverlayScaleMultiplier().ToString("0.00") + ")";
        }

        private float GetOverlayScaleMultiplier()
        {
            if (this.overlayScaleTrackBar == null)
            {
                return 1F;
            }

            var normalized = this.overlayScaleTrackBar.Value / 100F;
            if (normalized <= 1F)
            {
                return normalized;
            }

            return normalized * normalized;
        }

        private void DisposeOverlayImage()
        {
            if (this.overlayMapImage != null)
            {
                this.overlayMapImage.Dispose();
                this.overlayMapImage = null;
            }
        }

        private static Uri BuildMqttUri(MqttConnectionInfo info, out string wsPath)
        {
            var mqttHost = (info.MqttHost ?? string.Empty).Trim();
            var mqttUrl = (info.MqttUrl ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(mqttHost) && string.IsNullOrWhiteSpace(mqttUrl))
            {
                throw new InvalidOperationException("MQTT-Host und MQTT-URL fehlen.");
            }

            Uri hostUri;
            if (!Uri.TryCreate(mqttHost, UriKind.Absolute, out hostUri))
            {
                var hostValue = string.IsNullOrWhiteSpace(mqttHost) ? "mqtt.navimow.com" : mqttHost.Trim('/');
                hostUri = new Uri("wss://" + hostValue);
            }

            Uri urlUri;
            string pathAndQuery;
            if (Uri.TryCreate(mqttUrl, UriKind.Absolute, out urlUri))
            {
                pathAndQuery = urlUri.PathAndQuery;
            }
            else
            {
                pathAndQuery = string.IsNullOrWhiteSpace(mqttUrl) ? "/" : mqttUrl;
            }

            if (!pathAndQuery.StartsWith("/", StringComparison.Ordinal))
            {
                pathAndQuery = "/" + pathAndQuery;
            }

            wsPath = pathAndQuery;

            var builder = new UriBuilder(hostUri.Scheme, hostUri.Host, hostUri.IsDefaultPort ? 443 : hostUri.Port, pathAndQuery);
            return builder.Uri;
        }

        private static string BuildMqttClientId(string username)
        {
            var baseName = string.IsNullOrWhiteSpace(username) ? "unknown" : username.Trim();
            var safe = new StringBuilder();
            for (int i = 0; i < baseName.Length; i++)
            {
                var ch = baseName[i];
                safe.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            }

            var randomPart = Guid.NewGuid().ToString("N").Substring(0, 10);
            return "web_" + safe.ToString() + "_" + randomPart;
        }

        private static string MaskValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "<leer>";
            }

            if (value.Length <= 4)
            {
                return new string('*', value.Length);
            }

            return value.Substring(0, 2) + "***" + value.Substring(value.Length - 2);
        }

        private static void DrawMarker(Graphics graphics, Brush fillBrush, Pen borderPen, PointF center, float size)
        {
            var rect = new RectangleF(center.X - size / 2F, center.Y - size / 2F, size, size);
            graphics.FillEllipse(fillBrush, rect);
            graphics.DrawEllipse(borderPen, rect);
        }

        private static float Distance(PointF a, PointF b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return (float)Math.Sqrt((dx * dx) + (dy * dy));
        }
    }
}
