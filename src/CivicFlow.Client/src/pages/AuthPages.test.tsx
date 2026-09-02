import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { ThemeProvider } from '@mui/material'
import { describe, expect, it } from 'vitest'
import { AuthProvider } from '../auth/AuthContext'
import { civicTheme } from '../theme'
import { LoginPage, RegisterPage } from './AuthPages'

const renderPage = (page: React.ReactNode) => render(<ThemeProvider theme={civicTheme}><MemoryRouter><AuthProvider>{page}</AuthProvider></MemoryRouter></ThemeProvider>)

describe('authentication form metadata', () => {
  it('uses email and current-password autocomplete on login', () => { const { container } = renderPage(<LoginPage />); expect(screen.getByRole('textbox', { name: /email/i })).toHaveAttribute('autocomplete', 'email'); expect(container.querySelector('input[autocomplete="current-password"]')).toBeVisible() })
  it('uses email and new-password autocomplete and shows password rules on registration', () => { const { container } = renderPage(<RegisterPage />); expect(screen.getByRole('textbox', { name: /email/i })).toHaveAttribute('autocomplete', 'email'); expect(container.querySelector('input[autocomplete="new-password"]')).toBeVisible(); expect(screen.getByText('At least 10 characters')).toBeVisible() })
})
