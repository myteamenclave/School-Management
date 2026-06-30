namespace SchoolMgmt.Infrastructure.MultiTenancy;

public class SeedDataOptions
{
    public const string SectionName = "SeedData";

    // Fixed well-known id so it's identical across environments without a lookup.
    public Guid DefaultSchoolId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public string DefaultSchoolName { get; set; } = "Demo School";
}
