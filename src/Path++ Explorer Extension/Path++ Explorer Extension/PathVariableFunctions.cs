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

using Microsoft.Win32;
using System;
using System.IO;
using System.Windows.Forms;

namespace Path___Explorer_Extension
{
    public class PathVariableFunctions
    {
        static MessageDialogs msg = new MessageDialogs();

        public bool AddDirectoryToPath(string DirFullName, int VariableContext)
        {
            //{VariableContext: 0 --CurrentUser, 1 --LocalMachine }

            try {

                string PathVar = null;
                switch (VariableContext)
                {
                    case 0:
                        try
                        {
                            PathVar = (string)Registry.CurrentUser.OpenSubKey("Environment").GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames);
                        }
                        catch (Exception) { }
                        break;
                    case 1:
                        try
                        {
                            PathVar = (string)Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment\").GetValue("PATH", "", RegistryValueOptions.DoNotExpandEnvironmentNames);
                        }
                        catch (Exception) { }
                        break;
                    default:
                        msg.ShowError("Internal error");
                        break;

                }

                if (!string.IsNullOrEmpty(PathVar))
                {
                    if (PathVar.Substring(PathVar.Length - 1) != ";")
                        PathVar += ";" + DirFullName + ";";
                    else
                        PathVar += DirFullName + ";";

                }
                else
                    PathVar += DirFullName + ";";


                if (VariableContext == 0)
                {
                    if (WriteChangesToPATH(PathVar, 0))
                        return true;
                    else
                        return false;

                }
                else if (VariableContext == 1)
                {

                    if (WriteChangesToPATH(PathVar, 1))
                        return true;
                    else
                        return false;
                }
            }
            catch (Exception ex)
            {
                msg.ShowError("Error: " + ex.Message);
                return false;
                //msg.ShowMessage(ex.Message + "\n\n" + ex.ToString());
            }

            

            return false;
        }

        public void RemoveDirectoryFromPATH(string DirFullName)
        {
            try {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Path")))
                    return;

                //string PathVarUser = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User).Replace(';', '\n');
                string PathVar_User = null, PathVar_Machine = null; //Inflated with contextual data from registry
                string AlteredVar_User = null, AlteredVar_Machine = null; //Pre-populate these with entries NOT matching DirFullName and write to PATH after usr confirmation
                int Counter_User = 0, Counter_Machine = 0;
                bool IsInstanceFound_User = false, IsInstanceFound_Machine = false;

                try
                {
                    PathVar_User = (string)Registry.CurrentUser.OpenSubKey("Environment").GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames);
                    PathVar_User = PathVar_User.Replace(';', '\n').Trim();
                }
                catch (NullReferenceException) { }

                try
                {
                    PathVar_Machine = (string)Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment\").GetValue("PATH", "", RegistryValueOptions.DoNotExpandEnvironmentNames);
                    PathVar_Machine = PathVar_Machine.Replace(';', '\n').Trim();
                }
                catch (NullReferenceException) { }


                if (!string.IsNullOrEmpty(PathVar_User))
                {
                    if (IsDirectoryPresentInPATH(DirFullName, 0))
                    {

                        if (PathVar_User.Split('\n').Length > 1)
                        {
                            using (StringReader reader = new StringReader(PathVar_User))
                            {
                                string line;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    if (line != DirFullName)
                                        AlteredVar_User += line + "\n";//already build str with changes and perform merger if user confirms.
                                    else
                                        Counter_User++; //found instance
                                }
                            }
                        }
                        else
                        {
                            if (PathVar_User != DirFullName)
                                AlteredVar_User = PathVar_User;//already build str with changes and perform merger if user confirms.
                            else
                                Counter_User++;

                        }
                    }
                    //  else
                    //  msg.ShowWarning("nothing in user var"); //not found in user context // GUESS YOU DON'T HAVE TO SHOW THIS MSG LOL
                }

                if (!string.IsNullOrEmpty(PathVar_Machine))
                {
                    if (IsDirectoryPresentInPATH(DirFullName, 1))
                    {//checking for user
                     // MessageBox.Show(PathVarUser + "\n" + PathVarUser.Split('\n').Length.ToString());
                        if (PathVar_Machine.Split('\n').Length > 1)
                        {
                            using (StringReader reader = new StringReader(PathVar_Machine))
                            {
                                string line;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    if (line != DirFullName)
                                    {
                                        AlteredVar_Machine += line + "\n"; //already build str with changes and perform merger if user confirms.
                                    }
                                    else Counter_Machine++;//found instance
                                }
                            }
                        }
                        else
                        {
                            if (PathVar_Machine != DirFullName)
                                AlteredVar_Machine = PathVar_Machine;//already build str with changes and perform merger if user confirms.
                            else
                                Counter_Machine++;
                        }
                    }

