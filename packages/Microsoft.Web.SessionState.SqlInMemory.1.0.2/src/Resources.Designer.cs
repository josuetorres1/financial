﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.34003
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.Web.SessionState {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.Web.SessionState.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to connect to SQL Server In-Memory session database..
        /// </summary>
        internal static string Cant_connect_sql_session_database {
            get {
                return ResourceManager.GetString("Cant_connect_sql_session_database", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The session state information is not valid and might be corrupted..
        /// </summary>
        internal static string Invalid_session_state {
            get {
                return ResourceManager.GetString("Invalid_session_state", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to login to SQL Server In-Memory session database..
        /// </summary>
        internal static string Login_failed_sql_session_database {
            get {
                return ResourceManager.GetString("Login_failed_sql_session_database", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The &apos;connectionString&apos; property is not available in the configuration..
        /// </summary>
        internal static string No_database_found_in_connectionString {
            get {
                return ResourceManager.GetString("No_database_found_in_connectionString", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The session ID is longer than the maximum limit of characters..
        /// </summary>
        internal static string Session_id_too_long {
            get {
                return ResourceManager.GetString("Session_id_too_long", resourceCulture);
            }
        }
    }
}
