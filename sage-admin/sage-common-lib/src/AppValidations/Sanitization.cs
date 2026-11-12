using System.Text.RegularExpressions;

namespace SageCommonLib.AppValidations
{
    public class Sanitization
    {
        private static readonly Regex _tags = new Regex("<[^>]*(>|$)", RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        /// <summary>
        /// Strip every HTML tag from free text (names, descriptions, remarks). Sage does not accept rich text, so there is
        /// no tag whitelist (ixcheck keeps one). This is output hygiene only: SQL safety comes from parameterized queries.
        /// </summary>
        /// <param name="_inputValue"></param>
        /// <returns>string</returns>
        public static string Sanitize(string _inputValue)
        {
            if (string.IsNullOrEmpty(_inputValue))
            {
                return _inputValue;
            }
            return _tags.Replace(_inputValue, string.Empty).Trim();
        }
    }
}
