# Hostel and Boarding Module — Blocks, Rooms, Beds, Allocation, House Masters, Boarding Fees, Exeat, Roll Call, Visitor, Incident, Sick Bay, Reports, Printable Lists

## Blocks, Rooms and Beds with Capacity and Gender Designation

- **HostelBlock** tenant_id, name Shumba House, code SHU, gender_designation male/female/mixed, capacity total beds, total_rooms, is_active, description, location, house_master_staff_id, matron_staff_id, rooms collection, staffAssignments
- **HostelRoom** block_id, room_number 101/A1, floor, capacity beds in room, gender_designation male matching block, is_active, facilities ensuite/common bathroom, beds collection
- **HostelBed** room_id, block_id denormalized, bed_number 101A/101B or 1,2,3, status available/occupied/reserved/maintenance/out_of_order, condition good/fair/damaged/new/poor, current_student_id, current_allocation_id, last_issued_at, last_returned_at

## Allocation of Learners to Beds for Term with Conflict Detection and Waiting List

- **BedAllocation** student_id, bed_id, room_id, block_id, academic_year_id, term_id, allocation_date now, allocated_by_user_id, status active/vacated/transferred/expired, vacated_date/reason, fee_applied bool, fee_structure_item_id, waiting_list_id if allocated from waiting list
- Conflict detection: bed status must be available else throw "Bed not available status X - conflict detection", student already allocated in same year/term active throw "Learner already allocated to bed Y in same year/term - conflict"
- **WaitingList** student_id, preferred_block_id, preferred_room_id, gender male/female, academic_year/term, priority 0 high 1 normal, queue_position auto count waiting +1, status waiting/allocated/cancelled/expired, reason, requested_date, requested_by_user_id, allocated_at, allocated_bed_id. When no vacant beds, add to waiting list, when bed vacated first in waiting list (lowest queue_position) gets allocated.

## House Masters and Matrons Assigned to Blocks

- **BlockStaffAssignment** block_id, staff_id, role house_master/matron/assistant/prefect, assigned_date, unassigned_date, is_active, responsibilities. House master and matron assigned to blocks via block.house_master_staff_id and matron_staff_id plus assignments table for history

## Boarding Fees Integrated with Existing Fee Structure

- **BoardingFeeLink** bed_allocation_id, fee_item_id BOARDING, fee_structure_id, fee_structure_item_id, amount, currency
- Flow: When learner allocated to bed, ApplyBoardingFeeAsync finds or creates fee item BOARDING code Boarding recurrence per_term, finds or creates fee structure individual for learner year/term (priority individual > stream > grade > school), adds fee_structure_item amount = block fee? For demo 500 per term, description Boarding - BlockName, quantity 1 line_total amount, link BoardingFeeLink, fee_applied true. Fee flows into existing fee invoicing rather than parallel billing: fee invoice generation per term merges structures, includes boarding fee. Existing fee entities FeeItem, FeeStructure, FeeStructureItem, FeeInvoice not altered, just new rows, consuming existing fee services without altering them.

## Exeat and Leave Register Recording Departure, Expected Return, Actual Return and Authorising Person

- **ExeatRegister** student_id, block_id, bed_allocation_id, leave_type exeat/weekend/medical/emergency/holiday, reason, departure_date_time, expected_return_date_time, actual_return_date_time, status pending/approved/departed/returned/overdue/cancelled, authorised_by_user_id authorising person, authoriser_role house_master/head/matron, approved_by, contact_phone_during_leave, destination_address, accompanying_person, is_overdue computed status departed and now > expected and actual null
- Endpoints: POST /exeat create, GET /exeat/on-leave list on leave and overdue totalOnLeave totalOverdue, POST /exeat/{id}/return actualReturnDateTime notes
- Printable leave register for gate: GET /leave-register/gate/print HTML table student block leave type reason departure expected return contact phone authorising person for gate check ID allow departure only if on list

## Nightly Roll Call

- **RollCall** block_id, room_id nullable (if whole block), roll_call_date, roll_call_type nightly/morning/evening, status in_progress/completed/cancelled, conducted_by_user_id, completed_at, total_expected, total_present, total_absent, total_on_leave, entries collection
- **RollCallEntry** roll_call_id, block_id, room_id, bed_id, student_id, roll_call_date, status present/absent/on_leave/sick_bay, notes, marked_by_user_id
- Flow: Create roll call for block date type, total_expected = count active bed allocations in block, then mark entries present/absent/on_leave/sick_bay, completed total counts