                    ///    else
                    //  msg.ShowWarning("nothing in machine var"); //not found in mach context // GUESS YOU DON'T HAVE TO SHOW THIS MSG LOL
                }
                if (Counter_User > 0)
                {
                    try
                    {
                        IsInstanceFound_User = true;
                        AlteredVar_User = AlteredVar_User.Replace('\n', ';');
                    }
                    catch (Exception) { }
                }
                if (Counter_Machine > 0)
                {
                    try {
                        IsInstanceFound_Machine = true;
                        AlteredVar_Machine = AlteredVar_Machine.Replace('\n', ';');
                    }
                    catch (Exception) { }
                    
                }

                if (IsInstanceFound_User && IsInstanceFound_Machine)
                {
                    ConfirmationDialog confirmationDialog = new ConfirmationDialog(DirFullName, Counter_User, Counter_Machine);

                    if (confirmationDialog.ShowDialog() == DialogResult.OK)
                    {
                        switch (confirmationDialog.Op)
                        {
                            case -1:
                                Console.WriteLine("ERROR");
                                break;
                            case 0:
                                WriteChangesToPATH(AlteredVar_User, 0); //remove instances from User context only
                                Console.WriteLine("The command completed successfully!");
                                msg.ShowMessage("Success!");
                                break;
                            case 1:
                                WriteChangesToPATH(AlteredVar_Machine, 1); //remove instances from Machine context only
                                Console.WriteLine("The command completed successfully!");
                                msg.ShowMessage("Success!");
                                break;
                            case 2:
                                WriteChangesToPATH(AlteredVar_User, 0); //remove both instances
                                WriteChangesToPATH(AlteredVar_Machine, 1);
                                Console.WriteLine("The command completed successfully!");
                                msg.ShowMessage("Success!");
                                break;
                            default:
                                return;
                        }
                    }
                }

                else if (IsInstanceFound_User)
                {

                    if (msg.ConfirmAction("Found " + Counter_User + " instance(s) in User context\n\nWould you like to confirm removal?") == DialogResult.Yes)
                    {
                        if (WriteChangesToPATH(AlteredVar_User, 0))
                        {
                            Console.WriteLine("The command completed successfully!");
                            msg.ShowMessage("Success!");
                        }
                        else
                        {
                            Console.WriteLine("The requested operation failed due to an internal error");
                            msg.ShowError("Failed to perform requested operation due to an internal error");
                        }

                    }
                }


                else if (IsInstanceFound_Machine)
                {
                    if (msg.ConfirmAction("Found " + Counter_Machine + " instance(s) in Machine context\n\nWould you like to confirm removal?") == DialogResult.Yes)
                    {
                        if (WriteChangesToPATH(AlteredVar_Machine, 1))
                        {
                            Console.WriteLine("The command completed successfully!");
                            msg.ShowMessage("Success!");
                        }
                        else
                        {
                            Console.WriteLine("The requested operation failed due to an internal error");
                            msg.ShowError("Failed to perform requested operation due to an internal error");

                        }

                    }
                }

                else
                {
                    Console.WriteLine("Could not find the specified directory in PATH variable");
                    msg.ShowMessage("No instances found!");
                }

            }
            catch (Exception ex) {
                msg.ShowError("Error: " + ex.Message);
                return;
            }

        }

        public bool IsDirectoryPresentInPATH(string DirFullName, int VariableContext)
        {

            try {

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Path")))
                    return false;

                string PathVar = null;

                switch (VariableContext)
                {
                    case 0:
                        PathVar = (string)Registry.CurrentUser.OpenSubKey("Environment").GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames);
                        break;
                    case 1:
                        PathVar = (string)Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment\").GetValue("PATH", "", RegistryValueOptions.DoNotExpandEnvironmentNames);
                        break;
                    case 2:
                        PathVar = (string)Registry.CurrentUser.OpenSubKey("Environment").GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) + ";" + (string)Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment\").GetValue("PATH", "", RegistryValueOptions.DoNotExpandEnvironmentNames);
                        break;
                    default:
                        throw new Exception("Invalid context");
                }

                if (!string.IsNullOrEmpty(PathVar))
                    PathVar = PathVar.Replace(';', '\n');


                using (StringReader reader = new StringReader(PathVar))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Trim() == DirFullName)
                            return true;
                    }
                }

            }
            catch (Exception ex)
            {
                msg.ShowError("Error: " + ex.Message);
                return false;
            }

            //{VariableContext: 0 --CurrentUser, 1 --LocalMachine, 2 --Both }

                return false;
        }

        public bool WriteChangesToPATH(string MergeData, int VariableContext)
        {
            try {

                //{ VariableContext: 0 --CurrentUser, 1 --LocalMachine}
                if (MergeData == null)
                    MergeData = "";

                switch (VariableContext)
                {
                    case 0:
                        Registry.CurrentUser.OpenSubKey(@"Environment", true).SetValue("Path", MergeData);
                        return true;
                    case 1:
                        Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment\", true).SetValue("Path", MergeData);
                        return true;
                    default:
                        return false;
                }

            }
            catch (Exception ex)
            {
                msg.ShowError("Error: " + ex.Message);
                return false;
            }
        }
    }
}
