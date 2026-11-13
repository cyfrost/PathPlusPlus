using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PathPlusPlus.Core;

namespace PathPlusPlus.App
{
    /// <summary>The PATH editor: user and system PATH side by side, edited in memory and saved together.</summary>
    internal sealed class MainForm : Form
    {
        private const int SideButtonWidth = 130;

        private readonly OperationRunner _runner;
        private readonly ScopeTab _user;
        private readonly ScopeTab _machine;
        private readonly TabControl _tabs;
        private readonly Label _banner;
        private readonly Label _status;
        private readonly ToolTip _tips = new ToolTip();
        private readonly Button _add;
        private readonly Button _edit;
        private readonly Button _remove;
        private readonly Button _up;
        private readonly Button _down;
        private readonly Button _cleanup;
        private readonly Button _open;
        private readonly Button _save;
        private string _notice;

        public MainForm(OperationRunner runner)
        {
            _runner = runner;
            ISessionInfo session = runner.Session;

            SuspendLayout();
            Ui.InitForm(this, Ui.EditorTitle);
            if (session.IsElevated) Text += " (Administrator)";
            ClientSize = new Size(940, 560);
            MinimumSize = new Size(720, 420);
            KeyPreview = true;

            _user = new ScopeTab(EnvScope.User, "Your PATH", readOnly: session.IsDifferentUserThanDesktop);
            _machine = new ScopeTab(EnvScope.Machine, "System PATH", readOnly: false);

            _banner = Ui.Label(string.Empty, wrapWidth: 900);
            _banner.Margin = new Padding(3, 0, 3, 8);

            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabs.TabPages.Add(CreatePage(_user));
            _tabs.TabPages.Add(CreatePage(_machine));
            _tabs.SelectedIndexChanged += (s, e) => UpdateButtons();

            _add = SideButton("&Add…", OnAdd, "Add a folder at the end (Insert)");
            _edit = SideButton("&Edit…", OnEdit, "Edit the selected entry (Enter or double-click)");
            _remove = SideButton("&Remove", OnRemove, "Remove the selected entries (Delete)");
            _up = SideButton("Move &up", (s, e) => MoveSelection(-1), "Move up: earlier entries win (Alt+Up)");
            _down = SideButton("Move &down", (s, e) => MoveSelection(+1), "Move down (Alt+Down)");
            _cleanup = SideButton("&Clean up", OnCleanup, "Remove duplicate and empty entries");
            _open = SideButton("&Open folder", OnOpenFolder, "Open the selected folder in Explorer");
            var side = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(8, 24, 0, 0),
            };
            side.Controls.AddRange(new Control[] { _add, _edit, _remove, Spacer(), _up, _down, Spacer(), _cleanup, _open });

