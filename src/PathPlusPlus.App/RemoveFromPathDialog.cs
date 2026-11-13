using System;
using System.Collections.Generic;
using System.Windows.Forms;
using PathPlusPlus.Core;

namespace PathPlusPlus.App
{
    /// <summary>"Remove from PATH" — opened from the Explorer menu.</summary>
    internal sealed class RemoveFromPathDialog : Form
    {
        private readonly OperationRunner _runner;
        private readonly string _folder;
        private readonly CheckBox _user;
        private readonly CheckBox _machine;
        private readonly Button _ok;

        private RemoveFromPathDialog(OperationRunner runner, string folder, int userCount, int machineCount)
        {
            _runner = runner;
            _folder = folder;

            SuspendLayout();
            Ui.InitDialog(this, "Remove from PATH");

            TableLayoutPanel root = Ui.Column();
            Ui.AddRow(root, Ui.Heading("Remove this folder from PATH?"));
            Ui.AddRow(root, Ui.ReadOnlyBox(folder));

            _user = new CheckBox
            {
                Text = Ui.ScopeChoice(EnvScope.User, detail: Entries(userCount)),
                AutoSize = true,
                Enabled = userCount > 0,
                Checked = userCount > 0,
            };
            _machine = new CheckBox
            {
                Text = Ui.ScopeChoice(EnvScope.Machine, detail: Entries(machineCount), adminNote: machineCount > 0),
                AutoSize = true,
                Enabled = machineCount > 0,
                Checked = machineCount > 0,
            };
            Ui.AddRow(root, Ui.Group("Remove it from", _user, _machine));
            Ui.AddRow(root, Ui.Hint(Ui.BackupHint));

            _ok = Ui.Button("Remove", OnRemove);
            Ui.AddRow(root, Ui.ButtonRow(_ok, Ui.CancelButton(this)));

            Controls.Add(root);
            AcceptButton = _ok;
            ResumeLayout(false);
            PerformLayout();

            _user.CheckedChanged += (s, e) => UpdateState();
            _machine.CheckedChanged += (s, e) => UpdateState();
            UpdateState();
        }

        /// <summary>Shows the dialog, or just a message when the folder isn't in PATH at all.</summary>
        public static void Run(OperationRunner runner, string folder)
        {
            int user = runner.Service.GetPath(EnvScope.User).CountOf(folder);
            int machine = runner.Service.GetPath(EnvScope.Machine).CountOf(folder);
            if (user == 0 && machine == 0)
            {
                Ui.Info(null, "This folder isn't in your user PATH or the system PATH:\n\n" + folder);
                return;
            }
            using (var dialog = new RemoveFromPathDialog(runner, folder, user, machine))
            {
                Application.Run(dialog);
            }
        }

        private static string Entries(int n) => n == 0 ? "not present" : n == 1 ? "1 entry" : n + " entries";

        private void UpdateState()
        {
            _ok.Enabled = _user.Checked || _machine.Checked;
            Ui.SetShield(_ok, _machine.Checked && _runner.NeedsElevation(EnvScope.Machine));
        }

        private void OnRemove(object sender, EventArgs e)
        {
            var messages = new List<string>();
            bool failed = false;

            if (_user.Checked)
            {
                EnvResponse r = _runner.Run(EnvRequest.RemoveFromPath(EnvScope.User, _folder));
                messages.Add(r.Message);
                failed |= !r.Success;
                if (r.Success) _user.Checked = _user.Enabled = false;
            }
            if (_machine.Checked)
            {
                EnvResponse r = _runner.Run(EnvRequest.RemoveFromPath(EnvScope.Machine, _folder));
                if (r.Error == EnvError.Cancelled && messages.Count == 0) return;
                messages.Add(r.Message);
                failed |= !r.Success;
                if (r.Success) _machine.Checked = _machine.Enabled = false;
            }

            string text = string.Join("\n", messages);
            if (failed)
            {
                Ui.Error(this, text);
                if (_user.Checked || _machine.Checked) return; // Let the user retry what's left.
            }
            else
            {
                Ui.Info(this, text + "\n\n" + Ui.NewTerminalHint);
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