## Visitor Log

- **VisitorLog** student_id, block_id, visitor_name, relationship parent/guardian/sibling/friend/other, id_number national ID, phone, check_in_date_time, check_out_date_time, purpose, authorised_by_user_id house master authorising, belongings, status checked_in/checked_out/denied
- Endpoints: POST /visitors create, POST /visitors/{id}/checkout checkOutDateTime

## Incident and Sick Bay Records with Appropriate Access Restrictions

- **IncidentRecord** block_id, room_id, student_id, incident_type incident/sick_bay/disciplinary/medical/welfare, title, description, severity low/medium/high/critical, incident_date_time, reported_by_user_id, action_taken, follow_up_required, status open/investigating/resolved/closed, visibility house_master/matron/nurse/head/admin, is_confidential bool, assigned_to_user_id
- **SickBayRecord** student_id, block_id, check_in_date_time, check_out_date_time, symptoms, diagnosis, treatment, medication, status admitted/treated/discharged/referred, admitted_by_user_id matron/nurse, discharged_by, visibility matron/nurse/house_master/head, is_confidential true, notes
- Access restrictions: filter by visibility based on user role - house master sees house_master, matron sees matron/nurse, school admin/head sees all, student/guardian sees none unless not confidential. Implemented in controller GetIncidents filtering allowedVisibilities list based on user roles, confidential filtering.

## Reports on Occupancy, Vacancies, Learners on Leave and Boarding Revenue

- **OccupancyReportDto** block_id, blockName, capacity, occupied, available, reserved, maintenance, occupancyPercent occupied/total*100, rooms list RoomOccupancyDto roomId roomNumber capacity occupied available
- GET /reports/occupancy?blockId optional
- **VacancyReportDto** vacantBeds list BedDto, totalVacant, byBlock dict blockId->count, byGender dict gender->count
- GET /reports/vacancies
- **OnLeaveReportDto** onLeave list ExeatDto, overdue list, totalOnLeave, totalOverdue
- GET /exeat/on-leave
- **BoardingRevenueDto** blockId blockName feePerLearner assignedLearners totalInvoiced totalCollected totalArrears collectionRate currency - uses fee invoices for boarding fee item, similar to transport revenue report, fee flow into existing invoicing
- GET /reports/boarding-revenue?academicYearId&termId

## Printable Bed Allocation List per Block and Leave Register for Gate

- **Bed allocation list per block**: GET /blocks/{blockId}/bed-allocation-list/print returns HTML table border black printable A4: block name code gender, capacity, rooms, beds, student name number grade, house master matron, generated timestamp, printed from LearnCloud
- **Leave register for gate**: GET /leave-register/gate/print returns HTML table student block leave type reason departure expected return contact phone authorising person for gate check ID allow departure only if on list and authorised, record actual return time when student returns

## Permissions

- SCHOOL_ADMIN, HEAD_TEACHER, BOARDING_MASTER can create block/room/bed, allocate, exeat, roll call, visitor, incident
- MATRON can create sick bay, view incidents visibility matron/nurse
- BURSAR can view boarding revenue
- STUDENT/GUARDIAN cannot view hostel admin except own allocation? V1 restricted

## Migration V14_Hostel.sql

- hostel_blocks, hostel_rooms, hostel_beds accession/barcode unique per tenant, block_staff_assignments, bed_allocations unique tenant bed active and tenant student year term (conflict detection), waiting_lists queue_position, boarding_fee_links, exeat_registers, roll_calls unique tenant block date type, roll_call_entries, visitor_logs, incident_records, sick_bay_records, seed default boarding fee item BOARDING per tenant

## Frontend

- HostelModule.jsx 7 tabs: Blocks Rooms Beds Gender House Masters, Allocation Conflict & Waiting List Boarding Fees Integrated, Exeat Leave Register Gate, Roll Call Nightly, Visitors, Incidents Sick Bay Restricted Access, Reports Occupancy Vacancies OnLeave Revenue, Printable Bed Allocation & Leave Register Gate A4

