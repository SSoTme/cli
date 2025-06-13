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

                if (returnValue != 0)
                {
                    Console.WriteLine("\n\nPress any key to continue.");
                    Console.ReadKey();
                }

                return returnValue;
            }
            catch (ProjectNotConfiguredException ex)
            {
                return -1; // don't print stack trace
            }
            // other exceptions throw normally
        }
    }
}