using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
                loader._source = new DiaSource();

                loader._source.loadDataFromPdb(filePath);
                loader._source.openSession(out loader._session);
                return loader;
            }
            catch
            {
                return null;
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
