using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using LearnCloud.Domain.Entities;
using LearnCloud.MultiTenancy.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LearnCloud.IntegrationTests;

// Academic years, terms, grades, streams, students, enrolments and guardians, end to end
// through the HTTP API against PostgreSQL with the real migrations.
[Collection(ApiCollection.Name)]
public sealed class SchoolRecordsTests
{
    // Each test takes its own calendar years, so years created by different tests in the same
    // school never overlap (overlapping academic years are refused).
    private static int _lastYear = 2200;

    private readonly LearnCloudApiFixture _api;

    public SchoolRecordsTests(LearnCloudApiFixture api) => _api = api;

    [Fact]
    public async Task Academic_calendar_rules_are_enforced()
    {
        using var a = _api.ClientFor(_api.SchoolA);
        var y = NextYear();
        var yearId = Id(await Ok(a.PostAsJsonAsync("/api/academic/years", Year(y))));

        await Ok(a.PostAsJsonAsync($"/api/academic/years/{yearId}/terms", Term(1, $"{y}-01-10", $"{y}-04-10")));

        await ExpectAsync(a.PostAsJsonAsync($"/api/academic/years/{yearId}/terms", Term(2, $"{y}-04-01", $"{y}-07-01")), HttpStatusCode.BadRequest, "overlap");
        await ExpectAsync(a.PostAsJsonAsync($"/api/academic/years/{yearId}/terms", Term(2, $"{y}-11-01", $"{y + 1}-01-20")), HttpStatusCode.BadRequest, "within");
        await ExpectAsync(a.PostAsJsonAsync($"/api/academic/years/{yearId}/terms", Term(1, $"{y}-05-01", $"{y}-07-01")), HttpStatusCode.Conflict, "already exists");
        await ExpectAsync(a.PostAsJsonAsync($"/api/academic/years/{yearId}/terms", Term(2, $"{y}-08-01", $"{y}-07-01")), HttpStatusCode.UnprocessableEntity);

        await ExpectAsync(a.PostAsJsonAsync("/api/academic/years", new { name = $"Y{y}", startDate = "2190-01-01", endDate = "2190-12-01", isCurrent = false }), HttpStatusCode.Conflict, "already exists");
        await ExpectAsync(a.PostAsJsonAsync("/api/academic/years", new { name = $"Overlap {y}", startDate = $"{y}-06-01", endDate = $"{y + 1}-05-01", isCurrent = false }), HttpStatusCode.BadRequest, "overlap");

        // A year cannot shrink past its terms, and a year with terms cannot be deleted.
        await ExpectAsync(a.PutAsJsonAsync($"/api/academic/years/{yearId}", new { name = $"Y{y}", startDate = $"{y}-02-01", endDate = $"{y}-12-05" }), HttpStatusCode.BadRequest, "outside");
        await ExpectAsync(a.DeleteAsync($"/api/academic/years/{yearId}"), HttpStatusCode.BadRequest, "terms");
    }

    [Fact]
    public async Task Only_one_year_and_one_term_are_current()
    {
        using var a = _api.ClientFor(_api.SchoolA);
        var first = NextYear();
        var second = NextYear();
        var firstId = Id(await Ok(a.PostAsJsonAsync("/api/academic/years", Year(first))));
        var secondId = Id(await Ok(a.PostAsJsonAsync("/api/academic/years", Year(second))));
        var termId = Id(await Ok(a.PostAsJsonAsync($"/api/academic/years/{firstId}/terms", Term(1, $"{first}-01-10", $"{first}-04-10"))));

        await Ok(a.PostAsync($"/api/academic/years/{secondId}/set-current", null));
        var years = (await Ok(a.GetAsync("/api/academic/years"))).EnumerateArray().ToList();
        Assert.Single(years, x => x.GetProperty("isCurrent").GetBoolean());
        Assert.True(years.Single(x => Id(x) == secondId).GetProperty("isCurrent").GetBoolean());

        // Making a term current also makes its year current.
        await Ok(a.PostAsync($"/api/academic/terms/{termId}/set-current", null));
        var current = await Ok(a.GetAsync("/api/academic/current"));
        Assert.Equal(firstId, Id(current.GetProperty("year")));
        Assert.Equal(termId, Id(current.GetProperty("term")));
        years = (await Ok(a.GetAsync("/api/academic/years"))).EnumerateArray().ToList();
        Assert.Single(years, x => x.GetProperty("isCurrent").GetBoolean());
    }

