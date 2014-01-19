using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
