using System.Linq;
using System.Collections.Generic;
using Serilog.Core;
using Serilog.Events;

namespace RestSharp
{
    public class RestClientAutologEnricher : ILogEventEnricher
    {
        protected Dictionary<string, object> AllProperties { get; }

        protected string[] IgnoredProperties { get; }

        protected string[] PropertiesToDestructure { get; }

        public RestClientAutologEnricher(Dictionary<string, object> allProperties, string[] ignoredProperties, string[] propertiesToDestructure)
        {
            AllProperties = allProperties ?? new Dictionary<string, object>();
            IgnoredProperties = ignoredProperties ?? new string[0];
            PropertiesToDestructure = propertiesToDestructure ?? new string[0];
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            foreach (var property in AllProperties)
            {
                if (IgnoredProperties.Contains(property.Key))
                    continue;

                var destructureObjects = PropertiesToDestructure.Contains(property.Key);
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(property.Key, property.Value,
                    destructureObjects));
            }
        }
    }
}
