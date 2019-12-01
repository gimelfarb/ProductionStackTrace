using System;
using System.Collections.Generic;
using System.Text;

namespace ProductionStackTrace.Analyze.Console {
	using Microsoft.Win32;
	using Console = System.Console;
	class Program {
		static void Main(string[] args) {
			var interpreter = new ExceptionReportInterpreter();
			ProcessCommandArgs(interpreter, args);

			if (ConsoleEx.IsInputRedirected) {
				interpreter.Translate(Console.In, Console.Out);
			} else {
				foreach (var symPath in interpreter.SymbolPaths)
					Console.WriteLine("Symbols: {0}", symPath);

				Console.WriteLine("Paste a stack trace to analyze (ESC to exit) ...");
				while (true) {
					var reader = new ConsoleTextReader();
					interpreter.Translate(reader, Console.Out);

					if (reader.IsEscapeDetected) break;
				}
			}
		}

		static void ProcessCommandArgs(ExceptionReportInterpreter interpreter, string[] args) {
			for (var i = 0; i < args.Length; ++i) {
				switch (args[i].ToLowerInvariant()) {
					case "-vs":
						var vsVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");
						if (!string.IsNullOrEmpty(vsVersion)) {
							var vsDebuggerKey = string.Format(@"HKEY_CURRENT_USER\Software\Microsoft\VisualStudio\{0}\Debugger", vsVersion);
							var vsSymbolPaths = Registry.GetValue(vsDebuggerKey, "SymbolPath", null) as string;
							if (!string.IsNullOrEmpty(vsSymbolPaths))
								interpreter.SymbolPaths.AddRange(vsSymbolPaths.Split(';'));
						}
						break;
					case "-s":
						if (++i >= args.Length) goto default;
						interpreter.SymbolPaths.AddRange(args[i].Split(';'));
						break;
					default:
						Console.Error.WriteLine("Usage: {0} [-vs] [-s \"symbol path1;symbol path2\"]", "ProductionStackTrace.Analyze.Console");
						Environment.Exit(-1);
						break;
				}
			}
		}
	}
}
