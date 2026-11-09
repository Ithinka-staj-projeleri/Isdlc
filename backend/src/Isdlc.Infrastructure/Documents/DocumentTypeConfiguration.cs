using Isdlc.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Documents;

public class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(10);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasOne(x => x.ProcessStage).WithMany().HasForeignKey(x => x.ProcessStageId);
    }
}
