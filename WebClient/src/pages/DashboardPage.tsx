import { useTranslation } from '../useTranslation'
import { useEffect, useState } from 'react'
import { CalendarDays, ClipboardPlus, HeartPulse, Package, Receipt } from 'lucide-react'
import { Link } from 'react-router-dom'
import { type Dashboard, type OperationsToday, getJson } from '../api'
import { clinicDateTime } from '../schedule'
import { formatDate, formatMoney } from '../format'

export function DashboardPage() {
  const { t } = useTranslation()

  const [summary, setSummary] = useState<{ dashboard: Dashboard; operations: OperationsToday } | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    void Promise.all([getJson<Dashboard>('/api/dashboard'), getJson<OperationsToday>('/api/operations/today')])
      .then(([dashboard, operations]) => { if (active) setSummary({ dashboard, operations }) })
      .catch((cause: unknown) => { if (active) setError(cause instanceof Error ? cause.message : 'Today could not be loaded.') })
    return () => { active = false }
  }, [])

  const operations = summary?.operations
  return <>
    <header className="topbar">
      <div><p className="eyebrow">{summary?.dashboard.clinicName ?? t("Clinic workspace")}</p><h1>{t("Today")}</h1>
        {operations && <p>{formatDate(operations.date)} · {operations.timeZoneId}</p>}</div>
      <Link className="primary-action" to="/patients"><ClipboardPlus size={18} />{t("Start consultation")}</Link>
    </header>
    {error && <div className="notice error" role="alert">{t(error)}</div>}
    {!summary && !error && <p role="status">{t("Loading clinic activity...")}</p>}
    {summary && <>
      <section className="metrics" aria-label={t("Today at a glance")}>
        <article><CalendarDays size={20} /><span>{summary.dashboard.todayAppointments}</span><small>{t("Appointments today")}</small></article>
        <article><HeartPulse size={20} /><span>{summary.operations.activeConsultations}</span><small>{t("Care in progress")}</small></article>
        <article><Receipt size={20} /><span>{summary.operations.pendingAccounts}</span><small><Link to="/checkout">{t("Pending accounts")}</Link></small></article>
        <article><Package size={20} /><span>{summary.operations.lowStockProducts}</span><small><Link to="/inventory">{t("Low-stock products")}</Link></small></article>
      </section>
      <section className="content-grid">
        <section className="panel">
          <div className="panel-heading"><div><p className="eyebrow">{t("Clinical workspace")}</p><h2>{t("Care in progress")}</h2></div><HeartPulse size={22} /></div>
          {summary.operations.consultations.length === 0 && <p className="empty-state">{t("No consultations are in progress.")}</p>}
          <div className="history-list">{summary.operations.consultations.map(visit => <Link key={visit.id} to={`/consultations/${visit.id}`}>
            <span>{clinicDateTime(visit.startedAt, summary.operations.timeZoneId).date}</span>
            <strong>{visit.patientName}</strong><small>{visit.guardianName} · {visit.clinicianName}</small>
          </Link>)}</div>
        </section>
        <section className="panel">
          <div className="panel-heading"><div><p className="eyebrow">{t("Collection")}</p><h2>{t("Outstanding balances")}</h2></div><Receipt size={22} /></div>
          {summary.operations.balancesByCurrency.length === 0 && <p className="empty-state">{t("No pending balances.")}</p>}
          <dl className="record-details">{summary.operations.balancesByCurrency.map(balance => <div key={balance.currency}>
            <dt>{balance.currency}</dt><dd>{formatMoney(balance.outstandingBalance, balance.currency)}</dd>
          </div>)}</dl>
          <Link className="secondary-action" to="/checkout">{t("Review pending accounts")}</Link>
        </section>
      </section>
      <section className="panel appointments">
        <div className="panel-heading"><div><p className="eyebrow">{t("Schedule")}</p><h2>{t("Today and tomorrow")}</h2></div><Link to="/schedule">{t("Open schedule")}</Link></div>
        {summary.dashboard.upcomingAppointments.length === 0 && <p className="empty-state">{t("No upcoming appointments.")}</p>}
        <div className="appointment-list">{summary.dashboard.upcomingAppointments.map(appointment => {
          const local = clinicDateTime(appointment.startsAt, summary.operations.timeZoneId)
          return <Link key={appointment.id} className="appointment" to={`/appointments?appointmentId=${appointment.id}&month=${local.date.slice(0, 7)}`}>
            <time>{local.date} · {local.time}</time>
            <div><strong>{appointment.patientName}</strong><span>{t(appointment.reason)}</span></div>
            <small>{appointment.clinicianName}</small>
          </Link>
        })}</div>
      </section>
    </>}
  </>
}
