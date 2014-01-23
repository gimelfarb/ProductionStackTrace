using System;
using System.Collections.Generic;
using System.Text;

namespace ProductionStackTrace.Analyze.Console
{
    using Console = System.Console;
    class Program
    {
        static void Main(string[] args)
        {
            var interpreter = new ExceptionReportInterpreter();
            interpreter.SymbolPaths.Add(@"..\..\..\ProductionStackTrace.Test\bin\Debug");

            if (ConsoleEx.IsInputRedirected)
            {
                interpreter.Translate(Console.In, Console.Out);
            }
            else
            {
                Console.WriteLine("Paste a stack trace to analyze (ESC to exit) ...");
                while (true)
                {
                    var reader = new ConsoleTextReader();
                    interpreter.Translate(reader, Console.Out);

                    if (reader.IsEscapeDetected) break;
                }
            }
        }
    }
}
