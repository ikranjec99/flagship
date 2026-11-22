import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import type { AuditEvent, FlagListItem } from '../lib/types'
import { currentSelection, useAppStore } from '../store/appStore'

export function OverviewPage() {
  const store = useAppStore()
  const { project, environment } = currentSelection(store.projects, store.projectId, store.environmentId)
  const [flags, setFlags] = useState<FlagListItem[]>([])
  const [events, setEvents] = useState<AuditEvent[]>([])

  useEffect(() => {
    if (!project || !environment) return
    void Promise.all([
      api<FlagListItem[]>(`/api/projects/${project.id}/flags?environmentId=${environment.id}`),
      api<AuditEvent[]>(`/api/projects/${project.id}/audit?environmentId=${environment.id}`),
    ]).then(([nextFlags, nextEvents]) => {
      setFlags(nextFlags)
      setEvents(nextEvents)
    })
  }, [project, environment])

  const active = flags.filter((flag) => flag.enabled)
  const rollouts = flags.filter((flag) => flag.rolloutPercentage !== null)

  return (
    <>
      <div className="page-heading">
        <div><span className="eyebrow">{environment?.name.toUpperCase()}</span><h1>Overview</h1><p>What this environment is serving right now.</p></div>
        <button className="primary-button" onClick={() => store.navigate('flags')}>Manage flags</button>
      </div>
      <div className="metric-grid">
        <Metric value={flags.length} label="Total flags" />
        <Metric value={active.length} label="Active flags" tone="green" />
        <Metric value={flags.filter((flag) => flag.ruleCount > 0).length} label="With targeting" />
        <Metric value={rollouts.length} label="Partial rollouts" tone="violet" />
      </div>
      <div className="dashboard-grid">
        <section className="card">
          <div className="card-header"><div><h2>Active rollouts</h2><p>Features currently being released gradually.</p></div></div>
          {rollouts.length === 0 ? <Empty message="No percentage rollouts in this environment." /> : (
            <div className="simple-list">{rollouts.map((flag) => (
              <button key={flag.id} className="simple-list-row" onClick={() => store.openFlag(flag.id)}>
                <div><strong>{flag.name}</strong><code>{flag.key}</code></div>
                <span className="rollout-pill">{flag.rolloutPercentage}% on</span>
              </button>
            ))}</div>
          )}
        </section>
        <section className="card">
          <div className="card-header"><div><h2>Recent changes</h2><p>Latest configuration activity.</p></div></div>
          {events.length === 0 ? <Empty message="No changes recorded in this environment yet." /> : (
            <div className="timeline">{events.slice(0, 6).map((event) => (
              <div className="timeline-item" key={event.id}>
                <span className="timeline-dot" />
                <div><strong>{event.summary}</strong><p>{event.actor} · {relativeTime(event.occurredAt)}</p></div>
              </div>
            ))}</div>
          )}
        </section>
      </div>
    </>
  )
}

function Metric({ value, label, tone = '' }: { value: number; label: string; tone?: string }) {
  return <div className={`metric-card ${tone}`}><strong>{value}</strong><span>{label}</span></div>
}

function Empty({ message }: { message: string }) {
  return <div className="empty-compact">{message}</div>
}

function relativeTime(value: string) {
  const minutes = Math.floor((Date.now() - new Date(value).getTime()) / 60_000)
  if (minutes < 1) return 'just now'
  if (minutes < 60) return `${minutes}m ago`
  if (minutes < 1_440) return `${Math.floor(minutes / 60)}h ago`
  return `${Math.floor(minutes / 1_440)}d ago`
}
