import { Navigate, NavLink, Outlet, useLocation } from 'react-router-dom'
import {
  CalendarDays,
  ClipboardList,
  Settings,
  FileText,
  LayoutDashboard,
  PawPrint,
  ShoppingBag,
  Package,
  Boxes,
  LogIn,
  LogOut,
  Shield,
  Users,
} from 'lucide-react'
import { useAuthentication } from './auth'
import './App.css'

function App() {
  const location = useLocation()
  const { configured, currentUser, error, isLoading, signIn, signOut, selectClinic } = useAuthentication()

  const clinicName = currentUser?.clinicName ?? 'EasyVetClinic'
  const displayName = currentUser?.displayName ?? 'Sign-in required'

  if (!configured) return <main className="authentication-shell"><section className="panel authentication-panel"><h1>Sign-in setup required</h1><p>External ID configuration has not been supplied for this environment.</p></section></main>
  if (isLoading) return <main className="authentication-shell"><p className="loading-state">Verifying your clinic account...</p></main>
  if (!currentUser) return <main className="authentication-shell"><section className="panel authentication-panel"><p className="eyebrow">EasyVetClinic</p><h1>Sign in to continue</h1><p>{error ?? 'Use your clinic account to access the workspace.'}</p><button className="primary-action" type="button" onClick={() => void signIn()}><LogIn size={18} /> Sign in</button></section></main>
  if (sessionStorage.getItem('clinicInvitation') && location.pathname !== '/invite') return <Navigate to="/invite" replace />
  const platformPage = location.pathname === '/platform' || location.pathname === '/onboarding'
  const clinicAdmin = currentUser.role === 'ClinicAdmin' || currentUser.isPlatformOwner
  if (platformPage && !currentUser.isPlatformOwner) return <Navigate to="/" replace />
  if (location.pathname === '/clinic-admin' && !clinicAdmin) return <Navigate to="/" replace />
  if (!currentUser.clinicId && currentUser.isPlatformOwner && !platformPage && location.pathname !== '/invite') return <Navigate to="/platform" replace />
  const blocked = !currentUser.clinicId || (currentUser.isSuspended && !currentUser.isPlatformOwner)

  return (
    <main className="application-shell">
      <aside className="sidebar">
        <div className="brand"><PawPrint size={22} /> {clinicName}</div>
        {currentUser.clinics.length > 0 && <label className="clinic-selector">Clinic workspace<select aria-label="Clinic workspace" value={currentUser.clinicId ?? ''} onChange={(event) => void selectClinic(event.target.value || null)}><option value="">Select a clinic</option>{currentUser.clinics.map(clinic => <option key={clinic.clinicId} value={clinic.clinicId}>{clinic.clinicName}{clinic.isSuspended ? ' (suspended)' : ''}</option>)}</select></label>}
        <nav aria-label="Primary navigation">
          {!blocked && <>
          <NavLink end to="/"><LayoutDashboard size={18} /> Today</NavLink>
          <NavLink to="/patients"><PawPrint size={18} /> Patients</NavLink>
          <NavLink to="/consultations"><ClipboardList size={18} /> Consultations</NavLink>
          <NavLink to="/schedule"><CalendarDays size={18} /> Schedule</NavLink>
          <NavLink to="/documents"><FileText size={18} /> Documents</NavLink>
          <NavLink to="/checkout"><ShoppingBag size={18} /> Checkout</NavLink>
          <NavLink to="/inventory"><Boxes size={18} /> Inventory</NavLink>
          <NavLink to="/catalog"><Package size={18} /> Catalog</NavLink>
          <NavLink to="/clinic-profile"><Settings size={18} /> Clinic profile</NavLink>
          {clinicAdmin && <NavLink to="/clinic-admin"><Users size={18} /> Clinic administration</NavLink>}
          </>}
          {currentUser.isPlatformOwner && <NavLink to="/platform"><Shield size={18} /> Platform administration</NavLink>}
        </nav>
        <details className="account-details"><summary>Account details</summary><p>Entra object ID</p><code>{currentUser.objectId}</code></details>
        <div className="sidebar-footer"><span className="avatar">{displayName.slice(0, 2).toUpperCase()}</span><div><strong>{displayName}</strong><small>{currentUser.isPlatformOwner ? 'Platform owner' : currentUser.role ?? 'No clinic assigned'}</small></div><button className="icon-button" type="button" onClick={() => void signOut()} aria-label="Sign out"><LogOut size={17} /></button></div>
      </aside>
      <section className="workspace" key={currentUser.clinicId ?? 'platform'}>
        {currentUser.isPlatformOwner && currentUser.clinicId && <div className="notice">Platform support workspace: {clinicName}{currentUser.isSuspended ? ' (suspended for clinic users)' : ''}.</div>}
        {blocked && !platformPage && location.pathname !== '/invite'
          ? <section className="panel"><h1>{currentUser.isSuspended ? 'Clinic access suspended' : 'Clinic access required'}</h1><p>{currentUser.isSuspended ? 'Contact the platform owner to restore your clinic subscription access.' : 'Select an assigned clinic above, or ask your administrator for an invitation. Only the platform owner can register a clinic.'}</p><p>Your account ID: <code>{currentUser.objectId}</code></p></section>
          : <Outlet />}
      </section>
    </main>
  )
}

export default App
