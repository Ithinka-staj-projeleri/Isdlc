using Isdlc.Domain.Documents;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Documents;

public class ProjectDocumentConfiguration : IEntityTypeConfiguration<ProjectDocument>
{
    public void Configure(EntityTypeBuilder<ProjectDocument> builder)
    {
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.Link).HasMaxLength(1000);

        // One record per document in a project; two saves at the same moment cannot create two.
        builder.HasIndex(x => new { x.ProjectId, x.DocumentTypeId }).IsUnique();

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);
        builder.HasOne(x => x.DocumentType).WithMany().HasForeignKey(x => x.DocumentTypeId);

        builder.ConfigureAuditFields();
    }
}
