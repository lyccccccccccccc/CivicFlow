export interface SystemStatus {
  application: string
  phase: string
  status: string
  timestampUtc: string
}

export async function getSystemStatus(signal?: AbortSignal): Promise<SystemStatus> {
  const response = await fetch('/api/system/status', { signal })

  if (!response.ok) {
    throw new Error(`System status request failed with ${response.status}`)
  }

  return response.json() as Promise<SystemStatus>
}
