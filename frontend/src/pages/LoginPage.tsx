import { useState, type FormEvent } from 'react'
import { api } from '../lib/api'

export function LoginPage({ onAuthenticated }: { onAuthenticated: () => void }) {
  const [email, setEmail] = useState('admin@flagship.local')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError('')
    try {
      await api('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email, password }),
      })
      onAuthenticated()
    } catch {
      setError('The email or password is incorrect.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="auth-layout">
      <section className="auth-panel">
        <div className="brand auth-brand"><img className="brand-mark" src="/flagship-logo.svg" alt="" /><span>Flagship</span></div>
        <div className="auth-copy">
          <span className="eyebrow">SELF-HOSTED FEATURE MANAGEMENT</span>
          <h1>Release with control,<br />not crossed fingers.</h1>
          <p>Target users, roll out gradually, and switch risky features off without another deployment.</p>
        </div>
        <div className="auth-principles">
          <span>Deterministic</span><span>Auditable</span><span>Yours to operate</span>
        </div>
      </section>
      <section className="login-panel">
        <form className="login-card" onSubmit={(event) => void submit(event)}>
          <h2>Welcome back</h2>
          <p>Sign in to manage your projects.</p>
          <label>Email<input type="email" value={email} onChange={(event) => setEmail(event.target.value)} required /></label>
          <label>Password<input type="password" value={password} onChange={(event) => setPassword(event.target.value)} required autoFocus /></label>
          {error && <div className="form-error">{error}</div>}
          <button className="primary-button full-width" disabled={submitting}>
            {submitting ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      </section>
    </div>
  )
}
