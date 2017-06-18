﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.ObjectResolution;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Web.HealthCheck.NotificationMethods;

namespace Umbraco.Web.HealthCheck
{
    using System.Configuration;
    using System.Reflection;

    using Umbraco.Core.Configuration.HealthChecks;

    /// <summary>
    /// Resolves all health check instances
    /// </summary>
    /// <remarks>
    /// Each instance scoped to the lifespan of the http request
    /// </remarks>
    internal class HealthCheckNotificationMethodResolver : LazyManyObjectsResolverBase<HealthCheckNotificationMethodResolver, IHealthCheckNotificatationMethod>, IHealthCheckNotificationMethodsResolver
    {
        public HealthCheckNotificationMethodResolver(ILogger logger, Func<IEnumerable<Type>> lazyTypeList)
            : base(new HealthCheckNotificationMethodServiceProvider(), logger, lazyTypeList, ObjectLifetimeScope.Application)
        {
        }

        /// <summary>
        /// Returns all health check notification method instances
        /// </summary>
        public IEnumerable<IHealthCheckNotificatationMethod> NotificationMethods
        {
            get { return Values; }
        }

        /// <summary>
        /// This will ctor the IHealthCheckNotificatationMethod instances
        /// </summary>
	    private class HealthCheckNotificationMethodServiceProvider : IServiceProvider
        {
            public object GetService(Type serviceType)
            {
                var ctor = serviceType.GetConstructors().FirstOrDefault();
                if (ctor == null)
                {
                    return null;
                }

                // Load attribute from type in order to find alias for notification method
                var attribute = serviceType.GetCustomAttributes(typeof(HealthCheckNotificationMethodAttribute), true)
                    .FirstOrDefault() as HealthCheckNotificationMethodAttribute;
                if (attribute == null)
                {
                    return null;
                }

                // Using alias, get related configuration
                var healthCheckConfig = (HealthChecksSection)ConfigurationManager.GetSection("umbracoConfiguration/HealthChecks");
                var notificationMethods = healthCheckConfig.NotificationSettings.NotificationMethods;
                var notificationMethod = notificationMethods[attribute.Alias];
                if (notificationMethod == null)
                {
                    return null;
                }

                // Create array for constructor paramenters.  Will consists of common ones that all notification methods have as well
                // as those specific to this particular notification method.
                var baseType = typeof(NotificationMethodBase);
                var baseTypeCtor = baseType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First();
                var baseTypeCtorParamNames = baseTypeCtor.GetParameters().Select(x => x.Name);
                var ctorParams = new List<object> { notificationMethod.Enabled, notificationMethod.FailureOnly, notificationMethod.Verbosity };
                ctorParams.AddRange(ctor.GetParameters()
                    .Where(x => baseTypeCtorParamNames.Contains(x.Name) == false)
                    .Select(x => notificationMethod.Settings[x.Name].Value));

                // Instantiate the type with the constructor parameters
                return Activator.CreateInstance(serviceType, ctorParams.ToArray());
            }
        }
    }
}
