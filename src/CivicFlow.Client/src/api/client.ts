const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5168/api'

export type User = { id: string; email: string; firstName: string; lastName: string; roles: string[] }
export type AuthResponse = { accessToken: string; refreshToken: string; expiresAt: string; user: User }
export type Category = { id: string; name: string; description: string; firstResponseHours: number; resolutionHours: number; isActive: boolean }
export type Officer = { id: string; firstName: string; lastName: string; email?: string }
export type CaseItem = { id: string; referenceNumber: string; title: string; description: string; address: string; serviceCategoryId: string; categoryName: string; status: string; priority: string; assignedOfficerId?: string; assignedOfficerName?: string; submittedAtUtc: string; firstResponseDueAtUtc?: string; resolutionDueAtUtc?: string; updatedAtUtc?: string; slaState: string }
export type CaseDetail = { case: CaseItem; category: { id: string; name: string; firstResponseHours: number; resolutionHours: number }; assignedOfficer?: { id: string; name: string; email: string }; activities: Activity[] }
export type Activity = { id: string; type: string; message: string; isPublic: boolean; createdAtUtc: string; actorId: string; actorName?: string }
export type PagedResponse<T> = { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }
export type ChartRow = { label: string; count: number }
export type DashboardData = { open: number; unassigned: number; atRisk: number; overdue: number; waitingForResident: number; resolved: number; byStatus: ChartRow[]; byPriority: ChartRow[]; byCategory: ChartRow[]; officerWorkload: ChartRow[]; slaCases: Pick<CaseItem, 'id' | 'referenceNumber' | 'title' | 'priority' | 'status' | 'resolutionDueAtUtc' | 'categoryName' | 'slaState'>[] }

function savedAuth(): AuthResponse | null {
  try { return JSON.parse(localStorage.getItem('civicflow.auth') ?? 'null') as AuthResponse | null } catch { return null }
}

export async function api<T>(path: string, options: RequestInit = {}): Promise<T> {
  const auth = savedAuth()
  const response = await fetch(`${API_URL}${path}`, {
    ...options,
    headers: { 'Content-Type': 'application/json', ...(auth ? { Authorization: `Bearer ${auth.accessToken}` } : {}), ...options.headers },
  })
  if (!response.ok) {
    const body = await response.json().catch(() => ({})) as { message?: string; title?: string }
    throw new Error(body.message ?? body.title ?? `Request failed (${response.status})`)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
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
