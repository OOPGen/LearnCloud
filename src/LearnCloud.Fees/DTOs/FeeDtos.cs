using System;

namespace LearnCloud.Fees.DTOs;

// DTOs without audit fields IsDeleted etc. - fixes entity leakage H4

public record FeeItemDto(long Id, string Name, string Code, string Recurrence, bool IsProratable, bool IsOptional, string? GlCode, string? Description, DateTime CreatedAt);
public record FeeStructureDto(long Id, string Name, long AcademicYearId, long TermId, long? GradeId, long? StreamId, long? StudentId, string Status, string Currency, List<FeeStructureItemDto> Items, DateTime CreatedAt);
public record FeeStructureItemDto(long Id, long FeeStructureId, long FeeItemId, string Description, decimal Amount, string Currency, int Quantity, decimal LineTotal);
public record FeeInvoiceDto(long Id, string InvoiceNumber, long AcademicYearId, long TermId, long StudentId, string Status, decimal SubtotalAmount, decimal TotalAmount, decimal BalanceDue, string Currency, DateTime IssueDate, DateTime DueDate);
