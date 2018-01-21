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

using System;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;


namespace Path__
{
    class ElevatePermissions
    {
        public bool PerformOperation(Options options)
        {
            if (IsProcessElevated())
            {

                try
                {

                    Environment.SetEnvironmentVariable(options.VarName, options.VarVal, EnvironmentVariableTarget.Machine);
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\nError: " + ex.Message);
                    return false;
                }
            }
            else
            {

                ProcessStartInfo info = new ProcessStartInfo(Assembly.GetEntryAssembly().Location, " -n " + options.VarName + " -v " + options.VarVal + " -t mach")
                {
                    Verb = "runas" // indicates to elevate privileges
                };



                Process process = new Process
                {
                    EnableRaisingEvents = true, // enable WaitForExit()
                    StartInfo = info
                };
                
                bool ret = process.Start();
                process.WaitForExit();
                return ret;
            }
        }



        private static bool IsProcessElevated()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}
