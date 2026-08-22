# Transport Module — Routes, Vehicles, Drivers, Learner Assignment, Fees Flow into Existing Invoicing, Boarding Attendance, Notifications, Reports, Manifest

## Routes with Ordered Stops and Expected Times

- **Route** tenant_id, name Hillside Route A, code RTE-A, description, direction morning/evening/both, is_active, academic_year/term, total_distance_km, estimated_duration_minutes, fee_amount per term, currency USD, vehicle_id, driver_id, assistant_id, stops collection, assignments
- **RouteStop** tenant_id, route_id, name Hillside Shops, address, latitude/longitude, order_number ordered 1,2,3, expected_arrival_time 06:30, expected_departure_time, distance_from_start_km, estimated_minutes_from_start, is_active
- Endpoints: POST /routes create, GET /routes list with assigned/capacity utilisation, GET /routes/{id}, POST /routes/{id}/stops add, GET /routes/{id}/stops ordered, POST /routes/{id}/stops/reorder OrderedStopIds, triggers route change notification via messaging module

## Vehicles with Capacity, Registration, Insurance and Licence Expiry with Reminders

- **Vehicle** tenant_id, registration_number ACD 1234, make Toyota, model Coaster, capacity seats, year, fuel_type, insurance_expiry, licence_expiry ZINARA, fitness_expiry, service_due_date, is_active, status active/maintenance/retired, last_insurance_reminder_sent_at, last_licence_reminder_sent_at
- Endpoints: POST /vehicles, GET /vehicles, GET /vehicles/needing-reminder where insurance or licence expiry <=30 days threshold, reminder via messaging to school admin daily background job

## Drivers and Assistants with Licence Expiry Tracking

- **Driver** tenant_id, full_name, role driver/assistant, staff_id nullable link to staff if driver is also staff, licence_number, licence_type Class 2/4, licence_expiry, medical_expiry, phone, email, id_number, is_active, last_licence_reminder_sent_at
- Endpoints: POST /drivers, GET /drivers?role=driver/assistant, GET /drivers/needing-licence-reminder where licence_expiry <=30 days

## Learner Assignment to Route and Stop with Capacity Enforcement

- **TransportAssignment** tenant_id, student_id, route_id, pickup_stop_id, drop_stop_id nullable, assigned_date now, assigned_by_user_id, status active/inactive/suspended, unassigned_date, unassigned_reason, academic_year_id, term_id, fee_structure_item_id link to fee structure item created for transport fee, fee_applied bool
- Capacity enforcement: when assigning, check vehicle capacity vs current assigned count where route_id and status active, if capacity >0 and assigned >= capacity throw "Route capacity reached" - enforcement
- Also check if learner already assigned to another route in same year/term active, throw unassign first or transfer
- Endpoints: POST /assignments {studentId, routeId, pickupStopId, dropStopId, academicYearId, termId}, POST /assignments/bulk {studentIds, routeId, pickupStopId}, DELETE /assignments/{id} unassign with reason, GET /routes/{routeId}/assignments
- Frontend: learner assignment with capacity enforcement shows assigned/capacity utilisation percent, transport fees flow into existing invoicing note

## Transport Fees That Flow into Existing Fee Structure and Invoicing Rather Than Parallel Billing

- **TransportFeeLink** tenant_id, transport_assignment_id, fee_item_id TRANSPORT, fee_structure_id, fee_structure_item_id, amount, currency
- Flow: When learner assigned to route, ApplyTransportFeeAsync: find or create fee item TRANSPORT code Transport recurrence per_term, find or create fee structure for learner for year/term (individual transport fee structure that will be merged with grade/stream structures during invoice generation priority individual > stream > grade > school), add or update fee_structure_item amount = route.feeAmount, description Transport - RouteName - Pickup, quantity 1 line_total route.feeAmount, link TransportFeeLink, fee_applied true. Then fee invoice generation per term (existing fee module) will include transport fee because it merges structures. Existing fee entities FeeItem, FeeStructure, FeeStructureItem, FeeInvoice not altered, just new rows, consuming existing fee services without altering them. FeeStructureItem is part of existing fee module - we reuse.

## Boarding Attendance Per Trip If School Wants It

