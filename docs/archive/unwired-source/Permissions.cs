using System.Collections.Generic;

namespace LearnCloud.Authorization
{
    /// <summary>
    /// Single source of truth for permission strings. Bulawayo HQ.
    /// Format: module.action e.g. students.read
    /// </summary>
    public static class Permissions
    {
        public static class Tenants
        {
            public const string Read = "tenants.read";
            public const string Create = "tenants.create";
            public const string Update = "tenants.update";
            public const string Suspend = "tenants.suspend";
        }
        public static class Platform
        {
            public static class Plans
            {
                public const string Read = "platform.plans.read";
                public const string Manage = "platform.plans.manage";
            }
            public static class Subscriptions
            {
                public const string Read = "platform.subscriptions.read";
                public const string Manage = "platform.subscriptions.manage";
            }
            public static class Audit
            {
                public const string Read = "platform.audit.read";
            }
        }
        public static class Users
        {
            public const string Read = "users.read";
            public const string Create = "users.create";
            public const string Update = "users.update";
            public const string Disable = "users.disable";
            public const string Invite = "users.invite";
            public const string Import = "users.import";
        }
        public static class Roles
        {
            public const string Read = "roles.read";
            public const string Manage = "roles.manage";
        }
        public static class Settings
        {
            public const string Read = "settings.read";
            public const string Manage = "settings.manage";
            public const string AcademicYearManage = "settings.academicYear.manage";
        }
        public static class Audit
        {
            public const string Read = "audit.read";
            public const string Export = "audit.export";
        }
        public static class Academic
        {
            public const string AcademicYearsRead = "academicYears.read";
            public const string AcademicYearsManage = "academicYears.manage";
            public const string TermsRead = "terms.read";
            public const string TermsManage = "terms.manage";
            public const string GradesRead = "grades.read";
            public const string GradesManage = "grades.manage";
            public const string StreamsRead = "streams.read";
            public const string StreamsManage = "streams.manage";
            public const string SubjectsRead = "subjects.read";
            public const string SubjectsManage = "subjects.manage";
            public const string GradeSubjectsRead = "gradeSubjects.read";
            public const string GradeSubjectsManage = "gradeSubjects.manage";
            public const string RoomsRead = "rooms.read";
            public const string RoomsManage = "rooms.manage";
        }
        public static class Staff
        {
            public const string Read = "staff.read";
            public const string Write = "staff.write";
            public const string Delete = "staff.delete";
            public const string Import = "staff.import";
            public const string Export = "staff.export";
        }
        public static class Students
        {
            public const string Read = "students.read";
            public const string Write = "students.write";
            public const string Delete = "students.delete";
            public const string Archive = "students.archive";
            public const string Import = "students.import";
            public const string Export = "students.export";
            public const string PhotoUpload = "students.photo.upload";
        }
        public static class Enrolments
        {
            public const string Read = "enrolments.read";
            public const string Create = "enrolments.create";
            public const string Transfer = "enrolments.transfer";
            public const string Withdraw = "enrolments.withdraw";
            public const string Promote = "enrolments.promote";
            public const string ReadHistory = "enrolments.readHistory";
        }
        public static class Guardians
        {
            public const string Read = "guardians.read";
            public const string Write = "guardians.write";
            public const string Link = "guardians.link";
            public const string BillingAssign = "guardians.billing.assign";
            public const string Delete = "guardians.delete";
        }
        public static class Admissions
        {
            public const string Read = "admissions.read";
            public const string Create = "admissions.create";
            public const string UpdateStatus = "admissions.updateStatus";
            public const string Convert = "admissions.convert";
            public const string Delete = "admissions.delete";
            public const string Export = "admissions.export";
        }
        public static class Attendance
        {
            public const string Read = "attendance.read";
            public const string Mark = "attendance.mark";
            public const string Edit = "attendance.edit";
            public const string Delete = "attendance.delete";
            public const string Export = "attendance.export";
        }
        public static class Timetable
        {
            public const string Read = "timetable.read";
            public const string Manage = "timetable.manage";
            public const string Clone = "timetable.clone";
            public const string Export = "timetable.export";
        }
        public static class Fees
        {
            public const string StructuresRead = "fees.structures.read";
            public const string StructuresManage = "fees.structures.manage";
            public const string InvoicesRead = "fees.invoices.read";
            public const string InvoicesCreate = "fees.invoices.create";
            public const string InvoicesVoid = "fees.invoices.void";
            public const string InvoicesExport = "fees.invoices.export";
            public const string PaymentsRead = "fees.payments.read";
            public const string PaymentsCreate = "fees.payments.create";
            public const string PaymentsReverse = "fees.payments.reverse";
            public const string PaymentsExport = "fees.payments.export";
            public const string ReceiptsRead = "fees.receipts.read";
            public const string ReportsRead = "fees.reports.read";
            public const string DiscountApprove = "fees.discount.approve";
        }
        public static class Assessments
        {
            public const string CategoriesRead = "assessmentCategories.read";
            public const string CategoriesManage = "assessmentCategories.manage";
            public const string GradingRead = "grading.read";
            public const string GradingManage = "grading.manage";
            public const string Read = "assessments.read";
            public const string Manage = "assessments.manage";
        }
        public static class Marks
        {
            public const string Read = "marks.read";
            public const string Enter = "marks.enter";
            public const string EditOwn = "marks.editOwn";
            public const string Submit = "marks.submit";
            public const string Approve = "marks.approve";
            public const string Unlock = "marks.unlock";
            public const string Export = "marks.export";
        }
        public static class ReportCards
        {
            public const string Read = "reportCards.read";
            public const string Generate = "reportCards.generate";
            public const string Publish = "reportCards.publish";
            public const string Export = "reportCards.export";
        }
        public static class Messages
        {
            public const string Read = "messages.read";
            public const string Send = "messages.send";
            public const string SendOwnClass = "messages.sendOwnClass";
            public const string SendBroadcast = "messages.sendBroadcast";
            public const string Delete = "messages.delete";
            public const string ReadAll = "messages.readAll";
        }
        public static class Dashboard
        {
            public const string ViewAdmin = "dashboard.viewAdmin";
            public const string ViewHead = "dashboard.viewHead";
            public const string ViewBursar = "dashboard.viewBursar";
            public const string ViewRegistrar = "dashboard.viewRegistrar";
            public const string ViewTeacher = "dashboard.viewTeacher";
            public const string ViewParent = "dashboard.viewParent";
            public const string ViewStudent = "dashboard.viewStudent";
            public const string ViewPlatform = "dashboard.viewPlatform";
        }

