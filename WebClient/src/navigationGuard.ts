let guard: (() => boolean) | undefined

export function registerWorkspaceGuard(callback: () => boolean) {
  guard = callback
  return () => { if (guard === callback) guard = undefined }
}

export function canLeaveWorkspace() {
  return guard?.() ?? true
}
