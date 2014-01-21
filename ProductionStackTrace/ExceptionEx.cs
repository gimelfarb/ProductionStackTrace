using System;
using System.Collections.Generic;
using System.Text;

namespace ProductionStackTrace
{
    /// <summary>
    /// Extension methods for <see cref="Exception"/>
    /// </summary>
    public static class ExceptionEx
    {
        /// <summary>
        /// Produce a production style stack trace which contains necessary
        /// info to recreate full debug info with symbols.
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static string ToProductionString(this Exception ex)
        {
            return ExceptionReporting.GetExceptionReport(ex);
        }
    }
}

#if TARGET_NET_20

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Extenstion attribute definition for .NET 2.0 target framework to
    /// support extension method definitions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class ExtensionAttribute : Attribute {}
}

#endif