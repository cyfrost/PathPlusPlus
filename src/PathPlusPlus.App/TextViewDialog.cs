using System.Drawing;
using System.Windows.Forms;

namespace PathPlusPlus.App
{
    /// <summary>Read-only text viewer (backup contents).</summary>
    internal sealed class TextViewDialog : Form
    {
        public TextViewDialog(string window, string text)
        {
            SuspendLayout();
            Ui.InitForm(this, window);
            Ui.MakeChild(this);
            ClientSize = new Size(640, 380);
            MinimumSize = new Size(360, 240);
            MinimizeBox = false;

            var box = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Text = text,
                Font = new Font("Consolas", 9.75F),
            };

            var close = Ui.Button("Close");
            close.DialogResult = DialogResult.Cancel;
            FlowLayoutPanel row = Ui.ButtonRow(close);
            row.Dock = DockStyle.Bottom;

            Controls.Add(box);
            Controls.Add(row);
            CancelButton = close;
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
