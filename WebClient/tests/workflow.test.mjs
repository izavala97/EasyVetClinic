import test from 'node:test'
import assert from 'node:assert/strict'
import { clinicDateTime, appointmentReasons } from '../src/schedule.ts'
import { canLeaveWorkspace, registerWorkspaceGuard } from '../src/navigationGuard.ts'

test('clinic dates are independent of browser time zone', () => {
  assert.deepEqual(clinicDateTime('2026-10-08T15:00:00Z', 'America/Mexico_City'), { date: '2026-10-08', time: '09:00' })
})

test('clinic-local midnight is never displayed as 24:00', () => {
  assert.deepEqual(clinicDateTime('2026-10-08T06:00:00Z', 'America/Mexico_City'), { date: '2026-10-08', time: '00:00' })
})

test('clinic-local dates cross UTC day boundaries correctly', () => {
  assert.deepEqual(clinicDateTime('2026-10-08T01:00:00Z', 'America/Mexico_City'), { date: '2026-10-07', time: '19:00' })
})

test('appointment reasons have one shared unique vocabulary', () => {
  assert.equal(new Set(appointmentReasons).size, appointmentReasons.length)
  assert.ok(appointmentReasons.includes('General consultation'))
})

test('workspace changes respect and release the clinical navigation guard', () => {
  assert.equal(canLeaveWorkspace(), true)
  const dispose = registerWorkspaceGuard(() => false)
  assert.equal(canLeaveWorkspace(), false)
  dispose()
  assert.equal(canLeaveWorkspace(), true)
})

test('an old unmount cannot remove the current workspace guard', () => {
  const old = registerWorkspaceGuard(() => true)
  const current = registerWorkspaceGuard(() => false)
  old()
  assert.equal(canLeaveWorkspace(), false)
  current()
  assert.equal(canLeaveWorkspace(), true)
})
