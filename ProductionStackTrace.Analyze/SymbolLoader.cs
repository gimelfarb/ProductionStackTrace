using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Dia2Lib;
using System.Reflection.Metadata;
using Microsoft.DiaSymReader;

using Microsoft.Diagnostics.Symbols;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata.Ecma335;
using System.Linq;

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
		MetadataReader _sourceMR;

		private SymbolLoader() {
		}

		/// <summary>
		/// Open and load symbol information from the specified
		/// PDB file path.
		/// </summary>
		/// <param name="filePath"></param>
		/// <returns></returns>
		public static SymbolLoader Load(string filePath) {
			filePath = filePath.Replace("\\", "/");
			//try
			//{
			var loader = new SymbolLoader();
			try {
				
				loader._source = CoCreateDiaSource();
				loader._source.loadDataFromPdb(filePath);
			} catch (COMException e) {
				if (IsPortablePDB(filePath)) { //technically these should be readable using the normal debug diag but incase (or on another platform)
					using var strm = new FileStream(filePath, FileMode.Open);
					loader._sourceMR = MetadataReaderProvider.FromPortablePdbStream(strm).GetMetadataReader();
					return loader;
				}
				//var metadataProvider = new SymMetadataProvider(peStream);
				//var path = @"C:\temp\sym\NovaLib.TextUtilsStd.pdb\04A093952079443BBE6EF0C0455B63431";
				//var reader = new PEReader(new FileStream(Path.Combine(path, "NovaLib.TextUtilsStd.dll"), FileMode.Open));
				//reader.TryOpenAssociatedPortablePdb(Path.Combine(path, "NovaLib.TextUtilsStd.dll"),PdbStreamRed, out var newReader, out var pdbPath);
				//var mreader = newReader.GetMetadataReader();
				//var mrp = MetadataReaderProvider.FromPortablePdbStream(new FileStream(Path.Combine(path, "NovaLib.TextUtilsStd.pdb"), FileMode.Open));
				//mrp.GetMetadataReader();


					//mreader.
					//(ISymUnmanagedReader5)new SymBinder().GetReaderFromStream(
					//	pdbStream,
					//	SymUnmanagedReaderFactory.CreateSymReaderMetadataImport(metadataProvider));
					//var metadataProvider = new SymMetadataProvider();


				if (e.HResult == unchecked((int)0x806D000C))
					throw new Exception($"COMException: {e.Message} pdb file: {filePath} is in an invalid format, maybe your msdiag is out of date (or not registered, if only portable pdbs have been attempted), its a corrupt pdb (or portable pdb) or it is not meant for this system, pdb guid: {GetPDBGUID(filePath)}");
				//0x806D000C
				throw;
			}
			loader._source.openSession(out loader._session);

			return loader;
			//}
			//catch
			//{
			//    return null;
			//}
		}
		private static bool IsPortablePDB(string filePath) {
			using var pdbStream = File.OpenRead(filePath);
			var buff = new byte[4];
			if (pdbStream.Read(buff, 0, 4) != 4)
				return false;
			return Encoding.ASCII.GetString(buff).Equals("BSJB", StringComparison.OrdinalIgnoreCase);
		}


		private static Stream PdbStreamRed(string arg) {
			return new FileStream(arg, FileMode.Open);
		}

		public static Guid GetPDBGUID(String fileName) {
			using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read)) {
				using (BinaryReader binReader = new BinaryReader(fs)) {
					// This is where the GUID for the .pdb file is stored
					fs.Position = 0x00000a0c;

					//starts at 0xa0c but is pieced together up to 0xa1b
					byte[] guidBytes = binReader.ReadBytes(16);
					Guid pdbGuid = new Guid(guidBytes);
					return pdbGuid;
				}
			}
		}
		private static readonly Dictionary<Guid, string> s_msdiaGuids = new (){

			{ new Guid("E6756135-1E65-4D17-8576-610761398C3C"), "VS 2017/19/22 (msdia140.dll)" }, // VS 2017/19 (msdia140.dll)
            {new Guid("3BFCEA48-620F-4B6B-81F7-B9AF75454C7D"), "VS 2013 (msdia120.dll)"}, // VS 2013 (msdia120.dll)
            {new Guid("761D3BCD-1304-41D5-94E8-EAC54E4AC172"), "VS 2012 (msdia110.dll)"}, // VS 2012 (msdia110.dll)
            { new Guid("B86AE24D-BF2F-4AC9-B5A2-34B14E4CE11D"), "VS 2010 (msdia100.dll)"}, // VS 2010 (msdia100.dll)
            { new Guid("4C41678E-887B-4365-A09E-925D28DB33C2"), "VS 2008 (msdia90.dll)"}  // VS 2008 (msdia90.dll)

		};

		/// <summary>
		/// Helper to instantiate the DIA COM object. This depends on what
		/// version of msdiaxxx.dll is installed on the system. We must try
		/// each in order.
		/// </summary>
		/// <returns></returns>
		private static IDiaDataSource CoCreateDiaSource() {
			foreach (var msDiagVer in s_msdiaGuids) { 
				try {


					//if this fails need to call AS ADMIN: regsvr32 "c:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\ide\msdia140.dll"
					// or regsvr32 "C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\msdia140.dll"
					//return new DiaSource();
					var inst = (IDiaDataSource)Activator.CreateInstance(Type.GetTypeFromCLSID(msDiagVer.Key));
					Console.WriteLine($"Using msdiag: {msDiagVer.Value}");
					return inst;
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
			if (_sourceMR != null)
				return GetSourceLocByMetadataReader(methodMetadataToken, ilOffset);
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

		private SourceLocation GetSourceLocByMetadataReader(int methodMetadataToken, int ilOffset) {
			//_sourceMR.TryGetMethod
			//MethodId.FromToken();
			var handle = MetadataTokens.Handle(methodMetadataToken);
			var debugInfo = _sourceMR.GetMethodDebugInformation(((MethodDefinitionHandle)handle).ToDebugInformationHandle());
			var line = debugInfo.GetSequencePoints().Where(a=>a.IsHidden==false && a.Offset <= ilOffset).OrderByDescending(a=>a.Offset).FirstOrDefault();
			return new SourceLocation {LineNumber=line.StartLine,SourceFile= _sourceMR.GetString( _sourceMR.GetDocument( debugInfo.Document).Name) };


		}

		private void DebugWriteLineNumber(String what, uint rva) {
				IDiaEnumLineNumbers lineNumbers;
				_session.findLinesByRVA(rva, 1, out lineNumbers);
				foreach (IDiaLineNumber ln in lineNumbers)
					Debug.WriteLine($"{what} at {rva} For: {what} at {ln.lineNumber} -- {ln.sourceFile}");
			}
		}
	}
