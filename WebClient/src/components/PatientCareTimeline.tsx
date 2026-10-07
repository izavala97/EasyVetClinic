import { useEffect, useState, type FormEvent } from 'react'
import { apiFetch, ensureSuccess, getJson, type WeightRecord, type VaccinationRecord } from '../api'
import { useAuthentication } from '../auth'

export function PatientCareTimeline({ patientId, onWeightSaved }: { patientId: string; onWeightSaved: () => void }) {
  const { currentUser } = useAuthentication()
  const canRecord = ['ClinicAdmin', 'Veterinarian', 'SuperAdmin'].includes(currentUser?.role ?? '')
  const [weights, setWeights] = useState<WeightRecord[]>([])
  const [vaccines, setVaccines] = useState<VaccinationRecord[]>([])
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)
  const today = new Date().toLocaleDateString('en-CA')

  useEffect(() => {
    let active = true
    void Promise.all([getJson<WeightRecord[]>(`/api/patients/${patientId}/weights`),
      getJson<VaccinationRecord[]>(`/api/patients/${patientId}/vaccinations`)])
      .then(([weightRecords, vaccineRecords]) => { if (active) { setWeights(weightRecords); setVaccines(vaccineRecords) } })
      .catch((cause: unknown) => { if (active) setError(cause instanceof Error ? cause.message : 'Care history could not be loaded.') })
    return () => { active = false }
  }, [patientId])

  async function record(event: FormEvent<HTMLFormElement>, kind: 'weights' | 'vaccinations') {
    event.preventDefault()
    if (pending) return
    const form = event.currentTarget
    const fields = new FormData(form)
    const payload = kind === 'weights' ? { value: Number(fields.get('value')), unit: fields.get('unit'),
      measuredOn: fields.get('date'), recordedBy: currentUser?.displayName } :
      { vaccineName: fields.get('vaccineName'), administeredOn: fields.get('date'), nextDueOn: fields.get('nextDueOn') || null,
        lotNumber: fields.get('lotNumber'), veterinarianName: currentUser?.displayName }
    setPending(true); setError('')
    try {
      await ensureSuccess(await apiFetch(`/api/patients/${patientId}/${kind}`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload),
      }))
      if (kind === 'weights') { setWeights(await getJson<WeightRecord[]>(`/api/patients/${patientId}/weights`)); onWeightSaved() }
      else setVaccines(await getJson<VaccinationRecord[]>(`/api/patients/${patientId}/vaccinations`))
      form.reset()
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'The care record could not be saved.') }
    finally { setPending(false) }
  }

  return <section className="content-grid care-timeline">
    {error && <div className="notice error" role="alert">{error}</div>}
    <section className="panel"><h2>Weight history</h2>
      {weights.map(item => <p key={item.id}>{item.measuredOn}: <strong>{item.value} {item.unit}</strong> · {item.recordedBy}</p>)}
      {!weights.length && <p>No structured weight measurements recorded.</p>}
      {canRecord && <form className="clinical-entry-form" onSubmit={event => void record(event, 'weights')}>
        <label>Weight<input name="value" type="number" min="0.001" step="0.001" required /></label>
        <label>Unit<select name="unit"><option>kg</option><option>lb</option></select></label>
        <label>Measured on<input name="date" type="date" defaultValue={today} max={today} required /></label>
        <button className="secondary-action" type="submit" disabled={pending}>Record weight</button>
      </form>}
    </section>
    <section className="panel"><h2>Vaccinations</h2>
      {vaccines.map(item => <p key={item.id}><strong>{item.vaccineName}</strong> · {item.administeredOn} · Lot {item.lotNumber || 'not recorded'}
        {item.nextDueOn && ` · Next due ${item.nextDueOn}`}</p>)}
      {!vaccines.length && <p>No vaccinations recorded.</p>}
      {canRecord && <form className="clinical-entry-form" onSubmit={event => void record(event, 'vaccinations')}>
        <label>Vaccine<input name="vaccineName" required /></label>
        <label>Administered on<input name="date" type="date" defaultValue={today} max={today} required /></label>
        <label>Next due<input name="nextDueOn" type="date" /></label>
        <label>Lot number<input name="lotNumber" /></label>
        <p>Recording a vaccination does not dispense stock or add a charge.</p>
        <button className="secondary-action" type="submit" disabled={pending}>Record vaccination</button>
      </form>}
    </section>
  </section>
}
