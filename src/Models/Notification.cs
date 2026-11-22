using System;

namespace SourceGit.Models
{
    public class Notification
    {
        public static event Action<Notification> Raised;

        public string Group { get; set; }
        public bool IsError { get; set; }

        /// <summary>
        ///     Messages produced by git itself are in English. Translate them here so that every notification
        ///     (no matter where it was created) is shown in the UI language.
        /// </summary>
        public string Message
        {
            get => _message;
            set => _message = GitMessageTranslator.Translate(value);
        }

        public static void Send(string group, string message, bool isError = false)
        {
            Raised?.Invoke(new Notification
            {
                Group = group,
                Message = message,
                IsError = isError
            });
        }

        private string _message = string.Empty;
    }
}
