using System;
using PathPlusPlus.Core;

namespace PathPlusPlus.Cli
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                return new CliApp(EnvironmentServices.CreateRunner(), Console.Out, Console.Error).Run(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("pathpp: unexpected error: " + ex.Message);
                return (int)EnvError.Failed;
            }
        }
    }
}
