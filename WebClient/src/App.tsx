import { NavLink, Outlet } from 'react-router-dom'
import {
  CalendarDays,
  FileText,
  LayoutDashboard,
  PawPrint,
  ShoppingBag,
} from 'lucide-react'
import './App.css'

function App() {
  return (
    <main className="application-shell">
      <aside className="sidebar">
        <div className="brand"><PawPrint size={22} /> Alito's <span>Vet</span></div>
        <nav aria-label="Primary navigation">
          <NavLink end to="/"><LayoutDashboard size={18} /> Dashboard</NavLink>
          <NavLink to="/patients"><PawPrint size={18} /> Patients</NavLink>
          <NavLink to="/schedule"><CalendarDays size={18} /> Schedule</NavLink>
          <NavLink to="/documents"><FileText size={18} /> Documents</NavLink>
          <NavLink to="/point-of-sale"><ShoppingBag size={18} /> Point of sale</NavLink>
        </nav>
        <div className="sidebar-footer"><span className="avatar">AL</span><div><strong>Alondra Licona</strong><small>MVZ. Dipl.</small></div></div>
      </aside>
      <section className="workspace"><Outlet /></section>
    </main>
  )
}

export default App
