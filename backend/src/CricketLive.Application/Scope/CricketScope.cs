namespace CricketLive.Application.Scope;

/// <summary>
/// Which cricket this site covers: internationals between ICC Full Members, and India's own
/// national competitions.
/// </summary>
/// <remarks>
/// <para>
/// The provider serves every match it can find, which is a great deal more cricket than this site
/// is for. Its match list is 15,531 deep and its series index 1,190, and left unfiltered the
/// result was a home page offering Sheffield Shield fixtures and a series list where roughly a
/// third of the entries were associate-nation tours — Portugal in Finland, Malta Women in Cyprus,
/// the Viking Cup. All real cricket, none of it what anyone came here for.
/// </para>
/// <para>
/// <b>Two rules, and the second is not a special case of the first.</b> An international is a
/// match between two Full Member national sides. An Indian domestic match is not international at
/// all — Mumbai and Vidarbha are not nations — so it is recognised by its competition instead.
/// Nothing else is covered, which is a decision about this site and not a judgement about the
/// cricket.
/// </para>
/// <para>
/// <b>Deliberately decided from names, because names are all there is.</b> CricketData has no team
/// identifier and no country field on a series, so there is nothing to join against: a match
/// names two sides as free text and a series is a sentence. That makes this inherently a
/// best-effort reading rather than a lookup, which is why <see cref="Unrecognised"/> exists — the
/// gaps are meant to be discoverable rather than silent.
/// </para>
/// </remarks>
public static class CricketScope
{
    /// <summary>
    /// The twelve Test-playing nations, longest first.
    /// </summary>
    /// <remarks>
    /// Order matters for <see cref="NationOf"/>, which takes the first name a side starts with:
    /// "South Africa" has to be tried before any shorter name it begins with, and the sort keeps
    /// that true without anyone having to maintain the order by hand.
    /// </remarks>
    private static readonly string[] FullMembers =
    [
        .. new[]
        {
            "Afghanistan",
            "Australia",
            "Bangladesh",
            "England",
            "India",
            "Ireland",
            "New Zealand",
            "Pakistan",
            "South Africa",
            "Sri Lanka",
            "West Indies",
            "Zimbabwe",
        }.OrderByDescending(name => name.Length),
    ];

