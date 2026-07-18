using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Gradebook;

internal static class LetterResolver
{
    // bands should be ordered MinScore desc. Returns the first band containing the score.
    public static string? Resolve(IReadOnlyList<GradeScaleBand> bands, decimal? score) =>
        score is null ? null
        : bands.FirstOrDefault(b => score >= b.MinScore && score <= b.MaxScore)?.Letter;
}
