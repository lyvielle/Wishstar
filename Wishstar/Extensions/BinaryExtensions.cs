using System.Text;

namespace Wishstar.Extensions {
    public static class BinaryExtensions {
        public static string ByteArrayToString(this byte[] bytes) {
            StringBuilder hex = new(bytes.Length * 2);
            foreach(byte b in bytes) {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }

        public static byte[] HexStringToByteArray(this string hexString) {
            int NumberChars = hexString.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for(int i = 0; i < NumberChars; i += 2) {
                bytes[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            }
            return bytes;
        }
    }
}
