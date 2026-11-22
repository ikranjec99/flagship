import type { ReactNode } from 'react'
import { api } from '../lib/api'
import { currentSelection, useAppStore, type View } from '../store/appStore'

const navigation: Array<{ id: View; label: string; marker: string }> = [
  { id: 'overview', label: 'Overview', marker: '⌂' },
  { id: 'flags', label: 'Feature flags', marker: '⚑' },
  { id: 'keys', label: 'API keys', marker: '⌁' },
  { id: 'audit', label: 'Audit log', marker: '≡' },
]

export function AppShell({ children, onSignedOut }: { children: ReactNode; onSignedOut: () => void }) {
  const store = useAppStore()
  const { project, environment } = currentSelection(store.projects, store.projectId, store.environmentId)

  async function signOut() {
    await api<void>('/api/auth/logout', { method: 'POST' })
    onSignedOut()
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand"><span className="brand-mark">F</span><span>Flagship</span></div>

        <label className="field-label" htmlFor="project">Project</label>
        <select
          id="project"
          className="sidebar-select"
          value={project?.id ?? ''}
          onChange={(event) => store.selectProject(event.target.value)}
        >
          {store.projects.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
        </select>

        <nav>
          {navigation.map((item) => (
            <button
              key={item.id}
              className={`nav-item ${store.view === item.id && !store.selectedFlagId ? 'active' : ''}`}
              onClick={() => store.navigate(item.id)}
            >
              <span className="nav-marker">{item.marker}</span>{item.label}
            </button>
          ))}
        </nav>

        <div className="sidebar-footer">
          <a href="/openapi/v1.json" target="_blank" rel="noreferrer">API documentation</a>
          <button className="text-button" onClick={() => void signOut()}>Sign out</button>
        </div>
      </aside>

      <main className="main-area">
        <header className="topbar">
          <div>
            <div className="topbar-project">{project?.name}</div>
            <div className="topbar-caption">Feature management</div>
          </div>
          <div className={`environment-control ${environment?.key === 'production' ? 'production' : ''}`}>
            <span className="environment-dot" />
            <select
              aria-label="Environment"
              value={environment?.id ?? ''}
              onChange={(event) => store.selectEnvironment(event.target.value)}
            >
              {project?.environments.map((item) => (
                <option key={item.id} value={item.id}>{item.name}</option>
              ))}
            </select>
          </div>
        </header>
        <div className="page-container">{children}</div>
      </main>
    </div>
  )
}
