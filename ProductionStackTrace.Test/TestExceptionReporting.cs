using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit;
using NUnit.Framework;
using System.Reflection;
using ProductionStackTrace.Analyze;
using System.IO;
using System.Text.RegularExpressions;

namespace ProductionStackTrace.Test
{
    [TestFixture]
    public class TestExceptionReporting : MarshalByRefObject
    {
        [TestCase]
        public void TestSimpleException()
        {
            try
            {
                throw new Exception("Test exception");
            }
            catch (Exception ex)
            {
                var s = ExceptionReporting.GetExceptionReport(ex);
                Console.WriteLine(s);

                Assert.IsNotNull(s);
                StringAssert.StartsWith(ex.GetType().FullName + ": Test exception", s);

                var match = @"System\.Exception: Test exception\r\n" +
                    @"\s+at ProductionStackTrace\.Test!0x[0-9a-f]+!ProductionStackTrace\.Test\.TestExceptionReporting\.TestSimpleException\(\) \+0x[0-9a-f]+\r\n" +
                    @"==========\r\n" +
                    @"MODULE: ProductionStackTrace\.Test => ProductionStackTrace\.Test, Version=1\.0\.0\.0, Culture=neutral, PublicKeyToken=null; G:[0-9a-f]+; A:[0-9]+\r\n";
                StringAssert.IsMatch(match, s);
            }
        }

        [TestCase]
        public void TestNoSymbolsException()
        {
            InternalTestException(IsolatedMode.NoSymbols, "SomeClass.A()", "System.Exception: SomeClass.A", 
                new [] {new LineNumberInfo("SomeClass.cs", 13)});
            InternalTestException(IsolatedMode.NoSymbols, "SomeClass.B()",
                string.Format("System.InvalidOperationException: {0} ---> System.Exception: SomeClass.InternalCompare", GetEnvironmentResourceString("InvalidOperation_IComparerFailed")), 
                new [] {new LineNumberInfo("SomeClass.cs", 27), new LineNumberInfo("SomeClass.cs", 22)});
        }

        [TestCase]
        public void TestWithSymbolsException()
        {
            InternalTestException(IsolatedMode.WithSymbols, "SomeClass.A()", "System.Exception: SomeClass.A",
                new[] { new LineNumberInfo("SomeClass.cs", 13) });
            InternalTestException(IsolatedMode.WithSymbols, "SomeClass.B()",
                string.Format("System.InvalidOperationException: {0} ---> System.Exception: SomeClass.InternalCompare", GetEnvironmentResourceString("InvalidOperation_IComparerFailed")),
                new[] { new LineNumberInfo("SomeClass.cs", 27), new LineNumberInfo("SomeClass.cs", 22) });
        }

        #region class - LineNumberInfo

        private class LineNumberInfo
        {
            public string File { get; set; }
            public int LineNo { get; set; }
            public LineNumberInfo(string file, int lineNo)
            {
                this.File = file;
                this.LineNo = lineNo;
            }
        }

        #endregion

        #region enum - IsolatedMode

        private enum IsolatedMode
        {
            NoSymbols,
            WithSymbols
        }

        #endregion

        private static Regex _regexMethodSig = new Regex(@"(?<Class>([^\.]+\.)+)(?<Method>[^\(]+)(?<Params>\(.*\))");
        private void InternalTestException(IsolatedMode mode, string methodSig, string expectedException, LineNumberInfo[] expectedInfos)
        {
            // Parse the methodSig

            var assemblyName = "ProductionStackTrace.Test.Library";
            var defaultNamespace = assemblyName;

            var m = _regexMethodSig.Match(methodSig);
            if (m == Match.Empty) throw new ArgumentException("Invalid methodSig: " + methodSig);
            var className = m.Groups["Class"].ToString().TrimEnd('.');
            var methodName = m.Groups["Method"].ToString();
            var methodParams = m.Groups["Params"].ToString();

            // Execute in isolated environment with or without access to
            // PDB symbols information (meaning native stack trace either has
            // or doesn't have line number info)
            //
            // Customization in the .csproj file for this Test project in the
            // "AfterBuild" target will setup two subfolders - lib and libpdb.
            // 'lib' will contain just the DLL, and 'libpdb' will have DLL + PDB.
            //
            // Also original PDB location will be deleted, so that PDB can only
            // be found if it's on the Symbols Paths or in the folder as DLL.

            var env = new IsolatedEnvironment();
            env.LibPaths.Add(mode == IsolatedMode.NoSymbols ? "lib" : "libpdb");

            var r = env.Run(string.Format("{1}.{2}, {0}", assemblyName, defaultNamespace, className), methodName);

            Assert.IsNotNull(r.Exception);
            Assert.IsNotNullOrEmpty(r.ExceptionStackTrace);
            Assert.IsNotNullOrEmpty(r.ExceptionReport);
            StringAssert.StartsWith(expectedException + "\r\n", r.ExceptionStackTrace);
            StringAssert.StartsWith(expectedException + "\r\n", r.ExceptionReport);
            StringAssert.Contains("   at " + defaultNamespace + "." + methodSig, r.ExceptionStackTrace);
            Assert.AreNotEqual(r.ExceptionStackTrace, r.ExceptionReport);

            if (mode == IsolatedMode.NoSymbols)
                StringAssert.DoesNotContain(":line ", r.ExceptionStackTrace);
            else
                StringAssert.Contains(":line ", r.ExceptionStackTrace);

            var interpret = new ExceptionReportInterpreter();
            var sb = new StringBuilder();
            string parsedReport;

            if (mode != IsolatedMode.NoSymbols)
                interpret.SymbolPaths.Add("libpdb");

            // Run interpreter with no access to symbols
            // It should produce the same stack trace as the internal exception

            interpret.Translate(new StringReader(r.ExceptionReport), new StringWriter(sb));
            parsedReport = sb.ToString();

            // Parsed report should be exactly the same as we would obtain
            // from the original stack trace.
            //
            // - If the original stack trace was run without symbols, then
            //   interpreter didn't have access to symbols either, and so
            //   the output should match

            Assert.AreEqual(r.ExceptionStackTrace, parsedReport.TrimEnd());

            if (mode == IsolatedMode.NoSymbols)
            {
                // If we ran original report with no access to symbols, then
                // run it again - now with PDB symbols. The stack trace should now 
                // include line number info

                sb.Clear();
                interpret.SymbolPaths.Add("libpdb");
                interpret.Translate(new StringReader(r.ExceptionReport), new StringWriter(sb));
                parsedReport = sb.ToString();

                StringAssert.DoesNotStartWith(parsedReport, r.ExceptionReport);   // check it is different
                Assert.AreNotEqual(r.ExceptionStackTrace, parsedReport);          // check it is not same as default stack trace
            }

            // Check that the expected line number info is present

            foreach (var info in expectedInfos)
            {
                StringAssert.Contains("\\" + info.File + ":line " + info.LineNo, parsedReport);    // check it has the line info
            }
        }

        #region Helpers

        private static string GetEnvironmentResourceString(string id)
        {
            var m = typeof(Environment).GetMethod("GetResourceString", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
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

        #endregion
    }
}
