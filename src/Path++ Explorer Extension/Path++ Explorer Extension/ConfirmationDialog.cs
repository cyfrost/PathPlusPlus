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
    public partial class ConfirmationDialog : Form
    {
        public int Op = -1;

        public ConfirmationDialog(string directory, int userinstances, int machineinstances)
        {
            InitializeComponent();
            if (directory.Length > 48)
                label1.Text += directory.Substring(0, 48) + "...";
            else
                label1.Text += directory;
            label2.Text = "Found " + (userinstances + machineinstances) + " instances in User and Machine contexts' of PATH variable";
            checkBox2.Text = "Remove " + userinstances + " from User context";
            checkBox3.Text = "Remove " + machineinstances + " from Machine context";
            checkBox1.Checked = true;
            checkBox2.Checked = true;
            checkBox3.Checked = true;
            checkBox2.Enabled = false;
            checkBox3.Enabled = false;
            button1.DialogResult = DialogResult.OK;
            checkBox1.KeyDown += new KeyEventHandler(ReturnHandler);
            checkBox2.KeyDown += new KeyEventHandler(ReturnHandler);
            checkBox3.KeyDown += new KeyEventHandler(ReturnHandler);
            checkBox1.Select();
            
        }



        protected override bool ProcessCmdKey(ref Message mg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {

                if (new MessageDialogs().ConfirmAction("Are you sure you want to exit?") == DialogResult.Yes)
                {
                    Console.WriteLine("Operation canceled by user.");
                    Application.Exit();

                }
                return false;

            }
            return base.ProcessCmdKey(ref mg, keyData);
        }

        private void ReturnHandler(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                button1.PerformClick();
        }

        private void ChkBoxHandler() {
            if (!(checkBox1.Checked == false && checkBox2.Checked == false && checkBox3.Checked == false))

                button1.DialogResult = DialogResult.OK;
            else
                button1.DialogResult = DialogResult.None;
        }


        private void Button1_Click(object sender, EventArgs e)
        {
            
            if (!(checkBox1.Checked == false && checkBox2.Checked == false && checkBox3.Checked == false))
            {
                if (checkBox1.Checked)
                    Op = 2;
                else if (!checkBox1.Checked && checkBox2.Checked && checkBox3.Checked)
                    Op = 2;
                else if (!checkBox1.Checked && !checkBox2.Checked && checkBox3.Checked)
                    Op = 1;
                else if (!checkBox1.Checked && checkBox2.Checked && !checkBox3.Checked)
                    Op = 0;
                button1.DialogResult = DialogResult.OK;
            }
            else
                MessageBox.Show("Please select the instances you wish to remove", "Path++", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        }

        private void CheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {

                checkBox2.Enabled = false;
                checkBox3.Enabled = false;
                checkBox2.Checked = true;
                checkBox3.Checked = true;
            }
            else
            {
                checkBox2.Enabled = true;
                checkBox3.Enabled = true;

            }
            ChkBoxHandler();
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Operation canceled by user.");
            Application.Exit();
        }

        private void CheckBox2_CheckedChanged(object sender, EventArgs e)
        {
            ChkBoxHandler();
        }

        private void CheckBox3_CheckedChanged(object sender, EventArgs e)
        {
            ChkBoxHandler();
        }

        private void ConfirmationDialog_Load(object sender, EventArgs e)
        {

        }
    }
}
