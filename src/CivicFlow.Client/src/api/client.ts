const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5168/api'

export type User = { id: string; email: string; firstName: string; lastName: string; roles: string[] }
export type AuthResponse = { accessToken: string; refreshToken: string; expiresAt: string; user: User }
export type Profile = User & { fullName: string; isActive: boolean; version: string }
export type Category = { id: string; name: string; description: string; firstResponseHours: number; resolutionHours: number; isActive: boolean }
export type Officer = { id: string; firstName: string; lastName: string; email?: string }
export type CaseItem = { id: string; referenceNumber: string; title: string; description: string; address: string; latitude?: number; longitude?: number; serviceCategoryId: string; categoryName: string; status: string; priority: string; assignedOfficerId?: string; assignedOfficerName?: string; submittedAtUtc: string; firstResponseDueAtUtc?: string; firstResponseCompletedAtUtc?: string; resolutionDueAtUtc?: string; updatedAtUtc?: string; firstResponseSlaState: string; resolutionSlaState: string; slaState: string; nextSlaDueAtUtc?: string; nextSlaTarget?: string }
export type CaseDetail = { case: CaseItem; category: { id: string; name: string; firstResponseHours: number; resolutionHours: number }; assignedOfficer?: { id: string; name: string; email: string }; activities: Activity[] }
export type Activity = { id: string; type: string; label: string; section: 'conversation' | 'internal' | 'progress'; message: string; isPublic: boolean; createdAtUtc: string; actorName?: string }
export type CaseAttachment = { id: string; originalFileName: string; contentType: string; sizeBytes: number; visibility: 'Public' | 'Internal'; uploadedAtUtc: string; uploadedByUserId: string }
export type PagedResponse<T> = { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }
export type ChartRow = { label: string; count: number }
export type DashboardData = { open: number; unassigned: number; atRisk: number; overdue: number; firstResponseBreached: number; waitingForResident: number; resolved: number; byStatus: ChartRow[]; byPriority: ChartRow[]; byCategory: ChartRow[]; officerWorkload: ChartRow[]; slaCases: Pick<CaseItem, 'id' | 'referenceNumber' | 'title' | 'priority' | 'status' | 'assignedOfficerName' | 'firstResponseDueAtUtc' | 'firstResponseCompletedAtUtc' | 'resolutionDueAtUtc' | 'categoryName' | 'firstResponseSlaState' | 'resolutionSlaState' | 'slaState' | 'nextSlaDueAtUtc' | 'nextSlaTarget'>[] }

function savedAuth(): AuthResponse | null {
  try { return JSON.parse(localStorage.getItem('civicflow.auth') ?? 'null') as AuthResponse | null } catch { return null }
}

export async function api<T>(path: string, options: RequestInit = {}): Promise<T> {
  const auth = savedAuth()
  const headers = new Headers(options.headers)
  if (!(options.body instanceof FormData)) headers.set('Content-Type', 'application/json')
  if (auth) headers.set('Authorization', `Bearer ${auth.accessToken}`)
  const response = await fetch(`${API_URL}${path}`, {
    ...options,
    headers,
  })
  if (!response.ok) {
    const body = await response.json().catch(() => ({})) as { message?: string; detail?: string; title?: string; errors?: Record<string, string[]> }
    const fieldError = body.errors ? Object.values(body.errors).flat()[0] : undefined
    const message = fieldError ?? body.message ?? body.detail ?? body.title ?? `Request failed (${response.status})`
    throw new Error(response.status === 409 ? `${message} Refresh the page and try again.` : message)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export async function uploadAttachment(caseId: string, file: File, visibility: 'Public' | 'Internal', idempotencyKey: string) {
  const form = new FormData(); form.append('file', file); form.append('visibility', visibility)
  return api<CaseAttachment>(`/cases/${caseId}/attachments`, { method: 'POST', headers: { 'Idempotency-Key': idempotencyKey }, body: form })
}

export async function apiDownload(path: string, fileName: string) {
  const auth = savedAuth()
  const response = await fetch(`${API_URL}${path}`, { headers: auth ? { Authorization: `Bearer ${auth.accessToken}` } : {} })
  if (!response.ok) throw new Error(`Download failed (${response.status})`)
  const url = URL.createObjectURL(await response.blob())
  const anchor = document.createElement('a'); anchor.href = url; anchor.download = fileName; anchor.click()
  URL.revokeObjectURL(url)
}

export const authApi = {
  login: (email: string, password: string) => api<AuthResponse>('/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),
  register: (input: { email: string; password: string; firstName: string; lastName: string }) => api<AuthResponse>('/auth/register', { method: 'POST', body: JSON.stringify(input) }),
}

export const profileApi = {
  get: () => api<Profile>('/profile'),
  update: (fullName: string, version: string) => api<Profile>('/profile', { method: 'PUT', body: JSON.stringify({ fullName, version }) }),
  changePassword: (currentPassword: string, newPassword: string) => api<void>('/profile/change-password', { method: 'POST', body: JSON.stringify({ currentPassword, newPassword }) }),
}
