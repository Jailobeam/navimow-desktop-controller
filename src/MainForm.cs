using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
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
        private Label signalLabel;
        private Label positionLabel;
        private Label errorLabel;
        private Label timestampLabel;
        private Label mqttStatusLabel;
        private Panel mapPanel;
        private ListView allValuesListView;
        private TreeView deviceTreeView;
        private TreeView statusTreeView;
        private TreeView eventTreeView;
        private TreeView attributesTreeView;
        private TreeView locationTreeView;
        private TreeView mqttInfoTreeView;
        private TextBox rawJsonTextBox;
        private TextBox logTextBox;
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
            this.BackColor = Color.WhiteSmoke;
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            this.lastDetailRefreshUtc = DateTime.MinValue;

            this.BuildLayout();
            this.WireEvents();
            this.LoadOverlayState();

            this.session = this.sessionStore.Load();
            if (this.session != null && !string.IsNullOrWhiteSpace(this.session.AccessToken))
            {
                this.connectionStatusLabel.Text = "Gespeicherter Token geladen.";
                this.connectionStatusLabel.ForeColor = Color.DarkGreen;
            }
            else
            {
                this.connectionStatusLabel.Text = "Noch kein Token vorhanden.";
                this.connectionStatusLabel.ForeColor = Color.DarkRed;
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

        private void BuildLayout()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.Controls.Add(root);

            var header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.Padding = new Padding(14, 8, 14, 6);
            header.ColumnCount = 7;
            header.RowCount = 2;
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270F));
            root.Controls.Add(header, 0, 0);

            this.loginButton = new Button();
            this.loginButton.Text = "Login bei Navimow";
            this.loginButton.Dock = DockStyle.Fill;
            this.loginButton.Margin = new Padding(0, 0, 10, 0);
            header.Controls.Add(this.loginButton, 0, 0);

            var authLabel = new Label();
            authLabel.Text = "Authorization Code:";
            authLabel.Dock = DockStyle.Fill;
            authLabel.TextAlign = ContentAlignment.MiddleLeft;
            authLabel.Margin = new Padding(0, 0, 10, 0);
            header.Controls.Add(authLabel, 1, 0);

            this.authorizationCodeTextBox = new TextBox();
            this.authorizationCodeTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            this.authorizationCodeTextBox.Margin = new Padding(0, 5, 10, 5);
            header.Controls.Add(this.authorizationCodeTextBox, 2, 0);

            this.tokenButton = new Button();
            this.tokenButton.Text = "Token abrufen";
            this.tokenButton.Dock = DockStyle.Fill;
            this.tokenButton.Margin = new Padding(0, 0, 10, 0);
            header.Controls.Add(this.tokenButton, 3, 0);

            this.deleteTokenButton = new Button();
            this.deleteTokenButton.Text = "Token löschen";
            this.deleteTokenButton.Dock = DockStyle.Fill;
            this.deleteTokenButton.Margin = new Padding(0);
            header.Controls.Add(this.deleteTokenButton, 4, 0);

            this.connectionStatusLabel = new Label();
            this.connectionStatusLabel.Dock = DockStyle.Fill;
            this.connectionStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.connectionStatusLabel.AutoEllipsis = true;
            this.connectionStatusLabel.Margin = new Padding(0, 0, 0, 0);
            header.Controls.Add(this.connectionStatusLabel, 3, 1);
            header.SetColumnSpan(this.connectionStatusLabel, 2);

            var overlayHeader = new TableLayoutPanel();
            overlayHeader.Dock = DockStyle.Fill;
            overlayHeader.Margin = new Padding(6, 0, 0, 0);
            overlayHeader.RowCount = 2;
            overlayHeader.ColumnCount = 4;
            overlayHeader.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            overlayHeader.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            overlayHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94F));
            overlayHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94F));
            overlayHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            overlayHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            header.Controls.Add(overlayHeader, 5, 0);
            header.SetColumnSpan(overlayHeader, 2);
            header.SetRowSpan(overlayHeader, 2);

            var contentGrid = new TableLayoutPanel();
            contentGrid.Dock = DockStyle.Fill;
            contentGrid.Padding = new Padding(12, 10, 12, 12);
            contentGrid.ColumnCount = 2;
            contentGrid.RowCount = 1;
            contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320F));
            contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            contentGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(contentGrid, 0, 1);

            var leftHost = new Panel();
            leftHost.Dock = DockStyle.Fill;
            leftHost.Margin = new Padding(0, 0, 14, 0);
            contentGrid.Controls.Add(leftHost, 0, 0);

            var leftPanel = new Panel();
            leftPanel.Dock = DockStyle.Fill;
            leftPanel.AutoScroll = true;
            leftHost.Controls.Add(leftPanel);

            this.getDevicesButton = new Button();
            this.getDevicesButton.Text = "Geräte abrufen";
            this.getDevicesButton.Location = new Point(0, 12);
            this.getDevicesButton.Size = new Size(300, 34);
            leftPanel.Controls.Add(this.getDevicesButton);

            this.deviceComboBox = new ComboBox();
            this.deviceComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            this.deviceComboBox.Location = new Point(0, 56);
            this.deviceComboBox.Size = new Size(300, 24);
            leftPanel.Controls.Add(this.deviceComboBox);

            this.showStatusButton = new Button();
            this.showStatusButton.Text = "Status aktualisieren";
            this.showStatusButton.Location = new Point(0, 100);
            this.showStatusButton.Size = new Size(300, 34);
            leftPanel.Controls.Add(this.showStatusButton);

            this.showMqttInfoButton = new Button();
            this.showMqttInfoButton.Text = "MQTT Info aktualisieren";
            this.showMqttInfoButton.Location = new Point(0, 144);
            this.showMqttInfoButton.Size = new Size(300, 34);
            leftPanel.Controls.Add(this.showMqttInfoButton);

            this.disableMqttCheckBox = new CheckBox();
            this.disableMqttCheckBox.Text = "MQTT deaktivieren";
            this.disableMqttCheckBox.AutoSize = true;
            this.disableMqttCheckBox.Location = new Point(0, 188);
            leftPanel.Controls.Add(this.disableMqttCheckBox);

            this.loadMapImageButton = new Button();
            this.loadMapImageButton.Text = "Bild laden";
            this.loadMapImageButton.Dock = DockStyle.Fill;
            this.loadMapImageButton.Margin = new Padding(0, 0, 8, 6);
            leftPanel.Controls.Add(this.loadMapImageButton);

            this.clearMapImageButton = new Button();
            this.clearMapImageButton.Text = "Bild entfernen";
            this.clearMapImageButton.Dock = DockStyle.Fill;
            this.clearMapImageButton.Margin = new Padding(0, 0, 8, 6);
            leftPanel.Controls.Add(this.clearMapImageButton);

            this.editMapImageCheckBox = new CheckBox();
            this.editMapImageCheckBox.Text = "Bild bearbeiten";
            this.editMapImageCheckBox.AutoSize = true;
            this.editMapImageCheckBox.Margin = new Padding(0, 4, 0, 0);
            leftPanel.Controls.Add(this.editMapImageCheckBox);

            this.overlayScaleLabel = new Label();
            this.overlayScaleLabel.Text = "Skalierung: 100 % (x1.00)";
            this.overlayScaleLabel.AutoSize = false;
            this.overlayScaleLabel.Dock = DockStyle.Fill;
            this.overlayScaleLabel.Margin = new Padding(0);
            this.overlayScaleLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.overlayScaleLabel.Font = new Font(this.Font.FontFamily, 7.5F, FontStyle.Regular);
            leftPanel.Controls.Add(this.overlayScaleLabel);

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
            leftPanel.Controls.Add(this.overlayScaleTrackBar);

            this.overlayRotationLabel = new Label();
            this.overlayRotationLabel.Text = "Drehung: 0 Grad";
            this.overlayRotationLabel.AutoSize = false;
            this.overlayRotationLabel.Dock = DockStyle.Fill;
            this.overlayRotationLabel.Margin = new Padding(0);
            this.overlayRotationLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.overlayRotationLabel.Font = new Font(this.Font.FontFamily, 7.5F, FontStyle.Regular);
            leftPanel.Controls.Add(this.overlayRotationLabel);

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
            leftPanel.Controls.Add(this.overlayRotationTrackBar);

            var scaleHost = new TableLayoutPanel();
            scaleHost.Dock = DockStyle.Fill;
            scaleHost.Margin = new Padding(8, 0, 8, 0);
            scaleHost.RowCount = 2;
            scaleHost.ColumnCount = 1;
            scaleHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));
            scaleHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            scaleHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            scaleHost.Controls.Add(this.overlayScaleLabel, 0, 0);
            scaleHost.Controls.Add(this.overlayScaleTrackBar, 0, 1);
            overlayHeader.Controls.Add(scaleHost, 2, 0);
            overlayHeader.SetRowSpan(scaleHost, 2);

            var rotationHost = new TableLayoutPanel();
            rotationHost.Dock = DockStyle.Fill;
            rotationHost.Margin = new Padding(0, 0, 0, 0);
            rotationHost.RowCount = 2;
            rotationHost.ColumnCount = 1;
            rotationHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));
            rotationHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            rotationHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rotationHost.Controls.Add(this.overlayRotationLabel, 0, 0);
            rotationHost.Controls.Add(this.overlayRotationTrackBar, 0, 1);
            overlayHeader.Controls.Add(rotationHost, 3, 0);
            overlayHeader.SetRowSpan(rotationHost, 2);

            overlayHeader.Controls.Add(this.loadMapImageButton, 0, 0);
            overlayHeader.Controls.Add(this.clearMapImageButton, 1, 0);
            overlayHeader.Controls.Add(this.editMapImageCheckBox, 0, 1);
            overlayHeader.SetColumnSpan(this.editMapImageCheckBox, 2);

            var infoHeader = new Label();
            infoHeader.Text = "Übersicht:";
            infoHeader.Font = new Font(this.Font, FontStyle.Bold);
            infoHeader.AutoSize = true;
            infoHeader.Location = new Point(0, 226);
            leftPanel.Controls.Add(infoHeader);

            this.selectedDeviceLabel = this.CreateInfoLabel("Gerät: -", 252, leftPanel);
            this.deviceStateLabel = this.CreateInfoLabel("Zustand: -", 276, leftPanel);
            this.batteryLabel = this.CreateInfoLabel("Akku: -", 300, leftPanel);
            this.signalLabel = this.CreateInfoLabel("Signal: -", 324, leftPanel);
            this.positionLabel = this.CreateInfoLabel("Position: -", 348, leftPanel);
            this.errorLabel = this.CreateInfoLabel("Fehler: -", 372, leftPanel);
            this.timestampLabel = this.CreateInfoLabel("Zeit: -", 396, leftPanel);
            this.mqttStatusLabel = this.CreateInfoLabel("MQTT: -", 420, leftPanel);
            this.signalLabel.Visible = false;
            this.selectedDeviceLabel.Top = 252;
            this.deviceStateLabel.Top = 276;
            this.batteryLabel.Top = 300;
            this.signalLabel.Top = 324;
            this.positionLabel.Top = 348;
            this.errorLabel.Top = 372;
            this.timestampLabel.Top = 396;
            this.mqttStatusLabel.Top = 420;
            this.positionLabel.Top = this.signalLabel.Top;
            this.errorLabel.Top = this.positionLabel.Top + 24;
            this.timestampLabel.Top = this.errorLabel.Top + 24;
            this.mqttStatusLabel.Top = this.timestampLabel.Top + 24;

            var commandHeader = new Label();
            commandHeader.Text = "Kommandos:";
            commandHeader.Font = new Font(this.Font, FontStyle.Bold);
            commandHeader.AutoSize = true;
            commandHeader.Location = new Point(0, 468);
            leftPanel.Controls.Add(commandHeader);

            this.startButton = this.CreateCommandButton("Start", 0, 498);
            this.stopButton = this.CreateCommandButton("Stop", 76, 498);
            this.pauseButton = this.CreateCommandButton("Pause", 152, 498);
            this.resumeButton = this.CreateCommandButton("Resume", 228, 498);
            this.dockButton = this.CreateCommandButton("Dock", 0, 536);

            leftPanel.Controls.Add(this.startButton);
            leftPanel.Controls.Add(this.stopButton);
            leftPanel.Controls.Add(this.pauseButton);
            leftPanel.Controls.Add(this.resumeButton);
            leftPanel.Controls.Add(this.dockButton);

            var rightGrid = new TableLayoutPanel();
            rightGrid.Dock = DockStyle.Fill;
            rightGrid.Margin = new Padding(0);
            rightGrid.ColumnCount = 1;
            rightGrid.RowCount = 2;
            rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 330F));
            rightGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            contentGrid.Controls.Add(rightGrid, 1, 0);

            var mapHost = new Panel();
            mapHost.Dock = DockStyle.Fill;
            mapHost.Margin = new Padding(0);
            mapHost.Padding = new Padding(0);
            mapHost.BackColor = Color.Gainsboro;
            rightGrid.Controls.Add(mapHost, 0, 0);

            this.mapPanel = new Panel();
            this.mapPanel.Dock = DockStyle.Fill;
            this.mapPanel.BackColor = Color.White;
            this.mapPanel.Margin = new Padding(0);
            this.mapPanel.TabStop = true;
            this.mapPanel.Paint += this.MapPanel_Paint;
            mapHost.Controls.Add(this.mapPanel);
            EnableDoubleBuffer(this.mapPanel);

            var tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Multiline = false;
            tabs.Margin = new Padding(0, 8, 0, 0);
            rightGrid.Controls.Add(tabs, 0, 1);

            this.allValuesListView = new ListView();
            this.allValuesListView.Dock = DockStyle.Fill;
            this.allValuesListView.View = View.Details;
            this.allValuesListView.FullRowSelect = true;
            this.allValuesListView.GridLines = true;
            this.allValuesListView.Columns.Add("Pfad", 260);
            this.allValuesListView.Columns.Add("Label", 180);
            this.allValuesListView.Columns.Add("Wert", 420);
            tabs.TabPages.Add(this.CreateTab("Alle Werte", this.allValuesListView));
            EnableDoubleBuffer(this.allValuesListView);

            this.deviceTreeView = this.CreateTreeView();
            tabs.TabPages.Add(this.CreateTab("Gerät", this.deviceTreeView));

            this.statusTreeView = this.CreateTreeView();
            tabs.TabPages.Add(this.CreateTab("Status", this.statusTreeView));

            this.eventTreeView = this.CreateTreeView();
            tabs.TabPages.Add(this.CreateTab("MQTT Event", this.eventTreeView));

            this.attributesTreeView = this.CreateTreeView();
            tabs.TabPages.Add(this.CreateTab("MQTT Attribute", this.attributesTreeView));

            this.locationTreeView = this.CreateTreeView();
            tabs.TabPages.Add(this.CreateTab("MQTT Location", this.locationTreeView));

            this.mqttInfoTreeView = this.CreateTreeView();
            tabs.TabPages.Add(this.CreateTab("MQTT Info", this.mqttInfoTreeView));

            this.rawJsonTextBox = new TextBox();
            this.rawJsonTextBox.Dock = DockStyle.Fill;
            this.rawJsonTextBox.Multiline = true;
            this.rawJsonTextBox.ScrollBars = ScrollBars.Both;
            this.rawJsonTextBox.ReadOnly = true;
            this.rawJsonTextBox.WordWrap = false;
            this.rawJsonTextBox.Font = new Font("Consolas", 10F);
            tabs.TabPages.Add(this.CreateTab("Raw JSON", this.rawJsonTextBox));

            this.logTextBox = new TextBox();
            this.logTextBox.Dock = DockStyle.Fill;
            this.logTextBox.Multiline = true;
            this.logTextBox.ScrollBars = ScrollBars.Both;
            this.logTextBox.ReadOnly = true;
            this.logTextBox.WordWrap = false;
            this.logTextBox.Font = new Font("Consolas", 10F);
            tabs.TabPages.Add(this.CreateTab("Log", this.logTextBox));
        }

        private Label CreateInfoLabel(string text, int top, Control parent)
        {
            var label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Location = new Point(12, top);
            parent.Controls.Add(label);
            return label;
        }

        private Button CreateCommandButton(string text, int x, int y)
        {
            var button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(70, 30);
            return button;
        }

        private TreeView CreateTreeView()
        {
            var treeView = new TreeView();
            treeView.Dock = DockStyle.Fill;
            treeView.HideSelection = false;
            return treeView;
        }

        private TabPage CreateTab(string title, Control control)
        {
            var page = new TabPage(title);
            page.Controls.Add(control);
            return page;
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
            this.overlayScaleTrackBar.Scroll += (sender, args) => this.HandleOverlayScaleChanged();
            this.overlayRotationTrackBar.Scroll += (sender, args) => this.HandleOverlayRotationChanged();
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
                this.connectionStatusLabel.ForeColor = Color.DarkGreen;
                this.AppendLog("Token erfolgreich abgerufen.");
            }
            catch (Exception ex)
            {
                this.connectionStatusLabel.Text = "Token konnte nicht geladen werden.";
                this.connectionStatusLabel.ForeColor = Color.DarkRed;
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
            this.connectionStatusLabel.ForeColor = Color.DarkRed;
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
            this.allValuesListView.BeginUpdate();
            this.allValuesListView.Items.Clear();

            var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            this.AddFlattenedSection("device", this.lastDeviceInfo, addedPaths);
            this.AddFlattenedSection("status", this.lastStatus, addedPaths);
            this.AddFlattenedSection("event", this.lastEvent, addedPaths);
            this.AddFlattenedSection("attributes", this.lastAttributes, addedPaths);
            this.AddFlattenedSection("location", this.lastLocation, addedPaths);
            this.AddFlattenedSection("mqtt", this.lastMqttInfo, addedPaths);
            this.AddKnownFieldRows(addedPaths);

            foreach (ColumnHeader column in this.allValuesListView.Columns)
            {
                column.Width = -2;
            }

            this.allValuesListView.EndUpdate();
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
                var item = new ListViewItem(fullPath);
                item.SubItems.Add(label);
                item.SubItems.Add(FieldCatalog.GetDisplayValue(lastSegment, pair.Value));
                this.allValuesListView.Items.Add(item);
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

            this.rawJsonTextBox.Text = sections.Count == 0 ? "Noch keine Daten vorhanden." : string.Join(Environment.NewLine + Environment.NewLine, sections.ToArray());
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
                this.connectionStatusLabel.ForeColor = Color.DarkGreen;
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

                var item = new ListViewItem(fullPath);
                item.SubItems.Add(FieldCatalog.GetLabel(key));
                item.SubItems.Add(display);
                this.allValuesListView.Items.Add(item);
            }
        }

        private void AppendLog(string message)
        {
            var block = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine + Environment.NewLine;
            this.logTextBox.AppendText(block);
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
            e.Graphics.Clear(Color.White);

            using (var borderPen = new Pen(Color.Gainsboro, 1))
            {
                e.Graphics.DrawRectangle(borderPen, 0, 0, this.mapPanel.Width - 1, this.mapPanel.Height - 1);
            }

            if (this.pathPoints.Count == 0)
            {
                this.mapProjectionValid = false;
                this.DrawOverlayWithoutPath(e.Graphics);

                using (var font = new Font("Segoe UI", 11F))
                using (var brush = new SolidBrush(Color.Gray))
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
                using (var pathPen = new Pen(Color.Blue, 2F))
                {
                    e.Graphics.DrawLines(pathPen, screenPoints);
                }
            }

            DrawMarker(e.Graphics, Brushes.Red, Pens.DarkRed, screenPoints[0], 10F);
            DrawMarker(e.Graphics, Brushes.LimeGreen, Pens.DarkGreen, screenPoints[screenPoints.Length - 1], 10F);
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
                using (var pen = new Pen(Color.DarkOrange, 2F))
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
