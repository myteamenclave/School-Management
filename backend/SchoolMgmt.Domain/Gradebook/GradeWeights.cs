namespace SchoolMgmt.Domain.Gradebook;

// Within-subject term rollup weights. NOT cross-subject GPA weighting.
// Fixed school-wide constant for the demo (idea doc 12 — configurable per-subject is Not Doing).
public static class GradeWeights
{
    public const decimal Midterm = 0.30m;
    public const decimal Final = 0.40m;
    public const decimal Coursework = 0.30m;
}
