using Microsoft.Diagnostics.Symbols;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ProductionStackTrace.Analyze {
	/// <summary>
	/// Managed wrapper around dbghelp.dll (Windows SDK) calls to 
	/// SymFindFileInPath which implements the logic of find a symbol
	/// file based on input search criteria.
	/// </summary>
	public partial class SymbolSearch {

		/// <summary>
		/// Initialize a new instance of <see cref="SymbolSearch"/>.
		/// </summary>
		public SymbolSearch() {
			SymbolPaths = new SymbolPath(SymbolPath.SymbolPathFromEnvironment);
			this.SymbolPaths.Add(".");

		}
		public void ClearSymbolPaths() {
			SymbolPaths = new SymbolPath();
		}

		/// <summary>
		/// A list of paths to search. These can also be 'srv*' special
		/// paths to reference a symbols server.
		/// See http://msdn.microsoft.com/en-us/library/windows/desktop/ms680689(v=vs.85).aspx
		/// </summary>
		public SymbolPath SymbolPaths { get; private set; }

		/// <summary>
		/// Looks through the configured symbol paths to find a PDB symbol
		/// file matching specified name, GUID and age parameters.
		/// </summary>
		/// <param name="pdbFileName"></param>
		/// <param name="guid"></param>
		/// <param name="age"></param>
		/// <param name="version"></param>
		/// <returns></returns>
		/// <remarks>
		/// Note how we don't have to have a module loaded to find its PDB,
		/// we just look for a PDB file using name, GUID and age. These are
		/// the parameters that can be extracted from module's PE header,
		/// in the Debug Directory section.
		/// </remarks>
		public string FindPdbFile(string pdbFileName, Guid guid, int age, String version) {
			var flags = DbgHelp.SymSetOptions(DbgHelp.SymOptions.SYMOPT_DEBUG | DbgHelp.SymOptions.SYMOPT_EXACT_SYMBOLS);//exact symbols doesn't actually help

			var hProcess = new IntPtr(1);
#if TARGET_NET_20
            var searchPath = string.Join(";", this.SymbolPaths.ToArray());
#else
			var searchPath = SymbolPaths.ToString();
#endif
			DbgHelp.SymInitialize(hProcess, searchPath, false);

			var filePath = new StringBuilder(256);
			var guidHandle = GCHandle.Alloc(guid, GCHandleType.Pinned);
			try {
				if (!DbgHelp.SymFindFileInPath(hProcess, null, pdbFileName,
					guidHandle.AddrOfPinnedObject(), (uint)age, 0,
					DbgHelp.SSRVOPT_GUIDPTR, filePath, null, IntPtr.Zero))
					return null;

				var found_path = filePath.ToString();
				if (!VerifySymbolFileMatch(found_path, guid, age))
					return null;

				Debug.WriteLine($"found correct symbol file at: {found_path}");
				return found_path;
			} finally {
				guidHandle.Free();
				DbgHelp.SymCleanup(hProcess);
			}
		}
		public bool VerifySymbolFileMatch(String path, Guid guid, int age) {
			var info = ExtractInfoFromPDB(path);
			if (!info.guid.Equals(guid) || info.age != age) {
				Debug.WriteLine($"for symbol file {path} it did NOT find the right version searching for guid: {guid} age: {age} found guid: {info.guid} age: {info.age}, not returning symbol file");
				return false;
			} else
				return true;
		}
		public string AltFindPdbFile(ExceptionReportInterpreter.AssemblyMappedInfo info) {
			var qualified_path = $@"{info.PdbName}\{info.PdbGuid.ToString().ToUpper().Replace("-", "")}{info.PdbAge}\{info.PdbName}";

			foreach (var path in this.SymbolPaths.Elements) {
				if (path.Target.StartsWith("http", StringComparison.OrdinalIgnoreCase))//technically local drives > T: are considered remote https://github.com/microsoft/perfview/blob/master/src/TraceEvent/Symbols/SymbolPath.cs
					continue;

				var full_qualified_path = Path.Combine(path.Target, qualified_path);
				if (File.Exists(full_qualified_path))
					return full_qualified_path;
				if (path.Cache == null)
					continue;
				full_qualified_path = Path.Combine(path.Cache, qualified_path);
				if (File.Exists(full_qualified_path))
					return full_qualified_path;


				var version_without_end_zeros = info.Version;
				if (info.Version.EndsWith(".0"))
					version_without_end_zeros = info.Version.Substring(0, info.Version.Length - 2);

				var local_nuget = Path.Combine(path.Target, $"{info.AssemblyName}.{info.Version}.symbols.nupkg");
				if (!File.Exists(local_nuget))
					local_nuget = Path.Combine(path.Target, $"{info.AssemblyName}.{version_without_end_zeros}.symbols.nupkg");
				if (!File.Exists(local_nuget))
					return null;
				using (ZipArchive archive = ZipFile.OpenRead(local_nuget)) {
					foreach (ZipArchiveEntry entry in archive.Entries) {
						if (entry.Name.Equals(info.PdbName, StringComparison.OrdinalIgnoreCase)) {
							var target_file_info = new FileInfo(full_qualified_path);
							if (!target_file_info.Directory.Exists)
								target_file_info.Directory.Create();
							entry.ExtractToFile(target_file_info.FullName, false);
							if (!VerifySymbolFileMatch(target_file_info.FullName, info.PdbGuid, info.PdbAge))
								throw new Exception("Not sure why the verison matches but symbol file GUID/age does not");
							return target_file_info.FullName;
						}
					}
				}
				Debug.WriteLine($"looking at: {qualified_path}");
			}
			return null;
		}
		private (Guid guid, uint age) ExtractInfoFromPDB(String pdb_path) {//no better ways that actually work it seems
			var src = new Dia2Lib.DiaSource();
			src.loadDataFromPdb(pdb_path);
			Dia2Lib.IDiaSession _session;
			src.openSession(out _session);

			return (_session.globalScope.guid, _session.globalScope.age);
		}

		public static void SetDllDirectory(string path) {
			DbgHelp.SetDllDirectory(path);
		}
	}
}
