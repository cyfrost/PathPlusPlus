using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PathPlusPlus.App
{
    /// <summary>Small helpers so every window looks and behaves the same.</summary>
    internal static class Ui
    {
        public const string AppName = "Path++";
        private static Icon _icon;

        public static readonly Color ProblemColor = Color.FromArgb(176, 0, 32);
        public static readonly Color WarningColor = Color.FromArgb(150, 85, 0);

        public static Icon AppIcon
        {
            get
            {
                if (_icon == null)
                {
                    try
                    {
                        _icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                    }
                    catch (Exception)
                    {
                        _icon = SystemIcons.Application;
                    }
                }
                return _icon;
            }
        }

        /// <summary>Standard setup for a window built in code. Call between SuspendLayout/ResumeLayout.</summary>
        public static void InitForm(Form form, string title)
        {
            form.AutoScaleDimensions = new SizeF(96F, 96F);
            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.Font = SystemFonts.MessageBoxFont;
            form.Text = title;
            form.Icon = AppIcon;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.ShowInTaskbar = true;
        }

        /// <summary>Fixed-size dialog that sizes itself to its content.</summary>
        public static void InitDialog(Form form, string title)
        {
            InitForm(form, title);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.AutoSize = true;
            form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            form.Padding = new Padding(12);
        }

        public static TableLayoutPanel Column()
        {
            var panel = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            return panel;
        }

        public static void AddRow(TableLayoutPanel panel, Control control)
        {
            panel.RowCount++;
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(control, 0, panel.RowCount - 1);
        }

        public static Label Label(string text, int wrapWidth = 460, bool bold = false)
        {
            var label = new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(wrapWidth, 0),
                Margin = new Padding(3, 3, 3, 6),
            };
            if (bold) label.Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold);
            return label;
        }

        public static TextBox ReadOnlyBox(string text, int width = 460)
        {
            return new TextBox
            {
                Text = text,
                ReadOnly = true,
                Width = width,
                Margin = new Padding(3, 3, 3, 8),
                TabStop = false,
            };
        }

        public static Button Button(string text, EventHandler onClick = null, int minWidth = 88)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(minWidth, 26),
                FlatStyle = FlatStyle.System, // required for the UAC shield
                UseVisualStyleBackColor = true,
                Margin = new Padding(6, 3, 0, 3),
            };
            if (onClick != null) b.Click += onClick;
            return b;
        }

        /// <summary>A right-aligned OK/Cancel style button row.</summary>
        public static FlowLayoutPanel ButtonRow(params Button[] buttons)
        {
            var row = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 10, 0, 0),
                WrapContents = false,
            };
            // RightToLeft: add in reverse so the first button ends up leftmost.
            for (int i = buttons.Length - 1; i >= 0; i--) row.Controls.Add(buttons[i]);
            return row;
        }

        /// <summary>Shows or hides the UAC shield on a button.</summary>
        public static void SetShield(Button button, bool show)
        {
            if (button.FlatStyle != FlatStyle.System) button.FlatStyle = FlatStyle.System;
            if (button.IsHandleCreated)
            {
                NativeMethods.SendMessage(button.Handle, NativeMethods.BCM_SETSHIELD, IntPtr.Zero, new IntPtr(show ? 1 : 0));
            }
            else
            {
                button.HandleCreated += (s, e) =>
                    NativeMethods.SendMessage(button.Handle, NativeMethods.BCM_SETSHIELD, IntPtr.Zero, new IntPtr(show ? 1 : 0));
            }
        }

        public static void Info(IWin32Window owner, string text) =>
            MessageBox.Show(owner, text, AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);

        public static void Warn(IWin32Window owner, string text) =>
            MessageBox.Show(owner, text, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);

        public static void Error(IWin32Window owner, string text) =>
            MessageBox.Show(owner, text, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);

        public static bool Confirm(IWin32Window owner, string text) =>
            MessageBox.Show(owner, text, AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

        /// <summary>Opens the built-in Windows "Environment Variables" dialog.</summary>
        public static void OpenWindowsEnvironmentDialog()
        {
            string rundll = Path.Combine(Environment.SystemDirectory, "rundll32.exe");
            Process.Start(new ProcessStartInfo(rundll, "sysdm.cpl,EditEnvironmentVariables") { UseShellExecute = false });
        }

        public static void OpenFolder(string folder)
        {
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "\"" + folder + "\"")
            {
                UseShellExecute = false,
            });
        }
    }

    internal static class NativeMethods
    {
        public const int BCM_SETSHIELD = 0x160C;
        public const int SHCNE_ASSOCCHANGED = 0x08000000;
        public const uint SHCNF_IDLIST = 0x0000;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll")]
        public static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
    }
}
