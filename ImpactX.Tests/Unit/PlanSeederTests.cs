using System.Net;
using ImpactX.Core.Domain;
using ImpactX.Infrastructure.Data;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace ImpactX.Tests.Unit;

public class PlanSeederTests
{
    private sealed class FakeCosmosDbContext : CosmosDbContext
    {
        public Dictionary<Guid, Plan> PlansById { get; } = [];
        public Dictionary<string, int> PlanNameCounts { get; } = new(StringComparer.Ordinal);
        public List<Plan> CreatedPlans { get; } = [];
        public bool ThrowConflictOnCreate { get; set; }
        public bool ThrowUnauthorizedOnCreate { get; set; }
        public bool ThrowCancellationOnRead { get; set; }

        public FakeCosmosDbContext() : base(Options.Create(new CosmosDatabaseOptions
        {
            Endpoint = "https://localhost:443/",
            Key = "dGVzdC1rZXk="
        }))
        {
        }

        public override Task<Plan?> GetPlanByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (ThrowCancellationOnRead)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return Task.FromResult(PlansById.TryGetValue(id, out var plan) ? plan : null);
        }

        public override Task<int> CountPlansByNameAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(PlanNameCounts.TryGetValue(name, out var count) ? count : 0);

        public override Task CreatePlanAsync(Plan plan, CancellationToken cancellationToken = default)
        {
            if (ThrowConflictOnCreate)
            {
                throw new CosmosException("conflict", HttpStatusCode.Conflict, 0, "activity", 0);
            }

            if (ThrowUnauthorizedOnCreate)
            {
                throw new CosmosException("unauthorized", HttpStatusCode.Unauthorized, 0, "activity", 0);
            }

            CreatedPlans.Add(plan);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Seed_FreshDatabase_CreatesThreeDeterministicPlans()
    {
        var db = new FakeCosmosDbContext();

        await PlanSeeder.SeedPlansAsync(db, CancellationToken.None);

        Assert.Equal(3, db.CreatedPlans.Count);
        Assert.Equal(PlanSeeder.FreePlanId, db.CreatedPlans[0].Id);
        Assert.Equal(PlanSeeder.BasicPlanId, db.CreatedPlans[1].Id);
        Assert.Equal(PlanSeeder.PremiumPlanId, db.CreatedPlans[2].Id);
        Assert.Equal(["Free", "Basic", "Premium"], db.CreatedPlans.Select(p => p.Nombre).ToArray());
    }

    [Fact]
    public async Task Seed_FullySeededDatabase_IsIdempotent_NoWrites()
    {
        var db = new FakeCosmosDbContext();
        foreach (var plan in PlanSeeder.SeedPlans)
        {
            db.PlansById[plan.Id] = plan;
        }

        await PlanSeeder.SeedPlansAsync(db, CancellationToken.None);
        await PlanSeeder.SeedPlansAsync(db, CancellationToken.None);

        Assert.Empty(db.CreatedPlans);
    }

    [Fact]
    public async Task Seed_PartiallySeededDatabase_CreatesOnlyMissingPlans()
    {
        var db = new FakeCosmosDbContext();
        db.PlansById[PlanSeeder.FreePlanId] = PlanSeeder.SeedPlans[0];
        db.PlanNameCounts["Basic"] = 1;

        await PlanSeeder.SeedPlansAsync(db, CancellationToken.None);

        var created = Assert.Single(db.CreatedPlans);
        Assert.Equal(PlanSeeder.PremiumPlanId, created.Id);
    }

    [Fact]
    public async Task Seed_LegacyRandomIdPlan_SkippedByName_NoDuplicates()
    {
        // Plan sembrado antes de PR 2A con ID aleatorio: el point-read falla,
        // el COUNT por nombre detecta la existencia y evita duplicados.
        var db = new FakeCosmosDbContext();
        db.PlanNameCounts["Free"] = 1;

        await PlanSeeder.SeedPlansAsync(db, CancellationToken.None);

        Assert.DoesNotContain(db.CreatedPlans, p => p.Nombre == "Free");
        Assert.Equal(2, db.CreatedPlans.Count);
    }

    [Trait("Category", "Security")]
    [Fact]
    public async Task Seed_Conflict_IsHandledSafely_AsAlreadySeeded()
    {
        var db = new FakeCosmosDbContext { ThrowConflictOnCreate = true };

        await PlanSeeder.SeedPlansAsync(db, CancellationToken.None);

        Assert.Empty(db.CreatedPlans);
    }

    [Trait("Category", "Security")]
    [Fact]
    public async Task Seed_Unauthorized_Propagates_NotHidden()
    {
        var db = new FakeCosmosDbContext { ThrowUnauthorizedOnCreate = true };

        await Assert.ThrowsAsync<CosmosException>(
            () => PlanSeeder.SeedPlansAsync(db, CancellationToken.None));
    }

    [Fact]
    public async Task Seed_Cancellation_Propagates()
    {
        var db = new FakeCosmosDbContext { ThrowCancellationOnRead = true };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => PlanSeeder.SeedPlansAsync(db, cts.Token));
    }

    [Fact]
    public async Task Seed_DoesNotScanContainer_OnlyPointReadsAndSmallCounts()
    {
        var db = new FakeCosmosDbContext();
        var pointReads = 0;
        var nameCounts = 0;

        db.PlansById[PlanSeeder.FreePlanId] = PlanSeeder.SeedPlans[0];
        db.PlansById[PlanSeeder.BasicPlanId] = PlanSeeder.SeedPlans[1];
        db.PlansById[PlanSeeder.PremiumPlanId] = PlanSeeder.SeedPlans[2];

        var proxied = new PlanSeederProbe(db, () => pointReads++, () => nameCounts++);

        await PlanSeeder.SeedPlansAsync(proxied, CancellationToken.None);

        Assert.Equal(3, pointReads);
        Assert.Equal(0, nameCounts);
        Assert.Empty(db.CreatedPlans);
    }

    private sealed class PlanSeederProbe : CosmosDbContext
    {
        private readonly FakeCosmosDbContext _inner;
        private readonly Action _onPointRead;
        private readonly Action _onCount;

        public PlanSeederProbe(FakeCosmosDbContext inner, Action onPointRead, Action onCount)
            : base(Options.Create(new CosmosDatabaseOptions
            {
                Endpoint = "https://localhost:443/",
                Key = "dGVzdC1rZXk="
            }))
        {
            _inner = inner;
            _onPointRead = onPointRead;
            _onCount = onCount;
        }

        public override Task<Plan?> GetPlanByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _onPointRead();
            return _inner.GetPlanByIdAsync(id, cancellationToken);
        }

        public override Task<int> CountPlansByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            _onCount();
            return _inner.CountPlansByNameAsync(name, cancellationToken);
        }

        public override Task CreatePlanAsync(Plan plan, CancellationToken cancellationToken = default)
            => _inner.CreatePlanAsync(plan, cancellationToken);
    }
}