    [Fact]
    public async Task Student_moves_class_leaves_and_is_readmitted()
    {
        using var a = _api.ClientFor(_api.SchoolA);
        var cls = await NewClassAsync(a, level: 1, capacity: 5);
        var green = Id(await Ok(a.PostAsJsonAsync($"/api/academic/grades/{cls.GradeId}/streams", new { name = "Green", capacity = 1 })));

        var created = await Ok(a.PostAsJsonAsync("/api/students", NewStudent("Tendai", "Moyo", cls.StreamId, guardian: NewGuardian("Rudo", "Moyo", "+263771111111"))));
        var studentId = Id(created);
        Assert.Matches(new Regex(@"^\d{4}-\d{4}$"), created.GetProperty("studentNumber").GetString());
        Assert.Equal(cls.StreamId, created.GetProperty("currentPlacement").GetProperty("streamId").GetInt64());
        Assert.Equal(cls.TermId, created.GetProperty("currentPlacement").GetProperty("termId").GetInt64());
        Assert.Equal("new", Single(created.GetProperty("enrolments")).GetProperty("enrolmentType").GetString());
        Assert.True(Single(created.GetProperty("guardians")).GetProperty("isPrimaryContact").GetBoolean());

        var moved = await Ok(a.PostAsJsonAsync($"/api/students/{studentId}/enrolments", new { streamId = green }));
        var history = moved.GetProperty("enrolments").EnumerateArray().ToList();
        Assert.Equal(2, history.Count);
        Assert.Single(history, e => e.GetProperty("isCurrent").GetBoolean());
        Assert.Equal(green, moved.GetProperty("currentPlacement").GetProperty("streamId").GetInt64());
        var closed = history.Single(e => !e.GetProperty("isCurrent").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, closed.GetProperty("exitDate").ValueKind);

        // Green holds one student.
        await ExpectAsync(a.PostAsJsonAsync("/api/students", NewStudent("Chipo", "Ncube", green)), HttpStatusCode.BadRequest, "full");
        await ExpectAsync(a.PostAsJsonAsync($"/api/students/{studentId}/enrolments", new { streamId = green }), HttpStatusCode.BadRequest, "already in");

        var left = await Ok(a.PostAsJsonAsync($"/api/students/{studentId}/exit", new { reason = "withdrawn" }));
        Assert.Equal(JsonValueKind.Null, left.GetProperty("currentPlacement").ValueKind);
        Assert.Equal("inactive", left.GetProperty("status").GetString());
        var inactive = await Ok(a.GetAsync($"/api/students?status=inactive&search=Tendai&pageSize=100"));
        Assert.Contains(inactive.GetProperty("items").EnumerateArray(), s => Id(s) == studentId);

        var back = await Ok(a.PostAsJsonAsync($"/api/students/{studentId}/enrolments", new { streamId = cls.StreamId }));
        Assert.Equal("active", back.GetProperty("status").GetString());
        Assert.Equal("readmission", back.GetProperty("enrolments").EnumerateArray().First(e => e.GetProperty("isCurrent").GetBoolean()).GetProperty("enrolmentType").GetString());

        // Classes that students have used cannot be deleted.
        await ExpectAsync(a.DeleteAsync($"/api/academic/streams/{green}"), HttpStatusCode.BadRequest, "enrolled");
        await ExpectAsync(a.DeleteAsync($"/api/academic/grades/{cls.GradeId}"), HttpStatusCode.BadRequest, "streams");
    }

    [Fact]
    public async Task Moving_into_a_higher_grade_next_year_is_a_promotion()
    {
        using var a = _api.ClientFor(_api.SchoolA);
        var form1 = await NewClassAsync(a, level: 1, capacity: 30);
        var form2 = await NewClassAsync(a, level: 2, capacity: 30);
        var studentId = Id(await Ok(a.PostAsJsonAsync("/api/students", NewStudent("Farai", "Dube", form1.StreamId))));

        var promoted = await Ok(a.PostAsJsonAsync($"/api/students/{studentId}/enrolments", new { streamId = form2.StreamId, effectiveDate = $"{form2.Year}-01-10" }));

        var history = promoted.GetProperty("enrolments").EnumerateArray().ToList();
        Assert.Equal("promoted", history.Single(e => !e.GetProperty("isCurrent").GetBoolean()).GetProperty("enrolmentStatus").GetString());
        Assert.Equal("continuing", history.Single(e => e.GetProperty("isCurrent").GetBoolean()).GetProperty("enrolmentType").GetString());
        Assert.Equal(form2.YearId, promoted.GetProperty("currentPlacement").GetProperty("academicYearId").GetInt64());

        await ExpectAsync(a.PostAsJsonAsync($"/api/students/{studentId}/enrolments", new { streamId = form1.StreamId }), HttpStatusCode.BadRequest, "after");
    }

