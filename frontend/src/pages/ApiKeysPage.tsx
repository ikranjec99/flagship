import { useEffect, useState, type FormEvent } from 'react'
import { api } from '../lib/api'
import type { ApiKey, CreatedApiKey } from '../lib/types'
import { currentSelection, useAppStore } from '../store/appStore'

export function ApiKeysPage() {
  const store = useAppStore()
  const { environment } = currentSelection(store.projects, store.projectId, store.environmentId)
  const [keys, setKeys] = useState<ApiKey[]>([])
  const [showCreate, setShowCreate] = useState(false)
  const [created, setCreated] = useState<CreatedApiKey | null>(null)

  async function load() {
    if (!environment) return
    setKeys(await api<ApiKey[]>(`/api/environments/${environment.id}/api-keys`))
  }

  useEffect(() => { setCreated(null); void load() }, [environment?.id])

  async function revoke(key: ApiKey) {
    if (!environment || !window.confirm(`Revoke API key “${key.name}”? Applications using it will immediately lose access.`)) return
    await api(`/api/environments/${environment.id}/api-keys/${key.id}`, { method: 'DELETE' })
    await load()
  }

  return (
    <>
      <div className="page-heading"><div><span className="eyebrow">{environment?.name.toUpperCase()}</span><h1>Server API keys</h1><p>Authenticate trusted applications evaluating flags in this environment.</p></div><button className="primary-button" onClick={() => setShowCreate(true)}>+ Create key</button></div>
      {created && <section className="secret-card"><div><span className="eyebrow">COPY THIS NOW</span><h2>Your API key is ready</h2><p>For security, this secret will not be shown again.</p></div><div className="secret-value"><code>{created.secret}</code><button onClick={() => void navigator.clipboard.writeText(created.secret)}>Copy</button></div><button className="text-button" onClick={() => setCreated(null)}>I have stored this key</button></section>}
      <section className="card table-card">
        {keys.length === 0 ? <div className="empty-state"><div className="empty-icon">⌁</div><h3>No API keys for {environment?.name}</h3><p>Create a server key to evaluate flags from an application.</p></div> : <div className="data-table"><div className="table-row key-row table-head"><span>Name</span><span>Prefix</span><span>Created</span><span>Status</span><span /></div>{keys.map((key) => <div className="table-row key-row" key={key.id}><span><strong>{key.name}</strong></span><code>{key.prefix}…</code><span className="muted">{new Date(key.createdAt).toLocaleDateString()}</span><span className={`status-pill ${key.revokedAt ? 'off' : 'on'}`}><i />{key.revokedAt ? 'Revoked' : 'Active'}</span><button className="danger-text-button" disabled={!!key.revokedAt} onClick={() => void revoke(key)}>Revoke</button></div>)}</div>}
      </section>
      <section className="card integration-card"><span className="eyebrow">QUICK START</span><h2>Evaluate a flag</h2><pre><code>{`curl -X POST ${window.location.origin}/api/evaluate/v1/flags/new-checkout \\\n  -H "Authorization: Bearer <your-api-key>" \\\n  -H "Content-Type: application/json" \\\n  -d '{"subjectKey":"user-123","attributes":{"plan":"pro"}}'`}</code></pre></section>
      {showCreate && environment && <CreateKeyDialog environmentId={environment.id} onClose={() => setShowCreate(false)} onCreated={async (key) => { setShowCreate(false); setCreated(key); await load() }} />}
    </>
  )
}

function CreateKeyDialog({ environmentId, onClose, onCreated }: { environmentId: string; onClose: () => void; onCreated: (key: CreatedApiKey) => Promise<void> }) {
  const [name, setName] = useState('Backend service')
  async function submit(event: FormEvent) {
    event.preventDefault()
    const created = await api<CreatedApiKey>(`/api/environments/${environmentId}/api-keys`, { method: 'POST', body: JSON.stringify({ name }) })
    await onCreated(created)
  }
  return <div className="dialog-backdrop" onMouseDown={onClose}><form className="dialog" onSubmit={(event) => void submit(event)} onMouseDown={(event) => event.stopPropagation()}><div className="dialog-header"><div><h2>Create server API key</h2><p>Use a descriptive name so it is safe to rotate later.</p></div><button type="button" className="icon-button" onClick={onClose}>×</button></div><label>Key name<input value={name} onChange={(event) => setName(event.target.value)} required autoFocus /></label><div className="dialog-actions"><button type="button" className="secondary-button" onClick={onClose}>Cancel</button><button className="primary-button">Create key</button></div></form></div>
}
