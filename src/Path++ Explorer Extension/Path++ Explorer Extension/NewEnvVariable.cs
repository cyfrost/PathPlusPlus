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
    public partial class NewEnvVariable : Form
    {
        private MessageDialogs UDialogs = new MessageDialogs();
        public NewEnvVariable(string VariableValue)
        {
            InitializeComponent();
            textBox1.Text = VariableValue.Substring(VariableValue.LastIndexOf(@"\") + 1);
            textBox2.Text = VariableValue;
            textBox1.KeyDown += new KeyEventHandler(ReturnHandler);
            textBox2.KeyDown += new KeyEventHandler(ReturnHandler);
            comboBox1.KeyDown += new KeyEventHandler(ReturnHandler);
            comboBox1.Items.Add("Current user (" + Environment.UserName + ")");
            comboBox1.Items.Add("Machine (all users)");
            comboBox1.Text = "Current user (" + Environment.UserName + ")";
            textBox1.Select();
        }
        protected override bool ProcessCmdKey(ref Message mg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {

                if (UDialogs.ConfirmAction("Are you sure you want to exit?") == DialogResult.Yes)
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
                btn.PerformClick();
        }
        private void Button2_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void Button2_Click_1(object sender, EventArgs e)
        {
            Console.WriteLine("Operation canceled by user.");
            Application.Exit();
        }

        private void NewEnvVariable_Load(object sender, EventArgs e)
        {

        }

        private void btn_Click(object sender, EventArgs e)
        {
            if (textBox1.Text.ToLower() == "path")
            {
                if (UDialogs.ConfirmAction("Warning: The selected name is same as the 'PATH' variable which is critical to the proper operational status of your Operating System. Modifying the PATH variable often yields undesirable consequences. It's in your best interest not to proceed with this operation if you're not sure.\n\nWould you like to continue? (Not recommended)") == DialogResult.No)
                {
                    return;
                }
            }

            try
            {
                EnvironmentVariableTarget target = EnvironmentVariableTarget.User;

                if (comboBox1.Text == "Current user (" + Environment.UserName + ")")
                    target = EnvironmentVariableTarget.User;
                else if (comboBox1.Text == "Machine (all users)")
                    target = EnvironmentVariableTarget.Machine;
                /*else if (comboBox1.Text == "Both (User & Machine)") {
                    if (!string.IsNullOrEmpty(textBox1.Text) && !string.IsNullOrEmpty(textBox2.Text))
                    {
                        Environment.SetEnvironmentVariable(textBox1.Text, textBox2.Text, EnvironmentVariableTarget.User);
                        Environment.SetEnvironmentVariable(textBox1.Text, textBox2.Text, EnvironmentVariableTarget.Machine);
                        Console.WriteLine("The command completed successfully!");
                        new MessageDialogs().ShowMessage("Success!");
                        Application.Exit();
                    }
                }*/

                if (!string.IsNullOrEmpty(textBox1.Text) && !string.IsNullOrEmpty(textBox2.Text))
                {
                    Environment.SetEnvironmentVariable(textBox1.Text, textBox2.Text, target);
                    Console.WriteLine("The command completed successfully!");
                    new MessageDialogs().ShowMessage("Success!");
                    Application.Exit();
                }

                else
                    UDialogs.ShowWarning("One or more required fields are missing values");
            }
            catch (Exception ex) { UDialogs.ShowError("Error: " + ex.Message); Close(); }
        }
    }
}
