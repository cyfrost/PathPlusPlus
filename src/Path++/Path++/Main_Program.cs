/*
The MIT License (MIT)
---------------------

Path++ - A command-line utility to add, edit, and remove Environment variables

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
using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
namespace Path__
{
    class Options
    {
        [Option('n', "name", Required =true,
          HelpText = "The name of an environment variable.")]
        public string VarName { get; set; }
        [Option('v', "value", Required =true,
          HelpText = "A value to assign to the variable.")]
        public string VarVal { get; set; }
        [Option('t', "target", Required = true,
          HelpText = "Specifies the location of the environment variable.")]
        public string VarTarget { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }
        [HelpOption]
        public string GetUsage()
        {
            return new Main_Program()._desc;
        }
    }

    class Main_Program
    {
        public static string appname = Assembly.GetExecutingAssembly().GetName().Name;
        public static string copyright = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false).Cast<AssemblyCopyrightAttribute>().FirstOrDefault().Copyright;
        public static string desc = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false).Cast<AssemblyDescriptionAttribute>().FirstOrDefault().Description;
        public static string version = Assembly.GetExecutingAssembly().GetName().Version.ToString() + " (build " + DateTime.Now.ToString("Myy") + ")";


        public string _desc = "\n" + appname + " - " + desc + "\nversion " + version + "\n" + copyright + "\n\nusage: pathpp [-n [name]] [-v [value]] [-t [target]]\n\t-n\tThe name of an environment variable.\n\t-v\tA value to assign to the variable.\n\t-t\tOne of the enumeration values that specifies the location of the environment variable.\n\ttarget  - proc --Environment variable targetting the environment block associated with the current process\n\t\t- usr --Environment variable restricted to current user\n\t\t- mach --[Requires elevated access] Machine wide environment variable\n\nExample:\n---------\npathpp.exe -n foo -v \"bar\" -t mach\n\nDescription: The above command creates an Environment Variable using 'foobar' as its Name, 'fooVal' as its Value, and Location targetting 'Machine'\n\n";


        public static Options options = new Options();
        private static void Main(string[] args)
        {
            try
            {
                bool IsElevatedOp = false;
                EnvironmentVariableTarget target = EnvironmentVariableTarget.User;
                if (Parser.Default.ParseArguments(args, options))
                {
                    if (options.VarName != null && options.VarVal != null && options.VarTarget != null)
                    {

                        if (options.VarTarget == "proc")
                            target = EnvironmentVariableTarget.Process;
                        else if (options.VarTarget == "mach")
                            target = EnvironmentVariableTarget.Machine;

                        IsElevatedOp = (options.VarTarget == "mach") ? true : false;

                        if (IsElevatedOp)
                        {
                            try
                            {
                                new ElevatePermissions().PerformOperation(options);
                                Console.WriteLine("The command completed successfully.\n");
                                return;
                            }
                            catch (Exception)
                            {
                                Console.WriteLine("Error: Access denied, creation of Environment Variables targetting 'Machine' must be granted with Elevated access.");
                                return;
                            }
                        }

                    }


                    Environment.SetEnvironmentVariable(options.VarName, options.VarVal, target);
                    // Console.WriteLine("\nSuccessfully created Environment Variable:\n\nVariable Name: {0}\nVariable Value: {1}\nVariable Target: {2}", options.VarName, options.VarVal, target.ToString());
                    Console.WriteLine("The command completed successfully.\n");
                }

            }
            catch (Exception ex) {
                MessageBox.Show("Error: " + ex.Message, "Path++", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
           
        }



    }
}
