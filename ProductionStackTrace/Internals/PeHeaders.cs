﻿using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ProductionStackTrace.Internals {
	/// <summary>
	/// Reads in the header information of the Portable Executable format.
	/// Provides information such as the date the assembly was compiled.
	/// 
	/// Adapted from: http://code.cheesydesign.com/?p=572
	/// </summary>
	internal class PeHeaders {
		#region File Header Structures

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_DOS_HEADER {      // DOS .EXE header
			public UInt16 e_magic;              // Magic number
			public UInt16 e_cblp;               // Bytes on last page of file
			public UInt16 e_cp;                 // Pages in file
			public UInt16 e_crlc;               // Relocations
			public UInt16 e_cparhdr;            // Size of header in paragraphs
			public UInt16 e_minalloc;           // Minimum extra paragraphs needed
			public UInt16 e_maxalloc;           // Maximum extra paragraphs needed
			public UInt16 e_ss;                 // Initial (relative) SS value
			public UInt16 e_sp;                 // Initial SP value
			public UInt16 e_csum;               // Checksum
			public UInt16 e_ip;                 // Initial IP value
			public UInt16 e_cs;                 // Initial (relative) CS value
			public UInt16 e_lfarlc;             // File address of relocation table
			public UInt16 e_ovno;               // Overlay number
			public UInt16 e_res_0;              // Reserved words
			public UInt16 e_res_1;              // Reserved words
			public UInt16 e_res_2;              // Reserved words
			public UInt16 e_res_3;              // Reserved words
			public UInt16 e_oemid;              // OEM identifier (for e_oeminfo)
			public UInt16 e_oeminfo;            // OEM information; e_oemid specific
			public UInt16 e_res2_0;             // Reserved words
			public UInt16 e_res2_1;             // Reserved words
			public UInt16 e_res2_2;             // Reserved words
			public UInt16 e_res2_3;             // Reserved words
			public UInt16 e_res2_4;             // Reserved words
			public UInt16 e_res2_5;             // Reserved words
			public UInt16 e_res2_6;             // Reserved words
			public UInt16 e_res2_7;             // Reserved words
			public UInt16 e_res2_8;             // Reserved words
			public UInt16 e_res2_9;             // Reserved words
			public UInt32 e_lfanew;             // File address of new exe header
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_DATA_DIRECTORY {
			public UInt32 VirtualAddress;
			public UInt32 Size;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct IMAGE_OPTIONAL_HEADER32 {
			public UInt16 Magic;
			public Byte MajorLinkerVersion;
			public Byte MinorLinkerVersion;
			public UInt32 SizeOfCode;
			public UInt32 SizeOfInitializedData;
			public UInt32 SizeOfUninitializedData;
			public UInt32 AddressOfEntryPoint;
			public UInt32 BaseOfCode;
			public UInt32 BaseOfData;
			public UInt32 ImageBase;
			public UInt32 SectionAlignment;
			public UInt32 FileAlignment;
			public UInt16 MajorOperatingSystemVersion;
			public UInt16 MinorOperatingSystemVersion;
			public UInt16 MajorImageVersion;
			public UInt16 MinorImageVersion;
			public UInt16 MajorSubsystemVersion;
			public UInt16 MinorSubsystemVersion;
			public UInt32 Win32VersionValue;
			public UInt32 SizeOfImage;
			public UInt32 SizeOfHeaders;
			public UInt32 CheckSum;
			public UInt16 Subsystem;
			public UInt16 DllCharacteristics;
			public UInt32 SizeOfStackReserve;
			public UInt32 SizeOfStackCommit;
			public UInt32 SizeOfHeapReserve;
			public UInt32 SizeOfHeapCommit;
			public UInt32 LoaderFlags;
			public UInt32 NumberOfRvaAndSizes;

			public IMAGE_DATA_DIRECTORY ExportTable;
			public IMAGE_DATA_DIRECTORY ImportTable;
			public IMAGE_DATA_DIRECTORY ResourceTable;
			public IMAGE_DATA_DIRECTORY ExceptionTable;
			public IMAGE_DATA_DIRECTORY CertificateTable;
			public IMAGE_DATA_DIRECTORY BaseRelocationTable;
			public IMAGE_DATA_DIRECTORY Debug;
			public IMAGE_DATA_DIRECTORY Architecture;
			public IMAGE_DATA_DIRECTORY GlobalPtr;
			public IMAGE_DATA_DIRECTORY TLSTable;
			public IMAGE_DATA_DIRECTORY LoadConfigTable;
			public IMAGE_DATA_DIRECTORY BoundImport;
			public IMAGE_DATA_DIRECTORY IAT;
			public IMAGE_DATA_DIRECTORY DelayImportDescriptor;
			public IMAGE_DATA_DIRECTORY CLRRuntimeHeader;
			public IMAGE_DATA_DIRECTORY Reserved;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct IMAGE_OPTIONAL_HEADER64 {
			public UInt16 Magic;
			public Byte MajorLinkerVersion;
			public Byte MinorLinkerVersion;
			public UInt32 SizeOfCode;
			public UInt32 SizeOfInitializedData;
			public UInt32 SizeOfUninitializedData;
			public UInt32 AddressOfEntryPoint;
			public UInt32 BaseOfCode;
			public UInt64 ImageBase;
			public UInt32 SectionAlignment;
			public UInt32 FileAlignment;
			public UInt16 MajorOperatingSystemVersion;
			public UInt16 MinorOperatingSystemVersion;
			public UInt16 MajorImageVersion;
			public UInt16 MinorImageVersion;
			public UInt16 MajorSubsystemVersion;
			public UInt16 MinorSubsystemVersion;
			public UInt32 Win32VersionValue;
			public UInt32 SizeOfImage;
			public UInt32 SizeOfHeaders;
			public UInt32 CheckSum;
			public UInt16 Subsystem;
			public UInt16 DllCharacteristics;
			public UInt64 SizeOfStackReserve;
			public UInt64 SizeOfStackCommit;
			public UInt64 SizeOfHeapReserve;
			public UInt64 SizeOfHeapCommit;
			public UInt32 LoaderFlags;
			public UInt32 NumberOfRvaAndSizes;

			public IMAGE_DATA_DIRECTORY ExportTable;
			public IMAGE_DATA_DIRECTORY ImportTable;
			public IMAGE_DATA_DIRECTORY ResourceTable;
			public IMAGE_DATA_DIRECTORY ExceptionTable;
			public IMAGE_DATA_DIRECTORY CertificateTable;
			public IMAGE_DATA_DIRECTORY BaseRelocationTable;
			public IMAGE_DATA_DIRECTORY Debug;
			public IMAGE_DATA_DIRECTORY Architecture;
			public IMAGE_DATA_DIRECTORY GlobalPtr;
			public IMAGE_DATA_DIRECTORY TLSTable;
			public IMAGE_DATA_DIRECTORY LoadConfigTable;
			public IMAGE_DATA_DIRECTORY BoundImport;
			public IMAGE_DATA_DIRECTORY IAT;
			public IMAGE_DATA_DIRECTORY DelayImportDescriptor;
			public IMAGE_DATA_DIRECTORY CLRRuntimeHeader;
			public IMAGE_DATA_DIRECTORY Reserved;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct IMAGE_FILE_HEADER {
			public UInt16 Machine;
			public UInt16 NumberOfSections;
			public UInt32 TimeDateStamp;
			public UInt32 PointerToSymbolTable;
			public UInt32 NumberOfSymbols;
			public UInt16 SizeOfOptionalHeader;
			public UInt16 Characteristics;
		}

		// Grabbed the following 2 definitions from http://www.pinvoke.net/default.aspx/Structures/IMAGE_SECTION_HEADER.html

		[StructLayout(LayoutKind.Explicit)]
		public struct IMAGE_SECTION_HEADER {
			[FieldOffset(0)]
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
			public char[] Name;
			[FieldOffset(8)]
			public UInt32 VirtualSize;
			[FieldOffset(12)]
			public UInt32 VirtualAddress;
			[FieldOffset(16)]
			public UInt32 SizeOfRawData;
			[FieldOffset(20)]
			public UInt32 PointerToRawData;
			[FieldOffset(24)]
			public UInt32 PointerToRelocations;
			[FieldOffset(28)]
			public UInt32 PointerToLinenumbers;
			[FieldOffset(32)]
			public UInt16 NumberOfRelocations;
			[FieldOffset(34)]
			public UInt16 NumberOfLinenumbers;
			[FieldOffset(36)]
			public DataSectionFlags Characteristics;

			public string Section {
				get { return new string(Name); }
			}
		}

		[Flags]
		public enum DataSectionFlags : uint {
			/// <summary>
			/// Reserved for future use.
			/// </summary>
			TypeReg = 0x00000000,
			/// <summary>
			/// Reserved for future use.
			/// </summary>
			TypeDsect = 0x00000001,
			/// <summary>
			/// Reserved for future use.
			/// </summary>
			TypeNoLoad = 0x00000002,
			/// <summary>
			/// Reserved for future use.
			/// </summary>
			TypeGroup = 0x00000004,
			/// <summary>
			/// The section should not be padded to the next boundary. This flag is obsolete and is replaced by IMAGE_SCN_ALIGN_1BYTES. This is valid only for object files.
			/// </summary>
			TypeNoPadded = 0x00000008,
			/// <summary>
			/// Reserved for future use.
			/// </summary>
			TypeCopy = 0x00000010,
			/// <summary>
			/// The section contains executable code.
			/// </summary>
			ContentCode = 0x00000020,
			/// <summary>
			/// The section contains initialized data.
			/// </summary>
			ContentInitializedData = 0x00000040,
			/// <summary>
			/// The section contains uninitialized data.
			/// </summary>
			ContentUninitializedData = 0x00000080,
			/// <summary>
			/// Reserved for future use.
			/// </summary>
			LinkOther = 0x00000100,
			/// <summary>
			/// The section contains comments or other information. The .drectve section has this type. This is valid for object files only.
			/// </summary>
			LinkInfo = 0x00000200,
			/// <summary>
			/// Reserved for future use.
			/// </summary>
			TypeOver = 0x00000400,
			/// <summary>
			/// The section will not become part of the image. This is valid only for object files.
			/// </summary>
			LinkRemove = 0x00000800,
			/// <summary>
			/// The section contains COMDAT data. For more information, see section 5.5.6, COMDAT Sections (Object Only). This is valid only for object files.
			/// </summary>
			LinkComDat = 0x00001000,
			/// <summary>
			/// Reset speculative exceptions handling bits in the TLB entries for this section.
			/// </summary>
			NoDeferSpecExceptions = 0x00004000,
			/// <summary>
			/// The section contains data referenced through the global pointer (GP).
			/// </summary>
			RelativeGP = 0x00008000,
			/// <summary>
			/// Reserved for future use.
			/// </summary>
			MemPurgeable = 0x00020000,
			/// <summary>
			/// Reserved for future use.
			/// </summary>
			Memory16Bit = 0x00020000,
			/// <summary>
			/// Reserved for future use.
			/// </summary>
			MemoryLocked = 0x00040000,
			/// <summary>
			/// Reserved for future use.
			/// </summary>
			MemoryPreload = 0x00080000,
			/// <summary>
			/// Align data on a 1-byte boundary. Valid only for object files.
			/// </summary>
			Align1Bytes = 0x00100000,
			/// <summary>
			/// Align data on a 2-byte boundary. Valid only for object files.
			/// </summary>
			Align2Bytes = 0x00200000,
			/// <summary>
			/// Align data on a 4-byte boundary. Valid only for object files.
			/// </summary>
			Align4Bytes = 0x00300000,
			/// <summary>
			/// Align data on an 8-byte boundary. Valid only for object files.
			/// </summary>
			Align8Bytes = 0x00400000,
			/// <summary>
			/// Align data on a 16-byte boundary. Valid only for object files.
			/// </summary>
			Align16Bytes = 0x00500000,
			/// <summary>
			/// Align data on a 32-byte boundary. Valid only for object files.
			/// </summary>
			Align32Bytes = 0x00600000,
			/// <summary>
			/// Align data on a 64-byte boundary. Valid only for object files.
			/// </summary>
			Align64Bytes = 0x00700000,
			/// <summary>
			/// Align data on a 128-byte boundary. Valid only for object files.
			/// </summary>
			Align128Bytes = 0x00800000,
			/// <summary>
			/// Align data on a 256-byte boundary. Valid only for object files.
			/// </summary>
			Align256Bytes = 0x00900000,
			/// <summary>
			/// Align data on a 512-byte boundary. Valid only for object files.
			/// </summary>
			Align512Bytes = 0x00A00000,
			/// <summary>
			/// Align data on a 1024-byte boundary. Valid only for object files.
			/// </summary>
			Align1024Bytes = 0x00B00000,
			/// <summary>
			/// Align data on a 2048-byte boundary. Valid only for object files.
			/// </summary>
			Align2048Bytes = 0x00C00000,
			/// <summary>
			/// Align data on a 4096-byte boundary. Valid only for object files.
			/// </summary>
			Align4096Bytes = 0x00D00000,
			/// <summary>
			/// Align data on an 8192-byte boundary. Valid only for object files.
			/// </summary>
			Align8192Bytes = 0x00E00000,
			/// <summary>
			/// The section contains extended relocations.
			/// </summary>
			LinkExtendedRelocationOverflow = 0x01000000,
			/// <summary>
			/// The section can be discarded as needed.
			/// </summary>
			MemoryDiscardable = 0x02000000,
			/// <summary>
			/// The section cannot be cached.
			/// </summary>
			MemoryNotCached = 0x04000000,
			/// <summary>
			/// The section is not pageable.
			/// </summary>
			MemoryNotPaged = 0x08000000,
			/// <summary>
			/// The section can be shared in memory.
			/// </summary>
			MemoryShared = 0x10000000,
			/// <summary>
			/// The section can be executed as code.
			/// </summary>
			MemoryExecute = 0x20000000,
			/// <summary>
			/// The section can be read.
			/// </summary>
			MemoryRead = 0x40000000,
			/// <summary>
			/// The section can be written to.
			/// </summary>
			MemoryWrite = 0x80000000
		}

		private const UInt16 IMAGE_NT_OPTIONAL_HDR32_MAGIC = 0x10b;
		private const UInt16 IMAGE_NT_OPTIONAL_HDR64_MAGIC = 0x20b;

		#endregion File Header Structures

		#region Private Fields

		private bool _is32bit;
		/// <summary>
		/// The DOS header
		/// </summary>
		private IMAGE_DOS_HEADER dosHeader;
		/// <summary>
		/// The file header
		/// </summary>
		private IMAGE_FILE_HEADER fileHeader;
		/// <summary>
		/// Optional 32 bit file header 
		/// </summary>
		private IMAGE_OPTIONAL_HEADER32 optionalHeader32;
		/// <summary>
		/// Optional 64 bit file header 
		/// </summary>
		private IMAGE_OPTIONAL_HEADER64 optionalHeader64;
		/// <summary>
		/// Image Section headers. Number of sections is in the file header.
		/// </summary>
		private IMAGE_SECTION_HEADER[] imageSectionHeaders;

		#endregion Private Fields

		#region Public Methods

		private PeHeaders() {
		}

		public static PeHeaders FromAssembly(Assembly assembly) {
			var modulePtr = Marshal.GetHINSTANCE(assembly.ManifestModule);
			return FromUnmanagedPtr(modulePtr);
		}

		public static PeHeaders FromUnmanagedPtr(IntPtr memoryPtr) {
			var headers = new PeHeaders();
			headers.Load(memoryPtr, 0);
			return headers;
		}

		#endregion Public Methods

		#region Parsing

		private void Load(Stream stream) {
			BinaryReader reader = new BinaryReader(stream);
			dosHeader = FromBinaryReader<IMAGE_DOS_HEADER>(reader, null);

			// Add 4 bytes to the offset
			stream.Seek(dosHeader.e_lfanew, SeekOrigin.Begin);
			UInt32 ntHeadersSignature = reader.ReadUInt32();

			fileHeader = FromBinaryReader<IMAGE_FILE_HEADER>(reader, null);

			// Read optional header magic to determin 32-bit vs 64-bit
			var optMagic = reader.ReadUInt16();
			_is32bit = (optMagic != IMAGE_NT_OPTIONAL_HDR64_MAGIC);
			var lookahead = new byte[] { (byte)(optMagic >> 8), (byte)(optMagic & 0xff) };

			if (_is32bit) {
				optionalHeader32 = FromBinaryReader<IMAGE_OPTIONAL_HEADER32>(reader, lookahead);
			} else {
				optionalHeader64 = FromBinaryReader<IMAGE_OPTIONAL_HEADER64>(reader, lookahead);
			}

			imageSectionHeaders = new IMAGE_SECTION_HEADER[fileHeader.NumberOfSections];
			for (int headerNo = 0; headerNo < imageSectionHeaders.Length; ++headerNo) {
				imageSectionHeaders[headerNo] = FromBinaryReader<IMAGE_SECTION_HEADER>(reader, null);
			}
		}

		/// <summary>
		/// Reading from unmanaged memory pointer address.
		/// </summary>
		/// <param name="memPtr"></param>
		/// <param name="index"></param>
		private void Load(IntPtr memPtr, long index) {
			var startIndex = index;
			dosHeader = FromMemoryPtr<IMAGE_DOS_HEADER>(memPtr, ref index);
			index = startIndex + dosHeader.e_lfanew + 4;

			fileHeader = FromMemoryPtr<IMAGE_FILE_HEADER>(memPtr, ref index);

			// See the optiona header magic to determine 32-bit vs 64-bit
			var optMagic = Marshal.ReadInt16(new IntPtr(memPtr.ToInt64() + index));
			_is32bit = (optMagic != IMAGE_NT_OPTIONAL_HDR64_MAGIC);

			if (_is32bit) {
				optionalHeader32 = FromMemoryPtr<IMAGE_OPTIONAL_HEADER32>(memPtr, ref index);
			} else {
				optionalHeader64 = FromMemoryPtr<IMAGE_OPTIONAL_HEADER64>(memPtr, ref index);
			}

			imageSectionHeaders = new IMAGE_SECTION_HEADER[fileHeader.NumberOfSections];
			for (int headerNo = 0; headerNo < imageSectionHeaders.Length; ++headerNo) {
				imageSectionHeaders[headerNo] = FromMemoryPtr<IMAGE_SECTION_HEADER>(memPtr, ref index);
			}
		}

		private static T FromBinaryReader<T>(BinaryReader reader, byte[] lookahead) {
			// Read in a byte array
			byte[] bytes = new byte[Marshal.SizeOf(typeof(T))];
			var offset = 0;
			if (lookahead != null) {
				Array.Copy(lookahead, bytes, 0);
				offset = lookahead.Length;
			}
			reader.Read(bytes, offset, bytes.Length - offset);

			// Pin the managed memory while, copy it out the data, then unpin it
			GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
			T theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
			handle.Free();

			return theStructure;
		}

		private static T FromMemoryPtr<T>(IntPtr memPtr, ref long index) {
			var obj = (T)Marshal.PtrToStructure(new IntPtr(memPtr.ToInt64() + index), typeof(T));
			index += Marshal.SizeOf(typeof(T));
			return obj;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets if the optional header is 32 bit or not
		/// </summary>
		public bool Is32BitOptionalHeader {
			get {
				return _is32bit;
			}
		}

		/// <summary>
		/// Gets the file header
		/// </summary>
		public IMAGE_FILE_HEADER FileHeader {
			get {
				return fileHeader;
			}
		}

		/// <summary>
		/// Gets the optional header
		/// </summary>
		public IMAGE_OPTIONAL_HEADER32 OptionalHeader32 {
			get {
				return optionalHeader32;
			}
		}

		/// <summary>
		/// Gets the optional header
		/// </summary>
		public IMAGE_OPTIONAL_HEADER64 OptionalHeader64 {
			get {
				return optionalHeader64;
			}
		}

		public IMAGE_SECTION_HEADER[] ImageSectionHeaders {
			get {
				return imageSectionHeaders;
			}
		}

		/// <summary>
		/// Gets the timestamp from the file header
		/// </summary>
		public DateTime TimeStamp {
			get {
				// Timestamp is a date offset from 1970
				DateTime returnValue = new DateTime(1970, 1, 1, 0, 0, 0);

				// Add in the number of seconds since 1970/1/1
				returnValue = returnValue.AddSeconds(fileHeader.TimeDateStamp);
				// Adjust to local timezone
				returnValue += TimeZone.CurrentTimeZone.GetUtcOffset(returnValue);

				return returnValue;
			}
		}

		#endregion Properties
	}
}
