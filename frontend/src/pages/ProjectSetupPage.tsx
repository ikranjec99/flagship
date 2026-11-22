import { useState, type FormEvent } from 'react'
import { api } from '../lib/api'

export function ProjectSetupPage({ onCreated }: { onCreated: () => Promise<void> }) {
  const [name, setName] = useState('My application')
  const [error, setError] = useState('')

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError('')
    try {
      await api('/api/projects', { method: 'POST', body: JSON.stringify({ name }) })
      await onCreated()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not create the project.')
    }
  }

  return (
    <div className="setup-layout">
      <form className="setup-card" onSubmit={(event) => void submit(event)}>
        <div className="brand"><span className="brand-mark">F</span><span>Flagship</span></div>
        <span className="eyebrow">FIRST PROJECT</span>
        <h1>What are you shipping?</h1>
        <p>We will create development, staging, and production environments to get you started.</p>
        <label>Project name<input value={name} onChange={(event) => setName(event.target.value)} required autoFocus /></label>
        {error && <div className="form-error">{error}</div>}
        <button className="primary-button">Create project</button>
      </form>
    </div>
  )
}
