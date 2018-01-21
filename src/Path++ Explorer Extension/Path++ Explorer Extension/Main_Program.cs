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

using CommandLine;
using System.Diagnostics;
using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Linq;

namespace Path___Explorer_Extension
{

    class Options
    {

        [Option('a', "addnew",
          HelpText = "Add new Environment Variable targetting current invoke path")]
        public bool Flag_AddNewVariable { get; set; }

        [Option('m', "addpdirasnewvar",
         HelpText = "Add parent directory as new variable")]
        public bool Flag_AddParentDirAsNewVariable { get; set; }

        [Option('p', "addtopath",
         HelpText = "Add current directory to PATH variable")]
        public bool Flag_AddDirectoryToPath { get; set; }

                [Option('t', "addparentdirtopath",
         HelpText = "Add parent directory to PATH variable")]
        public bool Flag_AddParentDirectoryToPath { get; set; }

        [Option('r', "removefrompath",
          HelpText = "Removes instances of directory in Local and Machine contexts' of PATH variable")]
        public bool Flag_RemoveFromPath { get; set; }

        [Option('d', "removeparentdirfrompath",
          HelpText = "Removes instances of parent directory in Local and Machine contexts' of PATH variable")]
        public bool Flag_RemoveParentDirectoryFromPath { get; set; }

        [Option('o', "browseenvars",
         HelpText = "Launch system panel to view and manage Environment Variables")]
        public bool Flag_BrowseEnvVars { get; set; }


        [Option('v', "value",
          HelpText = "Holds the current directory path")]
        public string DirectoryFullName { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }
        [HelpOption]
        public string GetUsage()
        {

            return new Main_Program().UDesc;
        }
    }

    class Main_Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        public static string appname = Assembly.GetExecutingAssembly().GetName().Name;
        public static string copyright = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false).Cast<AssemblyCopyrightAttribute>().FirstOrDefault().Copyright;
        public static string desc = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false).Cast<AssemblyDescriptionAttribute>().FirstOrDefault().Description;
        public static string version = Assembly.GetExecutingAssembly().GetName().Version.ToString() + " (build " + DateTime.Now.ToString("Myy") + ")";

        public static string UsageDescription = "\n" + appname + " - " + desc + "\nversion " + version + "\n"  + copyright + "\n\nusage: pathpp_ext.exe [-v [value]] [flag]\n\n\t-v\tObject value  (most commonly the path of a directory or underlying file)\n\n\tFlag\tDescription\n\t----\t-----------\n\t-a\tAdd new Environment Variable with value supplied under <value>\n\t-p\tAdd directory to PATH variable (obtained through current invocation or set via Context Menu Handler)\n\t-r\tRemove directory from PATH variable (selective deletion available through contexts)\n\t-m\tAdd parent directory as new Environment Variable (usually used in Shell view for files)\n\t-t\tAdd parent directory to PATH variable (context selection available, set via Context Menu Handler)\n\t-d\tRemove parent directory from PATH variable\n\t-o\tBrowse Environment Variables via Control Panel Applet\n\nExamples\n--------\npathpp_ext.exe -v \"C:\\foo\\bar\\sample.txt\" -a\n\tCreates a new Environment Variable with value \"C:\\foo\\bar\\sample.txt\" after setting the name and VariableTarget via a GUI form.\n\npathpp_ext.exe -v \"C:\\foobar\\bin\" -p\n\tAdds \"C:\\foobar\\bin\" to PATH variable after setting the context set by the user.\n\npathpp_ext.exe -v \"C:\\foobar\\bin\" -r\n\tRemoves selected instances of the directory (\"C:\\foobar\\bin\") in PATH variable from selected contexts.\n\n";
        public string UDesc = UsageDescription;



        [STAThread]
        private static void Main(string[] args)
        {
            try { 
            var handle = GetConsoleWindow();

            // Hide

            Application.EnableVisualStyles();

            if (args.Length == 0)
            {
                Console.WriteLine(UsageDescription);
                return;
            }
            Options options = new Options();

            if (Parser.Default.ParseArguments(args, options))
            {
                if (!string.IsNullOrEmpty(options.DirectoryFullName)) {
                    ShowWindow(handle, SW_HIDE);
                    if (options.Flag_AddNewVariable)
                    {

                        new NewEnvVariable(options.DirectoryFullName).ShowDialog();
                        return;
                    }
                    else if (options.Flag_RemoveParentDirectoryFromPath)
                    {

                        new PathVariableFunctions().RemoveDirectoryFromPATH(options.DirectoryFullName.Substring(0, options.DirectoryFullName.LastIndexOf(@"\")));
                        return;

                    }
                    else if (options.Flag_RemoveFromPath)
                    {
                        new PathVariableFunctions().RemoveDirectoryFromPATH(options.DirectoryFullName);
                        return;
                    }
                    else if (options.Flag_AddDirectoryToPath)
                    {
                        new AddDirectoryToPATH(options.DirectoryFullName).ShowDialog();
                        return;

                    }
                    else if (options.Flag_AddParentDirectoryToPath)
                    {

                        new AddDirectoryToPATH(options.DirectoryFullName.Substring(0, options.DirectoryFullName.LastIndexOf(@"\"))).ShowDialog();
                        return;
                    }
                    else if (options.Flag_AddParentDirAsNewVariable) {

                        new NewEnvVariable(options.DirectoryFullName.Substring(0, options.DirectoryFullName.LastIndexOf(@"\"))).ShowDialog();
                        return;

                    }




                }

                if (options.Flag_BrowseEnvVars)
                    LaunchEnvironmentVariables();

            }

            }
            catch (Exception ex) {
                new MessageDialogs().ShowError("Error: " + ex.Message);
                return;
            }
        }

        private static void LaunchEnvironmentVariables() {

            ProcessStartInfo info = IsWinVistaOrHigher() ? info = new ProcessStartInfo(@"C:\Windows\System32\rundll32.exe", "sysdm.cpl,EditEnvironmentVariables") : new ProcessStartInfo(@"C:\Windows\System32\control.exe", "sysdm.cpl,,3");
            //info.Verb = "runas"; //Context is already elevated via app manifest
            Process process = new Process
            {
                StartInfo = info
            };
            process.Start();
        }

        static bool IsWinVistaOrHigher()
        {
            OperatingSystem OS = Environment.OSVersion;
            return (OS.Platform == PlatformID.Win32NT) && (OS.Version.Major >= 6);
        }

    }
}
