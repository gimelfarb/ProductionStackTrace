using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using ProductionStackTrace.Internals;

namespace ProductionStackTrace
{
    /// <summary>
    /// Main reporting entry point.
    /// </summary>
    public static class ExceptionReporting
    {
        /// <summary>
        /// Produce a production style stack trace containing necessary
        /// info to recreate full line-mapping with symbols.
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static string GetExceptionReport(Exception ex)
        {
            StringBuilder builder = null;
            var ctx = new ExceptionReportingContext();
            GetExceptionReport(ex, ctx, ref builder);

            if (ctx.AssemblyInfo.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("==========");

                var keys = new List<string>(ctx.AssemblyInfo.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);

                foreach (var key in keys)
                {
                    var info = ctx.AssemblyInfo[key];
                    builder.AppendFormat("MODULE: {0} => {1};", key, info.Assembly.FullName);
#if !SILVERLIGHT
                    if (info.DebugInfo != null)
                    {
                        builder.AppendFormat(" G:{0:N}; A:{1}", info.DebugInfo.Guid, info.DebugInfo.Age);

                        var pdbFileName = Path.GetFileName(info.DebugInfo.Path);
                        if (!string.Equals(pdbFileName, info.ShortName + ".pdb", StringComparison.OrdinalIgnoreCase))
                            builder.Append("; F:").Append(pdbFileName);
                    }
#endif
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Retrieve just the stack trace of given exception, including IL offsets,
        /// but not the full assembly to PDB mapping.
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static string GetStackTraceEx(Exception ex)
        {
            StringBuilder builder = null;
            GetStackTraceEx(ex, new ExceptionReportingContext(), ref builder);
            return builder.ToString();
        }

        /// <summary>
        /// Internal logic - adapted from Environment.GetStackTrace from .NET BCL.
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="ctx"></param>
        /// <param name="builder"></param>
        private static void GetExceptionReport(Exception ex, ExceptionReportingContext ctx, ref StringBuilder builder)
        {
            builder = builder ?? new StringBuilder(0xff);

            var message = ex.Message;
            builder.Append(ex.GetType().ToString());
            if (!string.IsNullOrEmpty(message))
                builder.Append(": ").Append(message);

            var innerEx = ex.InnerException;
            if (innerEx != null)
            {
                builder.Append(" ---> ");
                GetExceptionReport(innerEx, ctx, ref builder);
                builder.Append(Environment.NewLine).Append("   ");
                builder.Append(GetRuntimeResourceString("Exception_EndOfInnerExceptionStack") ??
                    "--- End of inner exception stack trace ---");
            }

            builder.Append(Environment.NewLine);
            GetStackTraceEx(ex, ctx, ref builder);
        }

        /// <summary>
        /// Internal logic - adapted from Environment.GetStackTrace from .NET BCL.
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="ctx"></param>
        /// <param name="builder"></param>
        private static void GetStackTraceEx(Exception ex, ExceptionReportingContext ctx, ref StringBuilder builder)
        {
            var st = new StackTrace(ex);
            var strAt = GetRuntimeResourceString("Word_At") ?? "at";

            bool isFirstLine = true;
            builder = builder ?? new StringBuilder(0xff);
            for (int i = 0; i < st.FrameCount; i++)
            {
                StackFrame frame = st.GetFrame(i);
                var method = frame.GetMethod();
                if (method != null)
                {
                    if (isFirstLine)
                    {
                        isFirstLine = false;
                    }
                    else
                    {
                        builder.Append(Environment.NewLine);
                    }
                    builder.AppendFormat(CultureInfo.InvariantCulture, "   {0} ", new object[] { strAt });
                    Type declaringType = method.DeclaringType;
                    if (declaringType != null)
                    {
                        // Output assembly short name, followed by method's metadata token,
                        // which is used to later lookup in PDB file

                        AppendAssemblyName(builder, declaringType.Assembly, ctx);
                        builder.Append("!");
                        builder.AppendFormat("0x{0:x8}", method.MetadataToken);
                        builder.Append("!");
                        builder.Append(declaringType.FullName.Replace('+', '.'));
                        builder.Append(".");
                    }
                    builder.Append(method.Name);
                    if ((method is MethodInfo) && ((MethodInfo)method).IsGenericMethod)
                    {
                        Type[] genericArguments = ((MethodInfo)method).GetGenericArguments();
                        builder.Append("[");
                        int index = 0;
                        bool firstArg = true;
                        while (index < genericArguments.Length)
                        {
                            if (!firstArg)
                            {
                                builder.Append(",");
                            }
                            else
                            {
                                firstArg = false;
                            }
                            builder.Append(genericArguments[index].Name);
                            index++;
                        }
                        builder.Append("]");
                    }
                    builder.Append("(");
                    ParameterInfo[] parameters = method.GetParameters();
                    bool firstParam = true;
                    for (int j = 0; j < parameters.Length; j++)
                    {
                        if (!firstParam)
                        {
                            builder.Append(", ");
                        }
                        else
                        {
                            firstParam = false;
                        }
                        string name = "<UnknownType>";
                        if (parameters[j].ParameterType != null)
                        {
                            name = parameters[j].ParameterType.Name;
                        }
                        builder.Append(name + " " + parameters[j].Name);
                    }
                    builder.Append(")");
                    var ilOffset = frame.GetILOffset();
                    if (ilOffset != -1)
                    {
                        // Output the IL Offset, which we can later map to a filename+line,
                        // using information inside a matching PDB file

                        builder.AppendFormat(CultureInfo.InvariantCulture, " +0x{0:x}", ilOffset);
                    }
                    if (GetIsLastFrameFromForeignExceptionStackTrace(frame))
                    {
                        builder.Append(Environment.NewLine);
                        builder.Append(GetRuntimeResourceString("Exception_EndStackTraceFromPreviousThrow") ?? 
                            "--- End of stack trace from previous location where exception was thrown ---");
                    }
                }
            }
        }

        /// <summary>
        /// Record assembly short name, and it's associated PDB file.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="assembly"></param>
        /// <param name="ctx"></param>
        private static void AppendAssemblyName(StringBuilder builder, Assembly assembly, ExceptionReportingContext ctx)
        {
            var assemblyName = assembly.FullName;
            int idxShortNameEnd = assemblyName.IndexOf(",");
            if (idxShortNameEnd > 0) assemblyName = assemblyName.Substring(0, idxShortNameEnd);

            // Make sure if two assemblies have the same short name, that we differentiate
            // them using a counter (e.g. "Assembly#2")

            AssemblyReportInfo info;
            var originalAssemblyName = assemblyName;
            var counter = 1;
            while (ctx.AssemblyInfo.TryGetValue(assemblyName, out info))
            {
                if (Object.ReferenceEquals(info.Assembly, assembly)) break;
                assemblyName = string.Format(CultureInfo.InvariantCulture, "{0}#{1}", originalAssemblyName, ++counter);
            }

            builder.Append(assemblyName);

            // Read information about associated PDB file from assembly

            if (info == null)
            {
                ctx.AssemblyInfo.Add(assemblyName, info = new AssemblyReportInfo() { Assembly = assembly, ShortName = originalAssemblyName });
#if !SILVERLIGHT
                info.DebugInfo = AssemblyDebugInfo.ReadAssemblyDebugInfo(assembly);
#endif
            }
        }

        /// <summary>
        /// Collecting information about each assembly (GUID + Age).
        /// </summary>
        private class ExceptionReportingContext
        {
            public Dictionary<string, AssemblyReportInfo> AssemblyInfo = new Dictionary<string, AssemblyReportInfo>(StringComparer.OrdinalIgnoreCase);
        }

        private class AssemblyReportInfo
        {
            public Assembly Assembly;
#if !SILVERLIGHT
            public AssemblyDebugInfo DebugInfo;
#endif
            public string ShortName;
        }

        /// <summary>
        /// Replicating internal .NET calls.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private static string GetRuntimeResourceString(string id)
        {
            var m = typeof(Environment).GetMethod("GetRuntimeResourceString", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null, new[] { typeof(string) }, null);
            if (m == null) return null;
            try
            {
                return (string)m.Invoke(null, new object[] { id });
            }
            catch (MemberAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Replicating internal .NET calls.
        /// </summary>
        /// <param name="sf"></param>
        /// <returns></returns>
        private static bool GetIsLastFrameFromForeignExceptionStackTrace(StackFrame sf)
        {
            var m = typeof(StackFrame).GetMethod("GetIsLastFrameFromForeignExceptionStackTrace", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (m == null) return false;
            try
            {
                return (bool)m.Invoke(sf, null);
            }
            catch (MemberAccessException)
            {
                return false;
            }
        }
    }
}
