using JsonMasking;
using Newtonsoft.Json;
using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using Serilog.Core;
using Serilog.Events;

// ReSharper disable once CheckNamespace
namespace RestSharp
{
    public class RestClientAutolog : RestClient, IRestClient
    {
        private class RestClientAutologEnricher : ILogEventEnricher
        {
            private Dictionary<string, object> AllProperties { get; }

            private string[] IgnoredProperties { get; }

            private string[] PropertiesToDestructure { get; }

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

        public Dictionary<string, string> AdditionalProperties { get; set; } = new Dictionary<string, string>();

        public static RestClientAutologConfiguration GlobalConfiguration { get; set; }

        public RestClientAutologConfiguration Configuration { get; set; }

        public RestClientAutolog(RestClientAutologConfiguration configuration)
        {
            Startup(configuration);
        }

        public RestClientAutolog(Uri baseUrl, RestClientAutologConfiguration configuration) : base(baseUrl)
        {
            Startup(configuration);
        }

        public RestClientAutolog(string baseUrl, RestClientAutologConfiguration configuration) : base(baseUrl)
        {
            Startup(configuration);
        }

        public RestClientAutolog(LoggerConfiguration loggerConfiguration)
        {
            Startup(new RestClientAutologConfiguration
            {
                LoggerConfiguration = loggerConfiguration
            });
        }

        public RestClientAutolog(Uri baseUrl, LoggerConfiguration loggerConfiguration) : base(baseUrl)
        {
            Startup(new RestClientAutologConfiguration
            {
                LoggerConfiguration = loggerConfiguration
            });
        }

        public RestClientAutolog(string baseUrl, LoggerConfiguration loggerConfiguration) : base(baseUrl)
        {
            Startup(new RestClientAutologConfiguration
            {
                LoggerConfiguration = loggerConfiguration
            });
        }

        public RestClientAutolog(string baseUrl, string message) : base(baseUrl)
        {
            Startup(new RestClientAutologConfiguration
            {
                MessageTemplateForError = message,
                MessageTemplateForSuccess = message
            });
        }

        public RestClientAutolog(string baseUrl) : base(baseUrl)
        {
            Startup(null);
        }

        public RestClientAutolog(Uri baseUrl) : base(baseUrl)
        {
            Startup(null);
        }

        public RestClientAutolog()
        {
            Startup(null);
        }

        private void Startup(RestClientAutologConfiguration configuration)
        {
            if (configuration == null)
            { 
                configuration = GlobalConfiguration != null
                    ? GlobalConfiguration.Clone()
                    : new RestClientAutologConfiguration();
            }

            Configuration = configuration;
        }

        public override IRestResponse Execute(IRestRequest request)
        {
            var stopwatch = Stopwatch.StartNew();

            var response = base.Execute(request);
            stopwatch.Stop(); 

            LogRequestAndResponse(response, stopwatch);

            return response;
        }

        public new Task<IRestResponse> ExecuteAsync(IRestRequest request, Method method, CancellationToken token = default)
        {
            request.Method = method;
            return ExecuteAsync(request, token);
        }

        public new Task<IRestResponse> ExecuteAsync(IRestRequest request, CancellationToken token = default)
        {
            var stopwatch = Stopwatch.StartNew();

            var response = base.ExecuteAsync(request, token).GetAwaiter().GetResult();
            stopwatch.Stop();

            LogRequestAndResponse(response, stopwatch);

            return Task.FromResult(response) ;
        }

        public new Task<IRestResponse<T>> ExecuteAsync<T>(IRestRequest request, CancellationToken token = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = base.ExecuteAsync<T>(request, token).GetAwaiter().GetResult();
            stopwatch.Stop();

            LogRequestAndResponse(response, stopwatch);

            return Task.FromResult(response);
        }

