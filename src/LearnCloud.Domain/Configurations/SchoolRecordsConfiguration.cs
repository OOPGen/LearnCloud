using System.Linq.Expressions;
using LearnCloud.Domain.Entities;
using LearnCloud.MultiTenancy.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LearnCloud.Domain.Configurations;

// Database rules for the core school records: academic years, terms, grades, streams,
// students, enrolments and guardians.
//
// Every reference between these tables includes tenant_id, and every referenced table has
// a unique (tenant_id, id) key. A row can therefore only point at a row of the same school:
// an enrolment for school B that names school A's stream is refused by PostgreSQL even if
// application code gets the lookup wrong.
//
// "Current" rows (the current academic year and term, a student's current enrolment, a
// student's primary contact) are unique through partial indexes, so two cannot coexist.
internal static class SchoolRecordKeys
{
    public const string CurrentActiveRows = "is_current = true AND is_deleted = false";

    public static void HasTenantKey<T>(this EntityTypeBuilder<T> b) where T : TenantOwnedEntity =>
        b.HasAlternateKey(x => new { x.TenantId, x.Id });

    public static void ReferencesInTenant<TDependent, TPrincipal>(
        this EntityTypeBuilder<TDependent> b, Expression<Func<TDependent, object?>> tenantAndForeignKey)
        where TDependent : TenantOwnedEntity
        where TPrincipal : TenantOwnedEntity =>
        b.HasOne<TPrincipal>().WithMany()
            .HasForeignKey(tenantAndForeignKey)
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);
}

public sealed class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> b)
    {
        b.HasTenantKey();
        b.Property(x => x.Name).HasMaxLength(50);
        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasDatabaseName("uq_academic_years_tenant_name")
            .HasFilter(SoftDelete.ActiveRowsFilter);
        b.HasIndex(x => x.TenantId).IsUnique().HasDatabaseName("uq_academic_years_one_current")
            .HasFilter(SchoolRecordKeys.CurrentActiveRows);
    }
}

public sealed class TermConfiguration : IEntityTypeConfiguration<Term>
{
    public void Configure(EntityTypeBuilder<Term> b)
    {
        b.HasTenantKey();
        b.Property(x => x.Name).HasMaxLength(50);
        b.ReferencesInTenant<Term, AcademicYear>(x => new { x.TenantId, x.AcademicYearId });
        b.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.TermNumber }).IsUnique().HasDatabaseName("uq_terms_year_number")
            .HasFilter(SoftDelete.ActiveRowsFilter);
        b.HasIndex(x => x.TenantId).IsUnique().HasDatabaseName("uq_terms_one_current")
            .HasFilter(SchoolRecordKeys.CurrentActiveRows);
    }
}

public sealed class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> b)
    {
        b.HasTenantKey();
        b.Property(x => x.Name).HasMaxLength(50);
        b.Property(x => x.Code).HasMaxLength(20);
        b.ReferencesInTenant<Grade, AcademicYear>(x => new { x.TenantId, x.AcademicYearId });
        b.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Code }).IsUnique().HasDatabaseName("uq_grades_year_code")
            .HasFilter(SoftDelete.ActiveRowsFilter);
        b.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.LevelOrder }).HasDatabaseName("idx_grades_year_level");
    }
}

public sealed class ClassStreamConfiguration : IEntityTypeConfiguration<ClassStream>
{
    public void Configure(EntityTypeBuilder<ClassStream> b)
    {
        b.HasTenantKey();
        b.Property(x => x.Name).HasMaxLength(50);
        b.ReferencesInTenant<ClassStream, Grade>(x => new { x.TenantId, x.GradeId });
        b.ReferencesInTenant<ClassStream, AcademicYear>(x => new { x.TenantId, x.AcademicYearId });
        b.HasIndex(x => new { x.TenantId, x.GradeId, x.Name }).IsUnique().HasDatabaseName("uq_class_streams_grade_name")
            .HasFilter(SoftDelete.ActiveRowsFilter);
    }
}

