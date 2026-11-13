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
    /// <summary>Lists automatic backups and restores them.</summary>
    internal sealed class BackupsForm : Form
    {
        private const string IntroText = "Path++ saves the previous value before every change. Restoring also saves a backup first, so it can be undone too.";

        private readonly OperationRunner _runner;
        private readonly Label _intro;
        private readonly ListView _list;
        private readonly Button _restore;
        private readonly Button _view;

        public BackupsForm(OperationRunner runner)
        {
            _runner = runner;

            SuspendLayout();
            Ui.InitForm(this, "Backups");
            Ui.MakeChild(this);
            ClientSize = new Size(720, 400);
            MinimumSize = new Size(560, 300);
            MinimizeBox = false;

            _intro = Ui.Label(IntroText, wrapWidth: 680);
            _intro.Dock = DockStyle.Top;

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
            _list.Columns.Add("Contents", 300);
            _list.SelectedIndexChanged += (s, e) => UpdateButtons();
            _list.DoubleClick += OnView;

            _restore = Ui.Button("&Restore…", OnRestore);
            _view = Ui.Button("&View…", OnView);
            Button openUser = Ui.Button("Open &user folder", (s, e) => OpenDir(EnvScope.User));
            Button openMachine = Ui.Button("Open &system folder", (s, e) => OpenDir(EnvScope.Machine));
            Button close = Ui.Button("Close");
            close.DialogResult = DialogResult.Cancel;
            FlowLayoutPanel buttons = Ui.ButtonRow(_restore, _view, openUser, openMachine, close);
            buttons.Dock = DockStyle.Bottom;
            buttons.Padding = new Padding(0, 12, 0, 0); // docked controls ignore Margin

            Controls.Add(_list);
            Controls.Add(_intro);
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
                string contents = b.Existed
                    ? (EnvValidation.IsPathName(b.Name) ? new PathList(b.Value).Entries.Count() + " entries" : b.Value.Length + " characters")
                    : "Did not exist";
                var item = new ListViewItem(b.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)) { Tag = b };
                item.SubItems.Add(b.Scope.DisplayName());
                item.SubItems.Add(b.Name);
                item.SubItems.Add(contents);
                _list.Items.Add(item);
            }
            _list.EndUpdate();
            _intro.Text = backups.Count == 0 ? "No backups yet. " + IntroText : IntroText;
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
            using (var dialog = new TextViewDialog(b.Scope.DisplayName() + " " + b.Name + " at " + b.TimestampUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture), text))
            {
                dialog.ShowDialog(this);
            }
        }

        private void OnRestore(object sender, EventArgs e)
        {
            BackupInfo b = Selected;
            if (b == null) return;
            if (!Ui.Confirm(this, "Restore the " + b.Scope.DisplayName() + " variable " + b.Name + " to how it was at " +
                                  b.TimestampUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) + "?\n\nIts current value is backed up first."))
            {
                return;
            }

            EnvResponse r = _runner.Run(EnvRequest.Restore(b.Scope, b.File));
            if (!Ui.ShowResult(this, r, EnvValidation.IsPathName(b.Name) ? Ui.NewTerminalHint : null)) return;
            RestoredAny |= r.Changed;
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