        private void LogRequestAndResponse(IRestResponse response, Stopwatch stopwatch)
        {
            if (Configuration.LoggerConfiguration != null)
                Log.Logger = Configuration.LoggerConfiguration.CreateLogger();

            var uri = BuildUri(response.Request);
            var properties = new Dictionary<string, object>();
            if (AdditionalProperties?.Any() == true)
            {
                foreach(var item in AdditionalProperties)
                {
                    properties.Add(item.Key, item.Value);
                }
            }

            properties.Add("Agent", "RestSharp");
            properties.Add("ElapsedMilliseconds", stopwatch.ElapsedMilliseconds);
            properties.Add("Method", response.Request.Method.ToString());
            properties.Add("Url", uri.AbsoluteUri);
            properties.Add("Host", uri.Host);
            properties.Add("Path", uri.AbsolutePath);
            properties.Add("Port", uri.Port);
            properties.Add("QueryString", uri.Query);
            properties.Add("Query", GetRequestQueryStringAsObject(response.Request));
            properties.Add("RequestBody", GetRequestBody(response.Request));
            properties.Add("RequestHeaders", GetRequestHeaders(response.Request));
            properties.Add("StatusCode", (int)response.StatusCode);
            properties.Add("StatusCodeFamily", ((int)response.StatusCode).ToString()[0] + "XX");
            properties.Add("StatusDescription", response.StatusDescription?.Replace(" ",""));
            properties.Add("ResponseStatus", response.ResponseStatus.ToString());
            properties.Add("ProtocolVersion", response.ProtocolVersion);
            properties.Add("IsSuccessful", response.IsSuccessful);
            properties.Add("ErrorMessage", response.ErrorMessage);
            properties.Add("ErrorException", GetResponseException(response.ErrorException));
            properties.Add("ResponseContent", GetResponseContent(response));
            properties.Add("ContentLength", response.ContentLength);
            properties.Add("ContentType", response.ContentType);
            properties.Add("ResponseHeaders", GetResponseHeaders(response));
            properties.Add("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));

            using (LogContext.Push(new RestClientAutologEnricher(properties,
                GetIgnoredProperties(response.Request), Configuration.PropertiesToDestructure)))
            {
                if (response.IsSuccessful)
                {
                    Log.Information(Configuration.MessageTemplateForSuccess);
                }
                else
                {
                    Log.Error(Configuration.MessageTemplateForError);
                }
            }
        }

        private static string[] GetIgnoredProperties(IRestRequest request)
        {
            var ignoredProperties = request.Parameters
                .FirstOrDefault(p => p.Type == ParameterType.HttpHeader && p.Name == "LogIgnored");

            return ignoredProperties?.Value == null ? new string[] { } : ignoredProperties.Value.ToString().Split(',');
        }

        private object GetRequestQueryStringAsObject(IRestRequest request)
        {
            var parameters = request.Parameters.Where(p => p.Type == ParameterType.QueryString);
            var grouped = parameters.GroupBy(r => r.Name);

            var result = grouped.ToDictionary(group => group.Key, group => string.Join(",", group.Select(r => r.Value)));

            return result.Any() ? result : null;
        }

        private object GetRequestBody(IRestRequest request)
        {
            var body = request?.Parameters?.FirstOrDefault(p => p.Type == ParameterType.RequestBody);

            var isJson = request?.Parameters?.Exists(p => 
                p.Type == ParameterType.HttpHeader &&
                p.Name == "Content-Type" && 
                p.Value?.ToString().Contains("json") == true
                ||
                p.Type == ParameterType.RequestBody &&
                (p.Name?.ToString().Contains("json") == true ||
                 p.DataFormat == DataFormat.Json ||
                 p.ContentType?.Contains("application/json") == true))
                ?? false;

            var isForm = request?.Parameters?.Exists(p =>
                p.Type == ParameterType.HttpHeader &&
                p.Name == "Content-Type" &&
                p.Value?.ToString().Contains("x-www-form-urlencoded") == true) 
                ?? false;

            if (body?.Value == null)
                return body?.Value;

            if (!isJson)
                return isForm ? GetContentAsObjectByContentTypeForm(body.Value.ToString()) : body.Value;

            var content = body.Value is string ? body.Value.ToString() : JsonConvert.SerializeObject(body.Value);
            return GetContentAsObjectByContentTypeJson(content, true, Configuration.JsonBlacklist);

        }

        private object GetResponseContent(IRestResponse response)
        {
            if (response.ContentLength > Configuration.MaxResponseContentLengthToLogInBytes)
                return $"**** Response contents suppressed: content length exceeds {Configuration.MaxResponseContentLengthToLogInBytes} maximum bytes. ****";

            var content = response.Content;
            var isJson = response.ContentType?.Contains("json") == true;
            if (content != null && isJson)
                return GetContentAsObjectByContentTypeJson(content, false, null);

            return content;
        }

        private static object GetContentAsObjectByContentTypeForm(string content)
        {
            var parts = content.Split('&');
            var partsKeyValue = parts.Select(r =>
                new
                {
                    Key = HttpUtility.UrlDecode(r.Split('=').FirstOrDefault()),
                    Value = HttpUtility.UrlDecode(r.Split('=').Skip(1).LastOrDefault())
                });

            var grouped = partsKeyValue.GroupBy(r => r.Key);

            return grouped.ToDictionary(group => group.Key, group => string.Join(",", group.Select(r => r.Value)));
        }

        private static object GetContentAsObjectByContentTypeJson(string content, bool maskJson, string[] blacklist)
        {
            try
            {
                if (maskJson && (blacklist?.Any() ?? false))
                    content = content.MaskFields(blacklist, "******");
            }
            catch (Exception ex)
            {
                using (LogContext.PushProperty("SourceContentValue", content))
                using (LogContext.PushProperty("BlacklistedProperties", blacklist))
                    Log.Warning(ex, "[RestClientAutolog] Unexpected error trying to mask blacklisted JSON fields...");
            }

            try
            {
                var jToken = JToken.Parse(content);
                return DeserializeAsObjectCore(jToken);
            }
            catch (Exception ex)
            {
                using (LogContext.PushProperty("SourceContentValue", content))
                using (LogContext.PushProperty("BlacklistedProperties", blacklist))
                    Log.Warning(ex, "[RestClientAutolog] Unexpected error trying to deserialize JSON content...");
            }

            return content;
        }

        /// <summary>
        /// Cast jtoken to object
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private static object DeserializeAsObjectCore(JToken token)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (token.Type)
            {
                case JTokenType.Object:
                    return token.Children<JProperty>()
                        .ToDictionary(prop => prop.Name,
                            prop => DeserializeAsObjectCore(prop.Value));

                case JTokenType.Array:
                    return token.Select(DeserializeAsObjectCore).ToList();

                default:
                    return ((JValue)token).Value;
            }
        }

