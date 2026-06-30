namespace SchoolMgmt.Infrastructure.MultiTenancy;

public class SeedDataOptions
{
    public const string SectionName = "SeedData";

    // Fixed well-known id so it's identical across environments without a lookup.
    public Guid DefaultSchoolId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public string DefaultSchoolName { get; set; } = "Demo School";

    // Demo Admin user — see specs/02-implement-auth.md "Seed demo user".
    // Plaintext password is documented in .claude/context/project.md, NOT here.
    public Guid DefaultAdminId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public string DefaultAdminEmail { get; set; } = "admin@demoschool.test";
    public string DefaultAdminDisplayName { get; set; } = "Demo Admin";
    // PasswordHasher<User> hash of the demo password (plaintext documented in .claude/context/project.md).
    public string DefaultAdminPasswordHash { get; set; } =
        "AQAAAAIAAYagAAAAEKiisSrJc8mQ6njS1KzU0A4Oud9J5xDsV6OPOJQTAtF7ot+vRLJievbiUGErEmVcsg==";
}
