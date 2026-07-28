import { useEffect, useState } from 'react'
import { CalendarDays, ClipboardPlus, HeartPulse, PawPrint } from 'lucide-react'
import { Link } from 'react-router-dom'
import { type Dashboard, getJson } from '../api'

export function DashboardPage() {
  const [dashboard, setDashboard] = useState<Dashboard | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    void getJson<Dashboard>('/api/dashboard').then(setDashboard).catch(() => {
      setError('The dashboard could not be loaded.')
    })
  }, [])

  return (
    <>
      <header className="topbar">
        <div><p className="eyebrow">{dashboard?.clinicName ?? 'Loading clinic...'}</p><h1>Good morning, Alondra</h1></div>
        <Link className="primary-action" to="/patients"><ClipboardPlus size={18} /> Start consultation</Link>
      </header>

      {error && <div className="notice error" role="alert">{error}</div>}

      <section className="metrics" aria-label="Today at a glance">
        <article><CalendarDays size={20} /><span>{dashboard?.todayAppointments ?? '-'}</span><small>Appointments today</small></article>
        <article><HeartPulse size={20} /><span>{dashboard?.activePatients ?? '-'}</span><small>Active patients</small></article>
        <article><PawPrint size={20} /><span>{dashboard?.boardingGuests ?? '-'}</span><small>Boarding guests</small></article>
      </section>

      <section className="panel appointments">
        <div className="panel-heading"><div><p className="eyebrow">Schedule</p><h2>Upcoming appointments</h2></div><CalendarDays size={22} /></div>
        <div className="appointment-list">
          {dashboard?.upcomingAppointments.map((appointment) => (
            <article key={`${appointment.time}-${appointment.patientName}`} className="appointment">
              <time>{appointment.time}</time>
              <div><strong>{appointment.patientName}</strong><span>{appointment.reason}</span></div>
              <small>{appointment.clinicianName}</small>
            </article>
          ))}
        </div>
      </section>
    </>
  )
}