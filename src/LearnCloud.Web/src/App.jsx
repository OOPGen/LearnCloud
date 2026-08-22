import { Suspense, lazy } from 'react'
import { BrowserRouter, Routes, Route } from 'react-router-dom'

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
        <p className="mt-3 text-sm text-neutral-600">Loading LearnCloud... Premium Enterprise Shell</p>
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
          <Route path="/dashboard" element={<Dashboard />} />
          <Route path="/analytics" element={<Dashboard />} />
          <Route path="/subjects" element={<SubjectsPage />} />
          <Route path="/academic-years" element={<SubjectsPage />} />
          <Route path="/grades" element={<SubjectsPage />} />
          <Route path="/fees/*" element={<FeesPage />} />
          <Route path="/fees/structures" element={<FeesPage />} />
          <Route path="/fees/invoices" element={<FeesPage />} />
          <Route path="/fees/payments" element={<FeesPage />} />
          <Route path="/fees/arrears" element={<FeesPage />} />
          <Route path="/attendance" element={<Dashboard />} />
          <Route path="/timetable" element={<Dashboard />} />
          <Route path="/students" element={<SubjectsPage />} />
          <Route path="/guardians" element={<SubjectsPage />} />
          <Route path="/staff" element={<SubjectsPage />} />
          <Route path="/messaging" element={<Dashboard />} />
          <Route path="/library" element={<Dashboard />} />
          <Route path="/reports" element={<Dashboard />} />
          <Route path="/settings" element={<Dashboard />} />
        </Routes>
      </Suspense>
    </BrowserRouter>
  )
}
