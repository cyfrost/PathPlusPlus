using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PathPlusPlus.Core;

namespace PathPlusPlus.App
{
    /// <summary>The PATH editor: user and system PATH side by side, edited in memory and saved together.</summary>
    internal sealed class MainForm : Form
    {
        private sealed class ScopeTab
        {
            public EnvScope Scope;
            public TabPage Page;
            public ListView List;
            public string Original = string.Empty;
            public List<string> Segments = new List<string>();
            public bool ReadOnly;

            public string Current => string.Join(";", Segments);
            public bool Dirty => !string.Equals(Current, Original, StringComparison.Ordinal);
        }

        private readonly OperationRunner _runner;
        private readonly ScopeTab _user;
        private readonly ScopeTab _machine;
        private readonly TabControl _tabs;
        private readonly Label _banner;
        private readonly Label _status;
        private readonly Button _add, _edit, _remove, _up, _down, _cleanup, _open, _save, _revert;

        public MainForm(OperationRunner runner)
        {
            _runner = runner;
            ISessionInfo session = runner.Session;

            SuspendLayout();
            Ui.InitForm(this, "Path++ - PATH editor" + (session.IsElevated ? " (Administrator)" : string.Empty));
            ClientSize = new Size(940, 560);
            MinimumSize = new Size(700, 420);
            Padding = new Padding(10);
            KeyPreview = true;

            _user = new ScopeTab { Scope = EnvScope.User, ReadOnly = session.IsDifferentUserThanDesktop };
            _machine = new ScopeTab { Scope = EnvScope.Machine };

            _banner = Ui.Label(string.Empty, wrapWidth: 900);
            _banner.ForeColor = Ui.WarningColor;

            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabs.TabPages.Add(CreatePage(_user, "Your PATH (" + session.UserName + ")"));
            _tabs.TabPages.Add(CreatePage(_machine, "System PATH (all users)"));
            _tabs.SelectedIndexChanged += (s, e) => UpdateButtons();

            _add = SideButton("&Add…", OnAdd);
            _edit = SideButton("&Edit…", OnEdit);
            _remove = SideButton("&Remove", OnRemove);
            _up = SideButton("Move &up", (s, e) => MoveSelection(-1));
            _down = SideButton("Move &down", (s, e) => MoveSelection(+1));
            _cleanup = SideButton("&Clean up", OnCleanup);
            _open = SideButton("&Open folder", OnOpenFolder);
            var side = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(8, 22, 0, 0),
            };
            side.Controls.AddRange(new Control[] { _add, _edit, _remove, Spacer(), _up, _down, Spacer(), _cleanup, _open });

            var backups = Ui.Button("&Backups…", OnBackups);
            var windows = Ui.Button("&Windows editor…", (s, e) => Ui.OpenWindowsEnvironmentDialog());
            var refresh = Ui.Button("Re&load", (s, e) => ReloadAll(askIfDirty: true));
            _revert = Ui.Button("Re&vert", (s, e) => ReloadAll(askIfDirty: true));
            _save = Ui.Button("&Save", OnSave);
            _status = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(8, 3, 3, 3) };