    /// <summary>
    /// What may follow a nation's name and still leave it a national side.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A squad below the senior men's team is still that country playing, so "India A", "England
    /// Women" and "Sri Lanka U19" are all in. The alternative — senior men only — would have cut
    /// the Women's T20 World Cup and every A tour, and "all international matches" plainly means
    /// more than eleven men.
    /// </para>
    /// <para>
    /// A closed list rather than "anything after the nation", because the remainder is what
    /// separates a national side from a club named after a place. Without it, "Professional County
    /// Club Select XI" would be unobjectionable and so would any franchise carrying a country in
    /// its name.
    /// </para>
    /// </remarks>
    private static readonly HashSet<string> SquadQualifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "A", "B", "Women", "Men", "Emerging", "XI",
        "U19", "U-19", "Under19", "Under-19", "U23", "U-23", "Under23", "Under-23",
        // What some boards call their second XI instead of "A".
        "Lions", "Wolves", "Shaheens",
    };

    /// <summary>
    /// India's own competitions, by the part of the name that identifies them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The BCCI's national tournaments and the two franchise leagues, and nothing below that: the
    /// state T20 leagues — Tamil Nadu Premier League, Maharaja Trophy, Andhra Premier League —
    /// are deliberately out. That was a choice between "everything played in India" and "India's
    /// national competitions", and the narrower one is what was asked for.
    /// </para>
    /// <para>
    /// <b>This list is the part of the policy that will go out of date.</b> A nation can be
    /// derived from a team name; a competition cannot be derived from anything, so each one has to
    /// be named here. A tournament absent from this list is simply not covered, and
    /// <see cref="Unrecognised"/> is how that becomes visible instead of being assumed away.
    /// </para>
    /// </remarks>
    private static readonly string[] IndianCompetitions =
    [
        "Indian Premier League",
        "Women's Premier League",
        "Womens Premier League",
        "Ranji Trophy",
        "Duleep Trophy",
        "Irani Cup",
        "Vijay Hazare Trophy",
        "Syed Mushtaq Ali Trophy",
        "Deodhar Trophy",
        "Challenger Trophy",
        "Col CK Nayudu Trophy",
        "Cooch Behar Trophy",
        "Vinoo Mankad Trophy",
    ];

    /// <summary>
    /// Whether one match is covered.
    /// </summary>
    /// <remarks>
    /// The competition is checked first because it settles the Indian domestic case outright, and
    /// those sides are states rather than countries so the international test cannot settle it.
    /// </remarks>
    public static bool IncludesMatch(string? seriesName, string? homeTeamName, string? awayTeamName)
        => IsIndianCompetition(seriesName)
            || (NationOf(homeTeamName) is not null && NationOf(awayTeamName) is not null);

    /// <summary>
    /// Whether a series is covered, judged from its name alone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Needed as well as <see cref="IncludesMatch"/> because the provider's series index carries a
    /// name, a date and a match count — no teams. So the only question answerable here is whether
    /// the name itself names two Full Members, which "England tour of Australia, 2026" does and
    /// "Viking Cup 2026" does not.
    /// </para>
    /// <para>
    /// <b>An unreadable name is treated as not covered, and that is safe in one direction only.</b>
    /// It is safe because a series we hold matches of is listed from those matches regardless of
    /// this — see <c>SeriesService.ListedAsync</c>, where the index can add a series but never
    /// remove one. So the cost of a name this cannot parse is that the series stays off the list
    /// until it has actually been played, rather than disappearing once it has. Tri-series and
    /// neutral-venue tournaments name one country or none, and that is the group this loses:
    /// "Tri Nation A Series in Sri Lanka" reads as one nation and waits for its first match.
    /// </para>
    /// </remarks>
    public static bool IncludesSeries(string? seriesName)
    {
        if (IsIndianCompetition(seriesName))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(seriesName))
        {
            return false;
        }

        var found = 0;

        foreach (var nation in FullMembers)
        {
            if (seriesName.Contains(nation, StringComparison.OrdinalIgnoreCase))
            {
                found++;
            }

            if (found == 2)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The Full Member nation a side belongs to, or <see langword="null"/> if it is not one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Public because it is the honest way to ask "is this a national side", and naming the nation
    /// rather than returning a bare bool makes that readable at the call site.
    /// </para>
    /// <para>
    /// No Full Member's name begins with another's, so taking the first prefix that matches is
    /// unambiguous. Worth noting that "India" is not a substring of "West Indies" — "Indies"
    /// diverges at the fifth letter — so the one collision this list looks like it has, it does
    /// not.
    /// </para>
    /// </remarks>
    public static string? NationOf(string? teamName)
    {
        var name = Normalise(teamName);

        if (name.Length == 0)
        {
            return null;
        }

        foreach (var nation in FullMembers)
        {
            if (!name.StartsWith(nation, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var remainder = name[nation.Length..];

            return IsSquadQualifier(remainder) ? nation : null;
        }

        return null;
    }

    /// <summary>
    /// A team name as the comparisons here need it.
    /// </summary>
    /// <remarks>
    /// The match index writes a side as <c>"India [IND]"</c> while every other endpoint writes
    /// "India", so the trailing code is removed rather than every caller having to know which
    /// source it is holding. Done by index rather than by regex because the shape is a fixed
    /// suffix and the whole rule is "drop what is in brackets at the end".
    /// </remarks>
    private static string Normalise(string? teamName)
    {
        var name = teamName?.Trim() ?? string.Empty;

        var bracket = name.LastIndexOf('[');

        return bracket > 0 && name.EndsWith(']') ? name[..bracket].TrimEnd() : name;
    }

    /// <summary>
    /// Whether what follows a nation's name leaves it a national side.
    /// </summary>
    /// <remarks>
    /// Every word has to be a known qualifier, so "India A Women" passes and "India Cements" does
    /// not. Nothing at all passes too, which is the senior side.
    /// </remarks>
    private static bool IsSquadQualifier(string remainder)
    {
        var words = remainder.Split(
            [' ', '-'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return words.Length == 0 || words.All(SquadQualifiers.Contains);
    }

    private static bool IsIndianCompetition(string? seriesName)
        => seriesName is not null
            && IndianCompetitions.Any(competition =>
                seriesName.Contains(competition, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Why a match was not covered, for logging — never shown to a reader.
    /// </summary>
    /// <remarks>
    /// The point of this is that the two lists above are hand-maintained and will fall behind the
    /// cricket. A new Indian competition, or a side the provider has started spelling differently,
    /// shows up here as a named reason rather than as a match that quietly stopped appearing.
    /// </remarks>
    public static string Unrecognised(string? seriesName, string? homeTeamName, string? awayTeamName)
    {
        var home = NationOf(homeTeamName);
        var away = NationOf(awayTeamName);

        return (home, away) switch
        {
            (null, null) => $"neither '{homeTeamName}' nor '{awayTeamName}' is a Full Member side, "
                + $"and '{seriesName}' is not a covered competition",
            (null, _) => $"'{homeTeamName}' is not a Full Member side",
            (_, null) => $"'{awayTeamName}' is not a Full Member side",
            _ => "covered",
        };
    }
}
