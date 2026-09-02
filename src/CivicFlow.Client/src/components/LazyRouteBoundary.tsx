import { Component, Suspense, type ErrorInfo, type ReactNode } from 'react'
import { ErrorState, PageLoading } from './ui'

class RouteErrorBoundary extends Component<{ children: ReactNode }, { error?: Error }> {
  state: { error?: Error } = {}
  static getDerivedStateFromError(error: Error) { return { error } }
  componentDidCatch(error: Error, info: ErrorInfo) { console.error('Unable to load route', error, info) }
  render() { return this.state.error ? <ErrorState title="This page could not be loaded" message="Refresh the page to try again." retry={() => window.location.reload()} /> : this.props.children }
}

export function LazyRouteBoundary({ children }: { children: ReactNode }) {
  return <RouteErrorBoundary><Suspense fallback={<PageLoading />}>{children}</Suspense></RouteErrorBoundary>
}
