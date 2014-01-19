using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductionStackTrace.Test.Library
{
    public class SomeClass
    {
        public static void A()
        {
            throw new Exception("SomeClass.A");
        }

        public static void B()
        {
            var list = new List<object>();
            list.Add(new object());
            list.Add(new object());
            list.Add(new object());
            list.Sort(InternalCompare);
        }

        private static int InternalCompare(object a, object b)
        {
            throw new Exception("SomeClass.InternalCompare");
        }
    }
}
