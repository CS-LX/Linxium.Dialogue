namespace Linxium.Dialogue {
    static class BasicHelper {
        public static bool HasContent(this string str, out string result) {
            result = str;
            return !string.IsNullOrEmpty(result);
        }
    }
}