using System.Text;
using LearnCloud.Core.DTOs;
using LearnCloud.Core.Validators;
using LearnCloud.Infrastructure.Text;
using Xunit;

namespace LearnCloud.Core.Tests;

public class CsvWriterTests
{
    [Theory]
    [InlineData("Moyo", "\"Moyo\"")]
    [InlineData("O\"Brien", "\"O\"\"Brien\"")]
    [InlineData("=1+1", "\"'=1+1\"")]
    [InlineData("+263771234567", "\"'+263771234567\"")]
    [InlineData("-5", "\"'-5\"")]
    [InlineData("@SUM(A1)", "\"'@SUM(A1)\"")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData(null, "\"\"")]
    public void Field_is_quoted_and_formulas_are_neutralised(string? value, string expected) =>
        Assert.Equal(expected, CsvWriter.Field(value));

    [Fact]
    public void File_starts_with_a_byte_order_mark_and_uses_crlf()
    {
        var bytes = CsvWriter.ToUtf8(new[] { CsvWriter.Row("a", "b"), CsvWriter.Row("Zoë", null) });
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        Assert.Equal("\"a\",\"b\"\r\n\"Zoë\",\"\"\r\n", Encoding.UTF8.GetString(bytes[3..]));
    }
}

public class SchoolRecordsValidatorTests
{
    [Fact]
    public void Term_must_end_after_it_starts()
    {
        var result = new CreateTermValidator().Validate(new CreateTermRequest("Term 1", 1, new DateTime(2026, 4, 1), new DateTime(2026, 1, 1), false));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTermRequest.EndDate));
    }

    [Fact]
    public void Academic_year_cannot_exceed_eighteen_months()
    {
        var result = new CreateAcademicYearValidator().Validate(new CreateAcademicYearRequest("2026", new DateTime(2026, 1, 1), new DateTime(2027, 12, 31), false));
        Assert.Contains(result.Errors, e => e.PropertyName == "EndDate");
    }

    [Fact]
    public void Guardian_link_needs_exactly_one_of_existing_or_new()
    {
        var validator = new LinkGuardianValidator();
        var both = new LinkGuardianRequest(5, new GuardianInput("A", "B", "+263771234567", null, null, null), "mother", true, false, false, true);
        var neither = both with { GuardianId = null, NewGuardian = null };
        var existing = both with { NewGuardian = null };

        Assert.False(validator.Validate(both).IsValid);
        Assert.False(validator.Validate(neither).IsValid);
        Assert.True(validator.Validate(existing).IsValid);
    }

    [Theory]
    [InlineData("+263 77 123 4567", true)]
    [InlineData("0771234567", true)]
    [InlineData("call me", false)]
    [InlineData("12", false)]
    public void Guardian_phone_format(string phone, bool valid) =>
        Assert.Equal(valid, new GuardianInputValidator().Validate(new GuardianInput("Rudo", "Moyo", phone, null, null, null)).IsValid);

    [Fact]
    public void Student_cannot_be_born_in_the_future_or_take_an_unknown_exit_reason()
    {
        var student = new CreateStudentRequest("Tendai", "Moyo", DateTime.UtcNow.AddDays(2), "female", null, null, 1, null, null, null, null);
        Assert.Contains(new CreateStudentValidator().Validate(student).Errors, e => e.PropertyName == nameof(CreateStudentRequest.Dob));
        Assert.False(new ExitStudentValidator().Validate(new ExitStudentRequest("expelled", null)).IsValid);
        Assert.True(new ExitStudentValidator().Validate(new ExitStudentRequest("graduated", null)).IsValid);
    }

    [Theory]
    [InlineData("graduated", "alumni")]
    [InlineData("transferred_out", "transferred")]
    [InlineData("withdrawn", "inactive")]
    public void Leaving_sets_the_student_status(string reason, string status) =>
        Assert.Equal(status, EnrolmentValues.StudentStatusAfterExit(reason));
}
