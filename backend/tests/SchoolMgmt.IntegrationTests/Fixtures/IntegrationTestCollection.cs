namespace SchoolMgmt.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "Postgres integration tests";
}
