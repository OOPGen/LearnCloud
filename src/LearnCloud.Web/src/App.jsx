import { Suspense, lazy } from 'react'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import RequireAuth from './lib/RequireAuth'

const Home = lazy(() => import('./pages/Home'))
const LoginSkewed = lazy(() => import('./pages/LoginSkewed'))
const Dashboard = lazy(() => import('./pages/Dashboard'))
const SubjectsPage = lazy(() => import('./pages/SubjectsPage'))
const AcademicYearsPage = lazy(() => import('./pages/AcademicYearsPage'))
const GradesPage = lazy(() => import('./pages/GradesPage'))
const StudentsPage = lazy(() => import('./pages/StudentsPage'))
const StudentDetailPage = lazy(() => import('./pages/StudentDetailPage'))
const GuardiansPage = lazy(() => import('./pages/GuardiansPage'))
const FeesPage = lazy(() => import('./pages/FeesPage'))
const ResetPasswordPage = lazy(() => import('./pages/ResetPasswordPage'))
const VerifyEmailPage = lazy(() => import('./pages/VerifyEmailPage'))

function LoadingFallback() {
  return (
    <div className="min-h-screen bg-neutral-50 flex items-center justify-center">
      <div className="text-center">
        <div className="w-8 h-8 border-4 border-primary-200 border-t-primary-800 rounded-full animate-spin mx-auto"></div>
        <p className="mt-3 text-sm text-neutral-600">Loading LearnCloud...</p>
      </div>
    </div>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <Suspense fallback={<LoadingFallback />}>
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/login" element={<LoginSkewed />} />
          <Route path="/reset-password" element={<ResetPasswordPage />} />
          <Route path="/verify-email" element={<VerifyEmailPage />} />
          {/* Signed-in area. Subjects and the school records pages (academic years, grades,
              students, guardians) use the API; Dashboard and Fees are still static previews.
              Links to modules without a page redirect to Students. */}
          <Route path="/dashboard" element={<RequireAuth><Dashboard /></RequireAuth>} />
          <Route path="/analytics" element={<RequireAuth><Dashboard /></RequireAuth>} />
          <Route path="/academic-years" element={<RequireAuth><AcademicYearsPage /></RequireAuth>} />
          <Route path="/grades" element={<RequireAuth><GradesPage /></RequireAuth>} />
          <Route path="/subjects" element={<RequireAuth><SubjectsPage /></RequireAuth>} />
          <Route path="/students" element={<RequireAuth><StudentsPage /></RequireAuth>} />
          <Route path="/students/:id" element={<RequireAuth><StudentDetailPage /></RequireAuth>} />
          <Route path="/guardians" element={<RequireAuth><GuardiansPage /></RequireAuth>} />
          <Route path="/fees/*" element={<RequireAuth><FeesPage /></RequireAuth>} />
          <Route path="*" element={<Navigate to="/students" replace />} />
        </Routes>
      </Suspense>
    </BrowserRouter>
  )
}
