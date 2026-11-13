using System;
using System.IO;
using System.Windows.Forms;
using PathPlusPlus.Core;

namespace PathPlusPlus.App
{
    /// <summary>"Create environment variable" — opened from the Explorer menu.</summary>
    internal sealed class NewVariableDialog : Form
    {
        private readonly OperationRunner _runner;
        private readonly TextBox _name;
        private readonly TextBox _value;
        private readonly RadioButton _machine;
        private readonly Label _note;
        private readonly Button _ok;

        public NewVariableDialog(OperationRunner runner, string value)
        {
            _runner = runner;

            SuspendLayout();
            Ui.InitDialog(this, "New environment variable");

            TableLayoutPanel root = Ui.Column();
            Ui.AddRow(root, Ui.Heading("Create an environment variable for this path:"));

            Ui.AddRow(root, Ui.Label("&Name:"));
            _name = Ui.InputBox(SuggestName(value));
            Ui.AddRow(root, _name);
            Ui.AddRow(root, Ui.Label("&Value:"));
            _value = Ui.InputBox(value);
            Ui.AddRow(root, _value);

            var user = new RadioButton { Text = Ui.ScopeChoice(EnvScope.User, "environment"), AutoSize = true, Checked = true };
            _machine = new RadioButton { Text = Ui.ScopeChoice(EnvScope.Machine, "environment"), AutoSize = true };
            Ui.AddRow(root, Ui.Group("Create it for", user, _machine));

            _note = Ui.Note();
            Ui.AddRow(root, _note);
            Ui.AddRow(root, Ui.Hint(Ui.BackupHint));

            _ok = Ui.Button("Create", OnCreate);
            Ui.AddRow(root, Ui.ButtonRow(_ok, Ui.CancelButton(this)));

            Controls.Add(root);
            AcceptButton = _ok;
            ResumeLayout(false);
            PerformLayout();

            _name.TextChanged += (s, e) => UpdateState();
            _value.TextChanged += (s, e) => UpdateState();
            _machine.CheckedChanged += (s, e) => UpdateState();
            Shown += (s, e) =>
            {
                _name.Focus();
                _name.SelectAll();
            };
            UpdateState();
        }

        private EnvScope Scope => _machine.Checked ? EnvScope.Machine : EnvScope.User;

        /// <summary>"C:\Tools\jdk-21" becomes "JDK_21".</summary>
        internal static string SuggestName(string path)
        {
            string leaf = Path.GetFileName(PathNormalizer.TrimTrailingSeparators(path ?? string.Empty));
            if (string.IsNullOrEmpty(leaf)) return string.Empty;
            char[] chars = leaf.ToUpperInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!(char.IsLetterOrDigit(chars[i]) || chars[i] == '_')) chars[i] = '_';
            }
            string name = new string(chars).Trim('_');
            if (name.Length > 0 && char.IsDigit(name[0])) name = "_" + name;
            return name;
        }

        private string ValidateInput()
        {
            string name = _name.Text.Trim();
            if (EnvValidation.IsPathName(name)) return "To change PATH, use \"Add to PATH\" instead. This would replace all of PATH.";
            return EnvValidation.ValidateName(name) ?? EnvValidation.ValidateValue(_value.Text);
        }

        private void UpdateState()
        {
            string error = _name.Text.Length == 0 ? null : ValidateInput();
            Ui.SetNote(_note, error, Ui.Tone.Error);
            _ok.Enabled = _name.Text.Trim().Length > 0 && error == null;
            Ui.SetShield(_ok, _runner.NeedsElevation(Scope));
        }

        private void OnCreate(object sender, EventArgs e)
        {
            string error = ValidateInput();
            if (error != null)
            {
                Ui.Error(this, error);
                return;
            }

            string name = _name.Text.Trim();
            RawValue existing = _runner.Service.GetVariable(Scope, name);
            if (existing != null && !Ui.Confirm(this,
                    name + " already exists in the " + Scope.DisplayName() + " environment:\n\n" + existing.Value + "\n\nReplace it? (A backup is saved first.)"))
            {
                return;
            }

            EnvResponse response = _runner.Run(EnvRequest.SetVariable(Scope, name, _value.Text, force: false));
            if (!Ui.ShowResult(this, response, "Use it as %" + name + "% in programs you start from now on, such as a new terminal window.")) return;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
