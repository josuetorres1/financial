using System.Web.Optimization;

namespace AngularJSProofofConcept
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new Bundle("~/bundles/App")
                .IncludeDirectory("~/Scripts/Angular", "*.js")
                .IncludeDirectory("~/Scripts", "*.js")
                //.IncludeDirectory("~/Scripts/i18n", "*.js")
                .IncludeDirectory("~/Scripts/Controllers", "*.js"));
        }
    }
}