        private object GetRequestHeaders(IRestRequest request)
        {
            var requestParameters = request.Parameters.Where(p => p.Type == ParameterType.HttpHeader);
            var clientParameters = DefaultParameters.Where(p => p.Type == ParameterType.HttpHeader);

            var parameters = requestParameters.Union(clientParameters);
            var grouped = parameters.GroupBy(r => r.Name);

            return grouped.ToDictionary(group => group.Key, group => string.Join(",", group.Select(r => r.Value)));
        }

        private static object GetResponseHeaders(IRestResponse response)
        {
            var parameters = response?.Headers ?? new List<Parameter>();
            var grouped = parameters.GroupBy(r => r.Name);
            var result = grouped.ToDictionary(group => group.Key, group => string.Join(",", group.Select(r => r.Value)));

            return result.Any() ? result : null;
        }

        private static object GetResponseException(Exception exception)
        {
            return exception != null
                ? new
                {
                    exception.Message,
                    exception.StackTrace,
                    InnerExceptions = exception is AggregateException aggregate && aggregate.InnerExceptions.Count > 0
                        ? aggregate.InnerExceptions.Select(GetResponseException).ToList()
                        : new List<object> { GetResponseException(exception.InnerException) }.Where(o => o != null)
                            .ToList()
                }
                : null;
        }
    }
}