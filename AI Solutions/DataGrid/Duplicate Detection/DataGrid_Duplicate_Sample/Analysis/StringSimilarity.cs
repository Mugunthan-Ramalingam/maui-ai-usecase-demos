using System.Text;
using System.Text.RegularExpressions;

namespace DataGrid_Duplicate_Sample;

/// <summary>
/// Classic string-similarity primitives used by the pre-filter and the
/// supporting-signal classifier. Self-contained — no external NuGet.
/// </summary>
public static class StringSimilarity
{
    public static int Levenshtein(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
        for (int j = 1; j <= b.Length; j++)
        {
            int cost = char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1;
            d[i, j] = Math.Min(
                Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                d[i - 1, j - 1] + cost);
        }
        return d[a.Length, b.Length];
    }

    public static double JaroWinkler(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;
        var jaro = Jaro(a.ToLowerInvariant(), b.ToLowerInvariant());
        // Winkler boost: up to 0.1 extra for matching 4-char prefix
        int prefix = 0;
        for (int i = 0; i < Math.Min(4, Math.Min(a.Length, b.Length)); i++)
            if (char.ToLowerInvariant(a[i]) == char.ToLowerInvariant(b[i])) prefix++;
            else break;
        return jaro + 0.1 * prefix * (1 - jaro);
    }

    private static double Jaro(string a, string b)
    {
        if (a == b) return 1.0;
        int matchDistance = Math.Max(a.Length, b.Length) / 2 - 1;
        if (matchDistance < 0) matchDistance = 0;
        var aMatches = new bool[a.Length];
        var bMatches = new bool[b.Length];
        int matches = 0, transpositions = 0;
        for (int i = 0; i < a.Length; i++)
        {
            int start = Math.Max(0, i - matchDistance);
            int end = Math.Min(i + matchDistance + 1, b.Length);
            for (int j = start; j < end; j++)
                if (!bMatches[j] && a[i] == b[j])
                {
                    aMatches[i] = true;
                    bMatches[j] = true;
                    matches++;
                    break;
                }
        }
        if (matches == 0) return 0.0;
        int k = 0;
        for (int i = 0; i < a.Length; i++)
            if (aMatches[i])
            {
                while (!bMatches[k]) k++;
                if (a[i] != b[k]) transpositions++;
                k++;
            }
        double m = matches;
        return (m / a.Length + m / b.Length + (m - transpositions / 2.0) / m) / 3.0;
    }

    /// <summary>
    /// Normalizes a free-form address line to a comparable canonical form:
    ///   - lower-cased
    ///   - common abbreviations expanded (St -> street, Ave -> avenue, etc.)
    ///   - punctuation removed
    ///   - whitespace collapsed and trimmed
    /// Returns the empty string if the address is null/whitespace.
    ///
    /// Two addresses that normalize to the same string are considered
    /// "identical enough" to be treated as a duplicate signal even when every
    /// other field differs (e.g. two unrelated people registered at the same
    /// physical address).
    /// </summary>
    public static string NormalizeAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return string.Empty;

        var s = address.ToLowerInvariant();

        // Token-level abbreviation expansion. We do this with a regex on whole
        // words so "st" inside "first" isn't rewritten.
        s = Regex.Replace(s, @"\bstreet\b",  "street",  RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bst\.",      "street",  RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bave\b",     "avenue",  RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bave\.",     "avenue",  RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bavenue\b",  "avenue",  RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\brd\b",      "road",    RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\brd\.",      "road",    RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\broad\b",    "road",    RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bblvd\b",    "boulevard", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bboulevard\b", "boulevard", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bdr\b",      "drive",   RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bdrive\b",   "drive",   RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bapt\b",     "apartment", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bapartment\b", "apartment", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bste\b",     "suite",   RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bsuite\b",   "suite",   RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bfl\b",      "floor",   RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bfloor\b",   "floor",   RegexOptions.IgnoreCase);

        // Strip punctuation (anything that isn't a letter, digit or space).
        s = Regex.Replace(s, @"[^a-z0-9\s]", " ");

        // Collapse whitespace and trim.
        s = Regex.Replace(s, @"\s+", " ").Trim();

        return s;
    }

    /// <summary>
    /// True when two addresses normalize to the same canonical form.
    /// Either side being empty counts as not-equivalent.
    /// </summary>
    public static bool AddressesAreEquivalent(string? a, string? b)
    {
        var na = NormalizeAddress(a);
        var nb = NormalizeAddress(b);
        if (na.Length == 0 || nb.Length == 0) return false;
        return string.Equals(na, nb, StringComparison.Ordinal);
    }

    /// <summary>
    /// American Soundex (kept compact, returns 4-char code or empty string).
    /// </summary>
    public static string Soundex(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        s = s.ToUpperInvariant();
        var sb = new StringBuilder();
        char? lastCode = null;
        sb.Append(char.IsLetter(s[0]) ? s[0] : '?');
        for (int i = 1; i < s.Length && sb.Length < 4; i++)
        {
            char c = s[i];
            if (!char.IsLetter(c)) continue;
            char code = c switch
            {
                'B' or 'F' or 'P' or 'V' => '1',
                'C' or 'G' or 'J' or 'K' or 'Q' or 'S' or 'X' or 'Z' => '2',
                'D' or 'T' => '3',
                'L' => '4',
                'M' or 'N' => '5',
                'R' => '6',
                _ => '0'
            };
            if (code != '0' && code != lastCode) sb.Append(code);
            lastCode = code;
        }
        while (sb.Length < 4) sb.Append('0');
        return sb.ToString();
    }
}
