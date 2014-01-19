using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductionStackTrace.Test
{
    [Serializable]
    public class IsolatedMethodResult
    {
        public object ReturnVal { get; set; }
        public Exception Exception { get; set; }
        public string ExceptionStackTrace { get; set; }
        public string ExceptionReport { get; set; }
    }
}
