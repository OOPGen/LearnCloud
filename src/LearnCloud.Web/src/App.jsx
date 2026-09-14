import { Suspense, lazy } from 'react'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import RequireAuth from './lib/RequireAuth'

const Home = lazy(() => import('./pages/Home'))
const LoginSkewed = lazy(() => import('./pages/LoginSkewed'))
const Dashboard = lazy(() => import('./pages/Dashboard'))
const SubjectsPage = lazy(() => import('./pages/SubjectsPage'))
const FeesPage = lazy(() => import('./pages/FeesPage'))

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
          {/* Signed-in area. Only Subjects is connected to the API so far; Dashboard and
              Fees are still static previews. Links to modules without a page (students,
              grades, ...) used to render the Subjects mockup and now redirect instead. */}
          <Route path="/dashboard" element={<RequireAuth><Dashboard /></RequireAuth>} />
          <Route path="/analytics" element={<RequireAuth><Dashboard /></RequireAuth>} />
          <Route path="/subjects" element={<RequireAuth><SubjectsPage /></RequireAuth>} />
          <Route path="/fees/*" element={<RequireAuth><FeesPage /></RequireAuth>} />
          <Route path="*" element={<Navigate to="/subjects" replace />} />
        </Routes>
      </Suspense>
    </BrowserRouter>
  )
}