        public static readonly IReadOnlyList<string> All = new List<string>
        {
            Tenants.Read, Tenants.Create, Tenants.Update, Tenants.Suspend,
            Platform.Plans.Read, Platform.Plans.Manage, Platform.Subscriptions.Read, Platform.Subscriptions.Manage, Platform.Audit.Read,
            Users.Read, Users.Create, Users.Update, Users.Disable, Users.Invite, Users.Import,
            Roles.Read, Roles.Manage,
            Settings.Read, Settings.Manage, Settings.AcademicYearManage,
            Audit.Read, Audit.Export,
            Academic.AcademicYearsRead, Academic.AcademicYearsManage, Academic.TermsRead, Academic.TermsManage,
            Academic.GradesRead, Academic.GradesManage, Academic.StreamsRead, Academic.StreamsManage,
            Academic.SubjectsRead, Academic.SubjectsManage, Academic.GradeSubjectsRead, Academic.GradeSubjectsManage,
            Academic.RoomsRead, Academic.RoomsManage,
            Staff.Read, Staff.Write, Staff.Delete, Staff.Import, Staff.Export,
            Students.Read, Students.Write, Students.Delete, Students.Archive, Students.Import, Students.Export, Students.PhotoUpload,
            Enrolments.Read, Enrolments.Create, Enrolments.Transfer, Enrolments.Withdraw, Enrolments.Promote, Enrolments.ReadHistory,
            Guardians.Read, Guardians.Write, Guardians.Link, Guardians.BillingAssign, Guardians.Delete,
            Admissions.Read, Admissions.Create, Admissions.UpdateStatus, Admissions.Convert, Admissions.Delete, Admissions.Export,
            Attendance.Read, Attendance.Mark, Attendance.Edit, Attendance.Delete, Attendance.Export,
            Timetable.Read, Timetable.Manage, Timetable.Clone, Timetable.Export,
            Fees.StructuresRead, Fees.StructuresManage, Fees.InvoicesRead, Fees.InvoicesCreate, Fees.InvoicesVoid, Fees.InvoicesExport,
            Fees.PaymentsRead, Fees.PaymentsCreate, Fees.PaymentsReverse, Fees.PaymentsExport, Fees.ReceiptsRead, Fees.ReportsRead, Fees.DiscountApprove,
            Assessments.CategoriesRead, Assessments.CategoriesManage, Assessments.GradingRead, Assessments.GradingManage,
            Assessments.Read, Assessments.Manage,
            Marks.Read, Marks.Enter, Marks.EditOwn, Marks.Submit, Marks.Approve, Marks.Unlock, Marks.Export,
            ReportCards.Read, ReportCards.Generate, ReportCards.Publish, ReportCards.Export,
            Messages.Read, Messages.Send, Messages.SendOwnClass, Messages.SendBroadcast, Messages.Delete, Messages.ReadAll,
            Dashboard.ViewAdmin, Dashboard.ViewHead, Dashboard.ViewBursar, Dashboard.ViewRegistrar,
            Dashboard.ViewTeacher, Dashboard.ViewParent, Dashboard.ViewStudent, Dashboard.ViewPlatform
        };
    }

    public static class RoleCodes
    {
        public const string PlatformSuperadmin = "PLATFORM_SUPERADMIN";
        public const string SchoolAdmin = "SCHOOL_ADMIN";
        public const string HeadTeacher = "HEAD_TEACHER";
        public const string DeputyHead = "DEPUTY_HEAD";
        public const string Bursar = "BURSAR";
        public const string Registrar = "REGISTRAR";
        public const string Teacher = "TEACHER";
        public const string Parent = "PARENT";
        public const string Student = "STUDENT";
    }
}
