import { useCallback, useEffect, useState } from 'react'
import { ApiError, api } from './lib/api'
import type { Project } from './lib/types'
import { AppShell } from './components/AppShell'
import { LoginPage } from './pages/LoginPage'
import { ProjectSetupPage } from './pages/ProjectSetupPage'
import { OverviewPage } from './pages/OverviewPage'
import { FlagsPage } from './pages/FlagsPage'
import { FlagEditorPage } from './pages/FlagEditorPage'
import { ApiKeysPage } from './pages/ApiKeysPage'
import { AuditPage } from './pages/AuditPage'
import { useAppStore } from './store/appStore'

export function App() {
  const [authState, setAuthState] = useState<'checking' | 'authenticated' | 'anonymous'>('checking')
  const [loadingProjects, setLoadingProjects] = useState(true)
  const { projects, view, selectedFlagId, setProjects } = useAppStore()

  const loadProjects = useCallback(async () => {
    setLoadingProjects(true)
    try {
      setProjects(await api<Project[]>('/api/projects'))
    } finally {
      setLoadingProjects(false)
    }
  }, [setProjects])

  useEffect(() => {
    api<{ email: string }>('/api/auth/me')
      .then(async () => {
        setAuthState('authenticated')
        await loadProjects()
      })
      .catch((error: unknown) => {
        if (error instanceof ApiError && error.status === 401) {
          setAuthState('anonymous')
          setLoadingProjects(false)
          return
        }
        setAuthState('anonymous')
        setLoadingProjects(false)
      })
  }, [loadProjects])

  if (authState === 'checking' || loadingProjects) {
    return <div className="center-screen"><div className="spinner" />Loading Flagship…</div>
  }

  if (authState === 'anonymous') {
    return <LoginPage onAuthenticated={() => {
      setAuthState('authenticated')
      void loadProjects()
    }} />
  }

  if (projects.length === 0) {
    return <ProjectSetupPage onCreated={loadProjects} />
  }

  let content
  if (selectedFlagId) {
    content = <FlagEditorPage flagId={selectedFlagId} />
  } else {
    content = {
      overview: <OverviewPage />,
      flags: <FlagsPage />,
      keys: <ApiKeysPage />,
      audit: <AuditPage />,
    }[view]
  }

  return <AppShell onSignedOut={() => setAuthState('anonymous')}>{content}</AppShell>
}