- **TransportAttendance** tenant_id, route_id, route_stop_id nullable, student_id, transport_assignment_id, trip_date, trip_type morning/evening, status boarded/missed/absent/excused, actual_boarding_time, marked_by_user_id, notes
- Endpoints: POST /attendance/boarding {routeId, routeStopId, tripDate, tripType, items [{studentId, status, notes, actualBoardingTime}]}, GET /attendance/boarding?routeId&tripDate&tripType
- Frontend: BoardingAttendanceTab routeId, tripDate, tripType, items studentId status boarded/missed/absent, mark button, triggers absence notification if absent/missed

## Route Change and Absence Notifications to Guardians Using Existing Messaging Module

- **TransportNotificationLog** tenant_id, route_id, student_id nullable, notification_type route_change/absence/assignment/unassignment, title, body, message_batch_id link to messaging module batch, recipients_json list studentIds, sent_at, sent_by_user_id
- NotifyRouteChangeAsync: route change (stops reordered, times changed, assignment) creates TransportNotificationLog and would call existing messaging module to send SMS/email to guardians of learners assigned to route via MessageBatch audience dynamic - for V1 log and would call messaging service
- NotifyAbsenceAsync: when boarding attendance marked absent/missed, creates log and would send SMS to guardian of that learner via existing messaging module

## Reports on Utilisation Per Route, Revenue Per Route, and Unassigned Learners

- **UtilisationReportDto** routeId, routeName, capacity, assigned, utilisationPercent assigned/capacity*100, boardedToday count, boardingRate boarded/assigned*100, stopsCount
- Endpoint GET /reports/utilisation returns for all routes
- **RevenueReportDto** routeId, routeName, feePerLearner, assignedLearners, totalInvoiced sum fee invoices for assigned students year/term, totalCollected sum amountPaid, totalArrears invoiced-collected, collectionRate collected/invoiced*100, currency
- Endpoint GET /reports/revenue?academicYearId&termId
- **UnassignedLearnersDto** students list unassigned, totalUnassigned, endpoint GET /reports/unassigned-learners?gradeId
- Unassigned learners: all students where not in transport_assignments active

## Printable Route Manifest for Each Driver

- **RouteManifestDto** routeId, routeName, code, direction, vehicle VehicleDto, driver DriverDto, assistant DriverDto, stops list StopManifestDto, generatedAt
- **StopManifestDto** stopId, stopName, address, orderNumber, expectedArrivalTime, learnersCount, learners list LearnerManifestDto
- **LearnerManifestDto** studentId, studentName, studentNumber, gradeName, streamName, guardianPhone, guardianName, pickupStopName, dropStopName
- Endpoint GET /routes/{routeId}/manifest returns manifest, GET /routes/{routeId}/manifest/print returns HTML table border black printable A4: route header vehicle driver assistant, table order stop name address expected arrival learners count learners per stop with names numbers grade stream guardian phone pickup/drop
- Frontend ManifestTab routeId input, Load Manifest button, Print A4 button target _blank to print endpoint

## Permissions

- SCHOOL_ADMIN, TRANSPORT_MANAGER can create route/vehicle/driver/assignment
- TEACHER can view assignments and mark boarding attendance for own route? Configurable
- BURSAR can view revenue reports
- DRIVER role can view own route manifest via printable endpoint

## Consuming Existing Modules Without Altering

- Fee flow: reuses FeeItem with code TRANSPORT, FeeStructure, FeeStructureItem, FeeInvoice via existing fee invoicing job - no parallel billing
- Messaging: uses existing messaging MessageBatch via TransportNotificationLog message_batch_id link, audience dynamic guardians of learners assigned to route
- Attendance: uses existing attendance? TransportAttendance separate but similar pattern

## Migration V13_Transport.sql

- routes, route_stops ordered, vehicles with registration unique per tenant, drivers with licence expiry, transport_assignments unique tenant student year term, transport_fee_links, transport_attendances unique tenant route student date type, transport_notification_logs, seed default fee item TRANSPORT per tenant

## Tests (to add)

- Capacity enforcement: assign beyond capacity throws
- Fee flow: assignment creates fee_structure_item with route fee amount, invoice generation includes transport fee
- Route manifest contains ordered stops and learners per stop
- Utilisation calculation assigned/capacity*100
- Unassigned learners not in transport_assignments
