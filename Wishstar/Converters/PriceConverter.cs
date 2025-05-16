using System.Text.Json;
using System.Text.Json.Serialization;
using Wishstar.Models;

namespace Wishstar.Converters {
    public class PriceConverter : JsonConverter<Price> {
        public override Price Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            double priceInEUR = reader.GetDouble();
            return new Price(priceInEUR, CurrencyType.EUR);
        }

        public override void Write(Utf8JsonWriter writer, Price value, JsonSerializerOptions options) {
            writer.WriteNumberValue(value.GetPrice(CurrencyType.EUR));
        }
    }
}