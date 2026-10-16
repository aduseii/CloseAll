using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace CloseAllApp
{
    class MainForm : Form
    {
        const int HOTKEY_ID = 1;
        static readonly Color Accent = Color.FromArgb(217, 48, 37);

        readonly Settings settings;
        bool startHidden;
        bool exiting, busy, populating, hotkeyOk, shownTrayTip;
        bool uiReady;          // true once every control exists (fixes the first-open crash)
        bool updating;
        UpdateInfo pendingUpdate;

        ListView list;
        TableLayoutPanel root, bottom;
        FlowLayoutPanel header, options;
        bool sized;
        ImageList icons;
        CheckBox chkForce, chkHotkey, chkStartup;
        NumericUpDown numWait;
        Label lblStatus, lblVersion;
        Button btnClose, btnRefresh, btnUpdate;
        NotifyIcon tray;
        ToolStripMenuItem trayUpdate;

        public MainForm(Settings s, bool hidden)
        {
            settings = s;
            startHidden = hidden;
            BuildUi();
            uiReady = true;
        }

        void BuildUi()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Close All";
            Font = new Font("Segoe UI", 9.75f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 540);
            BackColor = Color.White;

            root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(16) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Header
            header = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
            header.Controls.Add(new Label { Text = "Close All", Font = new Font("Segoe UI Semibold", 16f), AutoSize = true, Margin = new Padding(0) });
            header.Controls.Add(new Label { Text = "These windows will be closed. Untick any app you want to keep open.", ForeColor = Color.DimGray, AutoSize = true, Margin = new Padding(2, 2, 0, 0) });
            root.Controls.Add(header, 0, 0);

            // Window list
            icons = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(20, 20) };
            list = new ListView {
                View = View.Details, CheckBoxes = true, FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable, Dock = DockStyle.Fill,
                SmallImageList = icons, BorderStyle = BorderStyle.FixedSingle, MultiSelect = false
            };
            list.Columns.Add("App", 170);
            list.Columns.Add("Window", 300);
            list.ItemChecked += List_ItemChecked;
            list.Resize += (s, e) => FitColumns();
            root.Controls.Add(list, 0, 1);

            // Options
            options = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 10, 0, 6) };
            var forceRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            chkForce = new CheckBox { Text = "Force-close apps still open after", AutoSize = true, Checked = settings.Force, Margin = new Padding(0, 4, 4, 0) };
            numWait = new NumericUpDown { Minimum = 0, Maximum = 30, DecimalPlaces = 1, Increment = 0.5m, Width = 56, Margin = new Padding(0, 2, 4, 0) };
            numWait.Value = Math.Min(numWait.Maximum, Math.Max(numWait.Minimum, settings.WaitSeconds));
            var secs = new Label { Text = "seconds", AutoSize = true, Margin = new Padding(0, 5, 0, 0) };
            forceRow.Controls.AddRange(new Control[] { chkForce, numWait, secs });
            options.Controls.Add(forceRow);
            options.Controls.Add(new Label { Text = "Skips \"Save changes?\" prompts. Unsaved work in those apps is lost.", ForeColor = Color.DimGray, AutoSize = true, Margin = new Padding(18, 0, 0, 6) });
            chkHotkey = new CheckBox { Text = "Ctrl + Alt + Q closes everything (keeps running in the system tray)", AutoSize = true, Checked = settings.Hotkey, Margin = new Padding(0, 2, 0, 2) };
            chkStartup = new CheckBox { Text = "Start with Windows", AutoSize = true, Checked = Settings.StartupEnabled, Margin = new Padding(0, 2, 0, 2) };
            options.Controls.Add(chkHotkey);
            options.Controls.Add(chkStartup);

            var updateRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
            btnUpdate = new Button { Text = "Check for updates", AutoSize = true, FlatStyle = FlatStyle.System, Margin = new Padding(0, 0, 8, 0) };
            lblVersion = new Label { Text = "Version " + Updater.CurrentText, ForeColor = Color.DimGray, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            updateRow.Controls.Add(btnUpdate);
            updateRow.Controls.Add(lblVersion);
            options.Controls.Add(updateRow);
            root.Controls.Add(options, 0, 2);

            chkForce.CheckedChanged += (s, e) => { settings.Force = chkForce.Checked; numWait.Enabled = chkForce.Checked; settings.Save(); };
            numWait.Enabled = chkForce.Checked;
            numWait.ValueChanged += (s, e) => { settings.WaitSeconds = numWait.Value; settings.Save(); };
            chkHotkey.CheckedChanged += (s, e) => { settings.Hotkey = chkHotkey.Checked; settings.Save(); UpdateHotkey(); };
            chkStartup.CheckedChanged += (s, e) => { Settings.StartupEnabled = chkStartup.Checked; };
            btnUpdate.Click += (s, e) => { if (pendingUpdate != null) StartUpdate(pendingUpdate); else CheckForUpdates(true); };

            // Bottom bar
            bottom = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 0) };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            lblStatus = new Label { AutoSize = false, AutoEllipsis = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray, Margin = new Padding(0, 0, 8, 0) };
            btnRefresh = new Button { Text = "Refresh", AutoSize = true, Padding = new Padding(10, 4, 10, 4), FlatStyle = FlatStyle.System, Anchor = AnchorStyles.Right, Margin = new Padding(0, 0, 8, 0) };
            btnClose = new Button {
                Text = "Close all windows", AutoSize = true, Padding = new Padding(18, 8, 18, 8), FlatStyle = FlatStyle.Flat,
                BackColor = Accent, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 11f),
                Anchor = AnchorStyles.Right, Margin = new Padding(0), Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(190, 35, 28);
            btnRefresh.Click += (s, e) => RefreshList();
            btnClose.Click += (s, e) => DoCloseAll();
            bottom.Controls.Add(lblStatus, 0, 0);
            bottom.Controls.Add(btnRefresh, 1, 0);
            bottom.Controls.Add(btnClose, 2, 0);
            root.Controls.Add(bottom, 0, 3);
            Controls.Add(root);

            // Tray
            var menu = new ContextMenuStrip();
            menu.Items.Add("Close all windows", null, (s, e) => DoCloseAll());
            menu.Items.Add("Open Close All", null, (s, e) => ShowMain());
            trayUpdate = new ToolStripMenuItem("Check for updates", null, (s, e) => { if (pendingUpdate != null) StartUpdate(pendingUpdate); else CheckForUpdates(true); });
            menu.Items.Add(trayUpdate);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitApp());
            tray = new NotifyIcon { Icon = Icon ?? SystemIcons.Application, Text = "Close All (Ctrl+Alt+Q)", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += (s, e) => ShowMain();

            ResumeLayout(false);
            PerformLayout();
        }

        void FitColumns()
        {
            if (list == null || list.Columns.Count < 2) return;
            int w = list.ClientSize.Width - list.Columns[0].Width - 4;
            if (w > 80) list.Columns[1].Width = w;
        }

        protected override void SetVisibleCore(bool value)
        {
            if (startHidden)
            {
                startHidden = false;
                if (!IsHandleCreated) CreateHandle();
                value = false;
            }
            base.SetVisibleCore(value);
        }

        bool checkedOnce;
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateHotkey();
            // Quietly check GitHub for a newer version once per start (also when starting in the tray)
            if (!checkedOnce)
            {
                checkedOnce = true;
                BeginInvoke((MethodInvoker)(() => CheckForUpdates(false)));
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            FitToContent();
        }

        // Size the window so every control fits at the current display scaling
        // (125%, 150%, ...), without the user having to drag it bigger.
        void FitToContent()
        {
            if (sized || root == null) return;
            sized = true;
            try
            {
                float scale;
                using (var g = CreateGraphics()) scale = Math.Max(1f, g.DpiX / 96f);

                root.PerformLayout();
                int width = 0, fixedHeight = 0;
                foreach (Control c in new Control[] { header, options, bottom })
                {
                    Size pref = c.GetPreferredSize(new Size(int.MaxValue, int.MaxValue));
                    width = Math.Max(width, pref.Width + c.Margin.Horizontal);
                    fixedHeight += pref.Height + c.Margin.Vertical;
                }
                int pad = root.Padding.Vertical;
                int minList = (int)(140 * scale), niceList = (int)(260 * scale);
                width = Math.Max(width + root.Padding.Horizontal, (int)(520 * scale));

                Rectangle area = Screen.FromControl(this).WorkingArea;
                Size min = SizeFromClientSize(new Size(width, fixedHeight + pad + minList));
                Size want = SizeFromClientSize(new Size(width, fixedHeight + pad + niceList));
                min = new Size(Math.Min(min.Width, area.Width), Math.Min(min.Height, area.Height));
                want = new Size(Math.Min(want.Width, area.Width), Math.Min(want.Height, area.Height));

                MinimumSize = min;
                Size = new Size(Math.Max(Width, want.Width), Math.Max(Height, want.Height));
                if (Height > area.Height) Height = area.Height;
                if (Width > area.Width) Width = area.Width;
                Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
            }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            FitColumns();
            RefreshList();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            // Windows can activate the form before it has finished setting up; only refresh once it's ready.
            if (uiReady && Visible && !busy) RefreshList();
        }

        void UpdateHotkey()
        {
            if (!IsHandleCreated) return;
            try
            {
                Native.UnregisterHotKey(Handle, HOTKEY_ID);
                hotkeyOk = false;
                if (settings.Hotkey)
                {
                    hotkeyOk = Native.RegisterHotKey(Handle, HOTKEY_ID, 0x4003 /* Alt+Ctrl+NoRepeat */, 0x51 /* Q */);
                    if (!hotkeyOk) SetStatus("Ctrl+Alt+Q is already used by another app.");
                }
            }
            catch { }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == HOTKEY_ID) { DoCloseAll(); return; }
            base.WndProc(ref m);
        }

        void SetStatus(string s)
        {
            if (lblStatus != null) lblStatus.Text = s ?? "";
        }

        void RefreshList()
        {
            if (!uiReady || list == null || icons == null || lblStatus == null || btnClose == null) return;

            List<WinInfo> wins;
            try { wins = WindowCloser.GetOpenWindows(); }
            catch (Exception ex) { SetStatus("Error: " + ex.Message); return; }

            populating = true;
            list.BeginUpdate();
            try
            {
                list.Items.Clear();
                foreach (var w in wins.OrderBy(x => x.AppName ?? "", StringComparer.CurrentCultureIgnoreCase))
                {
                    string key = string.IsNullOrEmpty(w.ExePath) ? "?" : w.ExePath;
                    if (!icons.Images.ContainsKey(key))
                    {
                        Icon ic = null;
                        try { if (!string.IsNullOrEmpty(w.ExePath)) ic = Icon.ExtractAssociatedIcon(w.ExePath); } catch { }
                        try { icons.Images.Add(key, (ic ?? SystemIcons.Application).ToBitmap()); } catch { }
                    }
                    var item = new ListViewItem(w.AppName ?? w.ProcName ?? "", key);
                    item.Tag = w;
                    item.Checked = !settings.Keep.Contains(w.ProcName ?? "");
                    item.SubItems.Add(w.Title ?? "");
                    if (!item.Checked) item.ForeColor = Color.Gray;
                    list.Items.Add(item);
                }
            }
            catch (Exception ex) { SetStatus("Error: " + ex.Message); }
            finally
            {
                list.EndUpdate();
                populating = false;
            }

            if (!busy && !updating)
            {
                string cur = lblStatus.Text ?? "";
                if (cur.Length == 0 || cur.EndsWith("closed.") || cur.StartsWith("No ") || cur.EndsWith("open."))
                    SetStatus(wins.Count == 0 ? "No other windows open." : CountText());
            }
            btnClose.Enabled = !busy;
        }

        string CountText()
        {
            int on = list.Items.Cast<ListViewItem>().Count(i => i.Checked);
            return on + " of " + list.Items.Count + " windows will be closed.";
        }

        void List_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (populating) return;
            var w = e.Item == null ? null : e.Item.Tag as WinInfo;
            if (w == null) return;

            if (e.Item.Checked) settings.Keep.Remove(w.ProcName);
            else settings.Keep.Add(w.ProcName);
            settings.Save();

            // Apply to every window from the same app
            populating = true;
            try
            {
                foreach (ListViewItem it in list.Items)
                {
                    var o = it.Tag as WinInfo;
                    if (o != null && o.ProcName == w.ProcName)
                    {
                        it.Checked = e.Item.Checked;
                        it.ForeColor = e.Item.Checked ? SystemColors.WindowText : Color.Gray;
                    }
                }
            }
            finally { populating = false; }
            SetStatus(CountText());
        }

        void DoCloseAll()
        {
            if (busy || updating) return;
            busy = true;
            if (btnClose != null) btnClose.Enabled = false;
            SetStatus("Closing...");
            var keep = new HashSet<string>(settings.Keep, StringComparer.OrdinalIgnoreCase);
            bool force = settings.Force;
            double wait = (double)settings.WaitSeconds;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                CloseResult r = null; string err = null;
                try { r = WindowCloser.CloseAll(keep, force, wait); }
                catch (Exception ex) { err = ex.Message; }
                try
                {
                    BeginInvoke((MethodInvoker)(() =>
                    {
                        busy = false;
                        string msg;
                        if (err != null) msg = "Error: " + err;
                        else if (r.Asked == 0) msg = "Nothing to close.";
                        else
                        {
                            int closed = r.Asked - r.StillOpen;
                            msg = "Closed " + closed + " window" + (closed == 1 ? "" : "s") + ".";
                            if (r.StillOpen > 0) msg += " " + r.StillOpen + " couldn't be closed (e.g. admin apps).";
                        }
                        SetStatus(msg);
                        RefreshList();
                        if (!Visible) tray.ShowBalloonTip(2000, "Close All", msg, ToolTipIcon.None);
                    }));
                }
                catch { busy = false; }
            });
        }

        // ---------------- Updates ----------------

        void CheckForUpdates(bool userAsked)
        {
            if (updating) return;
            if (userAsked) { btnUpdate.Enabled = false; SetStatus("Checking for updates..."); }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                UpdateInfo u = null; string err = null;
                try { u = Updater.GetLatest(); }
                catch (Exception ex) { err = ex.Message; }
                try
                {
                    BeginInvoke((MethodInvoker)(() =>
                    {
                        btnUpdate.Enabled = true;
                        if (Updater.IsNewer(u))
                        {
                            pendingUpdate = u;
                            string label = "Update to " + u.Version.ToString(3);
                            btnUpdate.Text = label;
                            btnUpdate.FlatStyle = FlatStyle.Flat;
                            btnUpdate.BackColor = Accent;
                            btnUpdate.ForeColor = Color.White;
                            btnUpdate.FlatAppearance.BorderSize = 0;
                            trayUpdate.Text = label;
                            if (userAsked) StartUpdate(u);
                            else SetStatus("Version " + u.Version.ToString(3) + " is available. Click \"" + label + "\".");
                        }
                        else if (userAsked)
                        {
                            if (err != null) SetStatus("Couldn't check for updates: " + err);
                            else SetStatus("You have the latest version (" + Updater.CurrentText + ").");
                            if (!Visible) tray.ShowBalloonTip(2000, "Close All", lblStatus.Text, ToolTipIcon.None);
                        }
                    }));
                }
                catch { }
            });
        }

        void StartUpdate(UpdateInfo u)
        {
            if (updating || u == null) return;
            if (busy) { SetStatus("Wait for closing to finish, then try again."); return; }
            updating = true;
            btnUpdate.Enabled = false;
            trayUpdate.Enabled = false;
            SetStatus("Downloading version " + u.Version.ToString(3) + "...");
            bool hidden = !Visible;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                string err = null;
                try { Updater.InstallAndRestart(u, hidden); }
                catch (Exception ex) { err = ex.Message; }
                try
                {
                    BeginInvoke((MethodInvoker)(() =>
                    {
                        if (err == null) { ExitApp(); return; }
                        updating = false;
                        btnUpdate.Enabled = true;
                        trayUpdate.Enabled = true;
                        SetStatus("Update failed: " + err);
                        if (!Visible) tray.ShowBalloonTip(3000, "Close All", "Update failed: " + err, ToolTipIcon.Warning);
                    }));
                }
                catch { }
            });
        }

        // ---------------- Window / tray ----------------

        public void ShowMain()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        void ExitApp()
        {
            exiting = true;
            tray.Visible = false;
            Close();
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing && settings.Hotkey)
            {
                e.Cancel = true;
                Hide();
                if (!shownTrayTip)
                {
                    shownTrayTip = true;
                    tray.ShowBalloonTip(3000, "Close All is still running", "Press Ctrl+Alt+Q to close everything. Right-click the tray icon to exit.", ToolTipIcon.Info);
                }
                return;
            }
            tray.Visible = false;
            try { if (IsHandleCreated) Native.UnregisterHotKey(Handle, HOTKEY_ID); } catch { }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && tray != null) tray.Dispose();
            base.Dispose(disposing);
        }
    }
}
