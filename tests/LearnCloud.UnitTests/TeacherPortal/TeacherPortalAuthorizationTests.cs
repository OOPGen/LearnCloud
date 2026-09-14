using LearnCloud.AttendanceTimetable.Entities;
using LearnCloud.HR.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Entities;
using LearnCloud.TeacherPortal.Entities;
using LearnCloud.TeacherPortal.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LearnCloud.TeacherPortal.Tests;

public class TeacherPortalAuthorizationTests
{
    private LearnCloudDbContext CreateDb(out ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<LearnCloudDbContext>().UseInMemoryDatabase("teacher_auth_test_"+Guid.NewGuid()).Options;
        tenantContext = new TenantContext();
        var audit = new LearnCloud.MultiTenancy.Interceptors.AuditInterceptor(tenantContext);
        var db = new LearnCloudDbContext(options, tenantContext, audit);
        return db;
    }

    [Fact]
    public async Task Teacher_Cannot_Access_Another_Teachers_Class_Must_Fail()
    {
        var db = CreateDb(out var tenantContext);
        var tenantId = 1L;

        // Seed two teachers, two classes, overlapping students
        using (tenantContext.BeginTenantScope(tenantId))
        {
            // Teachers as staff profiles linked to user ids 100 and 200
            var teacherA = new Staff { TenantId=tenantId, UserId=100, StaffNumber="T001", FirstName="Alice", LastName="Moyo", EmploymentType="permanent" };
            var teacherB = new Staff { TenantId=tenantId, UserId=200, StaffNumber="T002", FirstName="Bob", LastName="Dube", EmploymentType="permanent" };
            db.Set<Staff>().AddRange(teacherA, teacherB);
            await db.SaveChangesAsync();

            var grade = new Grade { TenantId=tenantId, Name="Grade 5", Code="G5", AcademicYearId=2026 };
            db.Set<Grade>().Add(grade);
            await db.SaveChangesAsync();

            var streamA = new ClassStream { TenantId=tenantId, GradeId=grade.Id, Name="Blue", Capacity=40, AcademicYearId=2026, ClassTeacherStaffId=teacherA.Id };
            var streamB = new ClassStream { TenantId=tenantId, GradeId=grade.Id, Name="Green", Capacity=40, AcademicYearId=2026, ClassTeacherStaffId=teacherB.Id };
            db.Set<ClassStream>().AddRange(streamA, streamB);
            await db.SaveChangesAsync();

            // Timetable: teacher A teaches Blue, teacher B teaches Green
            var timetable = new Timetable { TenantId=tenantId, Name="Term2 2026", AcademicYearId=2026, TermId=1, EffectiveFrom=DateTime.UtcNow.AddDays(-10), Status="active" };
            db.Set<Timetable>().Add(timetable);
            await db.SaveChangesAsync();

            var slotA = new TimetableSlot { TenantId=tenantId, TimetableId=timetable.Id, GradeId=grade.Id, StreamId=streamA.Id, SubjectId=1, TeacherStaffId=teacherA.Id, DayOfWeek=1, PeriodNumber=1, StartTime=new TimeSpan(8,0,0), EndTime=new TimeSpan(8,45,0), AcademicYearId=2026, TermId=1 };
            var slotB = new TimetableSlot { TenantId=tenantId, TimetableId=timetable.Id, GradeId=grade.Id, StreamId=streamB.Id, SubjectId=1, TeacherStaffId=teacherB.Id, DayOfWeek=1, PeriodNumber=1, StartTime=new TimeSpan(8,0,0), EndTime=new TimeSpan(8,45,0), AcademicYearId=2026, TermId=1 };
            db.Set<TimetableSlot>().AddRange(slotA, slotB);
            await db.SaveChangesAsync();

            var studentA = new Student { TenantId=tenantId, StudentNumber="2026-001", FirstName="Thabo", LastName="Ndlovu", GradeId=grade.Id, StreamId=streamA.Id };
            var studentB = new Student { TenantId=tenantId, StudentNumber="2026-002", FirstName="Lindiwe", LastName="Moyo", GradeId=grade.Id, StreamId=streamB.Id };
            db.Set<Student>().AddRange(studentA, studentB);
            await db.SaveChangesAsync();

            // Assessment for stream A
            var assessmentA = new Assessment { TenantId=tenantId, GradeId=grade.Id, StreamId=streamA.Id, SubjectId=1, Name="Math Test 1", MaxScore=100, AcademicYearId=2026, TermId=1 };
            db.Set<Assessment>().Add(assessmentA);
            await db.SaveChangesAsync();
        }

        // Now test authorization service
        using (tenantContext.BeginTenantScope(tenantId))
        {
            var authz = new TeacherAuthorizationService(db);

            // Teacher A (staff id 1) should be assigned to Blue, not Green
            var teacherAId = await db.Set<Staff>().Where(s=>s.StaffNumber=="T001").Select(s=>s.Id).FirstAsync();
            var teacherBId = await db.Set<Staff>().Where(s=>s.StaffNumber=="T002").Select(s=>s.Id).FirstAsync();
            var gradeId = await db.Set<Grade>().Where(g=>g.Code=="G5").Select(g=>g.Id).FirstAsync();
            var streamBlueId = await db.Set<ClassStream>().Where(s=>s.Name=="Blue").Select(s=>s.Id).FirstAsync();
            var streamGreenId = await db.Set<ClassStream>().Where(s=>s.Name=="Green").Select(s=>s.Id).FirstAsync();

            // A can access Blue
            var canAccessBlue = await authz.IsAssignedToClassAsync(tenantId, teacherAId, gradeId, streamBlueId);
            Assert.True(canAccessBlue, "Teacher A should be able to access Blue (own class)");

            // A cannot access Green - must fail
            var canAccessGreen = await authz.IsAssignedToClassAsync(tenantId, teacherAId, gradeId, streamGreenId);
            Assert.False(canAccessGreen, "Teacher A must NOT be able to access Green (other teacher's class)");

            // Ensure method throws Unauthorized
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await authz.EnsureAssignedToClassAsync(tenantId, teacherAId, gradeId, streamGreenId));

            // B can access Green, not Blue
            var bCanGreen = await authz.IsAssignedToClassAsync(tenantId, teacherBId, gradeId, streamGreenId);
            Assert.True(bCanGreen);
            var bCanBlue = await authz.IsAssignedToClassAsync(tenantId, teacherBId, gradeId, streamBlueId);
            Assert.False(bCanBlue);

            // Test teaching subject in class - Teacher A teaches Math (subject 1) in Blue, but not in Green
            var canTeachMathBlue = await authz.IsTeachingSubjectInClassAsync(tenantId, teacherAId, gradeId, streamBlueId, 1);
            Assert.True(canTeachMathBlue);
            var canTeachMathGreen = await authz.IsTeachingSubjectInClassAsync(tenantId, teacherAId, gradeId, streamGreenId, 1);
            Assert.False(canTeachMathGreen, "Teacher A does not teach Math in Green, must fail for marks entry");

            // Marks entry should fail for other teacher's class
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await authz.EnsureTeachingSubjectInClassAsync(tenantId, teacherAId, gradeId, streamGreenId, 1));
        }
    }

    [Fact]
    public async Task Teacher_GetAssignedClasses_Only_Own()
    {
        var db = CreateDb(out var tenantContext);
        var tenantId = 1L;
        using (tenantContext.BeginTenantScope(tenantId))
        {
            var teacherA = new Staff { TenantId=tenantId, UserId=100, StaffNumber="T001", FirstName="Alice", LastName="Moyo" };
            var teacherB = new Staff { TenantId=tenantId, UserId=200, StaffNumber="T002", FirstName="Bob", LastName="Dube" };
            db.Set<Staff>().AddRange(teacherA, teacherB);
            await db.SaveChangesAsync();

            var grade = new Grade { TenantId=tenantId, Name="Grade 5", Code="G5", AcademicYearId=2026 };
            db.Set<Grade>().Add(grade);
            await db.SaveChangesAsync();

            var streamA = new ClassStream { TenantId=tenantId, GradeId=grade.Id, Name="Blue", Capacity=40, AcademicYearId=2026, ClassTeacherStaffId=teacherA.Id };
            var streamB = new ClassStream { TenantId=tenantId, GradeId=grade.Id, Name="Green", Capacity=40, AcademicYearId=2026, ClassTeacherStaffId=teacherB.Id };
            var streamC = new ClassStream { TenantId=tenantId, GradeId=grade.Id, Name="Red", Capacity=40, AcademicYearId=2026, ClassTeacherStaffId=teacherA.Id };
            db.Set<ClassStream>().AddRange(streamA, streamB, streamC);
            await db.SaveChangesAsync();
        }

        using (tenantContext.BeginTenantScope(tenantId))
        {
            var authz = new TeacherAuthorizationService(db);
            var teacherAId = await db.Set<Staff>().Where(s=>s.StaffNumber=="T001").Select(s=>s.Id).FirstAsync();
            var assigned = await authz.GetAssignedClassesAsync(tenantId, teacherAId);
            // Teacher A should have Blue and Red, not Green
            Assert.Equal(2, assigned.Count);
            Assert.DoesNotContain(assigned, x=>x.streamId == db.Set<ClassStream>().First(s=>s.Name=="Green").Id);
        }
    }

    [Fact]
    public async Task Tenant_Isolation_Teacher_Cannot_Access_Other_Tenant_Class()
    {
        var db = CreateDb(out var tenantContext);
        // Seed two tenants with same class ids overlapping
        using (var noTenantScope = tenantContext.BeginNoTenantScope("Seeding two tenants for isolation test", 1, "SYSTEM_JOB"))
        {
            // Tenant 1
            using (tenantContext.BeginTenantScope(1))
            {
                var teacher = new Staff { TenantId=1, UserId=100, StaffNumber="T001", FirstName="Alice", LastName="Moyo" };
                db.Set<Staff>().Add(teacher);
                var grade = new Grade { TenantId=1, Name="Grade 5", Code="G5", AcademicYearId=2026 };
                db.Set<Grade>().Add(grade);
                await db.SaveChangesAsync();
                var stream = new ClassStream { TenantId=1, GradeId=grade.Id, Name="Blue", Capacity=40, AcademicYearId=2026, ClassTeacherStaffId=teacher.Id };
                db.Set<ClassStream>().Add(stream);
                await db.SaveChangesAsync();
            }
            // Tenant 2 same grade/stream names, same ids potentially different due to auto-increment but same logical
            using (tenantContext.BeginTenantScope(2))
            {
                var teacher = new Staff { TenantId=2, UserId=200, StaffNumber="T001", FirstName="Alice", LastName="Moyo" };
                db.Set<Staff>().Add(teacher);
                var grade = new Grade { TenantId=2, Name="Grade 5", Code="G5", AcademicYearId=2026 };
                db.Set<Grade>().Add(grade);
                await db.SaveChangesAsync();
                var stream = new ClassStream { TenantId=2, GradeId=grade.Id, Name="Blue", Capacity=40, AcademicYearId=2026, ClassTeacherStaffId=teacher.Id };
                db.Set<ClassStream>().Add(stream);
                await db.SaveChangesAsync();
            }
        }

        using (tenantContext.BeginTenantScope(1))
        {
            var authz = new TeacherAuthorizationService(db);
            var teacherId = await db.Set<Staff>().Where(s=>s.TenantId==1).Select(s=>s.Id).FirstAsync();
            var gradeIdTenant1 = await db.Set<Grade>().Where(g=>g.TenantId==1).Select(g=>g.Id).FirstAsync();
            var streamIdTenant1 = await db.Set<ClassStream>().Where(s=>s.TenantId==1).Select(s=>s.Id).FirstAsync();

            var gradeIdTenant2 = await db.Set<Grade>().IgnoreQueryFilters().Where(g=>g.TenantId==2).Select(g=>g.Id).FirstAsync();
            var streamIdTenant2 = await db.Set<ClassStream>().IgnoreQueryFilters().Where(s=>s.TenantId==2).Select(s=>s.Id).FirstAsync();

            // In tenant 1 context, global filter hides tenant 2 data, so checking access to tenant 2's class should fail due to not found
            var canAccessOwn = await authz.IsAssignedToClassAsync(1, teacherId, gradeIdTenant1, streamIdTenant1);
            Assert.True(canAccessOwn);

            var canAccessOtherTenant = await authz.IsAssignedToClassAsync(1, teacherId, gradeIdTenant2, streamIdTenant2);
            Assert.False(canAccessOtherTenant, "Teacher from tenant 1 must not access class from tenant 2 even if IDs guessed - tenant filter + assignment check");
        }
    }
}

// Test data uses canonical entities: Domain (Grade, ClassStream, Student, Subject, Assessment),
// HR (Staff) and AttendanceTimetable (Timetable, TimetableSlot).
