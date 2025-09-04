using SSoTme.OST.Lib.DataClasses;
using Plossum.CommandLine;
using SassyMQ.Lib.RabbitMQ;
using SassyMQ.SSOTME.Lib.RabbitMQ;
using SassyMQ.SSOTME.Lib.RMQActors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SSoTme.OST.Lib.SassySDK.Derived;
using SSoTme.OST.Lib.Extensions;
using SSoTme.OST.Lib.CLIOptions;

namespace SSoTme.OST.ConApp
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                // Thread.Sleep(20000);
                var returnValue = -1;
                var handler = SSoTmeCLIHandler.CreateHandler(args);
                if (!handler.SuppressTranspile) returnValue = handler.TranspileProject();

                if (returnValue != 0 && !handler.SuppressKeyPress)
                {
                    SafeReadKey();
                }

                return returnValue;
            }
            catch (ProjectNotConfiguredException ex)
            {
                return -1;
            }
            catch (NoStackException ex)
            {
                ShowError(ex.Message);  // don't print stack trace
                return -1;
            }
            // other exceptions throw normally
        }
        
        private static void SafeReadKey()
        {
            try
            {
                if (!Console.IsInputRedirected && Environment.UserInteractive)
                {
                    Console.WriteLine("\n\nPress any key to continue.");
                    Console.ReadKey();
                }
            }
            catch (InvalidOperationException)
            {
                // Silently continue - we're in a redirected environment
            }
        }

        private static void ShowError(string msg, ConsoleColor color = ConsoleColor.Red)
        {
            var curColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(msg);
            Console.ForegroundColor = curColor;
        }
    }
}