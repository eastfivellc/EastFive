using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Web
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigAttribute : Attribute
    {
    }

    public class ConfigKeyAttribute : Attribute
    {
        public ConfigKeyAttribute(string description, DeploymentOverrides deploymentOverride)
        {
            this.Description = description;
            this.DeploymentOverride = deploymentOverride;
        }
        
        /// <summary>
        /// Where to find this value
        /// </summary>
        public virtual string Location { get; set; }

        /// <summary>
        /// What is provided by this value
        /// </summary>
        public virtual string Description { get; private set; }

        /// <summary>
        /// More information about the key.
        /// </summary>
        public virtual string MoreInfo { get; set; }


        /// <summary>
        /// Should the value provided in the (web|app).config be overwritten in a deployed environment.
        /// </summary>
        public virtual DeploymentOverrides DeploymentOverride { get; }
        
        /// <summary>
        /// Not overriding this value in a deployment is a security concern
        /// </summary>
        public virtual bool DeploymentSecurityConcern { get; set; }

        /// <summary>
        /// This value should not be stored in a version control repository or anything other location
        /// that is publicly available.
        /// </summary>
        /// <remarks>
        /// These values are generally placed in a (App|Web).{Env}.config file that is flagged
        /// as ignored (i.e. .gitignore file) by the source control system.
        /// </remarks>
        public virtual bool PrivateRepositoryOnly { get; set; }
    }

    public enum DeploymentOverrides
    {
        /// <summary>
        /// The system cannot operate correctly without this value being set per environment
        /// </summary>
        Mandatory,
        
        /// <summary>
        /// This value should be set per environment 
        /// </summary>
        Suggested,

        /// <summary>
        /// It is recommended to set this value per environment be the default will function
        /// </summary>
        Desireable,

        /// <summary>
        /// This value's default parameters will work for any environment and are only made
        /// available for configuration for optional tuning.
        /// </summary>
        Optional,

        /// <summary>
        /// This value should not be modified.
        /// </summary>
        Forbidden,
    }
}
