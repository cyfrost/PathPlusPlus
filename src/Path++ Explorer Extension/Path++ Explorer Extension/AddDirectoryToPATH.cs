/*

The MIT License (MIT)
---------------------

Path++ Explorer Extension - View and manage Environment Variables from the comfort of Context Menu!

Copyright © 2018 cyfrost <cyrus.frost@hotmail.com>

Permission is hereby granted, free of charge, to any person obtaining 
a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies
or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
FOR AND CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.

*/


using System;
using System.Windows.Forms;

namespace Path___Explorer_Extension
{
    public partial class AddDirectoryToPATH : Form
    {
        public string DirFullName = null;
        private MessageDialogs msg = new MessageDialogs();

        public AddDirectoryToPATH()
        {
            InitializeComponent();
        }

        public AddDirectoryToPATH(string DirFullName)
        {
            InitializeComponent();
            comboBox1.Items.Add("Current user (" + Environment.UserName + ")");
            comboBox1.Items.Add("Machine (all users)");
            comboBox1.Text = "Current user (" + Environment.UserName + ")";
            this.DirFullName = DirFullName;
            comboBox1.Select();
        }

        protected override bool ProcessCmdKey(ref Message mg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                
                if (msg.ConfirmAction("Are you sure you want to exit?") == DialogResult.Yes)
                {
                    Console.WriteLine("Operation canceled by user.");
                    Application.Exit();

                }
                return false;

            }
            return base.ProcessCmdKey(ref mg, keyData);
        }

        private void Button4_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Operation canceled by user.");
            Application.Exit();
        }

        private void Button3_Click(object sender, EventArgs e)
        {
            try { if (comboBox1.Text == "Current user (" + Environment.UserName + ")") {
                if (new PathVariableFunctions().AddDirectoryToPath(DirFullName, 0))
                {
                    Console.WriteLine("The command completed successfully!");
                    msg.ShowMessage("Success!");

                    Application.Exit();
                }
            }
            else if (comboBox1.Text == "Machine (all users)") {
                if (new PathVariableFunctions().AddDirectoryToPath(DirFullName, 1))
                {
                    Console.WriteLine("The command completed successfully!");
                    msg.ShowMessage("Success!");
                    Application.Exit();
                }
            }
            }
            catch (Exception ex) { msg.ShowError("Error: " + ex.Message); return; }

        }

        private void ComboBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                button3.PerformClick();
        }

        private void AddDirectoryToPATH_Load(object sender, EventArgs e)
        {

        }
    }
}
