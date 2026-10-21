using System.Text.RegularExpressions;

namespace DataGrid_Duplicate_Sample;

/// <summary>
/// Heuristic pre-filter that decides which customer pairs are worth sending
/// to the AI analyzer. The goal is to keep the AI prompt small and cheap —
/// we never want to ship 200 pairs to GPT-4o when only 12 of them look
/// like plausible duplicates.
///
/// Rules (any one of):
///  - Shared email local part (case-insensitive)
///  - Phone last-7-digits match
///  - Jaro-Winkler(name) >= 0.88  (catches reorders, initials, missing middle)
///  - Address line token overlap >= 0.6 (after stop-word removal)
///  - Company similarity >= 0.85 (Jaro-Winkler on the normalized company)
/// </summary>
public class PairCandidateGenerator
{
    private static readonly HashSet<string> AddressStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","of","and","or","at","in","on","unit","apt","apartment",
        "suite","ste","floor","fl","no","number","#"
    };

    public List<CustomerPair> BuildPairs(IEnumerable<Customer> customers)
    {
        var list = customers.ToList();
        var pairs = new List<CustomerPair>();
        int seq = 0;

        for (int i = 0; i < list.Count; i++)
        {
            for (int j = i + 1; j < list.Count; j++)
            {
                var a = list[i];
                var b = list[j];
                if (IsPlausibleDuplicate(a, b))
                {
                    pairs.Add(new CustomerPair
                    {
                        PairId = $"P{++seq:D4}",
                        A = ToSummary(a),
                        B = ToSummary(b)
                    });
                }
            }
        }
        return pairs;
    }

    private static bool IsPlausibleDuplicate(Customer a, Customer b)
    {
        // Email
        var aLocal = EmailLocal(a.Email);
        var bLocal = EmailLocal(b.Email);
        if (!string.IsNullOrEmpty(aLocal) && aLocal == bLocal) return true;

        // Phone — last 7 digits
        var aPhone = Digits(a.Phone);
        var bPhone = Digits(b.Phone);
        if (aPhone.Length >= 7 && bPhone.Length >= 7 &&
            aPhone[^7..] == bPhone[^7..]) return true;

        // Name
        if (StringSimilarity.JaroWinkler(NormalizeName(a.Name), NormalizeName(b.Name)) >= 0.88)
            return true;

        // Company
        var aCo = NormalizeCompany(a.Company);
        var bCo = NormalizeCompany(b.Company);
        if (!string.IsNullOrEmpty(aCo) && !string.IsNullOrEmpty(bCo) &&
            StringSimilarity.JaroWinkler(aCo, bCo) >= 0.85) return true;

        // Address
        var aTokens = AddressTokens(a.Address);
        var bTokens = AddressTokens(b.Address);
        if (aTokens.Count > 0 && bTokens.Count > 0)
        {
            var overlap = aTokens.Intersect(bTokens, StringComparer.OrdinalIgnoreCase).Count();
            var ratio = (double)overlap / Math.Max(1, Math.Min(aTokens.Count, bTokens.Count));
            if (ratio >= 0.6) return true;
        }

        return false;
    }

    private static CustomerSummary ToSummary(Customer c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Email = c.Email,
        Phone = c.Phone,
        Address = c.Address,
        City = c.City,
        State = c.State,
        PostalCode = c.PostalCode,
        Company = c.Company
    };

    private static string EmailLocal(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;
        var at = email.IndexOf('@');
        return at <= 0 ? string.Empty : email[..at].Trim().ToLowerInvariant();
    }

    private static string Digits(string s) =>
        string.IsNullOrEmpty(s) ? string.Empty : new string(s.Where(char.IsDigit).ToArray());

    private static string NormalizeName(string name) =>
        string.IsNullOrWhiteSpace(name) ? string.Empty :
        Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9\s]", "").Trim();

    private static string NormalizeCompany(string company)
    {
        if (string.IsNullOrWhiteSpace(company)) return string.Empty;
        var s = company.ToLowerInvariant();
        foreach (var suffix in new[] { " ltd", " llc", " inc", " pvt ltd", " corporation", " corp", " co", ".", ",", "  " })
            s = s.Replace(suffix, " ");
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    private static List<string> AddressTokens(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return new List<string>();
        var tokens = Regex.Split(address.ToLowerInvariant(), @"[^a-z0-9]+")
            .Where(t => t.Length > 1 && !AddressStopWords.Contains(t))
            .ToList();
        return tokens;
    }
}
