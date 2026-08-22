using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Entities;
using LearnCloud.ParentPortal.Entities;
using LearnCloud.ParentPortal.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LearnCloud.ParentPortal.Tests;

public class ParentPortalAuthorizationTests
{
    private LearnCloudDbContext CreateDb(out ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<LearnCloudDbContext>().UseInMemoryDatabase("parent_auth_test_"+Guid.NewGuid()).Options;
        tenantContext = new TenantContext();
        var audit = new LearnCloud.MultiTenancy.Interceptors.AuditInterceptor(tenantContext);
        var db = new LearnCloudDbContext(options, tenantContext, audit);
        return db;
    }

    [Fact]
    public async Task Parent_Cannot_Read_Another_Familys_Child_Must_Fail()
    {
        var db = CreateDb(out var tenantContext);
        var tenantId = 1L;

        // Seed two families overlapping
        using (tenantContext.BeginTenantScope(tenantId))
        {
            var guardianA = new Guardian { TenantId=tenantId, FirstName="John", LastName="Ndlovu", Phone="+263771111111", Email="john@test.co.zw", Address="Bulawayo" };
            var guardianB = new Guardian { TenantId=tenantId, FirstName="Mary", LastName="Moyo", Phone="+263772222222", Email="mary@test.co.zw" };
            db.Set<Guardian>().AddRange(guardianA, guardianB);
            await db.SaveChangesAsync();

            var grade = new Grade { TenantId=tenantId, Name="Grade 5", Code="G5", AcademicYearId=2026 };
            db.Grades.Add(grade);
            await db.SaveChangesAsync();

            var stream = new Stream { TenantId=tenantId, GradeId=grade.Id, Name="Blue", Capacity=40, AcademicYearId=2026 };
            db.Streams.Add(stream);
            await db.SaveChangesAsync();

            var studentA = new Student { TenantId=tenantId, StudentNumber="2026-001", FirstName="Thabo", LastName="Ndlovu", GradeId=grade.Id, StreamId=stream.Id, AcademicYearId=2026 };
            var studentB = new Student { TenantId=tenantId, StudentNumber="2026-002", FirstName="Lindiwe", LastName="Moyo", GradeId=grade.Id, StreamId=stream.Id, AcademicYearId=2026 };
            db.Set<Student>().AddRange(studentA, studentB);
            await db.SaveChangesAsync();

            var linkA = new GuardianStudentLink { TenantId=tenantId, GuardianId=guardianA.Id, StudentId=studentA.Id, RelationshipType="father", IsPrimaryContact=true, IsBillingContact=true };
            var linkB = new GuardianStudentLink { TenantId=tenantId, GuardianId=guardianB.Id, StudentId=studentB.Id, RelationshipType="mother", IsPrimaryContact=true, IsBillingContact=true };
            db.Set<GuardianStudentLink>().AddRange(linkA, linkB);
            await db.SaveChangesAsync();
        }

        using (tenantContext.BeginTenantScope(tenantId))
        {
            var authz = new ParentAuthorizationService(db);

            var guardianAId = await db.Set<Guardian>().Where(g=>g.Phone=="+263771111111").Select(g=>g.Id).FirstAsync();
            var guardianBId = await db.Set<Guardian>().Where(g=>g.Phone=="+263772222222").Select(g=>g.Id).FirstAsync();
            var studentAId = await db.Set<Student>().Where(s=>s.StudentNumber=="2026-001").Select(s=>s.Id).FirstAsync();
            var studentBId = await db.Set<Student>().Where(s=>s.StudentNumber=="2026-002").Select(s=>s.Id).FirstAsync();

            // Guardian A is guardian of Student A
            var isGuardianAOfA = await authz.IsGuardianOfStudentAsync(tenantId, guardianAId, studentAId);
            Assert.True(isGuardianAOfA);

            // Guardian A is NOT guardian of Student B - must fail
            var isGuardianAOfB = await authz.IsGuardianOfStudentAsync(tenantId, guardianAId, studentBId);
            Assert.False(isGuardianAOfB, "Guardian A must NOT be able to read another family's child B");

            // Ensure method throws Unauthorized
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await authz.EnsureGuardianOfStudentAsync(tenantId, guardianAId, studentBId));

            // Guardian B cannot read A
            var isGuardianBOfA = await authz.IsGuardianOfStudentAsync(tenantId, guardianBId, studentAId);
            Assert.False(isGuardianBOfA);

            // GetStudentIdsForGuardian returns only own children
            var childrenA = await authz.GetStudentIdsForGuardianAsync(tenantId, guardianAId);
            Assert.Single(childrenA);
            Assert.Equal(studentAId, childrenA[0]);

            var childrenB = await authz.GetStudentIdsForGuardianAsync(tenantId, guardianBId);
            Assert.Single(childrenB);
            Assert.Equal(studentBId, childrenB[0]);

            // Attempt to get children list for A should not include B
            Assert.DoesNotContain(studentBId, childrenA);
        }
    }

    [Fact]
    public async Task Parent_With_Multiple_Children_Can_Read_Both_But_Not_Other_Familys()
    {
        var db = CreateDb(out var tenantContext);
        var tenantId = 1L;

        using (tenantContext.BeginTenantScope(tenantId))
        {
            // Guardian with 2 children in different classes
            var guardian = new Guardian { TenantId=tenantId, FirstName="Paul", LastName="Ndlovu", Phone="+263773333333" };
            db.Set<Guardian>().Add(guardian);
            await db.SaveChangesAsync();

            var grade = new Grade { TenantId=tenantId, Name="Grade 5", Code="G5", AcademicYearId=2026 };
            db.Grades.Add(grade);
            await db.SaveChangesAsync();

            var streamBlue = new Stream { TenantId=tenantId, GradeId=grade.Id, Name="Blue", Capacity=40, AcademicYearId=2026 };
            var streamGreen = new Stream { TenantId=tenantId, GradeId=grade.Id, Name="Green", Capacity=40, AcademicYearId=2026 };
            db.Streams.AddRange(streamBlue, streamGreen);
            await db.SaveChangesAsync();

            var student1 = new Student { TenantId=tenantId, StudentNumber="2026-001", FirstName="Thabo", LastName="Ndlovu", GradeId=grade.Id, StreamId=streamBlue.Id, AcademicYearId=2026 };
            var student2 = new Student { TenantId=tenantId, StudentNumber="2026-002", FirstName="Lindiwe", LastName="Ndlovu", GradeId=grade.Id, StreamId=streamGreen.Id, AcademicYearId=2026 };
            var otherStudent = new Student { TenantId=tenantId, StudentNumber="2026-003", FirstName="Other", LastName="Child", GradeId=grade.Id, StreamId=streamBlue.Id, AcademicYearId=2026 };
            db.Set<Student>().AddRange(student1, student2, otherStudent);
            await db.SaveChangesAsync();

            db.Set<GuardianStudentLink>().AddRange(
                new GuardianStudentLink { TenantId=tenantId, GuardianId=guardian.Id, StudentId=student1.Id, IsPrimaryContact=true, IsBillingContact=true },
                new GuardianStudentLink { TenantId=tenantId, GuardianId=guardian.Id, StudentId=student2.Id, IsPrimaryContact=true, IsBillingContact=false }
            );
            await db.SaveChangesAsync();
        }

        using (tenantContext.BeginTenantScope(tenantId))
        {
            var authz = new ParentAuthorizationService(db);
            var guardianId = await db.Set<Guardian>().Where(g=>g.Phone=="+263773333333").Select(g=>g.Id).FirstAsync();
            var studentIds = await authz.GetStudentIdsForGuardianAsync(tenantId, guardianId);

            Assert.Equal(2, studentIds.Count);
            Assert.Contains(await db.Set<Student>().Where(s=>s.StudentNumber=="2026-001").Select(s=>s.Id).FirstAsync(), studentIds);
            Assert.Contains(await db.Set<Student>().Where(s=>s.StudentNumber=="2026-002").Select(s=>s.Id).FirstAsync(), studentIds);
            Assert.DoesNotContain(await db.Set<Student>().Where(s=>s.StudentNumber=="2026-003").Select(s=>s.Id).FirstAsync(), studentIds);
        }
    }

    [Fact]
    public async Task Tenant_Isolation_Parent_Cannot_Access_Other_Tenant_Child()
    {
        var db = CreateDb(out var tenantContext);
        // Tenant 1
        using (tenantContext.BeginTenantScope(1))
        {
            var guardian = new Guardian { TenantId=1, FirstName="John", LastName="A", Phone="111" };
            db.Set<Guardian>().Add(guardian);
            var grade = new Grade { TenantId=1, Name="Grade 5", Code="G5", AcademicYearId=2026 };
            db.Grades.Add(grade);
            await db.SaveChangesAsync();
            var stream = new Stream { TenantId=1, GradeId=grade.Id, Name="Blue", Capacity=40, AcademicYearId=2026 };
            db.Streams.Add(stream);
            await db.SaveChangesAsync();
            var student = new Student { TenantId=1, StudentNumber="2026-001", FirstName="Thabo", LastName="A", GradeId=grade.Id, StreamId=stream.Id, AcademicYearId=2026 };
            db.Set<Student>().Add(student);
            await db.SaveChangesAsync();
            db.Set<GuardianStudentLink>().Add(new GuardianStudentLink { TenantId=1, GuardianId=guardian.Id, StudentId=student.Id, IsPrimaryContact=true });
            await db.SaveChangesAsync();
        }
        // Tenant 2
        using (tenantContext.BeginTenantScope(2))
        {
            var guardian = new Guardian { TenantId=2, FirstName="John", LastName="A", Phone="111" };
            db.Set<Guardian>().Add(guardian);
            var grade = new Grade { TenantId=2, Name="Grade 5", Code="G5", AcademicYearId=2026 };
            db.Grades.Add(grade);
            await db.SaveChangesAsync();
            var stream = new Stream { TenantId=2, GradeId=grade.Id, Name="Blue", Capacity=40, AcademicYearId=2026 };
            db.Streams.Add(stream);
            await db.SaveChangesAsync();
            var student = new Student { TenantId=2, StudentNumber="2026-001", FirstName="Thabo", LastName="A", GradeId=grade.Id, StreamId=stream.Id, AcademicYearId=2026 };
            db.Set<Student>().Add(student);
            await db.SaveChangesAsync();
            db.Set<GuardianStudentLink>().Add(new GuardianStudentLink { TenantId=2, GuardianId=guardian.Id, StudentId=student.Id, IsPrimaryContact=true });
            await db.SaveChangesAsync();
        }

        // In tenant 1 context, global filter hides tenant 2 data, so guardian from tenant 1 cannot access student from tenant 2 even if IDs guessed
        using (tenantContext.BeginTenantScope(1))
        {
            var authz = new ParentAuthorizationService(db);
            var guardianIdTenant1 = await db.Set<Guardian>().Where(g=>g.TenantId==1).Select(g=>g.Id).FirstAsync();
            var studentIdTenant2 = await db.Set<Guardian>().IgnoreQueryFilters().Where(g=>g.TenantId==2).SelectMany(_=>db.Set<Student>().IgnoreQueryFilters().Where(s=>s.TenantId==2).Select(s=>s.Id)).FirstAsync();

            var canAccess = await authz.IsGuardianOfStudentAsync(1, guardianIdTenant1, studentIdTenant2);
            Assert.False(canAccess, "Parent from tenant 1 must not access child from tenant 2 even if ID guessed - tenant filter + guardian-child link");
        }
    }
}

