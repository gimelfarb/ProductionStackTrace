using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using ProductionStackTrace.Internals;

namespace ProductionStackTrace {
	/// <summary>
	/// Main reporting entry point.
	/// </summary>
	public static class ExceptionReporting {

		/// <summary>
		/// Produce a production style stack trace containing necessary
		/// info to recreate full line-mapping with symbols.
		/// </summary>
		/// <param name="ex"></param>
		/// <returns></returns>
		public static string GetExceptionReport(Exception ex) {
			return GetExceptionReportObject(ex).ToString();
		}
		/// <summary>
		/// Produce a production style stack trace containing necessary
		/// info to recreate full line-mapping with symbols.
		/// </summary>
		/// <param name="ex"></param>
		/// <returns></returns>
		public static LogExceptionReport GetExceptionReportObject(Exception ex) {

			var ctx = new ExceptionReportingContext();
			var ret = GetExceptionReport(ex, ctx);

			if (ctx.AssemblyInfo.Count > 0) {
				ret.AssemblyInfo = new LogAssemblyInfo[ctx.AssemblyInfo.Count];
				int cur = 0;

				var keys = new List<string>(ctx.AssemblyInfo.Keys);
				keys.Sort(StringComparer.OrdinalIgnoreCase);

				foreach (var key in keys) {
					ret.AssemblyInfo[cur++] = new LogAssemblyInfo(ctx.AssemblyInfo[key]) { ShortName = key };

				}
			}

			return ret;
		}

		/// <summary>
		/// Retrieve just the stack trace of given exception, including IL offsets,
		/// but not the full assembly to PDB mapping.
		/// </summary>
		/// <param name="ex"></param>
		/// <returns></returns>
		public static string GetStackTraceEx(Exception ex) {
			return GetStackTraceEx(ex, new ExceptionReportingContext()).ToString();
		}

		/// <summary>
		/// Internal logic - adapted from Environment.GetStackTrace from .NET BCL.
		/// </summary>
		/// <param name="ex"></param>
		/// <param name="ctx"></param>
		private static LogExceptionReport GetExceptionReport(Exception ex, ExceptionReportingContext ctx) {
			var ret = new LogExceptionReport();
			ret.Type = ex.GetType();
			ret.Message = ex.Message;
			

			var innerEx = ex.InnerException;
			if (innerEx != null) {
				ret.InnerException = GetExceptionReport(innerEx, ctx);
			}

			
			ret.StackTrace =  GetStackTraceEx(ex, ctx);
			return ret;
		}

		/// <summary>
		/// Internal logic - adapted from Environment.GetStackTrace from .NET BCL.
		/// </summary>
		/// <param name="ex"></param>
		/// <param name="ctx"></param>
		private static LogStackTrace GetStackTraceEx(Exception ex, ExceptionReportingContext ctx) {
			var st = new EnhancedStackTrace(ex);
			var ret = new LogStackTrace();
			var stack_frames = new List<LogStackFrame>();//we don't know if any frames are empty
			ret.StackFrames = stack_frames;




			foreach (var frame in st) {
				var method = frame.GetMethod();
				if (method != null) {
					var st_frame = new LogStackFrame(frame);
					stack_frames.Add(st_frame);
					st_frame.SourceFileName = frame.GetFileName();
					Type declaringType = method.DeclaringType;
					if (declaringType != null) {
						// Output assembly short name, followed by method's metadata token,
						// which is used to later lookup in PDB file

						st_frame.ShortAssemblyName = GetAssemblyNameAndAddToContext(declaringType.Assembly, ctx);
					}


				}
			}
			return ret;
		}

		/// <summary>
		/// Record assembly short name, and it's associated PDB file.
		/// </summary>
		/// <param name="builder"></param>
		/// <param name="assembly"></param>
		/// <param name="ctx"></param>
		/// <param name="record_only"></param>
		private static string GetAssemblyNameAndAddToContext(Assembly assembly, ExceptionReportingContext ctx) {
			var assemblyName = assembly.FullName;
			int idxShortNameEnd = assemblyName.IndexOf(",");
			if (idxShortNameEnd > 0) assemblyName = assemblyName.Substring(0, idxShortNameEnd);

			// Make sure if two assemblies have the same short name, that we differentiate
			// them using a counter (e.g. "Assembly#2")

			AssemblyReportInfo info;
			var originalAssemblyName = assemblyName;
			var counter = 1;
			while (ctx.AssemblyInfo.TryGetValue(assemblyName, out info)) {
				if (Object.ReferenceEquals(info.Assembly, assembly)) break;
				assemblyName = string.Format(CultureInfo.InvariantCulture, "{0}#{1}", originalAssemblyName, ++counter);
			}
			

			// Read information about associated PDB file from assembly

			if (info == null) {
				ctx.AssemblyInfo.Add(assemblyName, info = new AssemblyReportInfo() { Assembly = assembly, ShortName = originalAssemblyName });
				try {
					info.DebugInfo = AssemblyDebugInfo.ReadAssemblyDebugInfo(assembly);
				} catch { }
			}
			return assemblyName;
		}

		/// <summary>
		/// Collecting information about each assembly (GUID + Age).
		/// </summary>
		private class ExceptionReportingContext {
			public Dictionary<string, AssemblyReportInfo> AssemblyInfo = new Dictionary<string, AssemblyReportInfo>(StringComparer.OrdinalIgnoreCase);
		}

		internal class AssemblyReportInfo {
			public Assembly Assembly;
			public AssemblyDebugInfo DebugInfo;
			public string ShortName;
		}

		/// <summary>
		/// Replicating internal .NET calls.
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		internal static string GetRuntimeResourceString(string id) {
			var m = typeof(Environment).GetMethod("GetRuntimeResourceString", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
				null, new[] { typeof(string) }, null);
			if (m == null) return null;
			try {
				return (string)m.Invoke(null, new object[] { id });
			} catch (MemberAccessException) {
				return null;
			}
		}

		/// <summary>
		/// Replicating internal .NET calls.
		/// </summary>
		/// <param name="sf"></param>
		/// <returns></returns>
		internal static bool GetIsLastFrameFromForeignExceptionStackTrace(StackFrame sf) {
			var m = typeof(StackFrame).GetMethod("GetIsLastFrameFromForeignExceptionStackTrace", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (m == null) return false;
			try {
				return (bool)m.Invoke(sf, null);
			} catch (MemberAccessException) {
				return false;
			}
		}
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public delegate IntPtr MarshalGetHINSTANCEDel(Module module);

		public static MarshalGetHINSTANCEDel MarshalGetHINSTANCE = MarshalGetHINSTANCEDefault;

		internal static IntPtr MarshalGetHINSTANCEDefault(Module module) {//doesn't exist until dotnet 2.1
			var type = typeof(System.Runtime.InteropServices.Marshal);
			var method = type.GetMethod("GetHINSTANCE");
			if (method == null)
				throw new Exception("Unable to get System.Runtime.InteropServices.Marshal.GetHINSTANCE method");
			return (IntPtr)method.Invoke(null, new[] { module });
		}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
	}

}
