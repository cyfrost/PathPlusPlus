using System;
using System.Collections.Generic;
using System.Drawing;
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
        private readonly RadioButton _user;
        private readonly RadioButton _machine;
        private readonly CheckBox _prepend;
        private readonly Label _status;
        private readonly Button _ok;

        public AddToPathDialog(OperationRunner runner, string folder)
        {
            _runner = runner;
            _folder = PathNormalizer.TrimTrailingSeparators(folder);

            SuspendLayout();
            Ui.InitDialog(this, "Add to PATH - " + Ui.AppName);

            var root = Ui.Column();
            Ui.AddRow(root, Ui.Label("Add this folder to PATH so its programs can be run from any terminal:", bold: true));
            Ui.AddRow(root, Ui.ReadOnlyBox(_folder));

            var group = new GroupBox { Text = "Add it for", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, Padding = new Padding(8) };
            var options = Ui.Column();
            _user = new RadioButton { Text = "Just me (" + runner.Session.UserName + ")", AutoSize = true, Checked = true };
            _machine = new RadioButton { Text = "All users (system PATH, needs administrator approval)", AutoSize = true };
            Ui.AddRow(options, _user);
            Ui.AddRow(options, _machine);
            group.Controls.Add(options);
            Ui.AddRow(root, group);

            _prepend = new CheckBox { Text = "Put it first (its programs win over same-named ones elsewhere)", AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
            Ui.AddRow(root, _prepend);

            _status = Ui.Label(string.Empty);
            _status.ForeColor = Ui.WarningColor;
            Ui.AddRow(root, _status);

            _ok = Ui.Button("Add", OnAdd);
            var cancel = Ui.Button("Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            Ui.AddRow(root, Ui.ButtonRow(_ok, cancel));

            Controls.Add(root);
            AcceptButton = _ok;
            CancelButton = cancel;
            ResumeLayout(false);
            PerformLayout();

            _user.CheckedChanged += (s, e) => UpdateState();
            UpdateState();
        }

        private EnvScope Scope => _machine.Checked ? EnvScope.Machine : EnvScope.User;

        private void UpdateState()
        {
            var notes = new List<string>();
            if (!Directory.Exists(PathNormalizer.Expand(_folder))) notes.Add("This folder doesn't exist yet.");
            if (_runner.Service.GetPath(EnvScope.User).Contains(_folder)) notes.Add("It's already in your user PATH.");
            if (_runner.Service.GetPath(EnvScope.Machine).Contains(_folder)) notes.Add("It's already in the system PATH.");
            _status.Text = string.Join(" ", notes);
            _status.Visible = notes.Count > 0;
            Ui.SetShield(_ok, _runner.NeedsElevation(Scope));
        }

        private void OnAdd(object sender, EventArgs e)
        {
            EnvResponse response = _runner.Run(EnvRequest.AddToPath(Scope, _folder, _prepend.Checked));
            if (response.Error == EnvError.Cancelled) return;
            if (!response.Success)
            {
                Ui.Error(this, response.Message);
                return;
            }
            Ui.Info(this, response.Changed
                ? response.Message + "\n\nOpen a new terminal window to use it."
                : response.Message);
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>"Remove from PATH" — opened from the Explorer menu.</summary>
    internal sealed class RemoveFromPathDialog : Form
    {
        private readonly OperationRunner _runner;
        private readonly string _folder;
        private readonly CheckBox _user;
        private readonly CheckBox _machine;
        private readonly Button _ok;

        /// <summary>Shows the dialog, or just a message when the folder isn't in PATH at all.</summary>
        public static void Run(OperationRunner runner, string folder)
        {
            int user = runner.Service.GetPath(EnvScope.User).CountOf(folder);
            int machine = runner.Service.GetPath(EnvScope.Machine).CountOf(folder);
            if (user == 0 && machine == 0)
            {
                Ui.Info(null, "'" + folder + "' isn't in your user PATH or the system PATH.");
                return;
            }
            Application.Run(new RemoveFromPathDialog(runner, folder, user, machine));
        }

        private RemoveFromPathDialog(OperationRunner runner, string folder, int userCount, int machineCount)
        {
            _runner = runner;
            _folder = folder;

            SuspendLayout();
            Ui.InitDialog(this, "Remove from PATH - " + Ui.AppName);

            var root = Ui.Column();
            Ui.AddRow(root, Ui.Label("Remove this folder from PATH?", bold: true));
            Ui.AddRow(root, Ui.ReadOnlyBox(folder));

            _user = new CheckBox
            {
                Text = "From your user PATH (" + Entries(userCount) + ")",
                AutoSize = true,
                Enabled = userCount > 0,
                Checked = userCount > 0,
            };
            _machine = new CheckBox
            {
                Text = "From the system PATH (" + Entries(machineCount) + ", needs administrator approval)",
                AutoSize = true,
                Enabled = machineCount > 0,
                Checked = machineCount > 0,
            };
            Ui.AddRow(root, _user);
            Ui.AddRow(root, _machine);
            Ui.AddRow(root, Ui.Label("A backup is saved first, so you can undo this from Path++ > Backups.", wrapWidth: 460));

            _ok = Ui.Button("Remove", OnRemove);
            var cancel = Ui.Button("Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            Ui.AddRow(root, Ui.ButtonRow(_ok, cancel));

            Controls.Add(root);
            AcceptButton = _ok;
            CancelButton = cancel;
            ResumeLayout(false);
            PerformLayout();

            _user.CheckedChanged += (s, e) => UpdateState();
            _machine.CheckedChanged += (s, e) => UpdateState();
            UpdateState();
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
                Ui.Info(this, text + "\n\nOpen a new terminal window to see the change.");
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>"Create environment variable" — opened from the Explorer menu.</summary>
    internal sealed class NewVariableDialog : Form
    {
        private readonly OperationRunner _runner;
        private readonly TextBox _name;
        private readonly TextBox _value;
        private readonly RadioButton _machine;
        private readonly Label _status;
        private readonly Button _ok;

        public NewVariableDialog(OperationRunner runner, string value)
        {
            _runner = runner;

            SuspendLayout();
            Ui.InitDialog(this, "New environment variable - " + Ui.AppName);

            var root = Ui.Column();
            Ui.AddRow(root, Ui.Label("Create an environment variable for this path:", bold: true));

            Ui.AddRow(root, Ui.Label("Name:"));
            _name = new TextBox { Width = 460, Text = SuggestName(value) };
            Ui.AddRow(root, _name);
            Ui.AddRow(root, Ui.Label("Value:"));
            _value = new TextBox { Width = 460, Text = value };
            Ui.AddRow(root, _value);

            var group = new GroupBox { Text = "Create it for", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, Padding = new Padding(8), Margin = new Padding(3, 10, 3, 3) };
            var options = Ui.Column();
            var user = new RadioButton { Text = "Just me (" + runner.Session.UserName + ")", AutoSize = true, Checked = true };
            _machine = new RadioButton { Text = "All users (system variable, needs administrator approval)", AutoSize = true };
            Ui.AddRow(options, user);
            Ui.AddRow(options, _machine);
            group.Controls.Add(options);
            Ui.AddRow(root, group);

            _status = Ui.Label(string.Empty);
            _status.ForeColor = Ui.ProblemColor;
            Ui.AddRow(root, _status);

            _ok = Ui.Button("Create", OnCreate);
            var cancel = Ui.Button("Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            Ui.AddRow(root, Ui.ButtonRow(_ok, cancel));

            Controls.Add(root);
            AcceptButton = _ok;
            CancelButton = cancel;
            ResumeLayout(false);
            PerformLayout();

            _name.TextChanged += (s, e) => UpdateState();
            _value.TextChanged += (s, e) => UpdateState();
            _machine.CheckedChanged += (s, e) => UpdateState();
            Shown += (s, e) => { _name.Focus(); _name.SelectAll(); };
            UpdateState();
        }

        private EnvScope Scope => _machine.Checked ? EnvScope.Machine : EnvScope.User;

        /// <summary>"C:\Tools\jdk-21" becomes "JDK_21".</summary>
        internal static string SuggestName(string path)
        {
            string leaf = Path.GetFileName(PathNormalizer.TrimTrailingSeparators(path ?? string.Empty));
            if (string.IsNullOrEmpty(leaf)) return string.Empty;
            var chars = leaf.ToUpperInvariant().ToCharArray();
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
            if (EnvValidation.IsPathName(name)) return "To change PATH, use \"Add to PATH\" instead — this would replace all of PATH.";
            return EnvValidation.ValidateName(name) ?? EnvValidation.ValidateValue(_value.Text);
        }

        private void UpdateState()
        {
            string error = _name.Text.Length == 0 ? null : ValidateInput();
            _status.Text = error ?? string.Empty;
            _status.Visible = error != null;
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
            if (response.Error == EnvError.Cancelled) return;
            if (!response.Success)
            {
                Ui.Error(this, response.Message);
                return;
            }
            Ui.Info(this, response.Message + "\n\nUse it as %" + name + "% in new terminal windows and programs.");
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>Type or browse for a folder (used by the PATH editor's Add and Edit).</summary>
    internal sealed class EntryDialog : Form
    {
        private readonly TextBox _text;
        private readonly Label _status;
        private readonly Button _ok;

        public EntryDialog(string title, string initial)
        {
            SuspendLayout();
            Ui.InitDialog(this, title + " - " + Ui.AppName);
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;

            var root = Ui.Column();
            Ui.AddRow(root, Ui.Label("Folder (you can use variables such as %USERPROFILE%\\bin):"));

            var line = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0) };
            _text = new TextBox { Width = 440, Text = initial ?? string.Empty, Margin = new Padding(3, 4, 3, 3) };
            var browse = Ui.Button("Browse…", OnBrowse);
            line.Controls.Add(_text);
            line.Controls.Add(browse);
            Ui.AddRow(root, line);

            _status = Ui.Label(string.Empty, wrapWidth: 540);
            Ui.AddRow(root, _status);

            _ok = Ui.Button("OK");
            _ok.DialogResult = DialogResult.OK;
            var cancel = Ui.Button("Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            Ui.AddRow(root, Ui.ButtonRow(_ok, cancel));

            Controls.Add(root);
            AcceptButton = _ok;
            CancelButton = cancel;
            ResumeLayout(false);
            PerformLayout();

            _text.TextChanged += (s, e) => UpdateState();
            UpdateState();
        }

        public string Value => PathNormalizer.TrimTrailingSeparators(PathNormalizer.Clean(_text.Text));

        private void UpdateState()
        {
            string error = EnvValidation.ValidatePathEntry(_text.Text);
            if (error != null)
            {
                _status.Text = _text.Text.Trim().Length == 0 ? string.Empty : error;
                _status.ForeColor = Ui.ProblemColor;
                _ok.Enabled = false;
                return;
            }

            _ok.Enabled = true;
            bool exists;
            try
            {
                exists = PathNormalizer.HasUnresolvedVariable(_text.Text) || Directory.Exists(PathNormalizer.Expand(_text.Text));
            }
            catch (Exception)
            {
                exists = true;
            }
            _status.Text = exists ? string.Empty : "This folder doesn't exist (you can still add it).";
            _status.ForeColor = Ui.WarningColor;
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            string start = null;
            try
            {
                start = PathNormalizer.Expand(_text.Text);
            }
            catch (Exception)
            {
                // Ignore malformed text.
            }
            string picked = FolderPicker.Pick(this, "Choose a folder to add to PATH", start);
            if (picked != null) _text.Text = picked;
        }
    }

    /// <summary>Read-only text viewer (backup contents).</summary>
    internal sealed class TextViewDialog : Form
    {
        public TextViewDialog(string title, string text)
        {
            SuspendLayout();
            Ui.InitForm(this, title + " - " + Ui.AppName);
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(640, 380);
            MinimizeBox = false;
            Padding = new Padding(8);

            var box = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Text = text,
                Font = new Font(FontFamily.GenericMonospace, 9.5F),
            };
            var close = Ui.Button("Close");
            close.DialogResult = DialogResult.Cancel;
            var row = Ui.ButtonRow(close);
            row.Dock = DockStyle.Bottom;

            Controls.Add(box);
            Controls.Add(row);
            CancelButton = close;
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
