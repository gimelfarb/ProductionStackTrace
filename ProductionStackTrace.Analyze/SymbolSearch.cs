using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace ProductionStackTrace.Analyze
{
    /// <summary>
    /// Managed wrapper around dbghelp.dll (Windows SDK) calls to 
    /// SymFindFileInPath which implements the logic of find a symbol
    /// file based on input search criteria.
    /// </summary>
    public class SymbolSearch
    {
        #region DbgHelp

        private static class DbgHelp
        {
            [DllImport("dbghelp.dll", CharSet = CharSet.Unicode)]
            public static extern uint SymSetOptions(
                uint options);

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

            [return: MarshalAs(UnmanagedType.Bool)]
            public delegate bool SymFindFileInPathProc([MarshalAs(UnmanagedType.LPTStr)] string fileName, IntPtr context);

            public const uint SSRVOPT_GUIDPTR = 0x0008;
        }

        #endregion

        /// <summary>
        /// Initialize a new instance of <see cref="SymbolSearch"/>.
        /// </summary>
        public SymbolSearch()
        {
            this.SymbolPaths = new List<string>();
            this.SymbolPaths.Add(".");
        }

        /// <summary>
        /// A list of paths to search. These can also be 'srv*' special
        /// paths to reference a symbols server.
        /// See http://msdn.microsoft.com/en-us/library/windows/desktop/ms680689(v=vs.85).aspx
        /// </summary>
        public List<string> SymbolPaths { get; private set; }

        /// <summary>
        /// Looks through the configured symbol paths to find a PDB symbol
        /// file matching specified name, GUID and age parameters.
        /// </summary>
        /// <param name="pdbFileName"></param>
        /// <param name="guid"></param>
        /// <param name="age"></param>
        /// <returns></returns>
        /// <remarks>
        /// Note how we don't have to have a module loaded to find its PDB,
        /// we just look for a PDB file using name, GUID and age. These are
        /// the parameters that can be extracted from module's PE header,
        /// in the Debug Directory section.
        /// </remarks>
        public string FindPdbFile(string pdbFileName, Guid guid, int age)
        {
            DbgHelp.SymSetOptions(DbgHelp.SYMOPT_DEBUG);

            var hProcess = new IntPtr(1);
#if TARGET_NET_20
            var searchPath = string.Join(";", this.SymbolPaths.ToArray());
#else
            var searchPath = string.Join(";", this.SymbolPaths);
#endif
            DbgHelp.SymInitialize(hProcess, searchPath, false);

            var filePath = new StringBuilder(256);
            var guidHandle = GCHandle.Alloc(guid, GCHandleType.Pinned);
            try
            {
                if (!DbgHelp.SymFindFileInPath(hProcess, null, pdbFileName,
                    guidHandle.AddrOfPinnedObject(), (uint)age, 0,
                    DbgHelp.SSRVOPT_GUIDPTR, filePath, null, IntPtr.Zero))
                    return null;
            }
            finally
            {
                guidHandle.Free();
                DbgHelp.SymCleanup(hProcess);
            }

            return filePath.ToString();
        }
    }
}