// Stub entities for test compilation
public class Guardian : LearnCloud.MultiTenancy.Entities.TenantOwnedEntity { public long? UserId { get; set; } public string FirstName { get; set; } = ""; public string LastName { get; set; } = ""; public string Phone { get; set; } = ""; public string? Email { get; set; } public string? Address { get; set; } }
public class GuardianStudentLink : LearnCloud.MultiTenancy.Entities.TenantOwnedEntity { public long GuardianId { get; set; } public long StudentId { get; set; } public bool IsPrimaryContact { get; set; } public bool IsBillingContact { get; set; } }
public class Student : LearnCloud.MultiTenancy.Entities.TenantOwnedEntity { public string StudentNumber { get; set; } = ""; public string FirstName { get; set; } = ""; public string LastName { get; set; } public long GradeId { get; set; } public long StreamId { get; set; } public long AcademicYearId { get; set; } public string Status { get; set; } = "active"; public string? PhotoUrl { get; set; } }
public class Grade : LearnCloud.MultiTenancy.Entities.TenantOwnedEntity { public string Name { get; set; } = ""; public string Code { get; set; } = ""; public long AcademicYearId { get; set; } }
public class Stream : LearnCloud.MultiTenancy.Entities.TenantOwnedEntity { public long GradeId { get; set; } public string Name { get; set; } = ""; public int Capacity { get; set; } }
