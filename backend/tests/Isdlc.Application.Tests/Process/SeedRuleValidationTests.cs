using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Process;
using Isdlc.Application.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// The company's flows as they come from the templates pass the same checks a company's own edits will (Faz 7 · A7):
/// a seed file cannot hold a rule or a prerequisite the flow editor would refuse.
/// </summary>
[Collection("Database")]
public class SeedRuleValidationTests(DatabaseFixture database)
{
    [Fact]
    public async Task EveryStepOfEveryFlow_HasValidRulesAndPrerequisites()
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var steps = await db.ProcessSteps
            .AsNoTracking()
            .Select(s => new { Flow = s.ProcessFlow.Code, s.Code, s.Conditions, s.Prerequisites })
            .ToListAsync();
        var forms = (await db.QuestionSets.Select(q => q.Code).ToListAsync()).ToHashSet();
        var documents = (await db.DocumentTypes.Select(d => d.Code).ToListAsync()).ToHashSet();

        var errors = steps
            .SelectMany(s => StepRuleValidation.ValidateConditions(s.Conditions, [])
                .Concat(StepRuleValidation.ValidatePrerequisites(s.Prerequisites, forms, documents))
                .Select(e => $"{s.Flow}/{s.Code}: {e.Message}"))
            .ToList();

        Assert.NotEmpty(steps);
        Assert.Empty(errors);
    }
}
