using System;
using System.IO;
using System.Windows.Forms;
using PathPlusPlus.Core;

namespace PathPlusPlus.App
{
    /// <summary>Type or browse for a folder (used by the PATH editor's Add and Edit).</summary>
    internal sealed class EntryDialog : Form
    {
        private readonly TextBox _text;
        private readonly Label _note;
        private readonly Button _ok;

        public EntryDialog(string window, string heading, string initial)
        {
            SuspendLayout();
            Ui.InitDialog(this, window);
            Ui.MakeChild(this);

            TableLayoutPanel root = Ui.Column();
            Ui.AddRow(root, Ui.Heading(heading));
            Ui.AddRow(root, Ui.Label("&Folder (you can use variables such as %USERPROFILE%\\bin):"));

            var line = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0) };
            _text = Ui.InputBox(initial, Ui.ContentWidth - Ui.ButtonMinWidth - 6);
            _text.Margin = new Padding(3, 3, 0, 8);
            line.Controls.Add(_text);
            line.Controls.Add(Ui.Button("&Browse…", OnBrowse));
            Ui.AddRow(root, line);

            _note = Ui.Note();
            Ui.AddRow(root, _note);

            _ok = Ui.Button("OK");
            _ok.DialogResult = DialogResult.OK;
            Ui.AddRow(root, Ui.ButtonRow(_ok, Ui.CancelButton(this)));

            Controls.Add(root);
            AcceptButton = _ok;
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
                Ui.SetNote(_note, _text.Text.Trim().Length == 0 ? null : error, Ui.Tone.Error);
                _ok.Enabled = false;
                return;
            }

            _ok.Enabled = true;
            bool exists;
            try
            {
                exists = PathNormalizer.HasUnresolvedVariable(_text.Text) || Directory.Exists(PathNormalizer.Expand(_text.Text));
            }
            catch (ArgumentException)
            {
                exists = true;
            }
            Ui.SetNote(_note, exists ? null : "This folder doesn't exist yet (you can still add it).", Ui.Tone.Warning);
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            string start = PathNormalizer.Expand(_text.Text);
            string picked = FolderPicker.Pick(this, "Choose a folder", start);
            if (picked != null) _text.Text = picked;
        }
    }
}
