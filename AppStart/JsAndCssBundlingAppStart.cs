using System.Linq;
using System.Web.Optimization;
using AngularJSProofofConcept.AppStart;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;

[assembly: WebActivator.PreApplicationStartMethod(typeof(JsAndCssBundlingAppStart), "Start")]
 
namespace AngularJSProofofConcept.AppStart
{
    public static class JsAndCssBundlingAppStart
    {
        public static void Start()
        {
            DynamicModuleUtility.RegisterModule(typeof (BundleModule));
            InitializeDefaultSettings();
            RegisterFolders();
        }

        private static void InitializeDefaultSettings()
        {
            BundleCollection.AddDefaultFileExtensionReplacements(new FileExtensionReplacementList());
            BundleCollection.AddDefaultFileOrderings(Enumerable.Empty<BundleFileSetOrdering>().ToList());
            BundleCollection.AddDefaultIgnorePatterns(new IgnoreList());
        }

        private static void RegisterFolders()
        {
            var js = new DynamicFolderBundle("js", "*.js");
            BundleTable.Bundles.Add(js);

            var css = new DynamicFolderBundle("css", "*.css");
            BundleTable.Bundles.Add(css);

            var scss = new DynamicFolderBundle("scss", "*.scss");
            BundleTable.Bundles.Add(scss);
        }
    }
}