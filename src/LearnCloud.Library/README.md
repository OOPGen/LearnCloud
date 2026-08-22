# Library Module — Catalogue, Copies, Membership, Issue/Return, Renewals, Reservations, Overdue Fines Posting to Fee Account, Lost/Damaged, Stock Take, Reports, Barcode Scanner Fast Screen

## Catalogue with Title, Author, ISBN, Category, Copies and Shelf Location

- **LibraryCategory** tenant_id, name Fiction/Non-Fiction/Textbooks/Reference/Science, code FIC/NONFIC/TEXT, parent_category_id, is_active
- **Book** tenant_id, title, author, ISBN 13 digits, category_id, publisher, publication_year, edition, shelf_location e.g. A-1-3 Block-Shelf-Position, total_copies cached, available_copies cached, replacement_price decimal currency USD for lost/damaged charge, description, cover_image_url, status active/archived, Copies collection
- Seeded categories per tenant if none

## Barcode or Accession Number Per Copy

- **BookCopy** tenant_id, book_id, accession_number ACC-2026-00001 unique per tenant, barcode BC-12345-... unique per tenant (barcode gun types quickly + Enter), shelf_location per copy overrides book shelf, status available/issued/reserved/lost/damaged/under_maintenance, condition new/good/fair/poor/damaged, last_issued_at, last_returned_at, current_loan_id
- Barcode scanner: types quickly <50ms between keystrokes and sends Enter, handled via fast detection, trimmed, uppercased, handles trailing spaces, mixed case, partial matches when scanner fails

## Membership Derived from Learners and Staff with Configurable Borrowing Limits and Loan Periods

- **MembershipConfig** tenant_id, membership_type student/staff/teacher, max_books 3/5/10, loan_period_days 14/21/30, max_renewals 1/1/2, fine_per_day 1.00/0.50, currency USD, max_fine 50 cap, allow_reservations true, is_active. Seeded per tenant if none: student 3 books 14 days, teacher 10 books 30 days, staff 5 books 21 days
- **LibraryMember** tenant_id, membership_number LIB-2026-00001 unique per tenant, member_type student/staff/teacher, student_id nullable, staff_id nullable, user_id nullable linked account, full_name, status active/suspended/graduated/inactive, currently_borrowed, total_borrowed, outstanding_fines, currency, membership_expiry_date
- Derived: when student enrols, service creates LibraryMember automatically? For V1 manual create member via API POST /members {memberType student, studentId, fullName}. Membership derived from learners and staff means member linked to student_id or staff_id.

## Issue and Return with Due Dates

- **Loan** tenant_id, book_copy_id, book_id, member_id, student_id denormalized, staff_id, issue_date, due_date = issue_date + loan_period_days from config, return_date, status issued/returned/overdue/lost/damaged, issued_by_user_id, returned_by_user_id, renewal_count, is_overdue computed dueDate < now
- Issue: checks membership borrowing limits (currently_borrowed < max_books), copy status available, reservations - if reserved by other member and waiting list queue_position 1 is other member, block issue to current member, else allow. Creates loan, copy status issued, last_issued_at now, current_loan_id loan.id, member currently_borrowed++, total_borrowed++
- Return: finds active loan for copy, sets return_date now, status returned, returned_by, copy status available or damaged based on condition param, last_returned_at now, current_loan_id null, member currently_borrowed--. If overdue, generates fine.

## Renewals

- **LoanRenewal** tenant_id, loan_id, renewal_date now, previous_due_date, new_due_date = previous + loan_period_days, renewal_number, approved_by_user_id, reason
- Checks max_renewals from config, if renewal_count >= max_renewals throw max reached. Updates loan due_date, renewal_count++, creates renewal record.

## Reservations and Waiting List

- **Reservation** tenant_id, book_id, member_id, student_id, reservation_date now, expiry_date +7 days, status pending/fulfilled/cancelled/expired, queue_position = pending count for book +1, fulfilled_at, fulfilled_loan_id
- When book has pending reservations, issue blocked for non-reserved members, queue position shown. When copy returned, first pending reservation gets notified? For V1, when issue attempted, checks if reserved by other member, blocks. When reserved member issues, reservation fulfilled.

## Overdue Tracking with Fines That Post to Learner's Fee Account Through Existing Fee Services

- **Fine** tenant_id, loan_id, member_id, student_id (for posting to fee account), fineType overdue/lost/damaged, amount decimal, currency, daysOverdue, finePerDay, status pending/posted_to_fee_account/paid/waived, posted_to_fee_account bool, fee_invoice_id link to fee invoice created via existing fee services, fee_invoice_item_id, paid_at, waived_reason
- Overdue tracking: on return, if now.Date > dueDate.Date, daysOverdue = (now - dueDate).Days, fine per day from membership config, amount = daysOverdue * finePerDay rounded 2, max fine cap, creates fine pending
- **Post to learner's fee account through existing fee services without altering them:** PostFineToFeeAccountAsync:
  - Find or create fee item LIB_FINE code Library Fine recurrence one_off if not exists (consuming existing fee entities without altering them - creates new fee item)
  - Create fee invoice via existing fee invoice entity structure: invoice_number LIB-FINE-YYYYMMDD-XXXXX, academic_year 2026 term 1, student_id, subtotal = fine amount, total = fine amount, balance_due = fine amount, currency, issue_date now, due_date +7 days, status Issued, structure_hash library_fine_{fineId}
  - Create fee invoice item description "Library fine - overdue - Loan {loanId} - {days} days overdue" quantity 1 unit amount fine amount line total fine amount currency
  - Save, then fine.posted_to_fee_account true, fee_invoice_id = invoice.id, status posted_to_fee_account
  - This uses existing fee entities (FeeInvoice, FeeInvoiceItem) without altering their definitions, just creating new rows, consuming existing fee services (FeeCalculationService for rounding)
