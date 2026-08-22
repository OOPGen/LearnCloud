namespace LearnCloud.Core.DTOs;

public record SubjectDto(long Id, string Name, string Code, string? Description, bool IsCore, string? Department, string Status, int GradesOffered, DateTime CreatedAt);
public record CreateSubjectRequest(string Name, string Code, string? Description, bool IsCore, string? Department);
public record UpdateSubjectRequest(string? Name, string? Code, string? Description, bool? IsCore, string? Department, string? Status);

public record GradeSubjectDto(long Id, long GradeId, string GradeName, string GradeCode, long SubjectId, string SubjectName, long AcademicYearId, bool IsCompulsory);
public record AssignSubjectToGradeRequest(long GradeId, long SubjectId, long AcademicYearId, bool IsCompulsory);

public record SubjectListRequest(string? Search, string? Department, bool? IsCore, string? Status, string? SortBy, bool SortDesc, int Page = 1, int PageSize = 25);
public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize, int TotalPages);
