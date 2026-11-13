using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PathPlusPlus.Core;

namespace PathPlusPlus.App
{
    /// <summary>Lists automatic backups and restores them.</summary>
    internal sealed class BackupsForm : Form
    {
        private readonly OperationRunner _runner;
        private readonly ListView _list;
        private readonly Button _restore;
        private readonly Button _view;

        public BackupsForm(OperationRunner runner)
        {
            _runner = runner;

            SuspendLayout();
            Ui.InitForm(this, "Backups - " + Ui.AppName);
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(720, 400);
            MinimumSize = new Size(520, 300);
            MinimizeBox = false;
            Padding = new Padding(10);

            var intro = Ui.Label("Path++ saves the previous value before every change. Restoring also saves a backup first, so it can be undone too.", wrapWidth: 680);
            intro.Dock = DockStyle.Top;

            _list = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                Dock = DockStyle.Fill,
            };
            _list.Columns.Add("When", 150);
            _list.Columns.Add("Scope", 80);
            _list.Columns.Add("Variable", 140);
            _list.Columns.Add("Size / note", 300);
            _list.SelectedIndexChanged += (s, e) => UpdateButtons();
            _list.DoubleClick += (s, e) => OnView(s, e);

            _restore = Ui.Button("Restore…", OnRestore);
            _view = Ui.Button("View…", OnView);
            var openUser = Ui.Button("Open user folder", (s, e) => OpenDir(EnvScope.User), 120);
            var openMachine = Ui.Button("Open system folder", (s, e) => OpenDir(EnvScope.Machine), 120);
            var close = Ui.Button("Close");
            close.DialogResult = DialogResult.Cancel;
            var buttons = Ui.ButtonRow(_restore, _view, openUser, openMachine, close);
            buttons.Dock = DockStyle.Bottom;

            Controls.Add(_list);
            Controls.Add(intro);
            Controls.Add(buttons);
            CancelButton = close;
            ResumeLayout(false);
            PerformLayout();

            Load += (s, e) => Reload();
        }

        /// <summary>True when something was restored, so the editor should reload.</summary>
        public bool RestoredAny { get; private set; }

        private BackupInfo Selected => _list.SelectedItems.Count == 1 ? (BackupInfo)_list.SelectedItems[0].Tag : null;

        private void Reload()
        {
            IList<BackupInfo> backups = _runner.Service.Backups.List();
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (BackupInfo b in backups)
            {
                string note = b.Existed
                    ? (EnvValidation.IsPathName(b.Name) ? new PathList(b.Value).Entries.Count() + " entries" : b.Value.Length + " characters")
                    : "did not exist";
                var item = new ListViewItem(b.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")) { Tag = b };
                item.SubItems.Add(b.Scope.DisplayName());
                item.SubItems.Add(b.Name);
                item.SubItems.Add(note);
                _list.Items.Add(item);
            }
            _list.EndUpdate();
            if (_list.Items.Count > 0) _list.Items[0].Selected = true;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            BackupInfo b = Selected;
            _restore.Enabled = b != null;
            _view.Enabled = b != null;
            Ui.SetShield(_restore, b != null && _runner.NeedsElevation(b.Scope));
        }

        private void OnView(object sender, EventArgs e)
        {
            BackupInfo b = Selected;
            if (b == null) return;
            string text = !b.Existed
                ? "(" + b.Name + " did not exist at this point.)"
                : EnvValidation.IsPathName(b.Name)
                    ? string.Join(Environment.NewLine, new PathList(b.Value).Segments)
                    : b.Value;
            using (var dialog = new TextViewDialog(b.Scope.DisplayName() + " " + b.Name + " at " + b.TimestampUtc.ToLocalTime().ToString("g"), text))
            {
                dialog.ShowDialog(this);
            }
        }

        private void OnRestore(object sender, EventArgs e)
        {
            BackupInfo b = Selected;
            if (b == null) return;
            if (!Ui.Confirm(this, "Restore the " + b.Scope.DisplayName() + " variable " + b.Name + " to how it was at " +
                                  b.TimestampUtc.ToLocalTime().ToString("g") + "?\n\nIts current value is backed up first."))
            {
                return;
            }

            EnvResponse r = _runner.Run(EnvRequest.Restore(b.Scope, b.File));
            if (r.Error == EnvError.Cancelled) return;
            if (!r.Success)
            {
                Ui.Error(this, r.Message);
                return;
            }
            RestoredAny |= r.Changed;
            Ui.Info(this, r.Message);
            Reload();
        }

        private void OpenDir(EnvScope scope)
        {
            string dir = _runner.Service.Backups.GetDirectory(scope);
            if (!Directory.Exists(dir))
            {
                Ui.Info(this, "There are no " + scope.DisplayName() + " backups yet.\n\n" + dir);
                return;
            }
            Ui.OpenFolder(dir);
        }
    }
}