- Fine payment: when fee invoice paid via existing fee payment allocation, fine status paid

## Lost and Damaged Handling with Replacement Charge

- **LostDamagedRecord** tenant_id, book_copy_id, loan_id, member_id, type lost/damaged, condition damaged details, replacement_charge = book replacement_price, fine_amount overdue fine + replacement, currency, status pending/charged/paid/waived, fee_invoice_id
- Flow: POST /lost-damaged {bookCopyId, loanId, type lost/damaged, condition, replacementCharge, fineAmount, currency} → copy status lost/damaged, create record, create fine replacement + overdue, post to fee account via same method as overdue fine posting, creates fee invoice

## Stock Take with Discrepancy Report

- **StockTake** tenant_id, name Stock Take Term 2 2026, started_at now, completed_at, status in_progress/completed/cancelled, created_by_user_id, total_expected = count book_copies where tenant and not deleted, total_counted, discrepancies count
- **StockTakeItem** tenant_id, stock_take_id, book_copy_id, expected_status what system thinks (available/issued), counted_status what was counted (available/missing/damaged/wrong_location), discrepancy_type none/missing/extra/damaged/wrong_location, notes, counted_by_user_id
- Flow: Create stock take, total_expected = count copies, then count items via POST /stock-takes/{id}/count {bookCopyId, countedStatus, notes} — barcode scanner fast, each count creates StockTakeItem, compares expected_status vs counted_status, if different discrepancy_type set, stockTake total_counted++, discrepancies++ if discrepancy != none
- Discrepancy report GET /stock-takes/{id}/discrepancy-report returns stockTake, discrepancies list items where discrepancyType != none with bookCopy accession, book title, expected vs counted, totalExpected totalCounted discrepanciesCount

## Reports on Circulation, Popular Titles, Overdue Items and Inventory Value

- **Circulation report** GET /reports/circulation?from&to: totalIssues count loans where issue_date between from/to, totalReturns count where return_date between, totalRenewals, daily breakdown issues/returns per day
- **Popular titles** GET /reports/popular-titles?top=10: group loans by book_id count timesBorrowed order desc take top, return bookId title author timesBorrowed availableCopies totalCopies
- **Overdue items** GET /reports/overdue: loans where status issued and due_date < now and not deleted, include member, days overdue, fines total
- **Inventory value** GET /reports/inventory-value: totalTitles count books, totalCopies count copies, availableCopies count where status available, totalValue sum totalCopies * replacementPrice, byCategory copies value

## Fast Issue and Return Screen Designed for Barcode Scanner, and Typing When Scanner Fails

- **Frontend LibraryFastIssueReturn.jsx**:
  - Mode toggle Issue/Return buttons large min-h-touch 48px
  - Barcode input: ref focused, placeholder "Scan barcode or type accession e.g. ACC-2026-00001 or BC-12345 — trailing spaces handled", h-12 px-3 border-2 border-primary-200 rounded-lg text 16px prevents iOS zoom, onKeyDown Enter triggers issue/return
  - Scanner detection: measures time between keystrokes, <50ms fast typing => scanner detected, shows ✓ Scanner detected, >300ms typing mode, handles trailing spaces trim, mixed case uppercased, partial matches when scanner fails (contains search)
  - Member select for issue: dropdown members derived from learners and staff, shows membershipNumber fullName memberType currentlyBorrowed limit configurable
  - Issue button w-full h-12 rounded-full bg-primary-800 text-white font-semibold, Return button same
  - Result success: loan id, book title, accession, member name, issue due date, message Issued X to Y due dd/MM/yyyy, or Returned with overdue fine generated
  - Recent transactions list max 10, circulation report, reservations and waiting list info, overdue tracking fine posting note, lost/damaged handling

## Permissions

- LIBRARIAN, SCHOOL_ADMIN, HEAD_TEACHER can issue/return, TEACHER can issue? Configurable
- BURSAR can post fines to fee account, view fines
- Student can view own loans? Not in V1, but member can view own via portal

## Consuming Existing Fee Entities Without Altering

- PostFineToFeeAccount creates FeeItem LIB_FINE if not exists (new row, not altering existing table structure), creates FeeInvoice and FeeInvoiceItem using existing entities, updates fine posted_to_fee_account and fee_invoice_id, audit
- This reuses existing fee services FeeCalculationService for rounding, does not alter existing fee entities definitions

## Migration V12_Library.sql

- library_categories, books, book_copies with barcode/accession unique per tenant, membership_configs seeded per tenant student 3 books 14 days teacher 10 books 30 days, library_members, loans, loan_renewals, reservations, fines, lost_damaged_records, stock_takes, stock_take_items, indexes tenant leads, fulltext title author

## Tests (to add)

- Issue fails when member at borrowing limit
- Issue blocked when reserved by other member queue position 1
- Overdue fine calculation daysOverdue * finePerDay capped maxFine
- Fine posts to fee account creates fee invoice with LIB_FINE item and balance
- Fast issue handles trailing spaces, mixed case, partial matches
- Stock take discrepancy detected when expected available counted missing
