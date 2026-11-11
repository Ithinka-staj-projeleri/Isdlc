using Isdlc.Domain.QuestionBank;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.QuestionBank;

// The question bank's tables (docs/data-model.md → "Soru bankası"). Reference data first, project data after.

public class QuestionSetConfiguration : IEntityTypeConfiguration<QuestionSet>
{
    public void Configure(EntityTypeBuilder<QuestionSet> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.SourceDocument).HasMaxLength(100);
        builder.Property(x => x.Version).HasMaxLength(20);
        builder.Property(x => x.Bands).HasMaxLength(QuestionSet.BandsMaxLength);

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.Property(x => x.Key).HasMaxLength(150);
        builder.Property(x => x.WorkType).HasMaxLength(50);
        builder.Property(x => x.Section).HasMaxLength(50);
        builder.Property(x => x.Group).HasMaxLength(200);
        builder.Property(x => x.Topic).HasMaxLength(200);
        builder.Property(x => x.Text).HasMaxLength(2000);
        builder.Property(x => x.ExpectedInfo).HasMaxLength(2000);
        builder.Property(x => x.RiskDescription).HasMaxLength(2000);
        builder.Property(x => x.SuggestedAction).HasMaxLength(2000);
        builder.Property(x => x.Reference).HasMaxLength(1000);

        // Answers follow the key when the seed file changes, so it can never repeat.
        builder.HasIndex(x => x.Key).IsUnique();
        // A form lists the questions of one set, work type and subcategory, in order.
        builder.HasIndex(x => new { x.QuestionSetId, x.WorkType, x.Subcategory, x.Order });

        builder.HasOne(x => x.QuestionSet).WithMany(s => s.Questions).HasForeignKey(x => x.QuestionSetId);
    }
}

public class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> builder)
    {
        builder.Property(x => x.Key).HasMaxLength(200);
        builder.Property(x => x.Label).HasMaxLength(500);

        builder.HasIndex(x => x.Key).IsUnique();

        builder.HasOne(x => x.QuestionSet).WithMany(s => s.Options).HasForeignKey(x => x.QuestionSetId);
        builder.HasOne(x => x.Question).WithMany(q => q.Options).HasForeignKey(x => x.QuestionId);
    }
}

public class QuestionnaireConfiguration : IEntityTypeConfiguration<Questionnaire>
{
    public void Configure(EntityTypeBuilder<Questionnaire> builder)
    {
        builder.Property(x => x.WorkType).HasMaxLength(50);
        builder.Property(x => x.Participants).HasMaxLength(1000);

        // One filled-in form per project and set.
        builder.HasIndex(x => new { x.ProjectId, x.QuestionSetId }).IsUnique();

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);
        builder.HasOne(x => x.QuestionSet).WithMany().HasForeignKey(x => x.QuestionSetId);
        builder.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedById);

        builder.ConfigureAuditFields();
    }
}

public class QuestionAnswerConfiguration : IEntityTypeConfiguration<QuestionAnswer>
{
    public void Configure(EntityTypeBuilder<QuestionAnswer> builder)
    {
        // The database itself refuses a probability or severity outside 1–3.
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_QuestionAnswers_Probability", "[Probability] IS NULL OR [Probability] BETWEEN 1 AND 3");
            t.HasCheckConstraint("CK_QuestionAnswers_Severity", "[Severity] IS NULL OR [Severity] BETWEEN 1 AND 3");
        });

        builder.Property(x => x.Text).HasMaxLength(4000);
        builder.Property(x => x.Note).HasMaxLength(2000);

        // One answer per question in a form.
        builder.HasIndex(x => new { x.QuestionnaireId, x.QuestionId }).IsUnique();

        builder.HasOne(x => x.Questionnaire).WithMany(q => q.Answers).HasForeignKey(x => x.QuestionnaireId);
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);
        builder.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId);
        builder.HasOne(x => x.Option).WithMany().HasForeignKey(x => x.OptionId);

        builder.ConfigureAuditFields();
    }
}
