namespace USPSAddressValidator
{
    public static class Common
    {
        private static string PluralSuffix(int count)
        {
            return count == 1 ? "" : "s";
        }

        private static bool IsNumeric(string s)
        {
            return int.TryParse(s, out _);
        }
    }
}
