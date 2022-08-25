using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Dia2Lib;

namespace ProductionStackTrace.Analyze {
	/// <summary>
	/// Managed wrapper around DIA SDK (Debug Information Access)
	/// COM interfaces to load information from a PDB symbols file.
	/// </summary>
	public class SymbolLoader {
		// NOTE: Since this class is intended to be used on the analyzer
		// side, we rely on GC to cleanup and release COM references to
		// this RCW objects.

		IDiaDataSource _source;
		IDiaSession _session;

		private SymbolLoader() {
		}

		/// <summary>
		/// Open and load symbol information from the specified
		/// PDB file path.
		/// </summary>
		/// <param name="filePath"></param>
		/// <returns></returns>
		public static SymbolLoader Load(string filePath) {
			//try
			//{
			var loader = new SymbolLoader();
			loader._source = CoCreateDiaSource();

			loader._source.loadDataFromPdb(filePath);
			loader._source.openSession(out loader._session);
			return loader;
			//}
			//catch
			//{
			//    return null;
			//}
		}

		private static readonly Guid[] s_msdiaGuids = new[] {
			//"
			new Guid("E6756135-1E65-4D17-8576-610761398C3C"), // VS 2017/19 (msdia140.dll)
            new Guid("3BFCEA48-620F-4B6B-81F7-B9AF75454C7D"), // VS 2013 (msdia120.dll)
            new Guid("761D3BCD-1304-41D5-94E8-EAC54E4AC172"), // VS 2012 (msdia110.dll)
            new Guid("B86AE24D-BF2F-4AC9-B5A2-34B14E4CE11D"), // VS 2010 (msdia100.dll)
            new Guid("4C41678E-887B-4365-A09E-925D28DB33C2")  // VS 2008 (msdia90.dll)
        };

		/// <summary>
		/// Helper to instantiate the DIA COM object. This depends on what
		/// version of msdiaxxx.dll is installed on the system. We must try
		/// each in order.
		/// </summary>
		/// <returns></returns>
		private static IDiaDataSource CoCreateDiaSource() {
			for (var i = 0; i < s_msdiaGuids.Length; i++) {
				try {


					//if this fails need to call AS ADMIN: regsvr32 "c:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\ide\msdia140.dll"
					// or regsvr32 "C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\msdia140.dll"
					//return new DiaSource();
					return (IDiaDataSource)Activator.CreateInstance(Type.GetTypeFromCLSID(s_msdiaGuids[i]));
				} catch (COMException) {

				} catch (FileNotFoundException) { }

			}
			throw GetDiagSourceMayBeMissingException();
		}
		public static Exception GetDiagSourceMayBeMissingException() {
			throw new Exception($"Unable to create instance of DiaSource you likely need to run regsvr32 as admin on the path to msdia140.dll like: regsvr32 \"{GetSuggestedDiaLocation()}\"");
		}
		public static string GetSuggestedDiaLocation() {
			var dia_paths = new[] { @"DIA SDK\bin\amd64\", @"Common7\ide\", @"DIA SDK\bin\" };
			var folder_base = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) };
			var builds = new[] { "Enterpise", "Community", "Preview" };
			var years = new[] { "2022", "2019", "2016" };
			string first_path = null;
			foreach (var folder in folder_base) {
				foreach (var year in years) {
					foreach (var build in builds) {
						foreach (var paths in dia_paths) {

							var path = Path.Combine(folder, "Microsoft Visual Studio", year, build, paths, "msdia140.dll");
							if (first_path == null)
								first_path = path;
							if (File.Exists(path))
								return path;
						}
					}
				}
			}
			return first_path;
		}
		/// <summary>
		/// Obtain the original source code file and line number for the given
		/// IL offset in a method identified by a unique metadata token.
		/// </summary>
		/// <param name="methodMetadataToken"></param>
		/// <param name="ilOffset"></param>
		/// <returns></returns>
		/// <remarks>
		/// Metadata token is stored in the PDB file for each method. Additionally
		/// it can always be retrieved from MethodInfo via reflection. Exception
		/// report saves a meatdata token for each method in the stack trace.
		/// </remarks>
		public SourceLocation GetSourceLoc(int methodMetadataToken, int ilOffset) => GetSourceLoc(methodMetadataToken, ilOffset, true);
		public SourceLocation GetSourceLoc(int methodMetadataToken, int ilOffset, bool firstTry) {
			IDiaSymbol symMethod;
			_session.findSymbolByToken((uint)methodMetadataToken, SymTagEnum.SymTagFunction, out symMethod);

			if (symMethod == null) return null;
			Debug.WriteLine($"{symMethod.compilerName} libName: {symMethod.libraryName}  name: {symMethod.name} count: {symMethod.count} ");
			var rvaMethod = symMethod.relativeVirtualAddress;
			rvaMethod += (uint)ilOffset;

			IDiaEnumLineNumbers lineNumbers;
			_session.findLinesByRVA(rvaMethod, 1, out lineNumbers);

			foreach (IDiaLineNumber ln in lineNumbers) {
				var sourceFile = ln.sourceFile;
				//DebugWriteLineNumber($"official isFIrst: {firstTry}", rvaMethod);
				if (ln.lineNumber == 0xF00F00 || ln.lineNumber == 0xFEEFEE) { //justMycode markers try offset to get proper line
					return GetSourceLoc(methodMetadataToken, ilOffset + 32, false);
					//if (methodMetadataToken == 0x06000019) {

					//	//for (uint x = 3500; x < 9000; x+=50) {
					//	for (uint x = rvaMethod; x < rvaMethod + 200; x += 1) {
					//		DebugWriteLineNumber("", x);
					//		//DebugWriteLineNumber("", rvaMethod - x);

					//	}

					}
				return new SourceLocation() { LineNumber = ln.lineNumber, SourceFile = (sourceFile == null) ? null : sourceFile.fileName };
			}

			return null;
		}
			private void DebugWriteLineNumber(String what, uint rva) {
				IDiaEnumLineNumbers lineNumbers;
				_session.findLinesByRVA(rva, 1, out lineNumbers);
				foreach (IDiaLineNumber ln in lineNumbers)
					Debug.WriteLine($"{what} at {rva} For: {what} at {ln.lineNumber} -- {ln.sourceFile}");
			}
	}
}
