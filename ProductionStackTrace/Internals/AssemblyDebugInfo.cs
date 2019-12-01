using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ProductionStackTrace.Internals {
	/// <summary>
	/// Represents the information about PDB associated with Assembly.
	/// </summary>
	internal class AssemblyDebugInfo {
		public Guid Guid { get; private set; }
		public UInt32 Age { get; private set; }
		public string Path { get; private set; }

		private AssemblyDebugInfo() {
		}

		/// <summary>
		/// Retrieve PDB information from Assembly, by reading it's PE header.
		/// </summary>
		/// <param name="assembly"></param>
		/// <returns></returns>
		public static AssemblyDebugInfo ReadAssemblyDebugInfo(Assembly assembly) {
			// The trick is that GetHINSTANCE returns the real HINSTANCE handler,
			// which in Win32 API world is module's base address in memory
			// http://msdn.microsoft.com/en-us/library/windows/desktop/aa383751(v=vs.85).aspx
			//
			// Which means for a loaded Assembly we can get an unmanaged pointer
			// right to the start of the PE (Portable Executable) header. Every
			// binary on Windows - DLL or EXE - following the PE format.
			// http://msdn.microsoft.com/en-us/library/windows/desktop/ms680547(v=vs.85).aspx

			var modulePtr = Marshal.GetHINSTANCE(assembly.ManifestModule);

			// Parses PE headers structure from the module base address pointer

			var peHdrs = PeHeaders.FromUnmanagedPtr(modulePtr);

			// Depending on whether this is 32-bit or 64-bit module, the offsets are
			// slightly different

			uint debugOffset, debugSize;

			if (peHdrs.Is32BitOptionalHeader && peHdrs.OptionalHeader32.NumberOfRvaAndSizes >= 7) {
				debugOffset = peHdrs.OptionalHeader32.Debug.VirtualAddress;
				debugSize = peHdrs.OptionalHeader32.Debug.Size;
			} else if (!peHdrs.Is32BitOptionalHeader && peHdrs.OptionalHeader64.NumberOfRvaAndSizes >= 7) {
				debugOffset = peHdrs.OptionalHeader64.Debug.VirtualAddress;
				debugSize = peHdrs.OptionalHeader64.Debug.Size;
			} else {
				// In case DEBUG information is not in the module

				return null;
			}

			// Navigating to the DEBUG_DIRECTORY portion, which holds the debug information

			var debugPtr = new IntPtr(modulePtr.ToInt64() + debugOffset);
			var debugDirectory = (IMAGE_DEBUG_DIRECTORY)Marshal.PtrToStructure(debugPtr, typeof(IMAGE_DEBUG_DIRECTORY));

			// Check that DEBUG information is there and that it is in CODEVIEW/RSDS format,
			// which is what's used by .NET framework

			if (debugDirectory.Type != IMAGE_DEBUG_TYPE_CODEVIEW) return null;
			var rsdsPtr = new IntPtr(modulePtr.ToInt64() + debugDirectory.AddressOfRawData);
			if (Marshal.ReadInt32(rsdsPtr) != RSDS_SIGNATURE) return null;

			// Read the RSDS info, which includes the PDB GUID and its Age, together with
			// PDB filepath

			var rsdsInfo = (RSDS_DEBUG_FORMAT)Marshal.PtrToStructure(rsdsPtr, typeof(RSDS_DEBUG_FORMAT));
			var pathPtr = new IntPtr(rsdsPtr.ToInt64() + Marshal.SizeOf(typeof(RSDS_DEBUG_FORMAT)));
			var path = Marshal.PtrToStringAnsi(pathPtr);

			return new AssemblyDebugInfo() {
				Guid = new Guid(rsdsInfo.Guid),
				Age = rsdsInfo.Age,
				Path = path
			};
		}

		#region Struct definitions

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct IMAGE_DEBUG_DIRECTORY {
			public UInt32 Characteristics;
			public UInt32 TimeDateStamp;
			public UInt16 MajorVersion;
			public UInt16 MinorVersion;
			public UInt32 Type;
			public UInt32 SizeOfData;
			public UInt32 AddressOfRawData;
			public UInt32 PointerToRawData;
		}

		private const UInt32 IMAGE_DEBUG_TYPE_CODEVIEW = 2;
		private const UInt32 RSDS_SIGNATURE = 0x53445352;

		// http://www.godevtool.com/Other/pdb.htm
		[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
		private struct RSDS_DEBUG_FORMAT {
			public UInt32 Signature; // RSDS
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
			public Byte[] Guid;
			public UInt32 Age;
		}

		#endregion
	}
}