public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> b)
    {
        b.HasTenantKey();
        b.Property(x => x.StudentNumber).HasMaxLength(30);
        b.Property(x => x.FirstName).HasMaxLength(100);
        b.Property(x => x.LastName).HasMaxLength(100);
        b.Property(x => x.Gender).HasMaxLength(20);
        b.Property(x => x.NationalId).HasMaxLength(30);
        b.Property(x => x.PhotoUrl).HasMaxLength(500);
        b.Property(x => x.Status).HasMaxLength(20);

        // Grade, stream and year are the student's current (or last) placement, kept in step
        // with the current enrolment by the student records service.
        b.ReferencesInTenant<Student, Grade>(x => new { x.TenantId, x.GradeId });
        b.ReferencesInTenant<Student, ClassStream>(x => new { x.TenantId, x.StreamId });
        b.ReferencesInTenant<Student, AcademicYear>(x => new { x.TenantId, x.AcademicYearId });

        b.HasIndex(x => new { x.TenantId, x.StudentNumber }).IsUnique().HasDatabaseName("uq_students_tenant_number")
            .HasFilter(SoftDelete.ActiveRowsFilter);
        b.HasIndex(x => new { x.TenantId, x.StreamId }).HasDatabaseName("idx_students_stream");
        b.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("idx_students_status");
        b.HasIndex(x => new { x.TenantId, x.LastName, x.FirstName }).HasDatabaseName("idx_students_name");
    }
}

public sealed class StudentEnrolmentConfiguration : IEntityTypeConfiguration<StudentEnrolment>
{
    public void Configure(EntityTypeBuilder<StudentEnrolment> b)
    {
        b.Property(x => x.EnrolmentStatus).HasMaxLength(30);
        b.Property(x => x.EnrolmentType).HasMaxLength(30);

        b.ReferencesInTenant<StudentEnrolment, Student>(x => new { x.TenantId, x.StudentId });
        b.ReferencesInTenant<StudentEnrolment, AcademicYear>(x => new { x.TenantId, x.AcademicYearId });
        b.ReferencesInTenant<StudentEnrolment, Term>(x => new { x.TenantId, x.TermId });
        b.ReferencesInTenant<StudentEnrolment, Grade>(x => new { x.TenantId, x.GradeId });
        b.ReferencesInTenant<StudentEnrolment, ClassStream>(x => new { x.TenantId, x.StreamId });

        // Named indexes: the same columns carry a partial unique index and a plain one.
        b.HasIndex(x => new { x.TenantId, x.StudentId }, "uq_student_enrolments_one_current").IsUnique()
            .HasDatabaseName("uq_student_enrolments_one_current").HasFilter(SchoolRecordKeys.CurrentActiveRows);
        b.HasIndex(x => new { x.TenantId, x.StudentId }, "idx_student_enrolments_student").HasDatabaseName("idx_student_enrolments_student");
        b.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.GradeId, x.StreamId }).HasDatabaseName("idx_student_enrolments_class");
    }
}

public sealed class GuardianConfiguration : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> b)
    {
        b.HasTenantKey();
        b.Property(x => x.FirstName).HasMaxLength(100);
        b.Property(x => x.LastName).HasMaxLength(100);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.NationalId).HasMaxLength(30);
        b.HasIndex(x => new { x.TenantId, x.Phone }).HasDatabaseName("idx_guardians_phone");
        b.HasIndex(x => new { x.TenantId, x.LastName, x.FirstName }).HasDatabaseName("idx_guardians_name");
    }
}

public sealed class GuardianStudentLinkConfiguration : IEntityTypeConfiguration<GuardianStudentLink>
{
    public void Configure(EntityTypeBuilder<GuardianStudentLink> b)
    {
        b.Property(x => x.RelationshipType).HasMaxLength(30);
        b.ReferencesInTenant<GuardianStudentLink, Guardian>(x => new { x.TenantId, x.GuardianId });
        b.ReferencesInTenant<GuardianStudentLink, Student>(x => new { x.TenantId, x.StudentId });
        b.ReferencesInTenant<GuardianStudentLink, AcademicYear>(x => new { x.TenantId, x.AcademicYearId });

        b.HasIndex(x => new { x.TenantId, x.GuardianId, x.StudentId }).IsUnique().HasDatabaseName("uq_guardian_links_pair")
            .HasFilter(SoftDelete.ActiveRowsFilter);
        b.HasIndex(x => new { x.TenantId, x.StudentId }, "uq_guardian_links_one_primary").IsUnique()
            .HasDatabaseName("uq_guardian_links_one_primary").HasFilter("is_primary_contact = true AND is_deleted = false");
        b.HasIndex(x => new { x.TenantId, x.StudentId }, "idx_guardian_links_student").HasDatabaseName("idx_guardian_links_student");
    }
}
