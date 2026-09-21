using System.Globalization;
using System.Text;

namespace Roblox.Services;

public class FilterService : ServiceBase, IService
{
    private static readonly string[] baseFilteredWords =
    {
        "anal", "anally", "anus", "ballsac", "pussy", "ballsack", "beastiality",
        "beastility", "bestiality", "blowjob", "blowjobs", "boner", "bitch",
        "boob", "boobies", "boobs", "breast", "breasts", "buttfuck",
        "buttfucker", "cock", "cockride", "cocks", "cocksuck", "cocksucked",
        "cocksucker", "cocksucking", "cocksucks", "condom", "condoms", "condo",
        "cum", "cummer", "cumming", "cums", "cumshot", "cunilingus", "cunillingus",
        "cunnilingus", "dick", "dicks", "dildo", "dildos", "digga", "ejaculate",
        "ejaculated", "ejaculates", "ejaculating", "ejaculatings", "ejaculation",
        "faget", "fagg", "fag", "fagget", "fagging", "faggit", "faggot", "fagot",
        "faggots", "faggs", "fagit", "fagots", "fingerfuck", "fingerfucked",
        "fingerfucker", "fingerfuckers", "fingerfucking", "fingerfucks", "fistfuck",
        "fistfucked", "fistfucker", "fistfuckers", "fistfucking", "fistfuckings",
        "fistfucks", "gangbang", "gangbanged", "gangbangs", "gaysex", "hardcoresex",
        "hitler", "horniest", "horny", "hotsex", "jackingoff", "jackoff", "jackxoff",
        "jerkxoff", "kidsinasanbox", "kkk", "masterbait", "masterbate", "masturbate",
        "molest", "mycock", "nazi", "nazis", "niger", "nigger", "niigger", "niggers",
        "niiggers", "ngga", "negger", "neckhurt", "nigga", "n0gga", "nhigga", "n8ggas",
        "niigba", "niga", "nude", "nudism", "nudist", "orgasim", "orgasims", "orgasm",
        "orgasms", "pern", "pecker", "pedo", "pedobear", "penis", "phonesex", "porn",
        "pron", "porno", "pornography", "goon", "pornos", "pren", "prostitute",
        "paygorn", "raip", "raiping", "rape", "raped", "raper", "raping", "rapist",
        "schlong", "sex", "sexx", "sexxx", "sexxy", "sexytiem", "sexytime", "slut",
        "sluts", "sperm", "strip", "stripper", "stripperpole", "strippers", "swastika",
        "thong", "titties", "titty", "urcock", "vaggina", "vagina", "vegina",
        "vibrator", "wanker", "whore", "whorehouse", "yourcock", "femb", "fembx",
        "jerkingoff", "jerkoff", "kys", "killyourself", "killurself", "retard", "nigg"
    };

    private static readonly HashSet<string> canonicalFilteredWords;

    static FilterService()
    {
        canonicalFilteredWords = new HashSet<string>();
        foreach (var word in baseFilteredWords)
        {
            string canonical = GetCanonicalText(word, true);
            if (!string.IsNullOrEmpty(canonical)) canonicalFilteredWords.Add(canonical);
        }
    }

    public bool IsTextFiltered(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;

        var words = input.Split(new[] { ' ', '.', ',', '!', '?', '_', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            string canonicalWord = GetCanonicalText(word, true);
            if (canonicalFilteredWords.Contains(canonicalWord)) return true;
        }

        string condensedInput = GetCanonicalText(input, true);
        foreach (var badWord in canonicalFilteredWords)
        {
            if (badWord.Length > 3 && condensedInput.Contains(badWord))
            {
                return true;
            }
        }

        return false;
    }

    public string FilterText(string input)
    {
        if (IsTextFiltered(input))
        {
            return new string('#', input.Length);
        }

        return CleanText(input);
    }

    private static string GetCanonicalText(string input, bool collapseDuplicates)
    {
        if (string.IsNullOrEmpty(input)) return "";

        string normalized = input.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        StringBuilder sb = new StringBuilder(normalized.Length);
        char lastChar = '\0';

        foreach (char c in normalized)
        {
            if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c) || char.IsSeparator(c))
            {
                continue;
            }

            char mappedChar = c switch
            {
                '$' or '5' => 's',
                '@' or '4' => 'a',
                '!' or '1' or '|' => 'i',
                '0' => 'o',
                '3' => 'e',
                '7' => 't',
                '8' => 'b',
                'v' => 'u',
                _ => c
            };

            if (collapseDuplicates)
            {
                if (mappedChar != lastChar)
                {
                    sb.Append(mappedChar);
                    lastChar = mappedChar;
                }
            }
            else
            {
                sb.Append(mappedChar);
            }
        }

        return sb.ToString();
    }

    public static string CleanText(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        string normalized = input.Normalize(NormalizationForm.FormKC);
        StringBuilder sb = new StringBuilder(normalized.Length);

        foreach (char c in normalized)
        {
            if (char.IsSurrogate(c))
            {
                sb.Append(c);
                continue;
            }

            var category = char.GetUnicodeCategory(c);

            if (category == UnicodeCategory.NonSpacingMark ||
                category == UnicodeCategory.Format ||
                category == UnicodeCategory.Control ||
                category == UnicodeCategory.PrivateUse)
            {
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    public bool IsReusable() => true;
    public bool IsThreadSafe() => true;
}