using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductionStackTrace.Analyze
{
    /// <summary>
    /// Encapsulated original source code file and line number.
    /// </summary>
    public class SourceLocation
    {
        public string SourceFile { get; set; }
        public long LineNumber { get; set; }
    }
}
