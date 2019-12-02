using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ProductionStackTrace.Analyze
{
    /// <summary>
    /// Implements main logic for parsing exception report and retrieving
    /// associated PDB symbols information to display original source file
    /// and line number for each stack frame.
    /// </summary>
    public class ExceptionReportInterpreter
    {
        // Regex for the stack frame line, it looks like this:
        // <space> at AssemblyName!0x<hex method token>!Namespace.Class.Method(<arg types>) +0x<hex IL Offset>
        //         at MyAssembly!0x1234567!MyAssembly.Class1.A() +0xc

        private static readonly Regex s_regexStackTraceLine =
            new Regex(@"^\s*at\s+(?<Assembly>[^<>:\""/\\|?*\u0000-\u001f!]+)!0x(?<MDToken>[0-9a-f]+)!(?<Method>[^\(]+)\((?<Args>[^\)]*)\)\s+\+0x(?<ILOffset>[0-9a-f]+)", RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // Regex to detect a separator line ('=================')

        private static readonly Regex s_regexFooterSeparator =
            new Regex(@"={3,}");

        // Regex for the assembly info mapping section, which looks like this:
        // MODULE: AssemblyName => AssemblyFullyQualifiedName; G:27657a27fg376787d6; A:1

        private static readonly Regex s_regexAssemblyMapping =
            new Regex(@"MODULE: (?<Assembly>[^\s]+(?=\s+\=>))\s+\=>\s+(?<AssemblyFQN>[^;]+)(;\s+(?<KeyValue>[a-z]+\:[^;]+))+", RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

		private static readonly Regex s_regexVersionMapping = new Regex(@"Version=(?<Version>[0-9\.]+)");

        private SymbolSearch _symSearch;

        /// <summary>
        /// Initialize a new instance of <see cref="ExceptionReportInterpreter"/>.
        /// </summary>
        public ExceptionReportInterpreter()
        {
            _symSearch = new SymbolSearch();
        }
		public string AltDbgHelpDll;

        /// <summary>
        /// List of symbol search paths to pass to <see cref="SymbolSearch.SymbolPaths"/>.
        /// </summary>
        public Microsoft.Diagnostics.Symbols.SymbolPath SymbolPaths { get { return _symSearch.SymbolPaths; } }

		public string ExceptionFileToRead { get; set; }

		public void ClearSymbolPaths() => _symSearch.ClearSymbolPaths();

		/// <summary>
		/// Helper to hold info about the assembly from parsed report.
		/// </summary>
		public class AssemblyMappedInfo
        {
            public string FullyQualifiedName = string.Empty;
			public string AssemblyName;
			public string PdbName => AssemblyName + ".pdb";
			public string Version;
            public Dictionary<string, string> Attributes = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            public Guid PdbGuid;
            public int PdbAge;
            public string PdbPath;

            // If GUID + Age are specified, we should be able to locate the matching PDB
            public bool fPdbSpecified;
            public SymbolLoader PdbSymbolLoader;
        }

        /// <summary>
        /// Convert exception stack trace report from the format produced by
        /// ExceptionReporting.GetExceptionReport (ProductionStackTrace), with no source mapping,
        /// to a standard stack trace containing original source and line number mapping.
        /// </summary>
        /// <param name="r">Input stack trace report</param>
        /// <param name="w">Output standard stack trace</param>
        public void Translate(TextReader r, TextWriter w)
        {
            var mapping = new Dictionary<string, AssemblyMappedInfo>(StringComparer.OrdinalIgnoreCase);

            var line = String.Empty;
            var linesBuf = new List<string>(32);
            while ((line = r.ReadLine()) != null)
            {
                // Read lines into memory until separator '=========' is found

                var m = s_regexFooterSeparator.Match(line);
                if (m != Match.Empty)
                {
                    // Now process all of the 'MODULE: ' mapping lines from the
                    // footer section

                    while ((line = r.ReadLine()) != null)
                    {
                        m = s_regexAssemblyMapping.Match(line);
                        if (m != Match.Empty)
                        {
                            // Extract the Fully Qualified Assembly Name, and also GUID and Age parameters

                            var assemblyName = m.Groups["Assembly"].ToString();
                            var info = new AssemblyMappedInfo() { FullyQualifiedName = m.Groups["AssemblyFQN"].ToString() };
                            foreach (Capture c in m.Groups["KeyValue"].Captures)
                            {
                                var kv = c.Value.Split(':');
                                info.Attributes[kv[0]] = kv[1];
                            }
							var match = s_regexVersionMapping.Match(info.FullyQualifiedName);
							if (match.Success)
								info.Version = match.Groups["Version"].ToString();
							var assemblyFileName = info.FullyQualifiedName;
							int idxShortNameEnd = assemblyFileName.IndexOf(',');
							if (idxShortNameEnd > 0)
								assemblyFileName = assemblyFileName.Substring(0, idxShortNameEnd);
							info.AssemblyName = assemblyFileName;

							// If GUID and Age are specified, that means that assembly has debug information
							// and the correspodning PDB file would have the matching GUID + Age attributes

							string pdbGuidStr, pdbAgeStr;
                            if (info.Attributes.TryGetValue("G", out pdbGuidStr) &&
                                info.Attributes.TryGetValue("A", out pdbAgeStr))
                            {
                                int pdbAge;
                                Guid pdbGuid;
                                if (int.TryParse(pdbAgeStr, out pdbAge) &&
                                    TryParseGuid(pdbGuidStr, out pdbGuid))
                                {
                                    info.PdbAge = pdbAge;
                                    info.PdbGuid = pdbGuid;
                                    info.fPdbSpecified = true;

                                    // PDB filename is derived from assembly name + '.pdb' extension


                                    // Lookup PDB file using configured Symbol Search Paths. If found, then
                                    // load PDB symbol information - it will be used below to find source line numbers

                                    info.PdbPath = _symSearch.FindPdbFile(info.PdbName, info.PdbGuid, info.PdbAge, info.Version);
									if (info.PdbPath == null)
										info.PdbPath = _symSearch.AltFindPdbFile(info);

									if (info.PdbPath != null)
                                        info.PdbSymbolLoader = SymbolLoader.Load(info.PdbPath);
                                }
                            }

                            mapping[assemblyName] = info;
                        }
                    }
                    break;
                }

                // Save lines into a memory buffer until later processing.
                // Can't process yet, because we have to read the assembly mapping
                // section first, which is at the bottom.

                linesBuf.Add(line);
            }

            // Now that we have the mapping information, can process the buffered
            // stack trace lines. Note that this is robust even if mapping info was
            // not specified.

            for (var i = 0; i < linesBuf.Count; ++i)
            {
                // Check that this looks like stack frame line (e.g. "  at Namespace.Class.Method()")

                line = linesBuf[i];
                var m = s_regexStackTraceLine.Match(line);
                if (m != Match.Empty)
                {
                    var assemblyName = m.Groups["Assembly"].ToString();
                    var mdTokenStr = m.Groups["MDToken"].ToString();
                    var ilOffsetStr = m.Groups["ILOffset"].ToString();

                    // Check if given Assembly has information about the matching PDB

                    AssemblyMappedInfo info;
                    if (mapping.TryGetValue(assemblyName, out info) && info.fPdbSpecified)
                    {
                        int ilOffset, mdToken;
                        if (int.TryParse(mdTokenStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out mdToken) &&
                            int.TryParse(ilOffsetStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ilOffset))
                        {
                            // Print out the stack frame standard portion in any case, even if we fail
                            // to load the symbols file

                            w.Write("   at ");
                            w.Write(m.Groups["Method"]);
                            w.Write("(");
                            w.Write(m.Groups["Args"]);
                            w.Write(")");

                            // But if symbols have been successfully loaded, then find the original source
                            // file and line based on method token and IL offset

                            if (info.PdbSymbolLoader != null)
                            {
                                var srcLoc = info.PdbSymbolLoader.GetSourceLoc(mdToken, ilOffset);
                                if (srcLoc != null)
                                    w.Write(" in {0}:line {1}", srcLoc.SourceFile, srcLoc.LineNumber);
                            }

                            w.WriteLine();
                            continue;
                        }
                    }
                }

                // Output the line if we did not convert it. That way we only transform
                // the lines that we understand, and copy over the rest

                w.WriteLine(line);
            }

            // Before returning ensure that everything is written out

            w.Flush();
        }

        private static bool TryParseGuid(string str, out Guid guid)
        {
#if TARGET_NET_20
            try {
                guid = new Guid(str);
                return true;
            } catch (FormatException) {
                guid = Guid.Empty;
                return false;
            }
#else
            return Guid.TryParse(str, out guid);
#endif
        }
    }
}
