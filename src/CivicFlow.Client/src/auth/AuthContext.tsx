/* eslint-disable react-refresh/only-export-components */
import { createContext, useContext, useMemo, useState, type ReactNode } from 'react'
import { authApi, type AuthResponse, type User } from '../api/client'

type AuthContextValue = {
  user: User | null
  login: (email: string, password: string) => Promise<void>
  register: (input: { email: string; password: string; firstName: string; lastName: string }) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

function readAuth(): AuthResponse | null {
  try { return JSON.parse(localStorage.getItem('civicflow.auth') ?? 'null') as AuthResponse | null } catch { return null }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuth] = useState<AuthResponse | null>(readAuth)
  const save = (next: AuthResponse) => { localStorage.setItem('civicflow.auth', JSON.stringify(next)); setAuth(next) }
  const value = useMemo<AuthContextValue>(() => ({
    user: auth?.user ?? null,
    login: async (email, password) => save(await authApi.login(email, password)),
    register: async (input) => save(await authApi.register(input)),
    logout: () => { localStorage.removeItem('civicflow.auth'); setAuth(null) },
  }), [auth])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside AuthProvider')
  return context
}