            var left = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0) };
            backups.Margin = new Padding(0, 3, 0, 3);
            left.Controls.AddRange(new Control[] { backups, windows, refresh, _status });
            var right = Ui.ButtonRow(_save, _revert);
            right.Margin = new Padding(0);

            var bottom = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.Controls.Add(left, 0, 0);
            bottom.Controls.Add(right, 1, 0);

            var root = new TableLayoutPanel { ColumnCount = 2, RowCount = 3, Dock = DockStyle.Fill };
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

        // ------------------------------------------------------------------ layout helpers

        private TabPage CreatePage(ScopeTab tab, string title)
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
            list.Columns.Add("Folder", 520);
            list.Columns.Add("Status", 220);
            list.SelectedIndexChanged += (s, e) => UpdateButtons();
            list.DoubleClick += (s, e) => OnEdit(s, e);

            tab.List = list;
            tab.Page = new TabPage(title) { Padding = new Padding(2), UseVisualStyleBackColor = true };
            tab.Page.Controls.Add(list);
            return tab.Page;
        }

        private static Button SideButton(string text, EventHandler onClick)
        {
            var b = Ui.Button(text, onClick, 130);
            b.AutoSize = false;
            b.Size = new Size(130, 28);
            b.Margin = new Padding(0, 0, 0, 6);
            return b;
        }

        private static Control Spacer() => new Panel { Size = new Size(1, 8), Margin = new Padding(0) };

        private void FitColumns()
        {
            foreach (ScopeTab tab in new[] { _user, _machine })
            {
                if (tab.List == null || tab.List.Columns.Count < 3) continue;
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
            Render();
        }

        private void Reload(ScopeTab tab)
        {
            tab.Original = _runner.Service.GetRawPath(tab.Scope);
            tab.Segments = new PathList(tab.Original).Segments.ToList();
        }

        private void Render()
        {
            RenderTab(_user, _machine);
            RenderTab(_machine, _user);
            FitColumns();

            var notes = new List<string>();
            if (_user.ReadOnly)
            {
                notes.Add("Path++ is running as '" + _runner.Session.UserName + "', not as the person signed in to this desktop, " +
                          "so your user PATH is shown read-only. Start Path++ normally (not 'Run as administrator') to edit it.");
            }
            if (!_runner.Session.IsElevated)
            {
                notes.Add("Saving changes to the system PATH asks for administrator approval.");
            }
            _banner.Text = string.Join(" ", notes);
            _banner.ForeColor = _user.ReadOnly ? Ui.ProblemColor : SystemColors.GrayText;
            UpdateButtons();
        }

        private void RenderTab(ScopeTab tab, ScopeTab other)
        {
            var selected = new HashSet<int>(tab.List.SelectedIndices.Cast<int>());
            IList<EntryInfo> infos = PathAnalyzer.Analyze(tab.Segments, other.Segments);

            tab.List.BeginUpdate();
            tab.List.Items.Clear();
            foreach (EntryInfo info in infos)
            {
                bool empty = info.Status == EntryStatus.Empty;
                var item = new ListViewItem((info.Index + 1).ToString()) { UseItemStyleForSubItems = false, Tag = info.Index };
                item.SubItems.Add(empty ? "(empty entry)" : info.Text);
                var status = item.SubItems.Add(info.Describe(tab.Scope));

                if (empty)
                {
                    item.SubItems[1].ForeColor = SystemColors.GrayText;
                    status.ForeColor = SystemColors.GrayText;
                }
                else if ((info.Status & (EntryStatus.Missing | EntryStatus.NotAbsolute | EntryStatus.UnresolvedVariable)) != 0)
                {
                    status.ForeColor = Ui.ProblemColor;
                }
                else if ((info.Status & EntryStatus.Duplicate) != 0)
                {
                    status.ForeColor = Ui.WarningColor;
                }
                else
                {
                    status.ForeColor = SystemColors.GrayText;
                }

                if (!empty && info.Expanded != null && !string.Equals(info.Expanded, PathNormalizer.Clean(info.Text), StringComparison.Ordinal))
                {
                    item.ToolTipText = info.Expanded;
                }
                tab.List.Items.Add(item);
            }
            foreach (int i in selected.Where(i => i < tab.List.Items.Count)) tab.List.Items[i].Selected = true;
            tab.List.EndUpdate();

            int count = tab.Segments.Count(s => PathNormalizer.Clean(s).Length > 0);
            string baseTitle = tab.Scope == EnvScope.User ? "Your PATH (" + _runner.Session.UserName + ")" : "System PATH (all users)";
            tab.Page.Text = baseTitle + " - " + count + (tab.Dirty ? " *" : string.Empty);
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
            _revert.Enabled = dirty;
            Ui.SetShield(_save, _machine.Dirty && _runner.NeedsElevation(EnvScope.Machine));

            var pending = new List<string>();
            if (_user.Dirty) pending.Add("your PATH");
            if (_machine.Dirty) pending.Add("system PATH");
            _status.Text = pending.Count == 0 ? string.Empty : "Unsaved changes: " + string.Join(", ", pending);
        }

        private void SelectEntry(ScopeTab tab, int index)
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
            using (var dialog = new EntryDialog("Add folder to " + (tab.Scope == EnvScope.User ? "your" : "the system") + " PATH", string.Empty))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var list = new PathList(tab.Segments);
                int existing = list.IndexOf(dialog.Value);
                if (existing >= 0)
                {
                    Ui.Info(this, "That folder is already in this PATH (entry " + (existing + 1) + ").");
                    SelectEntry(tab, existing);
                    return;
                }

                // Insert after the selection, or at the end.
                int at = tab.List.SelectedIndices.Count > 0 ? tab.List.SelectedIndices.Cast<int>().Max() + 1 : tab.Segments.Count;
                while (at == tab.Segments.Count && at > 0 && PathNormalizer.Clean(tab.Segments[at - 1]).Length == 0) at--;
                tab.Segments.Insert(at, dialog.Value);
                Render();
                SelectEntry(tab, at);
            }
        }

        private void OnEdit(object sender, EventArgs e)
        {
            ScopeTab tab = Active;
            if (tab.ReadOnly || tab.List.SelectedIndices.Count != 1) return;
            int index = tab.List.SelectedIndices[0];
            using (var dialog = new EntryDialog("Edit PATH entry", PathNormalizer.Clean(tab.Segments[index])))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                tab.Segments[index] = dialog.Value;
                Render();
                SelectEntry(tab, index);
            }
        }

        private void OnRemove(object sender, EventArgs e)
        {
            ScopeTab tab = Active;
            if (tab.ReadOnly || tab.List.SelectedIndices.Count == 0) return;
            List<int> indices = tab.List.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList();
            foreach (int i in indices) tab.Segments.RemoveAt(i);
            Render();
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
            Render();
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
            Render();
            Ui.Info(this, "Removed " + removed + " duplicate or empty entr" + (removed == 1 ? "y" : "ies") + ". Click Save to keep the change.");
        }

        private void OnOpenFolder(object sender, EventArgs e)
        {
            ScopeTab tab = Active;
            if (tab.List.SelectedIndices.Count != 1) return;
            string folder = PathNormalizer.Expand(tab.Segments[tab.List.SelectedIndices[0]]);
            if (!Directory.Exists(folder))
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
            Render();
            _status.Text = "Saved. Open a new terminal window to use the updated PATH.";
            return true;
        }

        private void OnSave(object sender, EventArgs e) => Save();

        private void OnBackups(object sender, EventArgs e)
        {
            using (var form = new BackupsForm(_runner))
            {
                form.ShowDialog(this);
                if (!form.RestoredAny) return;
            }
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
            if (e.Control && e.KeyCode == Keys.S) { Save(); e.Handled = true; }
            else if (e.KeyCode == Keys.F5) { ReloadAll(askIfDirty: true); e.Handled = true; }
            else if (ActiveControl is TabControl || ActiveControl is ListView)
            {
                if (e.KeyCode == Keys.Delete) { OnRemove(sender, e); e.Handled = true; }
                else if (e.KeyCode == Keys.Insert) { OnAdd(sender, e); e.Handled = true; }
                else if (e.KeyCode == Keys.Enter) { OnEdit(sender, e); e.Handled = true; }
                else if (e.Alt && e.KeyCode == Keys.Up) { MoveSelection(-1); e.Handled = true; }
                else if (e.Alt && e.KeyCode == Keys.Down) { MoveSelection(+1); e.Handled = true; }
            }
        }
    }
}
