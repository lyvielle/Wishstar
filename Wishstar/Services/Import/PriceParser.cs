using System.Globalization;
using System.Text.RegularExpressions;
using Wishstar.Models;

namespace Wishstar.Services.Import {
    /// <param name="CanHaveSuperscriptCents">No decimals and at least 3 digits, e.g. "€27999" read from "€279⁹⁹".</param>
    public readonly record struct PriceMatch(double Amount, CurrencyType? Currency, bool CanHaveSuperscriptCents);

    /// <summary>Finds prices with a currency in a line of text, e.g. "279,99 €", "$1,299.99", "EUR 89,00", "¥3,980", "€59,–".</summary>
    public static partial class PriceParser {
        private const string Currency = @"US\$|\$|€|EUR|£|GBP|¥|￥|円|JPY|RSD|din\.?|дин\.?";

        // number: 1.299,99 | 1,299.99 | 1 299,99 | 3,980 | 59,– | 279 99 (split cents, OCR of superscript)
        private const string Number = @"\d{1,3}(?:[.,'’   ]\d{3})+(?:[.,](?:\d{1,2}|[-–—]{1,2}))?(?!\d)|\d+(?:[.,](?:\d{1,2}|[-–—]{1,2}))?(?!\d)";

        [GeneratedRegex(@"(?<![\p{L}])(?<c1>" + Currency + @")\s{0,2}(?<n>" + Number + @")(?:\s(?<cents>\d{2})(?![\d.,]))?(?:\s{0,2}(?<c2>" + Currency + @")(?![\p{L}]))?"
            + @"|(?<n2>" + Number + @")(?:\s(?<cents2>\d{2})(?![\d.,]))?\s{0,2}(?<c3>" + Currency + @")(?![\p{L}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex PriceRegex();

        public static IEnumerable<PriceMatch> FindAll(string text) {
            foreach (Match match in PriceRegex().Matches(text)) {
                string currencyToken = match.Groups["c1"].Success ? match.Groups["c1"].Value
                    : match.Groups["c2"].Success ? match.Groups["c2"].Value
                    : match.Groups["c3"].Value;
                string number = match.Groups["n"].Success ? match.Groups["n"].Value : match.Groups["n2"].Value;
                string cents = match.Groups["cents"].Success ? match.Groups["cents"].Value : match.Groups["cents2"].Value;

                // A minus sign right before the number is a discount ("-20 €"), not a price.
                int start = match.Index;
                if (start > 0 && text[start - 1] is '-' or '−' or '–') {
                    continue;
                }

                if (ParseCurrency(currencyToken) is not CurrencyType currency || ParseAmount(number, cents, currency) is not double amount || amount <= 0) {
                    continue;
                }

                bool plainDigits = number.All(char.IsDigit) && number.Length >= 3 && string.IsNullOrEmpty(cents);
                yield return new PriceMatch(amount, currency, plainDigits && currency != CurrencyType.YEN);
            }
        }

        [GeneratedRegex(@"^\s*(?<n>\d{1,3}(?:[.,'’\u00A0\u202F ]\d{3})+(?:[.,](?:\d{1,2}|[-–—]{1,2}))?|\d+[.,](?:\d{2}|[-–—]{1,2}))\s*\*?\s*$")]
        private static partial Regex BareAmountRegex();

        /// <summary>
        /// A line that is only a formatted amount ("3,980", "49,99") – OCR sometimes drops the currency symbol.
        /// Plain integers are not accepted, they are too often quantities or ratings.
        /// </summary>
        public static PriceMatch? ParseBareAmount(string text) {
            Match match = BareAmountRegex().Match(text);
            if (!match.Success || ParseAmount(match.Groups["n"].Value, null, CurrencyType.EUR) is not double amount || amount <= 0) {
                return null;
            }

            return new PriceMatch(amount, null, false);
        }

        public static CurrencyType? ParseCurrency(string token) {
            return token.Trim().TrimEnd('.').ToUpperInvariant() switch {
                "€" or "EUR" => CurrencyType.EUR,
                "$" or "US$" or "USD" => CurrencyType.USD,
                "£" or "GBP" => CurrencyType.GBP,
                "¥" or "￥" or "円" or "JPY" => CurrencyType.YEN,
                "RSD" or "DIN" or "ДИН" => CurrencyType.RSD,
                _ => null
            };
        }

        public static double? ParseAmount(string number, string? splitCents, CurrencyType currency) {
            string n = number.Trim();
            bool dashDecimals = n.EndsWith('-') || n.EndsWith('–') || n.EndsWith('—');
            if (dashDecimals) {
                n = n.TrimEnd('-', '–', '—');
                n = n.TrimEnd('.', ',');
            }

            string integerPart = n;
            string decimals = string.Empty;
            int lastSeparator = n.LastIndexOfAny(['.', ',']);
            if (lastSeparator >= 0 && n.Length - lastSeparator - 1 is 1 or 2) {
                integerPart = n[..lastSeparator];
                decimals = n[(lastSeparator + 1)..];
            }

            integerPart = new string([.. integerPart.Where(char.IsDigit)]);
            if (integerPart.Length == 0) {
                return null;
            }

            // "€279 99" – OCR of a price with superscript cents (yen has no cents).
            if (decimals.Length == 0 && !dashDecimals && !string.IsNullOrEmpty(splitCents) && currency != CurrencyType.YEN) {
                decimals = splitCents;
            }

            string normalized = decimals.Length > 0 ? $"{integerPart}.{decimals}" : integerPart;
            return double.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double value) ? value : null;
        }
    }
}
