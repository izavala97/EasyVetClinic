import { Navigate, NavLink, Outlet, useLocation } from 'react-router-dom'
import {
  CalendarDays,
  ClipboardList,
  Settings,
  FileText,
  LayoutDashboard,
  PawPrint,
  ShoppingBag,
  LogIn,
  LogOut,
} from 'lucide-react'
import { useAuthentication } from './auth'
import './App.css'

function App() {
  const location = useLocation()
  const { configured, currentUser, error, isLoading, signIn, signOut } = useAuthentication()

  const clinicName = currentUser?.clinicName ?? 'EasyVetClinic'
  const displayName = currentUser?.displayName ?? 'Sign-in required'

  if (!configured) return <main className="authentication-shell"><section className="panel authentication-panel"><h1>Sign-in setup required</h1><p>External ID configuration has not been supplied for this environment.</p></section></main>
  if (isLoading) return <main className="authentication-shell"><p className="loading-state">Verifying your clinic account...</p></main>
  if (!currentUser) return <main className="authentication-shell"><section className="panel authentication-panel"><p className="eyebrow">EasyVetClinic</p><h1>Sign in to continue</h1><p>{error ?? 'Use your clinic account to access the workspace.'}</p><button className="primary-action" type="button" onClick={() => void signIn()}><LogIn size={18} /> Sign in</button></section></main>
  if (!currentUser.clinicId && location.pathname !== '/onboarding') return <Navigate to="/onboarding" replace />
  if (currentUser.clinicId && location.pathname === '/onboarding') return <Navigate to="/" replace />

  return (
    <main className="application-shell">
      <aside className="sidebar">
        <div className="brand"><PawPrint size={22} /> {clinicName}</div>
        <nav aria-label="Primary navigation">
          <NavLink end to="/"><LayoutDashboard size={18} /> Dashboard</NavLink>
          <NavLink to="/patients"><PawPrint size={18} /> Patients</NavLink>
          <NavLink to="/consultations"><ClipboardList size={18} /> Consultations</NavLink>
          <NavLink to="/schedule"><CalendarDays size={18} /> Schedule</NavLink>
          <NavLink to="/documents"><FileText size={18} /> Documents</NavLink>
          <NavLink to="/point-of-sale"><ShoppingBag size={18} /> Point of sale</NavLink>
          <NavLink to="/clinic-profile"><Settings size={18} /> Clinic profile</NavLink>
        </nav>
        <div className="sidebar-footer"><span className="avatar">{displayName.slice(0, 2).toUpperCase()}</span><div><strong>{displayName}</strong><small>{currentUser?.role ?? 'Authenticate to continue'}</small></div><button className="icon-button" type="button" onClick={() => void signOut()} aria-label="Sign out"><LogOut size={17} /></button></div>
      </aside>
      <section className="workspace"><Outlet /></section>
    </main>
  )
}

export default App
