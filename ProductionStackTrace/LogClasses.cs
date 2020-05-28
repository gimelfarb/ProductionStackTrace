using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductionStackTrace {

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	public class LogStackTrace {
		public IEnumerable<LogStackFrame> StackFrames;
		public override string ToString() { var sb = new StringBuilder(0xff); ToString(sb); return sb.ToString(); }
		public void ToString(StringBuilder builder) {
			bool isFirstLine = true;
			foreach (var frame in StackFrames) {
				if (isFirstLine) {
					isFirstLine = false;
				} else {
					builder.Append(Environment.NewLine);
				}

				frame.ToString(builder);


			}
		}
	}
	public class LogStackFrame {
		private EnhancedStackFrame frame;
		public LogStackFrame(EnhancedStackFrame frame) { this.frame = frame; from_raw = true; }
		public LogStackFrame() { }
		public void LoadObjectPropertiesFromRaw() {
			if (!from_raw)
				return;
			from_raw = false;
			var method = frame.GetMethod();
			MethodMetadataToken = method.MetadataToken;
			IlOffset = frame.GetILOffset();
			SourceFileName = frame.GetFileName();
			SourceFileLine = frame.GetFileLineNumber();
			MethodInfo = frame.MethodInfo.ToString();
			IsLastFrameFromForeignExceptionStackTrace = ExceptionReporting.GetIsLastFrameFromForeignExceptionStackTrace(frame);
		}
		public override string ToString() { var sb = new StringBuilder(0xff); ToString(sb); return sb.ToString(); }
		public void ToString(StringBuilder builder) {
			var strAt = ExceptionReporting.GetRuntimeResourceString("Word_At") ?? "at";
			var method = from_raw ? frame.GetMethod() : null;
			builder.AppendFormat(CultureInfo.InvariantCulture, "   {0} ", new object[] { strAt });
			var no_file_info = String.IsNullOrWhiteSpace(SourceFileName);
			if (no_file_info) {
				builder.Append(ShortAssemblyName);
				builder.Append("!");
				builder.AppendFormat("0x{0:x8}", from_raw ? method.MetadataToken : MethodMetadataToken);
				builder.Append("!");
			}
			if (from_raw)
				frame.MethodInfo.Append(builder); //will require a PR approved before we can use
			else
				builder.Append(MethodInfo);

			if (no_file_info) {
				var ilOffset = from_raw ? frame.GetILOffset() : IlOffset;
				if (ilOffset != -1) {
					// Output the IL Offset, which we can later map to a filename+line,
					// using information inside a matching PDB file
					builder.AppendFormat(CultureInfo.InvariantCulture, " +0x{0:x}", ilOffset);
				}
			}
			else
				builder.Append($" in {(from_raw ? frame.GetFileName() : SourceFileName)}:line {(from_raw ? frame.GetFileLineNumber() : SourceFileLine)}");


			if (from_raw ? ExceptionReporting.GetIsLastFrameFromForeignExceptionStackTrace(frame) : IsLastFrameFromForeignExceptionStackTrace) {
				builder.Append(Environment.NewLine);
				builder.Append(ExceptionReporting.GetRuntimeResourceString("Exception_EndStackTraceFromPreviousThrow") ??
					"--- End of stack trace from previous location where exception was thrown ---");
			}

		}
		public int MethodMetadataToken;
		public string ShortAssemblyName;
		public string MethodInfo;
		public string SourceFileName;
		public int SourceFileLine;
		public int IlOffset;
		public bool IsLastFrameFromForeignExceptionStackTrace;
		private bool from_raw;
	}
	public class LogExceptionReport {
		public Type Type;
		public string Message;

		public void LoadObjectPropertiesFromRaw() {
			foreach (var frame in StackTrace.StackFrames)
				frame.LoadObjectPropertiesFromRaw();
			if (AssemblyInfo != null) {
				foreach (var assembly in AssemblyInfo)
					assembly.LoadObjectPropertiesFromRaw();
			}
			InnerException?.LoadObjectPropertiesFromRaw();

		}
		public LogStackTrace StackTrace;
		public LogExceptionReport InnerException;
		public LogAssemblyInfo[] AssemblyInfo;
		public override string ToString() { var sb = new StringBuilder(0xff); ToString(sb); return sb.ToString(); }
		public void ToString(StringBuilder builder) {
			builder.Append(Type.ToString());
			if (!string.IsNullOrEmpty(Message))
				builder.Append(": ").Append(Message);

			if (InnerException != null) {
				builder.Append(" ---> ");
				InnerException.ToString(builder);
				builder.Append(Environment.NewLine).Append("   ");
				builder.Append(ExceptionReporting.GetRuntimeResourceString("Exception_EndOfInnerExceptionStack") ??
					"--- End of inner exception stack trace ---");

			}
			builder.Append(Environment.NewLine);
			StackTrace.ToString(builder);
			if (AssemblyInfo?.Length > 0) {
				builder.AppendLine();
				builder.AppendLine("==========");
				foreach (var assembly in AssemblyInfo) {
					assembly.ToString(builder);
					builder.AppendLine();
				}
			}
		}
	}
	public class LogAssemblyInfo {
		public LogAssemblyInfo() { }
		internal LogAssemblyInfo(ExceptionReporting.AssemblyReportInfo info) {
			this.info = info;
			from_raw = true;
		}
		public string ShortName;//may have an incrementer at end if multiple assemblies with same short name
		public string OrigShortName;
		public string AssemblyFullName;
		public uint Age;
		public Guid Guid;
		public string PdbFileName;
		public bool DebugInfoPresent;
		private ExceptionReporting.AssemblyReportInfo info;
		private bool from_raw;
		public void LoadObjectPropertiesFromRaw() {
			if (!from_raw)
				return;
			from_raw = false;
			AssemblyFullName = info.Assembly.FullName;
			DebugInfoPresent = info.DebugInfo != null;
			if (DebugInfoPresent) {
				Guid = info.DebugInfo.Guid;
				Age = info.DebugInfo.Age;
				PdbFileName = info.DebugInfo.Path;
				OrigShortName = info.ShortName;
			}
		}
		public override string ToString() { var sb = new StringBuilder(0xff); ToString(sb); return sb.ToString(); }
		public void ToString(StringBuilder builder) {

			builder.AppendFormat("MODULE: {0} => {1};", ShortName, from_raw ? info.Assembly.FullName : AssemblyFullName);
			if ( (from_raw ? info.DebugInfo != null : DebugInfoPresent)) {
				builder.AppendFormat(" G:{0:N}; A:{1}", from_raw ? info.DebugInfo.Guid : Guid, from_raw ? info.DebugInfo.Age : Age);

				var pdbFileName = from_raw ? info.DebugInfo.Path : PdbFileName;
				var pos = pdbFileName.LastIndexOfAny(new[] { '\\', '/' });
				if (pos != -1)
					pdbFileName = pdbFileName.Substring(pos + 1);
				if (!string.Equals(pdbFileName, (from_raw ? info.ShortName : OrigShortName) + ".pdb", StringComparison.OrdinalIgnoreCase))
					builder.Append("; F:").Append(pdbFileName);
			}

		}


	}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

}
