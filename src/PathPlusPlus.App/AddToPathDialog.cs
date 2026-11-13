using System;
using System.IO;
using System.Windows.Forms;
using PathPlusPlus.Core;

namespace PathPlusPlus.App
{
    /// <summary>"Add to PATH" — opened from the Explorer menu.</summary>
    internal sealed class AddToPathDialog : Form
    {
        private readonly OperationRunner _runner;
        private readonly string _folder;
        private readonly bool _inUser;
        private readonly bool _inMachine;
        private readonly RadioButton _machine;
        private readonly CheckBox _prepend;
        private readonly Label _note;
        private readonly Button _ok;

        public AddToPathDialog(OperationRunner runner, string folder)
        {
            _runner = runner;
            _folder = PathNormalizer.TrimTrailingSeparators(folder);
            _inUser = runner.Service.GetPath(EnvScope.User).Contains(_folder);
            _inMachine = runner.Service.GetPath(EnvScope.Machine).Contains(_folder);

            SuspendLayout();
            Ui.InitDialog(this, "Add to PATH");

            TableLayoutPanel root = Ui.Column();
            Ui.AddRow(root, Ui.Heading("Add this folder to PATH so its programs can be run from any terminal:"));
            Ui.AddRow(root, Ui.ReadOnlyBox(_folder));

            var user = new RadioButton { Text = Ui.ScopeChoice(EnvScope.User), AutoSize = true, Checked = !_inUser || _inMachine };
            _machine = new RadioButton { Text = Ui.ScopeChoice(EnvScope.Machine), AutoSize = true, Checked = _inUser && !_inMachine };
            Ui.AddRow(root, Ui.Group("Add it for", user, _machine));

            _prepend = new CheckBox { Text = "Put it first (its programs win over same-named ones elsewhere)", AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
            Ui.AddRow(root, _prepend);

            _note = Ui.Note();
            Ui.AddRow(root, _note);
            Ui.AddRow(root, Ui.Hint(Ui.BackupHint));

            _ok = Ui.Button("Add", OnAdd);
            Ui.AddRow(root, Ui.ButtonRow(_ok, Ui.CancelButton(this)));

            Controls.Add(root);
            AcceptButton = _ok;
            ResumeLayout(false);
            PerformLayout();

            _machine.CheckedChanged += (s, e) => UpdateState();
            UpdateState();
        }

        private EnvScope Scope => _machine.Checked ? EnvScope.Machine : EnvScope.User;

        private void UpdateState()
        {
            bool already = Scope == EnvScope.Machine ? _inMachine : _inUser;
            _ok.Enabled = !already;
            Ui.SetShield(_ok, _runner.NeedsElevation(Scope));

            if (already)
            {
                Ui.SetNote(_note, "This folder is already in " + (Scope == EnvScope.Machine ? "the system PATH." : "your user PATH."), Ui.Tone.Info);
            }
            else if (!Directory.Exists(PathNormalizer.Expand(_folder)))
            {
                Ui.SetNote(_note, "This folder doesn't exist yet.", Ui.Tone.Warning);
            }
            else if (_inUser || _inMachine)
            {
                Ui.SetNote(_note, "It's already in " + (_inUser ? "your user PATH." : "the system PATH."), Ui.Tone.Info);
            }
            else
            {
                Ui.SetNote(_note, null, Ui.Tone.Info);
            }
        }

        private void OnAdd(object sender, EventArgs e)
        {
            if (!Ui.ShowResult(this, _runner.Run(EnvRequest.AddToPath(Scope, _folder, _prepend.Checked)))) return;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