    [Fact]
    public async Task Guardians_are_linked_with_one_primary_contact()
    {
        using var a = _api.ClientFor(_api.SchoolA);
        var cls = await NewClassAsync(a, level: 1, capacity: 30);
        var phone = $"+26377{Random.Shared.Next(1000000, 9999999)}";
        var student = await Ok(a.PostAsJsonAsync("/api/students", NewStudent("Nyasha", "Sibanda", cls.StreamId, guardian: NewGuardian("Tapiwa", "Sibanda", "+263772222222"))));
        var studentId = Id(student);
        var firstGuardianId = Single(student.GetProperty("guardians")).GetProperty("guardianId").GetInt64();

        var second = await Ok(a.PostAsJsonAsync($"/api/students/{studentId}/guardians", new
        {
            newGuardian = new { firstName = "Grace", lastName = "Sibanda", phone, email = "grace@example.test" },
            relationshipType = "mother", isPrimaryContact = true, isBillingContact = true, isEmergencyContact = true, canPickup = true,
        }));
        var links = (await Ok(a.GetAsync($"/api/students/{studentId}/guardians"))).EnumerateArray().ToList();
        Assert.Equal(2, links.Count);
        Assert.Single(links, l => l.GetProperty("isPrimaryContact").GetBoolean());
        Assert.True(links.Single(l => l.GetProperty("linkId").GetInt64() == second.GetProperty("linkId").GetInt64()).GetProperty("isPrimaryContact").GetBoolean());

        await ExpectAsync(a.PostAsJsonAsync($"/api/students/{studentId}/guardians", new { guardianId = firstGuardianId, relationshipType = "father", isPrimaryContact = false, isBillingContact = false, isEmergencyContact = false, canPickup = true }), HttpStatusCode.Conflict, "already linked");
        await ExpectAsync(a.PostAsJsonAsync($"/api/students/{studentId}/guardians", new { guardianId = firstGuardianId, newGuardian = new { firstName = "X", lastName = "Y", phone }, relationshipType = "father" }), HttpStatusCode.UnprocessableEntity);

        var found = await Ok(a.GetAsync($"/api/guardians?search={Uri.EscapeDataString(phone)}"));
        Assert.Single(found.GetProperty("items").EnumerateArray());

        await ExpectAsync(a.DeleteAsync($"/api/guardians/{firstGuardianId}"), HttpStatusCode.BadRequest, "Unlink");
        var firstLinkId = links.Single(l => l.GetProperty("guardianId").GetInt64() == firstGuardianId).GetProperty("linkId").GetInt64();
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/students/{studentId}/guardians/{firstLinkId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/guardians/{firstGuardianId}")).StatusCode);
    }

    [Fact]
    public async Task School_cannot_read_or_use_another_schools_records()
    {
        using var a = _api.ClientFor(_api.SchoolA);
        using var b = _api.ClientFor(_api.SchoolB);
        var aClass = await NewClassAsync(a, level: 1, capacity: 30);
        var aStudent = await Ok(a.PostAsJsonAsync("/api/students", NewStudent("Private", "Learner", aClass.StreamId, guardian: NewGuardian("Private", "Parent", "+263773333333"))));
        var aStudentId = Id(aStudent);
        var aGuardianId = Single(aStudent.GetProperty("guardians")).GetProperty("guardianId").GetInt64();

        var bClass = await NewClassAsync(b, level: 1, capacity: 30);
        var bStudentId = Id(await Ok(b.PostAsJsonAsync("/api/students", NewStudent("Own", "Learner", bClass.StreamId))));

        var bList = await Ok(b.GetAsync("/api/students?pageSize=100&search=Learner"));
        Assert.DoesNotContain(bList.GetProperty("items").EnumerateArray(), s => Id(s) == aStudentId);

        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/students/{aStudentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/students/{aStudentId}", new { firstName = "Hijacked", lastName = "X", studentNumber = "HIJACK1" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"/api/students/{aStudentId}/exit", new { reason = "withdrawn" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/students/{aStudentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/guardians/{aGuardianId}")).StatusCode);

