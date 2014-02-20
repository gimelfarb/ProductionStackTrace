using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Dia2Lib;

namespace ProductionStackTrace.Analyze
{
    /// <summary>
    /// Managed wrapper around DIA SDK (Debug Information Access)
    /// COM interfaces to load information from a PDB symbols file.
    /// </summary>
    public class SymbolLoader
    {
        // NOTE: Since this class is intended to be used on the analyzer
        // side, we rely on GC to cleanup and release COM references to
        // this RCW objects.

        IDiaDataSource _source;
        IDiaSession _session;

        private SymbolLoader()
        {
        }

        /// <summary>
        /// Open and load symbol information from the specified
        /// PDB file path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static SymbolLoader Load(string filePath)
        {
            try
            {
                var loader = new SymbolLoader();
                loader._source = CoCreateDiaSource();

                loader._source.loadDataFromPdb(filePath);
                loader._source.openSession(out loader._session);
                return loader;
            }
            catch
            {
                return null;
            }
        }

        private static readonly Guid[] s_msdiaGuids = new[] {
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
        private static IDiaDataSource CoCreateDiaSource()
        {
            var i = 0;
            while (true)
            {
                try
                {
                    return (IDiaDataSource)Activator.CreateInstance(Type.GetTypeFromCLSID(s_msdiaGuids[i]));
                }
                catch (COMException)
                {
                    if (++i >= s_msdiaGuids.Length) throw;
                }
            }
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
        public SourceLocation GetSourceLoc(int methodMetadataToken, int ilOffset)
        {
            IDiaSymbol symMethod;
            _session.findSymbolByToken((uint)methodMetadataToken, SymTagEnum.SymTagFunction, out symMethod);

            var rvaMethod = symMethod.relativeVirtualAddress;
            rvaMethod += (uint)ilOffset;

            IDiaEnumLineNumbers lineNumbers;
            _session.findLinesByRVA(rvaMethod, 1, out lineNumbers);

            foreach (IDiaLineNumber ln in lineNumbers)
            {
                var sourceFile = ln.sourceFile;
                return new SourceLocation() { LineNumber = ln.lineNumber, SourceFile = (sourceFile == null) ? null : sourceFile.fileName };
            }

            return null;
        }
    }
}
