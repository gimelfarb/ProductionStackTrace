using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ProductionStackTrace.Test
{
    /// <summary>
    /// Helper class to run methods in an isolated AppDomain to control
    /// whether PDB symbols will be found or not.
    /// </summary>
    public class IsolatedEnvironment : MarshalByRefObject
    {
        public List<string> LibPaths { get; private set; }

        public IsolatedEnvironment()
        {
            this.LibPaths = new List<string>();
        }

        /// <summary>
        /// Run a method using reflection. Used as an entry point
        /// to a newly created isolated AppDomain.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="method"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private IsolatedMethodResult RunMethod(string type, string method, object[] args)
        {
            var r = new IsolatedMethodResult();
            try
            {
                var t = Type.GetType(type);
                var m = t.GetMethod(method);
                r.ReturnVal = m.Invoke(null, args);
            }
            catch (TargetInvocationException ex)
            {
                r.Exception = ex.InnerException;
                r.ExceptionStackTrace = r.Exception.ToString();
                r.ExceptionReport = r.Exception.ToProductionString();
            }
            return r;
        }

        /// <summary>
        /// Run specified static method in an isolated AppDomain.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="method"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public IsolatedMethodResult Run(string type, string method, params object[] args)
        {
            // Copy setup info from current AppDomain

            var setup = AppDomain.CurrentDomain.SetupInformation;
            var info = new AppDomainSetup();
            info.ApplicationBase = setup.ApplicationBase;
            info.CachePath = setup.CachePath;
            info.ConfigurationFile = setup.ConfigurationFile;
            info.ShadowCopyDirectories = setup.ShadowCopyDirectories;
            info.ShadowCopyFiles = setup.ShadowCopyFiles;

            // Setup where to look for extra assemblies

            if (this.LibPaths.Count > 0)
                info.PrivateBinPath = string.Join(";", this.LibPaths);

            var domain = AppDomain.CreateDomain("IsolatedTest", null, info);
            try
            {
                // Use itself as the entry point in the newly created domain
                // (i.e. the remote instance of ourselves)

                var t = (IsolatedEnvironment)domain.CreateInstanceAndUnwrap(Assembly.GetExecutingAssembly().FullName, GetType().FullName);
                return t.RunMethod(type, method, args);
            }
            finally
            {
                AppDomain.Unload(domain);
            }
        }
    }
}
