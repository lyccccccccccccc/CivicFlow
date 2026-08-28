const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5168/api'

export type User = { id: string; email: string; firstName: string; lastName: string; roles: string[] }
export type AuthResponse = { accessToken: string; refreshToken: string; expiresAt: string; user: User }
export type Category = { id: string; name: string; description: string; firstResponseHours: number; resolutionHours: number }
export type CaseItem = { id: string; referenceNumber: string; title: string; status: string; priority: string; submittedAtUtc: string; resolutionDueAtUtc?: string; assignedOfficerId?: string }
export type CaseDetail = { case: CaseItem & { description: string; address: string; serviceCategoryId: string; residentId: string }; category: { id: string; name: string }; activities: Activity[] }
export type Activity = { id: string; type: string; message: string; isPublic: boolean; createdAtUtc: string; actorId: string }
export type DashboardData = { open: number; overdue: number; resolvedLast30Days: number; unassigned: number; byStatus: { status: string; count: number }[] }

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

export const authApi = {
  login: (email: string, password: string) => api<AuthResponse>('/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),
  register: (input: { email: string; password: string; firstName: string; lastName: string }) => api<AuthResponse>('/auth/register', { method: 'POST', body: JSON.stringify(input) }),
}
