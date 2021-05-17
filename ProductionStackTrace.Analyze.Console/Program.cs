using System;
using System.Collections.Generic;
using System.Text;

namespace ProductionStackTrace.Analyze.Console {
	using Microsoft.Win32;
	using System.IO;
	using Console = System.Console;
	class Program {
		static void Main(string[] args) {

			var interpreter = new ExceptionReportInterpreter();
			ProcessCommandArgs(interpreter, args);
			if (String.IsNullOrWhiteSpace(interpreter.AltDbgHelpDll)) {

				var alt_dbg_help_locations = (IntPtr.Size == 8) ?
					new[] { @"c:\Program Files (x86)\Windows Kits\10\Debuggers\x64", @"c:\Program Files (x86)\Windows Kits\8\Debuggers\x64", @"c:\Program Files (x86)\Windows Kits\8.1\Debuggers\x64" }
					: new[] { @"c:\Program Files (x86)\Windows Kits\10\Debuggers\x86", @"c:\Program Files (x86)\Windows Kits\8\Debuggers\x86", @"c:\Program Files (x86)\Windows Kits\8.1\Debuggers\x86" };
				foreach (var loc in alt_dbg_help_locations) {
					var full_path = Path.Combine(loc, "dbghelp.dll");
					if (File.Exists(full_path))
						interpreter.AltDbgHelpDll = loc;
				}
			}
			if (!String.IsNullOrWhiteSpace(interpreter.AltDbgHelpDll))
				SymbolSearch.SetDllDirectory(interpreter.AltDbgHelpDll);

			if (ConsoleEx.IsInputRedirected) {
				interpreter.Translate(Console.In, Console.Out);
			} else {
				foreach (var symPath in interpreter.SymbolPaths.Elements)
					Console.WriteLine("Symbols: {0}", symPath.Target);

				if (File.Exists(interpreter.ExceptionFileToRead)) {
					using (var f_reader = new StreamReader(interpreter.ExceptionFileToRead)) {
						interpreter.Translate(f_reader, Console.Out);
					}
					Console.WriteLine("Done -ExceptionFile READ");
				}

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
								interpreter.SymbolPaths.Add(vsSymbolPaths);
						}
						break;
					case "-dbghelp"://alternate dbghelp.dll location to use
						if (++i >= args.Length) goto default;
						interpreter.AltDbgHelpDll = args[i];
						break;
					case "-exceptionfile":
						if (++i >= args.Length) goto default;
						interpreter.ExceptionFileToRead = args[i];
						break;
					case "-s":
						if (++i >= args.Length) goto default;
						interpreter.SymbolPaths.Add(args[i]);
						break;
					default:
						Console.Error.WriteLine(@"Usage: {0} [-ExceptionFile ""path/to/exception.file""] [-vs] [-dbghelp ""path/to/dbghelp.dll""] [-s ""symbol path1;symbol path2""]", "ProductionStackTrace.Analyze.Console");
						Environment.Exit(-1);
						break;
				}
			}
		}
	}
}
