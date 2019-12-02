using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ProductionStackTrace.Analyze {
	public partial class SymbolSearch {


		private static class DbgHelp {

			[DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
			public static extern SymOptions SymSetOptions(SymOptions SymOptions);

			public const uint SYMOPT_DEBUG = 0x80000000;

			[DllImport("dbghelp.dll", CharSet = CharSet.Unicode)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool SymInitialize(
				IntPtr hProcess,
				[MarshalAs(UnmanagedType.LPTStr)] string UserSearchPath,
				[MarshalAs(UnmanagedType.Bool)] bool fInvadeProcess);

			[DllImport("dbghelp.dll", CharSet = CharSet.Unicode)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool SymCleanup(
				IntPtr hProcess);

			[DllImport("dbghelp.dll", CharSet = CharSet.Unicode)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool SymFindFileInPath(
			  IntPtr hProcess,
			  [MarshalAs(UnmanagedType.LPTStr)] string SearchPath,
			  [MarshalAs(UnmanagedType.LPTStr)] string FileName,
			  IntPtr id,
			  uint two,
			  uint three,
			  uint flags,
			  StringBuilder FilePath,
			  SymFindFileInPathProc callback,
			  IntPtr context
			);

			[DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)] // SetLastError=true, but if we set it to false we don't have to catch exceptions
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool SymSrvGetFileIndexInfo(string file, ref SYMSRV_INDEX_INFO info, [MarshalAs(UnmanagedType.U4)] int flags);

			[DllImport("dbghelp.dll", CharSet = CharSet.Unicode, SetLastError = true)]
			public static extern bool SymSrvGetFileIndexInfoW(String file, ref SYMSRV_INDEX_INFO info, uint flags);


			[return: MarshalAs(UnmanagedType.Bool)]
			public delegate bool SymFindFileInPathProc([MarshalAs(UnmanagedType.LPTStr)] string fileName, IntPtr context);

			public const uint SSRVOPT_GUIDPTR = 0x0008;

			[Flags]
			public enum SymOptions : uint {
				SYMOPT_ALLOW_ABSOLUTE_SYMBOLS = 0x00000800,
				SYMOPT_ALLOW_ZERO_ADDRESS = 0x01000000,
				SYMOPT_AUTO_PUBLICS = 0x00010000,
				SYMOPT_CASE_INSENSITIVE = 0x00000001,
				SYMOPT_DEBUG = 0x80000000,
				SYMOPT_DEFERRED_LOADS = 0x00000004,
				SYMOPT_DISABLE_SYMSRV_AUTODETECT = 0x02000000,
				SYMOPT_EXACT_SYMBOLS = 0x00000400,
				SYMOPT_FAIL_CRITICAL_ERRORS = 0x00000200,
				SYMOPT_FAVOR_COMPRESSED = 0x00800000,
				SYMOPT_FLAT_DIRECTORY = 0x00400000,
				SYMOPT_IGNORE_CVREC = 0x00000080,
				SYMOPT_IGNORE_IMAGEDIR = 0x00200000,
				SYMOPT_IGNORE_NT_SYMPATH = 0x00001000,
				SYMOPT_INCLUDE_32BIT_MODULES = 0x00002000,
				SYMOPT_LOAD_ANYTHING = 0x00000040,
				SYMOPT_LOAD_LINES = 0x00000010,
				SYMOPT_NO_CPP = 0x00000008,
				SYMOPT_NO_IMAGE_SEARCH = 0x00020000,
				SYMOPT_NO_PROMPTS = 0x00080000,
				SYMOPT_NO_PUBLICS = 0x00008000,
				SYMOPT_NO_UNQUALIFIED_LOADS = 0x00000100,
				SYMOPT_OVERWRITE = 0x00100000,
				SYMOPT_PUBLICS_ONLY = 0x00004000,
				SYMOPT_SECURE = 0x00040000,
				SYMOPT_UNDNAME = 0x00000002,
			};

			[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
			public struct SYMSRV_INDEX_INFO {
				public int sizeofstruct;
				[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 261)]
				public string file;
				public UInt32 stripped;
				public UInt32 timestamp;
				public Int32 size;
				[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 261)]
				public string dbgfile;
				[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 261)]
				public string pdbfile;
				public Guid guid;
				public Int32 sig;
				public Int32 age;
			}
			[StructLayout(LayoutKind.Sequential, Pack = 1)]
			public struct API_VERSION {
				public ushort MajorVersion;
				public ushort MinorVersion;
				public ushort Revision;
				public ushort Reserved;
			}
			[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
			public static extern bool SetDllDirectory(string lpPathName);

			[DllImport("dbghelp.dll", SetLastError = true, PreserveSig = true, CharSet = CharSet.Unicode, EntryPoint = "ImagehlpApiVersionEx")]
			internal static extern IntPtr ImagehlpApiVersionEx_Internal(ref API_VERSION AppVersion);
			public static API_VERSION ImagehlpApiVersionEx(ref API_VERSION AppVersion) {
				API_VERSION val = (API_VERSION)Marshal.PtrToStructure(ImagehlpApiVersionEx_Internal(ref AppVersion), typeof(API_VERSION));
				return val;
			}
			internal delegate bool SymRegisterCallbackProc64(IntPtr hProcess, DebugAction action_code, ulong callback_data, ulong user_context);

			[DllImport("DbgHelp.dll")]
			internal static extern bool SymRegisterCallback64(IntPtr hProcess, SymRegisterCallbackProc64 callback, ulong user_context);

			public enum DebugAction : uint {
				/// <summary>
				/// Display verbose information.
				/// </summary>
				/// <remarks>The CallbackData parameter is a pointer to a string.</remarks>
				CBA_DEBUG_INFO = 0x10000000,

				/// <summary>
				/// Deferred symbol loading has started. To cancel the symbol load, return TRUE.
				/// </summary>
				/// <remarks>The CallbackData parameter is a pointer to a IMAGEHLP_DEFERRED_SYMBOL_LOAD64 structure.</remarks>
				CBA_DEFERRED_SYMBOL_LOAD_CANCEL = 0x00000007,

				/// <summary>
				/// Deferred symbol load has completed.
				/// </summary>
				/// <remarks>The CallbackData parameter is a pointer to a IMAGEHLP_DEFERRED_SYMBOL_LOAD64 structure.</remarks>
				CBA_DEFERRED_SYMBOL_LOAD_COMPLETE = 0x00000002,

				/// <summary>
				/// Deferred symbol load has failed.
				/// </summary>
				/// <remarks>
				/// The CallbackData parameter is a pointer to a IMAGEHLP_DEFERRED_SYMBOL_LOAD64 structure. The symbol handler will attempt to load the symbols again if the callback function sets the FileName member of this structure.
				/// </remarks>
				CBA_DEFERRED_SYMBOL_LOAD_FAILURE = 0x00000003,

				/// <summary>
				/// Deferred symbol load has partially completed. The symbol loader is unable to read the image header from either the image file or the specified module.
				/// </summary>
				/// <remarks>
				/// The CallbackData parameter is a pointer to a IMAGEHLP_DEFERRED_SYMBOL_LOAD64 structure. The symbol handler will attempt to load the symbols again if the callback function sets the FileName member of this structure.
				/// DbgHelp 5.1:  This value is not supported.
				/// </remarks>
				CBA_DEFERRED_SYMBOL_LOAD_PARTIAL = 0x00000020,

				/// <summary>
				/// Deferred symbol load has started.
				/// </summary>
				/// <remarks>
				/// The CallbackData parameter is a pointer to a IMAGEHLP_DEFERRED_SYMBOL_LOAD64 structure.
				/// </remarks>
				CBA_DEFERRED_SYMBOL_LOAD_START = 0x00000001,

				/// <summary>
				/// Duplicate symbols were found. This reason is used only in COFF or CodeView format.
				/// </summary>
				/// <remarks>
				/// The CallbackData parameter is a pointer to a IMAGEHLP_DUPLICATE_SYMBOL64 structure. To specify which symbol to use, set the SelectedSymbol member of this structure.
				/// </remarks>
				CBA_DUPLICATE_SYMBOL = 0x00000005,

				/// <summary>
				/// Display verbose information. If you do not handle this event, the information is resent through the CBA_DEBUG_INFO event.
				/// </summary>
				/// <remarks>
				/// The CallbackData parameter is a pointer to a IMAGEHLP_CBA_EVENT structure. 
				/// </remarks>
				CBA_EVENT = 0x00000010,

				/// <summary>
				/// The loaded image has been read.
				/// </summary>
				/// <remarks>
				/// The CallbackData parameter is a pointer to a IMAGEHLP_CBA_READ_MEMORY structure. The callback function should read the number of bytes specified by the bytes member into the buffer specified by the buf member, and update the bytesread member accordingly.
				/// </remarks>
				CBA_READ_MEMORY = 0x00000006,

				/// <summary>
				/// Symbol options have been updated. To retrieve the current options, call the SymGetOptions function.
				/// </summary>
				/// <remarks>
				/// The CallbackData parameter should be ignored.
				/// </remarks>
				CBA_SET_OPTIONS = 0x00000008,

				/// <summary>
				/// Display verbose information for source server. If you do not handle this event, the information is resent through the CBA_DEBUG_INFO event.
				/// </summary>
				/// <remarks>
				/// The CallbackData parameter is a pointer to a IMAGEHLP_CBA_EVENT structure.
				/// DbgHelp 6.6 and earlier:  This value is not supported.
				/// </remarks>
				CBA_SRCSRV_EVENT = 0x40000000,

				/// <summary>
				/// Display verbose information for source server.
				/// </summary>
				/// <remarks>
				/// The CallbackData parameter is a pointer to a string.
				/// DbgHelp 6.6 and earlier:  This value is not supported.
				/// </remarks>
				CBA_SRCSRV_INFO = 0x20000000,

				/// <summary>
				/// Symbols have been unloaded.
				/// </summary>
				/// <remarks>
				/// The CallbackData parameter should be ignored.
				/// </remarks>
				CBA_SYMBOLS_UNLOADED = 0x00000004,
			}
		}
	}
}
