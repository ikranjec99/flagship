import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { api } from '../lib/api'
import type { FlagListItem } from '../lib/types'
import { currentSelection, useAppStore } from '../store/appStore'

export function FlagsPage() {
  const store = useAppStore()
  const { project, environment } = currentSelection(store.projects, store.projectId, store.environmentId)
  const [flags, setFlags] = useState<FlagListItem[]>([])
  const [search, setSearch] = useState('')
  const [showCreate, setShowCreate] = useState(false)
  const [includeArchived, setIncludeArchived] = useState(false)
  const [loading, setLoading] = useState(true)

  async function load() {
    if (!project || !environment) return
    setLoading(true)
    try {
      setFlags(await api<FlagListItem[]>(`/api/projects/${project.id}/flags?environmentId=${environment.id}&includeArchived=${includeArchived}`))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { void load() }, [project?.id, environment?.id, includeArchived])

  const filtered = useMemo(() => {
    const query = search.trim().toLowerCase()
    return query ? flags.filter((flag) => flag.name.toLowerCase().includes(query) || flag.key.includes(query)) : flags
  }, [flags, search])

  return (
    <>
      <div className="page-heading">
        <div><span className="eyebrow">{environment?.name.toUpperCase()}</span><h1>Feature flags</h1><p>Control releases independently from deployments.</p></div>
        <button className="primary-button" onClick={() => setShowCreate(true)}>+ Create flag</button>
      </div>
      <section className="card table-card">
        <div className="table-toolbar">
          <input className="search-input" placeholder="Search by name or key…" value={search} onChange={(event) => setSearch(event.target.value)} />
          <div className="toolbar-options"><label><input type="checkbox" checked={includeArchived} onChange={(event) => setIncludeArchived(event.target.checked)} /> Show archived</label><span className="muted">{filtered.length} {filtered.length === 1 ? 'flag' : 'flags'}</span></div>
        </div>
        {loading ? <div className="empty-state">Loading flags…</div> : filtered.length === 0 ? (
          <div className="empty-state"><div className="empty-icon">⚑</div><h3>{flags.length ? 'No matching flags' : 'Create your first feature flag'}</h3><p>{flags.length ? 'Try another search.' : 'Deploy code safely, then decide when and who receives it.'}</p>{!flags.length && <button className="secondary-button" onClick={() => setShowCreate(true)}>Create flag</button>}</div>
        ) : (
          <div className="data-table">
            <div className="table-row table-head"><span>Flag</span><span>Status</span><span>Serving</span><span>Rules</span><span>Updated</span></div>
            {filtered.map((flag) => (
              <button className="table-row" key={flag.id} onClick={() => store.openFlag(flag.id)}>
                <span className="flag-cell"><strong>{flag.name}</strong><code>{flag.key}</code></span>
                <span><span className={`status-pill ${flag.enabled && !flag.isArchived ? 'on' : 'off'}`}><i />{flag.isArchived ? 'Archived' : flag.enabled ? 'Active' : 'Inactive'}</span></span>
                <span>{servingText(flag)}</span>
                <span>{flag.ruleCount}</span>
                <span className="muted">{new Date(flag.updatedAt).toLocaleDateString()}</span>
              </button>
            ))}
          </div>
        )}
      </section>
      {showCreate && project && <CreateFlagDialog projectId={project.id} onClose={() => setShowCreate(false)} onCreated={async () => { setShowCreate(false); await load() }} />}
    </>
  )
}

function servingText(flag: FlagListItem) {
  if (flag.isArchived) return 'Not evaluated'
  if (!flag.enabled) return 'Always off'
  if (flag.rolloutPercentage !== null) return `${flag.rolloutPercentage}% on`
  return flag.defaultValue ? 'On by default' : 'Off by default'
}

function CreateFlagDialog({ projectId, onClose, onCreated }: { projectId: string; onClose: () => void; onCreated: () => Promise<void> }) {
  const [name, setName] = useState('')
  const [key, setKey] = useState('')
  const [description, setDescription] = useState('')
  const [keyTouched, setKeyTouched] = useState(false)
  const [error, setError] = useState('')

  function updateName(value: string) {
    setName(value)
    if (!keyTouched) setKey(value.toLowerCase().trim().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, ''))
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError('')
    try {
      await api(`/api/projects/${projectId}/flags`, {
        method: 'POST',
        body: JSON.stringify({ name, key, description }),
      })
      await onCreated()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not create the flag.')
    }
  }

  return (
    <div className="dialog-backdrop" onMouseDown={onClose}>
      <form className="dialog" onSubmit={(event) => void submit(event)} onMouseDown={(event) => event.stopPropagation()}>
        <div className="dialog-header"><div><h2>Create feature flag</h2><p>Flags start inactive in every environment.</p></div><button type="button" className="icon-button" onClick={onClose}>×</button></div>
        <label>Name<input value={name} onChange={(event) => updateName(event.target.value)} autoFocus required placeholder="New checkout" /></label>
        <label>Key<input className="mono-input" value={key} onChange={(event) => { setKeyTouched(true); setKey(event.target.value) }} required placeholder="new-checkout" /><small>Used in application code and immutable after creation.</small></label>
        <label>Description <span className="optional">optional</span><textarea value={description} onChange={(event) => setDescription(event.target.value)} rows={3} /></label>
        {error && <div className="form-error">{error}</div>}
        <div className="dialog-actions"><button type="button" className="secondary-button" onClick={onClose}>Cancel</button><button className="primary-button">Create flag</button></div>
      </form>
    </div>
  )
}
