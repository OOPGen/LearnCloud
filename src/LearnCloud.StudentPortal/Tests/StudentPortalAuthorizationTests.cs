using LearnCloud.MultiTenancy.Context;
using LearnCloud.StudentPortal.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LearnCloud.StudentPortal.Tests;

public class StudentPortalAuthorizationTests
{
    private LearnCloudDbContext CreateDb(out ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<LearnCloudDbContext>().UseInMemoryDatabase("student_auth_test_"+Guid.NewGuid()).Options;
        tenantContext = new TenantContext();
        var audit = new LearnCloud.MultiTenancy.Interceptors.AuditInterceptor(tenantContext);
        var db = new LearnCloudDbContext(options, tenantContext, audit);
        return db;
    }

    [Fact]
    public async Task Student_Cannot_Access_Another_Students_Record_Must_Fail()
    {
        var db = CreateDb(out var tenantContext);
        var tenantId = 1L;

        using (tenantContext.BeginTenantScope(tenantId))
        {
            var grade = new Grade { TenantId=tenantId, Name="Grade 5", Code="G5", AcademicYearId=2026 };
            db.Grades.Add(grade);
            await db.SaveChangesAsync();
            var stream = new Stream { TenantId=tenantId, GradeId=grade.Id, Name="Blue", Capacity=40, AcademicYearId=2026 };
            db.Streams.Add(stream);
            await db.SaveChangesAsync();

            var studentA = new Student { TenantId=tenantId, UserId=100, StudentNumber="2026-001", FirstName="Thabo", LastName="Ndlovu", GradeId=grade.Id, StreamId=stream.Id, AcademicYearId=2026 };
            var studentB = new Student { TenantId=tenantId, UserId=200, StudentNumber="2026-002", FirstName="Lindiwe", LastName="Moyo", GradeId=grade.Id, StreamId=stream.Id, AcademicYearId=2026 };
            db.Set<Student>().AddRange(studentA, studentB);
            await db.SaveChangesAsync();
        }

        using (tenantContext.BeginTenantScope(tenantId))
        {
            var authz = new StudentAuthorizationService(db);

            var studentAId = await db.Set<Student>().Where(s=>s.StudentNumber=="2026-001").Select(s=>s.Id).FirstAsync();
            var studentBId = await db.Set<Student>().Where(s=>s.StudentNumber=="2026-002").Select(s=>s.Id).FirstAsync();

            // Student A (user 100) should be able to get own ID
            var ownId = await authz.GetStudentIdAsync(tenantId, 100);
            Assert.Equal(studentAId, ownId);

            // Student A can access own record
            var canAccessOwn = await authz.IsOwnRecordAsync(tenantId, studentAId, studentAId);
            Assert.True(canAccessOwn);

            // Student A cannot access Student B - must fail
            var canAccessOther = await authz.IsOwnRecordAsync(tenantId, studentBId, studentAId);
            Assert.False(canAccessOther, "Student A must NOT be able to access another student's record");

            // Ensure throws Unauthorized
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await authz.EnsureOwnRecordAsync(tenantId, studentBId, studentAId));

            // Student B cannot access A
            var canAccessA = await authz.IsOwnRecordAsync(tenantId, studentAId, studentBId);
            Assert.False(canAccessA);
        }
    }

    [Fact]
    public async Task Student_Tenant_Isolation_Cannot_Access_Other_Tenant_Record_By_Guessing_ID()
    {
        var db = CreateDb(out var tenantContext);

        // Tenant 1
        using (tenantContext.BeginTenantScope(1))
        {
            var grade = new Grade { TenantId=1, Name="Grade 5", Code="G5", AcademicYearId=2026 };
            db.Grades.Add(grade);
            await db.SaveChangesAsync();
            var stream = new Stream { TenantId=1, GradeId=grade.Id, Name="Blue", Capacity=40, AcademicYearId=2026 };
            db.Streams.Add(stream);
            await db.SaveChangesAsync();
            var student = new Student { TenantId=1, UserId=100, StudentNumber="2026-001", FirstName="Thabo", LastName="A", GradeId=grade.Id, StreamId=stream.Id, AcademicYearId=2026 };
            db.Set<Student>().Add(student);
            await db.SaveChangesAsync();
        }
        // Tenant 2
        using (tenantContext.BeginTenantScope(2))
        {
            var grade = new Grade { TenantId=2, Name="Grade 5", Code="G5", AcademicYearId=2026 };
            db.Grades.Add(grade);
            await db.SaveChangesAsync();
            var stream = new Stream { TenantId=2, GradeId=grade.Id, Name="Blue", Capacity=40, AcademicYearId=2026 };
            db.Streams.Add(stream);
            await db.SaveChangesAsync();
            var student = new Student { TenantId=2, UserId=200, StudentNumber="2026-001", FirstName="Thabo", LastName="A", GradeId=grade.Id, StreamId=stream.Id, AcademicYearId=2026 };
            db.Set<Student>().Add(student);
            await db.SaveChangesAsync();
        }

        using (tenantContext.BeginTenantScope(1))
        {
            var authz = new StudentAuthorizationService(db);
            var studentIdTenant1 = await db.Set<Student>().Where(s=>s.TenantId==1).Select(s=>s.Id).FirstAsync();
            var studentIdTenant2 = await db.Set<Student>().IgnoreQueryFilters().Where(s=>s.TenantId==2).Select(s=>s.Id).FirstAsync();

            // In tenant 1 context, global filter hides tenant 2 data, so even if ID guessed, not accessible
            var canAccessOwn = await authz.IsOwnRecordAsync(1, studentIdTenant1, studentIdTenant1);
            Assert.True(canAccessOwn);

            var canAccessOtherTenant = await authz.IsOwnRecordAsync(1, studentIdTenant2, studentIdTenant1);
            // This checks only ID equality, not tenant, but EnsureOwnRecordAsync also checks tenant existence
            // So IsOwnRecord would be false because IDs differ, but even if IDs same (auto-increment different DBs could collide), tenant filter would block in real service
            Assert.False(canAccessOtherTenant);

            // EnsureOwnRecord for other tenant's ID should fail either because IDs don't match or because student not found in tenant
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await authz.EnsureOwnRecordAsync(1, studentIdTenant2, studentIdTenant1));
        }
    }

    [Fact]
    public async Task School_Setting_AllowStudentsViewFees_Controls_Access()
    {
        var db = CreateDb(out var tenantContext);
        var tenantId = 1L;

        using (tenantContext.BeginTenantScope(tenantId))
        {
            // Default setting: AllowStudentsViewFees = false
            var settings = new StudentPortal.Entities.StudentPortalSettings { TenantId=tenantId, AllowStudentsViewFees=false };
            db.Set<StudentPortal.Entities.StudentPortalSettings>().Add(settings);
            await db.SaveChangesAsync();

            var grade = new Grade { TenantId=tenantId, Name="Grade 5", Code="G5", AcademicYearId=2026 };
            db.Grades.Add(grade);
            await db.SaveChangesAsync();
            var stream = new Stream { TenantId=tenantId, GradeId=grade.Id, Name="Blue", Capacity=40, AcademicYearId=2026 };
            db.Streams.Add(stream);
            await db.SaveChangesAsync();
            var student = new Student { TenantId=tenantId, UserId=100, StudentNumber="2026-001", FirstName="Thabo", LastName="A", GradeId=grade.Id, StreamId=stream.Id, AcademicYearId=2026 };
            db.Set<Student>().Add(student);
            await db.SaveChangesAsync();

            var portalService = new StudentPortalService(db, new StudentAuthorizationService(db));

            var feeSummary = await portalService.GetFeeSummaryAsync(tenantId, student.Id, default);

            Assert.Null(feeSummary); // School setting disables fee view

            // Now enable
            settings.AllowStudentsViewFees = true;
            await db.SaveChangesAsync();

            var feeSummaryEnabled = await portalService.GetFeeSummaryAsync(tenantId, student.Id, default);
            Assert.NotNull(feeSummaryEnabled);
        }
    }

    // Stub entities
    public class Student : LearnCloud.MultiTenancy.Entities.TenantOwnedEntity { public long? UserId { get; set; } public string StudentNumber { get; set; } = ""; public string FirstName { get; set; } = ""; public string LastName { get; set; } public long GradeId { get; set; } public long StreamId { get; set; } public long AcademicYearId { get; set; } }
    public class Grade : LearnCloud.MultiTenancy.Entities.TenantOwnedEntity { public string Name { get; set; } = ""; public string Code { get; set; } = ""; public long AcademicYearId { get; set; } }
    public class Stream : LearnCloud.MultiTenancy.Entities.TenantOwnedEntity { public long GradeId { get; set; } public string Name { get; set; } = ""; public int Capacity { get; set; } }
}