            Button backups = Ui.Button("&Backups…", OnBackups);
            backups.Margin = new Padding(0);
            Button windows = Ui.Button("&Windows editor…", (s, e) => Ui.OpenWindowsEnvironmentDialog());
            _tips.SetToolTip(windows, "Open the built-in Windows Environment Variables dialog");
            Button reload = Ui.Button("Re&load", (s, e) => ReloadAll(askIfDirty: true));
            _tips.SetToolTip(reload, "Reload PATH from Windows, discarding unsaved edits (F5)");
            _save = Ui.Button("&Save", OnSave);
            _tips.SetToolTip(_save, "Save your changes (Ctrl+S)");
            _status = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 6, 3, 0) };

            var left = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0) };
            left.Controls.AddRange(new Control[] { backups, windows, _status });
            FlowLayoutPanel right = Ui.ButtonRow(_save, reload);
            right.Margin = new Padding(0);

            var bottom = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.Controls.Add(left, 0, 0);
            bottom.Controls.Add(right, 1, 0);

            var root = new TableLayoutPanel { ColumnCount = 2, RowCount = 3, Dock = DockStyle.Fill, Margin = new Padding(0) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(_banner, 0, 0);
            root.SetColumnSpan(_banner, 2);
            root.Controls.Add(_tabs, 0, 1);
            root.Controls.Add(side, 1, 1);
            root.Controls.Add(bottom, 0, 2);
            root.SetColumnSpan(bottom, 2);
            Controls.Add(root);

            ResumeLayout(false);
            PerformLayout();

            Load += (s, e) => ReloadAll(askIfDirty: false);
            Resize += (s, e) => FitColumns();
            FormClosing += OnClosing;
            KeyDown += OnKeyDown;
        }

        private ScopeTab Active => _tabs.SelectedIndex == 1 ? _machine : _user;

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tips.Dispose();
            base.Dispose(disposing);
        }

        // ------------------------------------------------------------------ layout helpers

        private TabPage CreatePage(ScopeTab tab)
        {
            var list = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = true,
                ShowItemToolTips = true,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
            };
            list.Columns.Add("#", 40, HorizontalAlignment.Right);
            list.Columns.Add("Folder", 560);
            list.Columns.Add("Notes", 200);
            list.SelectedIndexChanged += (s, e) => UpdateButtons();
            list.DoubleClick += OnEdit;

            tab.List = list;
            tab.Page = new TabPage(tab.Title) { Padding = new Padding(2), UseVisualStyleBackColor = true };
            tab.Page.Controls.Add(list);
            return tab.Page;
        }

        private Button SideButton(string text, EventHandler onClick, string tip)
        {
            Button b = Ui.Button(text, onClick, SideButtonWidth);
            b.AutoSize = false;
            b.Size = new Size(SideButtonWidth, Ui.ButtonHeight);
            b.Margin = new Padding(0, 0, 0, 6);
            _tips.SetToolTip(b, tip);
            return b;
        }

        private static Panel Spacer() => new Panel { Size = new Size(1, 8), Margin = new Padding(0) };

        private void FitColumns()
        {
            foreach (ScopeTab tab in new[] { _user, _machine })
            {
                if (tab.List == null) continue;
                int width = tab.List.ClientSize.Width - tab.List.Columns[0].Width - tab.List.Columns[2].Width - 4;
                if (width > 100) tab.List.Columns[1].Width = width;
            }
        }

        // ------------------------------------------------------------------ data

        private void ReloadAll(bool askIfDirty)
        {
            if (askIfDirty && (_user.Dirty || _machine.Dirty) &&
                !Ui.Confirm(this, "Discard your unsaved changes and reload PATH from Windows?"))
            {
                return;
            }
            Reload(_user);
            Reload(_machine);
            _notice = null;
            Render();
        }

        private void Reload(ScopeTab tab)
        {
            tab.Original = _runner.Service.GetRawPath(tab.Scope);
            tab.Segments = new PathList(tab.Original).Segments.ToList();
        }

        /// <summary>Call after any in-memory edit.</summary>
        private void Edited()
        {
            _notice = null;
            Render();
        }

        private void Render()
        {
            RenderTab(_user, _machine);
            RenderTab(_machine, _user);
            FitColumns();

            if (_user.ReadOnly)
            {
                _banner.Text = "Path++ is running as '" + _runner.Session.UserName + "', not as the person signed in to this desktop, " +
                               "so your user PATH is read-only here. Start Path++ normally (not 'Run as administrator') to edit it.";
                _banner.ForeColor = Ui.ToneColor(Ui.Tone.Error);
            }
            else
            {
                _banner.Text = _runner.Session.IsElevated
                    ? "Earlier entries win when two folders contain a program with the same name."
                    : "Earlier entries win when two folders contain a program with the same name. Saving the system PATH asks for administrator approval.";
                _banner.ForeColor = Ui.ToneColor(Ui.Tone.Info);
            }
            UpdateButtons();
        }

        private static void RenderTab(ScopeTab tab, ScopeTab other)
        {
            var selected = new HashSet<int>(tab.List.SelectedIndices.Cast<int>());
            IList<EntryInfo> infos = PathAnalyzer.Analyze(tab.Segments, other.Segments);

            tab.List.BeginUpdate();
            tab.List.Items.Clear();
            foreach (EntryInfo info in infos)
            {
                bool empty = info.Status == EntryStatus.Empty;
                var item = new ListViewItem((info.Index + 1).ToString(CultureInfo.CurrentCulture)) { UseItemStyleForSubItems = false, Tag = info.Index };
                item.SubItems.Add(empty ? "(empty entry)" : info.Text);
                ListViewItem.ListViewSubItem notes = item.SubItems.Add(info.Describe(tab.Scope));
                notes.ForeColor = Ui.ToneColor(ToneOf(info.Status));
                if (empty) item.SubItems[1].ForeColor = Ui.ToneColor(Ui.Tone.Info);

                if (!empty && info.Expanded != null && !string.Equals(info.Expanded, PathNormalizer.Clean(info.Text), StringComparison.Ordinal))
                {
                    item.ToolTipText = info.Expanded;
                }
                tab.List.Items.Add(item);
            }
            foreach (int i in selected.Where(i => i < tab.List.Items.Count)) tab.List.Items[i].Selected = true;
            tab.List.EndUpdate();

            int count = tab.Segments.Count(s => PathNormalizer.Clean(s).Length > 0);
            tab.Page.Text = tab.Title + " (" + count + ")" + (tab.Dirty ? " *" : string.Empty);
        }

        private static Ui.Tone ToneOf(EntryStatus status)
        {
            if ((status & (EntryStatus.Missing | EntryStatus.NotAbsolute | EntryStatus.UnresolvedVariable)) != 0) return Ui.Tone.Error;
            if ((status & EntryStatus.Duplicate) != 0) return Ui.Tone.Warning;
            return Ui.Tone.Info;
        }

        private void UpdateButtons()
        {
            ScopeTab tab = Active;
            int selected = tab.List.SelectedIndices.Count;
            bool editable = !tab.ReadOnly;

            _add.Enabled = editable;
            _edit.Enabled = editable && selected == 1;
            _remove.Enabled = editable && selected > 0;
            _up.Enabled = editable && selected > 0 && tab.List.SelectedIndices.Cast<int>().Min() > 0;
            _down.Enabled = editable && selected > 0 && tab.List.SelectedIndices.Cast<int>().Max() < tab.Segments.Count - 1;
            _cleanup.Enabled = editable && tab.Segments.Count > 0;
            _open.Enabled = selected == 1;

            bool dirty = _user.Dirty || _machine.Dirty;
            _save.Enabled = dirty;
            Ui.SetShield(_save, _machine.Dirty && _runner.NeedsElevation(EnvScope.Machine));

            var pending = new List<string>();
            if (_user.Dirty) pending.Add("your PATH");
            if (_machine.Dirty) pending.Add("system PATH");
            if (pending.Count > 0)
            {
                _status.Text = "Unsaved changes: " + string.Join(", ", pending);
                _status.ForeColor = Ui.ToneColor(Ui.Tone.Warning);
            }
            else
            {
                _status.Text = _notice ?? string.Empty;
                _status.ForeColor = Ui.ToneColor(Ui.Tone.Info);
            }
        }

        private static void SelectEntry(ScopeTab tab, int index)
        {
            tab.List.SelectedIndices.Clear();
            if (index >= 0 && index < tab.List.Items.Count)
            {
                tab.List.Items[index].Selected = true;
                tab.List.Items[index].Focused = true;
                tab.List.EnsureVisible(index);
            }
            tab.List.Focus();
        }

        // ------------------------------------------------------------------ commands

        private void OnAdd(object sender, EventArgs e)
        {
            ScopeTab tab = Active;
            if (tab.ReadOnly) return;
            string target = tab.Scope == EnvScope.User ? "your user PATH" : "the system PATH";
            using (var dialog = new EntryDialog("Add folder", "Add a folder to the end of " + target + ":", string.Empty))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var list = new PathList(tab.Segments);
                int existing = list.IndexOf(dialog.Value);
                if (existing >= 0)
                {
                    Ui.Info(this, "That folder is already in " + target + " (entry " + (existing + 1) + ").");
                    SelectEntry(tab, existing);
                    return;
                }

                // Same behaviour as the Explorer menu and 'pathpp add': append, without leaving empty entries behind.
                list.Add(dialog.Value, prepend: false);
                tab.Segments = list.Segments.ToList();
                Edited();
                SelectEntry(tab, tab.Segments.Count - 1);
            }
        }

        private void OnEdit(object sender, EventArgs e)
        {
            ScopeTab tab = Active;
            if (tab.ReadOnly || tab.List.SelectedIndices.Count != 1) return;
            int index = tab.List.SelectedIndices[0];
            using (var dialog = new EntryDialog("Edit entry", "Edit this PATH entry:", PathNormalizer.Clean(tab.Segments[index])))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                tab.Segments[index] = dialog.Value;
                Edited();
                SelectEntry(tab, index);
            }
        }

        private void OnRemove(object sender, EventArgs e)
        {
            ScopeTab tab = Active;
            if (tab.ReadOnly || tab.List.SelectedIndices.Count == 0) return;
            List<int> indices = tab.List.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList();
            foreach (int i in indices) tab.Segments.RemoveAt(i);
            tab.List.SelectedIndices.Clear();
            Edited();
            SelectEntry(tab, Math.Min(indices.Last(), tab.Segments.Count - 1));
        }

        private void MoveSelection(int direction)
        {
            ScopeTab tab = Active;
            if (tab.ReadOnly || tab.List.SelectedIndices.Count == 0) return;
            List<int> indices = tab.List.SelectedIndices.Cast<int>().OrderBy(i => i).ToList();
            if (direction < 0 && indices.First() == 0) return;
            if (direction > 0 && indices.Last() == tab.Segments.Count - 1) return;

            IEnumerable<int> order = direction < 0 ? indices : Enumerable.Reverse(indices);
            foreach (int i in order)
            {
                string tmp = tab.Segments[i + direction];
                tab.Segments[i + direction] = tab.Segments[i];
                tab.Segments[i] = tmp;
            }
            tab.List.SelectedIndices.Clear();
            Edited();
            foreach (int i in indices) tab.List.Items[i + direction].Selected = true;
            tab.List.EnsureVisible(indices[0] + direction);
            tab.List.Focus();
        }

        private void OnCleanup(object sender, EventArgs e)
        {
            ScopeTab tab = Active;
            if (tab.ReadOnly) return;
            var list = new PathList(tab.Segments);
            int removed = list.RemoveDuplicatesAndEmpty();
            if (removed == 0)
            {
                Ui.Info(this, "No duplicate or empty entries found.\n\nFolders marked \"Folder not found\" are left alone, because they may be on a drive that isn't connected right now. Select and remove them yourself if you no longer need them.");
                return;
            }
            tab.Segments = list.Segments.ToList();
            tab.List.SelectedIndices.Clear();
            Edited();
            Ui.Info(this, "Removed " + removed + " duplicate or empty entr" + (removed == 1 ? "y" : "ies") + ". Click Save to keep the change.");
        }

        private void OnOpenFolder(object sender, EventArgs e)
        {
            ScopeTab tab = Active;
            if (tab.List.SelectedIndices.Count != 1) return;
            string folder = PathNormalizer.Expand(tab.Segments[tab.List.SelectedIndices[0]]);
            if (folder.Length == 0 || !Directory.Exists(folder))
            {
                Ui.Warn(this, "This folder doesn't exist:\n\n" + folder);
                return;
            }
            Ui.OpenFolder(folder);
        }

        private bool Save()
        {
            foreach (ScopeTab tab in new[] { _user, _machine })
            {
                if (!tab.Dirty) continue;

                if (tab.Current.Length > EnvValidation.LegacyPathWarningLength &&
                    tab.Original.Length <= EnvValidation.LegacyPathWarningLength &&
                    !Ui.Confirm(this, "The " + tab.Scope.DisplayName() + " PATH will be " + tab.Current.Length + " characters long. " +
                                      "Some older programs only read the first " + EnvValidation.LegacyPathWarningLength + ".\n\nSave anyway?"))
                {
                    return false;
                }

                EnvResponse r = _runner.Run(EnvRequest.SetPath(tab.Scope, tab.Current, tab.Original));
                if (r.Error == EnvError.Cancelled) return false;
                if (r.Error == EnvError.Conflict)
                {
                    if (Ui.Confirm(this, r.Message + "\n\nReload the " + tab.Scope.DisplayName() + " PATH now? Your unsaved edits to it will be lost."))
                    {
                        Reload(tab);
                        Render();
                    }
                    return false;
                }
                if (!r.Success)
                {
                    Ui.Error(this, r.Message);
                    return false;
                }
                Reload(tab);
            }

            _notice = "Saved. " + Ui.NewTerminalHint;
            Render();
            return true;
        }

        private void OnSave(object sender, EventArgs e) => Save();

        private void OnBackups(object sender, EventArgs e)
        {
            bool restored;
            using (var form = new BackupsForm(_runner))
            {
                form.ShowDialog(this);
                restored = form.RestoredAny;
            }
            if (!restored) return;
            if (_user.Dirty || _machine.Dirty)
            {
                Ui.Info(this, "A backup was restored. Click Reload to see it (this discards your unsaved edits).");
                return;
            }
            ReloadAll(askIfDirty: false);
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (!_user.Dirty && !_machine.Dirty) return;
            DialogResult answer = MessageBox.Show(this, "Save your changes to PATH before closing?", Ui.AppName,
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel || (answer == DialogResult.Yes && !Save()))
            {
                e.Cancel = true;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.S)
            {
                Save();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                ReloadAll(askIfDirty: true);
                e.Handled = true;
            }
            else if (ActiveControl is ListView)
            {
                if (e.KeyCode == Keys.Delete) OnRemove(sender, e);
                else if (e.KeyCode == Keys.Insert) OnAdd(sender, e);
                else if (e.KeyCode == Keys.Enter) OnEdit(sender, e);
                else if (e.Alt && e.KeyCode == Keys.Up) MoveSelection(-1);
                else if (e.Alt && e.KeyCode == Keys.Down) MoveSelection(+1);
                else return;
                e.Handled = true;
            }
        }
    }
}
