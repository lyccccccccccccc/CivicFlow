import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { ThemeProvider } from '@mui/material'
import { describe, expect, it } from 'vitest'
import axe from 'axe-core'
import { AuthProvider } from '../auth/AuthContext'
import { civicTheme } from '../theme'
import { LoginPage, RegisterPage } from './AuthPages'
import { developmentDemoEmails } from '../config/demoAccounts'

const renderPage = (page: React.ReactNode) => render(<ThemeProvider theme={civicTheme}><MemoryRouter><AuthProvider>{page}</AuthProvider></MemoryRouter></ThemeProvider>)

describe('authentication form metadata', () => {
  it('uses email and current-password autocomplete on login', () => { const { container } = renderPage(<LoginPage />); expect(screen.getByRole('textbox', { name: /email/i })).toHaveAttribute('autocomplete', 'email'); expect(container.querySelector('input[autocomplete="current-password"]')).toBeVisible() })
  it('uses email and new-password autocomplete and shows password rules on registration', () => { const { container } = renderPage(<RegisterPage />); expect(screen.getByRole('textbox', { name: /email/i })).toHaveAttribute('autocomplete', 'email'); expect(container.querySelector('input[autocomplete="new-password"]')).toBeVisible(); expect(screen.getByText('At least 10 characters')).toBeVisible() })
  it('matches the default development seed domain without exposing a password', () => expect(developmentDemoEmails()).toEqual(['resident@civicflow.local', 'officer@civicflow.local', 'manager@civicflow.local', 'admin@civicflow.local']))

  it('supports an explicit non-sensitive development domain override', () => expect(developmentDemoEmails('example.com')).toEqual(['resident@example.com', 'officer@example.com', 'manager@example.com', 'admin@example.com']))

  it.each([['login', <LoginPage />], ['registration', <RegisterPage />]])('has no serious or critical axe violations on the %s page', async (_, page) => {
    const { container } = renderPage(page)
    const results = await axe.run(container)
    expect(results.violations.filter(result => ['serious', 'critical'].includes(result.impact ?? ''))).toEqual([])
  })
})