        // B cannot place its records into A's classes or attach A's guardian.
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync("/api/students", NewStudent("Sneaky", "Learner", aClass.StreamId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"/api/students/{bStudentId}/enrolments", new { streamId = aClass.StreamId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync("/api/academic/grades", new { academicYearId = aClass.YearId, name = "Sneaky", code = "SNK", levelOrder = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/academic/streams/{aClass.StreamId}", new { name = "Renamed", capacity = 99 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"/api/students/{bStudentId}/guardians", new { guardianId = aGuardianId, relationshipType = "guardian", isPrimaryContact = false, isBillingContact = false, isEmergencyContact = false, canPickup = true })).StatusCode);

        var stillA = await Ok(a.GetAsync($"/api/students/{aStudentId}"));
        Assert.Equal("Private", stillA.GetProperty("firstName").GetString());
        Assert.NotEqual(JsonValueKind.Null, stillA.GetProperty("currentPlacement").ValueKind);
    }

    // The composite (tenant_id, id) keys make PostgreSQL itself refuse a cross-school
    // reference, even from code that bypasses the services.
    [Fact]
    public async Task Database_refuses_references_across_schools_and_a_second_current_enrolment()
    {
        using var a = _api.ClientFor(_api.SchoolA);
        using var b = _api.ClientFor(_api.SchoolB);
        var aClass = await NewClassAsync(a, level: 1, capacity: 30);
        var bClass = await NewClassAsync(b, level: 1, capacity: 30);
        var bStudentId = Id(await Ok(b.PostAsJsonAsync("/api/students", NewStudent("Db", "Probe", bClass.StreamId))));

        await using (var scope = _api.Factory.Services.CreateAsyncScope())
        {
            using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().BeginTenantScope(_api.SchoolB.TenantId);
            var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            db.Set<StudentEnrolment>().Add(new StudentEnrolment
            {
                TenantId = _api.SchoolB.TenantId, StudentId = bStudentId, IsCurrent = false,
                AcademicYearId = aClass.YearId, TermId = aClass.TermId, GradeId = aClass.GradeId, StreamId = aClass.StreamId,
            });
            var crossSchool = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Contains("foreign key", crossSchool.InnerException?.Message ?? "", StringComparison.OrdinalIgnoreCase);
        }

        await using (var scope = _api.Factory.Services.CreateAsyncScope())
        {
            using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().BeginTenantScope(_api.SchoolB.TenantId);
            var db = scope.ServiceProvider.GetRequiredService<LearnCloudDbContext>();
            db.Set<StudentEnrolment>().Add(new StudentEnrolment
            {
                TenantId = _api.SchoolB.TenantId, StudentId = bStudentId, IsCurrent = true,
                AcademicYearId = bClass.YearId, TermId = bClass.TermId, GradeId = bClass.GradeId, StreamId = bClass.StreamId,
            });
            var second = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Contains("uq_student_enrolments_one_current", second.InnerException?.Message ?? "");
        }
    }

    [Fact]
    public async Task Student_export_is_csv_with_formulas_neutralised()
    {
        using var a = _api.ClientFor(_api.SchoolA);
        var cls = await NewClassAsync(a, level: 1, capacity: 30);
        var marker = $"Exp{Random.Shared.Next(100000, 999999)}";
        await Ok(a.PostAsJsonAsync("/api/students", NewStudent(marker, "=HYPERLINK(\"http://evil.test\")", cls.StreamId)));

        var response = await a.GetAsync($"/api/students/export?search={marker}");
        await LearnCloudApiFixture.EnsureSuccessAsync(response, "export");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.StartsWith("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"Student number\"", csv);
        Assert.Contains("\"'=HYPERLINK(\"\"http://evil.test\"\")\"", csv);
    }

    [Fact]
    public async Task Setup_wizard_creates_the_real_calendar_and_classes()
    {
        using var b = _api.ClientFor(_api.SchoolB);
        const int y = 2190;

        await Ok(b.PutAsJsonAsync("/api/setup/step/3", new { name = $"{y}", startDate = $"{y}-01-12", endDate = $"{y}-12-04", isCurrent = true }));
        var terms = new
        {
            count = 3,
            terms = new[]
            {
                new { name = "Term 1", termNumber = 1, startDate = $"{y}-01-12", endDate = $"{y}-04-09", isCurrent = true },
                new { name = "Term 2", termNumber = 2, startDate = $"{y}-05-11", endDate = $"{y}-08-06", isCurrent = false },
                new { name = "Term 3", termNumber = 3, startDate = $"{y}-09-07", endDate = $"{y}-12-04", isCurrent = false },
            },
        };
        await Ok(b.PutAsJsonAsync("/api/setup/step/4", terms));
        // Saving again with every term shifted must not trip over the old dates.
        await Ok(b.PutAsJsonAsync("/api/setup/step/4", new
        {
            count = 3,
            terms = new[]
            {
                new { name = "Term 1", termNumber = 1, startDate = $"{y}-01-19", endDate = $"{y}-04-16", isCurrent = true },
                new { name = "Term 2", termNumber = 2, startDate = $"{y}-05-18", endDate = $"{y}-08-13", isCurrent = false },
                new { name = "Term 3", termNumber = 3, startDate = $"{y}-09-14", endDate = $"{y}-12-04", isCurrent = false },
            },
        }));
        await Ok(b.PutAsJsonAsync("/api/setup/step/5", new
        {
            classes = new[]
            {
                new { gradeName = "Form 1", gradeCode = "F1", streams = new[] { new { name = "Blue", capacity = 40 }, new { name = "Green", capacity = 40 } } },
                new { gradeName = "Form 2", gradeCode = "F2", streams = new[] { new { name = "Blue", capacity = 35 } } },
            },
        }));

        var current = await Ok(b.GetAsync("/api/academic/current"));
        Assert.Equal($"{y}", current.GetProperty("year").GetProperty("name").GetString());
        Assert.Equal(3, current.GetProperty("year").GetProperty("terms").GetArrayLength());
        Assert.Equal("Term 1", current.GetProperty("term").GetProperty("name").GetString());
        Assert.Equal(new DateTime(y, 1, 19), current.GetProperty("term").GetProperty("startDate").GetDateTime().Date);

        var grades = (await Ok(b.GetAsync("/api/academic/grades"))).EnumerateArray().ToList();
        var yearId = Id(current.GetProperty("year"));
        Assert.Equal(new[] { "F1", "F2" }, grades.Select(g => g.GetProperty("code").GetString()));
        Assert.All(grades, g => Assert.Equal(yearId, g.GetProperty("academicYearId").GetInt64()));
        Assert.Equal(2, grades[0].GetProperty("streams").GetArrayLength());

        // Invalid step data is refused instead of being stored.
        await ExpectAsync(b.PutAsJsonAsync("/api/setup/step/3", new { name = "not a year", startDate = $"{y}-01-12", endDate = $"{y}-12-04", isCurrent = true }), HttpStatusCode.BadRequest);
    }

    // ---- helpers ------------------------------------------------------------------------

    private sealed record ClassPlacement(int Year, long YearId, long TermId, long GradeId, long StreamId);

    private static int NextYear() => Interlocked.Increment(ref _lastYear);

    private static object Year(int y) => new { name = $"Y{y}", startDate = $"{y}-01-10", endDate = $"{y}-12-05", isCurrent = false };

    private static object Term(int number, string start, string end) => new { name = $"Term {number}", termNumber = number, startDate = start, endDate = end, isCurrent = false };

    private static async Task<ClassPlacement> NewClassAsync(HttpClient client, int level, int capacity)
    {
        var y = NextYear();
        var yearId = Id(await Ok(client.PostAsJsonAsync("/api/academic/years", Year(y))));
        var termId = Id(await Ok(client.PostAsJsonAsync($"/api/academic/years/{yearId}/terms", Term(1, $"{y}-01-10", $"{y}-04-10"))));
        await Ok(client.PostAsJsonAsync($"/api/academic/years/{yearId}/terms", Term(2, $"{y}-05-05", $"{y}-08-05")));
        var gradeId = Id(await Ok(client.PostAsJsonAsync("/api/academic/grades", new { academicYearId = yearId, name = $"Form {level}", code = $"F{level}", levelOrder = level })));
        var streamId = Id(await Ok(client.PostAsJsonAsync($"/api/academic/grades/{gradeId}/streams", new { name = "Blue", capacity })));
        return new ClassPlacement(y, yearId, termId, gradeId, streamId);
    }

    private static object NewStudent(string first, string last, long streamId, object? guardian = null) =>
        new { firstName = first, lastName = last, gender = "female", dob = "2012-03-04", streamId, guardian };

    private static object NewGuardian(string first, string last, string phone) => new
    {
        newGuardian = new { firstName = first, lastName = last, phone },
        relationshipType = "guardian", isPrimaryContact = false, isBillingContact = true, isEmergencyContact = true, canPickup = true,
    };

    private static long Id(JsonElement e) => e.GetProperty("id").GetInt64();

    private static JsonElement Single(JsonElement array) => Assert.Single(array.EnumerateArray());

    private static async Task<JsonElement> Ok(Task<HttpResponseMessage> call)
    {
        var response = await call;
        await LearnCloudApiFixture.EnsureSuccessAsync(response, $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task ExpectAsync(Task<HttpResponseMessage> call, HttpStatusCode status, string? detailContains = null)
    {
        var response = await call;
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {(int)status}, got {(int)response.StatusCode}: {body}");
        if (detailContains is not null) Assert.Contains(detailContains, body, StringComparison.OrdinalIgnoreCase);
    }
}
