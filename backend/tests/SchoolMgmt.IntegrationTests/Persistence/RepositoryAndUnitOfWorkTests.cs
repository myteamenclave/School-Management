using Microsoft.EntityFrameworkCore;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Infrastructure.Persistence;
using SchoolMgmt.Infrastructure.Persistence.Repositories;
using SchoolMgmt.IntegrationTests.Fixtures;
using SchoolMgmt.IntegrationTests.TestSupport;

namespace SchoolMgmt.IntegrationTests.Persistence;

[Collection(IntegrationTestCollection.Name)]
public class RepositoryAndUnitOfWorkTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task Repository_AddAsync_DoesNotPersist_UntilUnitOfWork_SaveChangesAsync()
    {
        var connectionString = await fixture.CreateProbeDatabaseAsync();
        var tenantId = Guid.NewGuid();
        await using var context = PostgresContainerFixture.CreateProbeDbContext(connectionString, tenantId);

        IRepository<ProbeEntity> repository = new Repository<ProbeEntity>(context);
        IUnitOfWork unitOfWork = new UnitOfWork(context);

        var probe = new ProbeEntity { Name = "Not yet saved" };
        await repository.AddAsync(probe);

        await using (var readContext = PostgresContainerFixture.CreateProbeDbContext(connectionString, tenantId))
        {
            Assert.False(await readContext.Probes.AnyAsync(p => p.Id == probe.Id));
        }

        await unitOfWork.SaveChangesAsync();

        await using (var readContext = PostgresContainerFixture.CreateProbeDbContext(connectionString, tenantId))
        {
            Assert.True(await readContext.Probes.AnyAsync(p => p.Id == probe.Id));
        }
    }

    [Fact]
    public async Task UnitOfWork_RollbackAsync_DiscardsChanges_MadeBetweenBeginAndRollback()
    {
        var connectionString = await fixture.CreateProbeDatabaseAsync();
        var tenantId = Guid.NewGuid();
        await using var context = PostgresContainerFixture.CreateProbeDbContext(connectionString, tenantId);

        IRepository<ProbeEntity> repository = new Repository<ProbeEntity>(context);
        IUnitOfWork unitOfWork = new UnitOfWork(context);

        var probe = new ProbeEntity { Name = "Will be rolled back" };

        await unitOfWork.BeginTransactionAsync();
        await repository.AddAsync(probe);
        await unitOfWork.SaveChangesAsync();
        await unitOfWork.RollbackAsync();

        await using var readContext = PostgresContainerFixture.CreateProbeDbContext(connectionString, tenantId);
        Assert.False(await readContext.Probes.AnyAsync(p => p.Id == probe.Id));
    }

    [Fact]
    public async Task UnitOfWork_CommitAsync_PersistsChanges_MadeBetweenBeginAndCommit()
    {
        var connectionString = await fixture.CreateProbeDatabaseAsync();
        var tenantId = Guid.NewGuid();
        await using var context = PostgresContainerFixture.CreateProbeDbContext(connectionString, tenantId);

        IRepository<ProbeEntity> repository = new Repository<ProbeEntity>(context);
        IUnitOfWork unitOfWork = new UnitOfWork(context);

        var probe = new ProbeEntity { Name = "Will be committed" };

        await unitOfWork.BeginTransactionAsync();
        await repository.AddAsync(probe);
        await unitOfWork.SaveChangesAsync();
        await unitOfWork.CommitAsync();

        await using var readContext = PostgresContainerFixture.CreateProbeDbContext(connectionString, tenantId);
        Assert.True(await readContext.Probes.AnyAsync(p => p.Id == probe.Id));
    }
}
