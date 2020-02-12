namespace RestSharp.Serilog.Auto
{
    // ReSharper disable once UnusedMember.Global
    public static class RestClientAutologExtension
    {
        public static IRestClient AddLogAdditionalInfo(this IRestClient restClient, string key, string value)
        {
            if (!(restClient is RestClientAutolog))
            {
                return restClient;
            }

            var restClientAutolog = (RestClientAutolog)restClient;

            restClientAutolog.AddDefaultParameter(key, value, ParameterType.HttpHeader);
            restClientAutolog.AdditionalProperties[key] = value;

            return restClientAutolog;
        }
    }
}
