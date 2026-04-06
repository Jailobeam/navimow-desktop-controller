using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace NavimowDesktopController
{
    internal sealed class MainForm : Form
    {
        private readonly NavimowApiClient apiClient;
        private readonly SessionStore sessionStore;
        private readonly List<NavimowDevice> devices;
        private readonly List<PointF> pathPoints;

        private SessionData session;
        private MqttWsClient mqttClient;
        private Dictionary<string, object> lastDeviceInfo;
        private Dictionary<string, object> lastStatus;
        private Dictionary<string, object> lastEvent;
        private Dictionary<string, object> lastAttributes;
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
        private TreeView mqttInfoTreeView;
        private TextBox rawJsonTextBox;
        private TextBox logTextBox;
        private Timer refreshTimer;
        private string lastMqttFailureSignature;

        public MainForm()
        {
            this.apiClient = new NavimowApiClient();
            this.sessionStore = new SessionStore();
            this.devices = new List<NavimowDevice>();
            this.pathPoints = new List<PointF>();

            this.Text = "Navimow Controller";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(1180, 760);
            this.ClientSize = new Size(1320, 860);
            this.BackColor = Color.WhiteSmoke;
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            this.BuildLayout();
            this.WireEvents();

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

            base.OnFormClosed(e);
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this.Controls.Add(root);

            var header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.Padding = new Padding(14, 12, 14, 8);
            header.ColumnCount = 6;
            header.RowCount = 1;
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390F));
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
            this.authorizationCodeTextBox.Dock = DockStyle.Fill;
            this.authorizationCodeTextBox.Margin = new Padding(0, 3, 10, 3);
            header.Controls.Add(this.authorizationCodeTextBox, 2, 0);

            this.tokenButton = new Button();
            this.tokenButton.Text = "Token abrufen";
            this.tokenButton.Dock = DockStyle.Fill;
            this.tokenButton.Margin = new Padding(0, 0, 10, 0);
            header.Controls.Add(this.tokenButton, 3, 0);

            this.deleteTokenButton = new Button();
            this.deleteTokenButton.Text = "Token löschen";
            this.deleteTokenButton.Dock = DockStyle.Fill;
            this.deleteTokenButton.Margin = new Padding(0, 0, 10, 0);
            header.Controls.Add(this.deleteTokenButton, 4, 0);

            this.connectionStatusLabel = new Label();
            this.connectionStatusLabel.Dock = DockStyle.Fill;
            this.connectionStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.connectionStatusLabel.AutoEllipsis = true;
            header.Controls.Add(this.connectionStatusLabel, 5, 0);

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
            this.mapPanel.Paint += this.MapPanel_Paint;
            mapHost.Controls.Add(this.mapPanel);

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

            this.deviceTreeView = this.CreateTreeView();
            tabs.TabPages.Add(this.CreateTab("Gerät", this.deviceTreeView));

            this.statusTreeView = this.CreateTreeView();
            tabs.TabPages.Add(this.CreateTab("Status", this.statusTreeView));

            this.eventTreeView = this.CreateTreeView();
            tabs.TabPages.Add(this.CreateTab("MQTT Event", this.eventTreeView));

            this.attributesTreeView = this.CreateTreeView();
            tabs.TabPages.Add(this.CreateTab("MQTT Attribute", this.attributesTreeView));

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
            this.deviceComboBox.SelectedIndexChanged += async (sender, args) => await this.HandleDeviceSelectionChangedAsync();
            this.startButton.Click += async (sender, args) => await this.SendCommandAsync("start");
            this.stopButton.Click += async (sender, args) => await this.SendCommandAsync("stop");
            this.pauseButton.Click += async (sender, args) => await this.SendCommandAsync("pause");
            this.resumeButton.Click += async (sender, args) => await this.SendCommandAsync("resume");
            this.dockButton.Click += async (sender, args) => await this.SendCommandAsync("dock");
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
                this.lastStatus = await this.apiClient.GetDeviceStatusAsync(this.session.AccessToken, deviceId);
                this.ApplyStatusSummary(this.lastStatus);
                this.UpdateVisualizations();

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

            this.AppendLog("TOPIC : " + message.Topic + Environment.NewLine + "PAYLOAD:" + Environment.NewLine + message.Payload);

            Dictionary<string, object> payload;
            try
            {
                payload = JsonUtils.ParseObject(message.Payload);
            }
            catch
            {
                return;
            }

            if (message.Topic.EndsWith("/state", StringComparison.OrdinalIgnoreCase))
            {
                this.lastStatus = payload;
                this.ApplyStatusSummary(payload);
            }
            else if (message.Topic.EndsWith("/event", StringComparison.OrdinalIgnoreCase))
            {
                this.lastEvent = payload;
            }
            else if (message.Topic.EndsWith("/attributes", StringComparison.OrdinalIgnoreCase))
            {
                this.lastAttributes = payload;
            }

            this.UpdateVisualizations();
        }

        private void ApplyStatusSummary(Dictionary<string, object> status)
        {
            if (status == null)
            {
                return;
            }

            this.deviceStateLabel.Text = "Zustand: " + this.FormatField(status, "vehicleState", "state", "status");
            this.batteryLabel.Text = "Akku: " + this.FormatField(status, "battery", "batteryLevel", "electricity");
            this.signalLabel.Text = "Signal: " + this.FormatField(status, "signal", "gpsSignal", "signal_strength", "signalStrength");
            this.positionLabel.Text = "Position: " + this.GetPositionText(status);
            this.errorLabel.Text = "Fehler: " + this.FormatField(status, "error_code", "errorCode", "error_message");
            this.timestampLabel.Text = "Zeit: " + this.FormatField(status, "time", "timestamp");

            var x = JsonUtils.GetDouble(status, "postureX");
            var y = JsonUtils.GetDouble(status, "postureY");
            if (x.HasValue && y.HasValue)
            {
                var point = new PointF((float)x.Value, (float)y.Value);
                if (this.pathPoints.Count == 0 || Distance(this.pathPoints[this.pathPoints.Count - 1], point) > 0.01f)
                {
                    this.pathPoints.Add(point);
                    if (this.pathPoints.Count > 800)
                    {
                        this.pathPoints.RemoveAt(0);
                    }
                }
            }

            this.mapPanel.Invalidate();
        }

        private void UpdateVisualizations()
        {
            this.UpdateTreeView(this.deviceTreeView, "Device", this.lastDeviceInfo);
            this.UpdateTreeView(this.statusTreeView, "Status", this.lastStatus);
            this.UpdateTreeView(this.eventTreeView, "Event", this.lastEvent);
            this.UpdateTreeView(this.attributesTreeView, "Attributes", this.lastAttributes);
            this.UpdateTreeView(this.mqttInfoTreeView, "MQTT", this.lastMqttInfo);
            this.UpdateAllValuesList();
            this.UpdateRawJson();
        }

        private void UpdateTreeView(TreeView treeView, string rootLabel, Dictionary<string, object> data)
        {
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

            this.AddFlattenedSection("device", this.lastDeviceInfo);
            this.AddFlattenedSection("status", this.lastStatus);
            this.AddFlattenedSection("event", this.lastEvent);
            this.AddFlattenedSection("attributes", this.lastAttributes);
            this.AddFlattenedSection("mqtt", this.lastMqttInfo);

            foreach (ColumnHeader column in this.allValuesListView.Columns)
            {
                column.Width = -2;
            }

            this.allValuesListView.EndUpdate();
        }

        private void AddFlattenedSection(string prefix, Dictionary<string, object> data)
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
            this.lastMqttInfo = null;
            this.deviceStateLabel.Text = "Zustand: -";
            this.batteryLabel.Text = "Akku: -";
            this.signalLabel.Text = "Signal: -";
            this.positionLabel.Text = "Position: -";
            this.errorLabel.Text = "Fehler: -";
            this.timestampLabel.Text = "Zeit: -";
            this.mqttStatusLabel.Text = this.disableMqttCheckBox != null && this.disableMqttCheckBox.Checked ? "MQTT: Manuell deaktiviert" : "MQTT: -";
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

        private string GetPositionText(Dictionary<string, object> status)
        {
            var x = JsonUtils.GetString(status, "postureX");
            var y = JsonUtils.GetString(status, "postureY");
            if (!string.IsNullOrWhiteSpace(x) || !string.IsNullOrWhiteSpace(y))
            {
                return "X=" + x + ", Y=" + y;
            }

            var lat = JsonUtils.GetString(status, "latitude");
            var lng = JsonUtils.GetString(status, "longitude");
            if (!string.IsNullOrWhiteSpace(lat) || !string.IsNullOrWhiteSpace(lng))
            {
                return "Lat=" + lat + ", Lng=" + lng;
            }

            return "-";
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

        private void StopMqttClient()
        {
            if (this.mqttClient != null)
            {
                this.mqttClient.PublishReceived -= this.MqttClient_PublishReceived;
                this.mqttClient.Dispose();
                this.mqttClient = null;
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
                using (var font = new Font("Segoe UI", 11F))
                using (var brush = new SolidBrush(Color.Gray))
                {
                    e.Graphics.DrawString("Noch keine Positionsdaten vorhanden.", font, brush, new PointF(18F, 18F));
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
            float scaleX = (this.mapPanel.ClientSize.Width - padding * 2F) / width;
            float scaleY = (this.mapPanel.ClientSize.Height - padding * 2F) / height;
            float scale = Math.Max(0.1F, Math.Min(scaleX, scaleY));

            var screenPoints = this.pathPoints.Select(p =>
                new PointF(
                    padding + ((p.X - minX) * scale),
                    this.mapPanel.ClientSize.Height - padding - ((p.Y - minY) * scale)))
                .ToArray();

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
