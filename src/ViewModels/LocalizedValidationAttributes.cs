using System.ComponentModel.DataAnnotations;

namespace SourceGit.ViewModels
{
    /// <summary>
    ///     Validation attributes used by the popups. Data annotations can only hold compile-time constants,
    ///     so the messages are stored as locale resource keys here and resolved when the error is produced.
    /// </summary>
    public sealed class LocalizedRequiredAttribute(string key) : RequiredAttribute
    {
        public override string FormatErrorMessage(string name) => App.Text(key);
    }

    public sealed class LocalizedRegularExpressionAttribute(string pattern, string key) : RegularExpressionAttribute(pattern)
    {
        public override string FormatErrorMessage(string name) => App.Text(key);
    }

    public sealed class LocalizedEmailAddressAttribute(string key) : ValidationAttribute
    {
        public override bool IsValid(object value) => _inner.IsValid(value);

        public override string FormatErrorMessage(string name) => App.Text(key);

        private static readonly EmailAddressAttribute _inner = new();
    }
}
