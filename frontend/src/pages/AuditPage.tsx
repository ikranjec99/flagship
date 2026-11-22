import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { AuditEvent } from '../lib/types'
import { currentSelection, useAppStore } from '../store/appStore'

export function AuditPage() {
  const store = useAppStore()
  const { project, environment } = currentSelection(store.projects, store.projectId, store.environmentId)
  const [events, setEvents] = useState<AuditEvent[]>([])

  useEffect(() => {
    if (!project || !environment) return
    void api<AuditEvent[]>(`/api/projects/${project.id}/audit?environmentId=${environment.id}`).then(setEvents)
  }, [project?.id, environment?.id])

  return <><div className="page-heading"><div><span className="eyebrow">{environment?.name.toUpperCase()}</span><h1>Audit log</h1><p>A durable history of changes to this environment.</p></div></div><section className="card audit-card">{events.length === 0 ? <div className="empty-state"><h3>No environment changes yet</h3><p>Flag configuration and API key changes will appear here.</p></div> : <div className="audit-list">{events.map((event) => <article key={event.id}><span className="audit-icon">{event.action === 'created' ? '+' : '↻'}</span><div><h3>{event.summary}</h3><p>{event.actor} · {new Date(event.occurredAt).toLocaleString()}</p>{event.note && <blockquote>{event.note}</blockquote>}</div><code>{event.entityType}</code></article>)}</div>}</section></>
}
