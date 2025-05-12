namespace Wishstar.Models {
    public enum CurrencyType {
        EUR,
        RSD,
        YEN,
        USD,
        GBP
    }

    public class CurrencyDescriptor(CurrencyType currencyType, string name, string symbol) {
        public CurrencyType CurrencyType { get; } = currencyType;
        public string Name { get; } = name;
        public string Symbol { get; } = symbol;
        public string Code { get; } = Enum.GetName(currencyType) ?? "?";

        public static CurrencyDescriptor[] GetAllCurrencies() {
            return [
                new CurrencyDescriptor(CurrencyType.EUR, "Euro", "€"),
                new CurrencyDescriptor(CurrencyType.RSD, "Serbian Dinar", "RSD"),
                new CurrencyDescriptor(CurrencyType.YEN, "Japanese Yen", "¥"),
                new CurrencyDescriptor(CurrencyType.USD, "US Dollar", "$"),
                new CurrencyDescriptor(CurrencyType.GBP, "British Pound", "£")
            ];
        }

        public static CurrencyDescriptor GetDescriptor(CurrencyType currencyType) {
            return GetAllCurrencies().FirstOrDefault(c => c.CurrencyType == currencyType) ?? new CurrencyDescriptor(currencyType, "Unknown", "?");
        }
    }

    public class Price {
        public double EUR { get; }
        public double RSD { get; }
        public double YEN { get; }
        public double USD { get; }
        public double GBP { get; }

        private const double EUR_TO_RSD = 117.5;
        private const double EUR_TO_YEN = 157.0;
        private const double EUR_TO_USD = 1.08;
        private const double EUR_TO_GBP = 0.86;

        public Price(double amount, CurrencyType currencyType) {
            double amountInEUR = currencyType switch {
                CurrencyType.EUR => amount,
                CurrencyType.RSD => amount / EUR_TO_RSD,
                CurrencyType.YEN => amount / EUR_TO_YEN,
                CurrencyType.USD => amount / EUR_TO_USD,
                CurrencyType.GBP => amount / EUR_TO_GBP,
                _ => throw new ArgumentException("Unsupported currency type.")
            };

            EUR = amountInEUR;
            RSD = amountInEUR * EUR_TO_RSD;
            YEN = amountInEUR * EUR_TO_YEN;
            USD = amountInEUR * EUR_TO_USD;
            GBP = amountInEUR * EUR_TO_GBP;
        }

        public double GetPrice(CurrencyType currencyType) {
            return currencyType switch {
                CurrencyType.EUR => EUR,
                CurrencyType.RSD => RSD,
                CurrencyType.YEN => YEN,
                CurrencyType.USD => USD,
                CurrencyType.GBP => GBP,
                _ => throw new ArgumentException("Unsupported currency type.")
            };
        }

        public string ToString(CurrencyType currencyType) {
            return currencyType switch {
                CurrencyType.EUR => $"{EUR:F2} €",
                CurrencyType.RSD => $"{RSD:F2} RSD",
                CurrencyType.YEN => $"{YEN:F2} ¥",
                CurrencyType.USD => $"{USD:F2} $",
                CurrencyType.GBP => $"{GBP:F2} £",
                _ => throw new ArgumentException("Unsupported currency type.")
            };
        }

        public static Price FromEUR(double eur) {
            return new Price(eur, CurrencyType.EUR);
        }

        public static Price FromRSD(double rsd) {
            return new Price(rsd, CurrencyType.RSD);
        }

        public static Price FromYEN(double yen) {
            return new Price(yen, CurrencyType.YEN);
        }

        public static Price FromUSD(double usd) {
            return new Price(usd, CurrencyType.USD);
        }

        public static Price FromGBP(double gbp) {
            return new Price(gbp, CurrencyType.GBP);
        }

        public static Price CreateDefault() {
            return new Price(0, CurrencyType.EUR);
        }
    }
}