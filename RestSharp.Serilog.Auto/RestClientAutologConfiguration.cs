using Serilog;

// ReSharper disable once CheckNamespace
namespace RestSharp
{
    public class RestClientAutologConfiguration
    {
        internal const string DefaultMessageTemplateForError =
            "[{Application}] HTTP {Method} {Url} responded {StatusCode} in {ElapsedMilliseconds} ms";

        internal const string DefaultMessageTemplateForSuccess =
            "[{Application}] HTTP {Method} {Url} responded {StatusCode} in {ElapsedMilliseconds} ms";

        private string _messageTemplateForError;

        public string MessageTemplateForError
        {
            get => string.IsNullOrWhiteSpace(_messageTemplateForError)
                ? DefaultMessageTemplateForError
                : _messageTemplateForError;
            set => _messageTemplateForError = value;
        }

        private string _messageTemplateForSuccess;

        public string MessageTemplateForSuccess
        {
            get => string.IsNullOrWhiteSpace(_messageTemplateForSuccess)
                ? DefaultMessageTemplateForSuccess
                : _messageTemplateForSuccess;
            set => _messageTemplateForSuccess = value;
        }

        public string[] JsonBlacklist { get; set; }

        public LoggerConfiguration LoggerConfiguration { get; set; }

        public string[] PropertiesToDestructure { get; set; }

        /// <summary>
        /// Defines the maximum response content length that should be logged, in bytes.
        /// If the response content length exceeds this value, the response contents won't be logged at all,
        /// avoiding a possible overload at the logging system. The default value is 128KB (154112 bytes).
        /// </summary>
        public long MaxResponseContentLengthToLogInBytes { get; set; } = 1024 * 128;

        public RestClientAutologConfiguration Clone()
        {
            return (RestClientAutologConfiguration)MemberwiseClone();
        }
    }
}
