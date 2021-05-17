using System;
using System.Collections.Generic;
using System.Text;

namespace ProductionStackTrace.Analyze {
	/// <summary>
	/// Encapsulated original source code file and line number.
	/// </summary>
	public class SourceLocation {
		/// <summary>
		/// Full path to the original source file.
		/// </summary>
		public string SourceFile { get; set; }
		/// <summary>
		/// Line number within the source file.
		/// </summary>
		public long LineNumber { get; set; }
	}
}
