import { useEffect, useState } from 'react'
import { ApiError, api } from '../lib/api'
import type { ConditionOperator, EvaluationResult, FlagDetail, Rule } from '../lib/types'
import { currentSelection, useAppStore } from '../store/appStore'

type EditableCondition = {
  attribute: string
  operator: ConditionOperator
  comparisonText: string
}

type EditableRule = {
  name: string
  serveValue: boolean
  conditions: EditableCondition[]
}

const operators: Array<{ value: ConditionOperator; label: string }> = [
  { value: 'Equals', label: 'equals' },
  { value: 'NotEquals', label: 'does not equal' },
  { value: 'OneOf', label: 'is one of' },
  { value: 'NotOneOf', label: 'is not one of' },
  { value: 'GreaterThan', label: 'is greater than' },
  { value: 'LessThan', label: 'is less than' },
]

export function FlagEditorPage({ flagId }: { flagId: string }) {
  const store = useAppStore()
  const { environment } = currentSelection(store.projects, store.projectId, store.environmentId)
  const [flag, setFlag] = useState<FlagDetail | null>(null)
  const [enabled, setEnabled] = useState(false)
  const [defaultValue, setDefaultValue] = useState(false)
  const [rollout, setRollout] = useState<number | null>(null)
  const [rules, setRules] = useState<EditableRule[]>([])
  const [showReview, setShowReview] = useState(false)
  const [note, setNote] = useState('')
  const [error, setError] = useState('')
  const [testSubject, setTestSubject] = useState('user-123')
  const [testAttributes, setTestAttributes] = useState('{\n  "plan": "pro",\n  "country": "HR"\n}')
  const [testResult, setTestResult] = useState<EvaluationResult | null>(null)

  async function load() {
    if (!environment) return
    const next = await api<FlagDetail>(`/api/flags/${flagId}?environmentId=${environment.id}`)
    setFlag(next)
    setEnabled(next.enabled)
    setDefaultValue(next.defaultValue)
    setRollout(next.rolloutPercentage)
    setRules(next.rules.map(toEditableRule))
    setError('')
    setTestResult(null)
  }

  useEffect(() => { void load() }, [flagId, environment?.id])

  function addRule() {
    setRules([...rules, {
      name: `Rule ${rules.length + 1}`,
      serveValue: true,
      conditions: [{ attribute: 'plan', operator: 'Equals', comparisonText: '"beta"' }],
    }])
  }

  function updateRule(index: number, update: (rule: EditableRule) => EditableRule) {
    setRules(rules.map((rule, ruleIndex) => ruleIndex === index ? update(rule) : rule))
  }

  async function save() {
    if (!flag || !environment) return
    setError('')
    try {
      const serializedRules: Rule[] = rules.map((rule) => ({
        name: rule.name,
        serveValue: rule.serveValue,
        conditions: rule.conditions.map((condition) => ({
          attribute: condition.attribute,
          operator: condition.operator,
          comparisonValue: JSON.parse(condition.comparisonText) as unknown,
        })),
      }))
      const updated = await api<FlagDetail>(`/api/flags/${flag.id}/configuration/${environment.id}`, {
        method: 'PUT',
        body: JSON.stringify({
          enabled,
          defaultValue,
          rolloutPercentage: rollout,
          expectedVersion: flag.version,
          changeNote: note || null,
          rules: serializedRules,
        }),
      })
      setFlag(updated)
      setRules(updated.rules.map(toEditableRule))
      setShowReview(false)
      setNote('')
    } catch (reason) {
      if (reason instanceof SyntaxError) {
        setError('Condition values must be valid JSON, for example "pro", 10, true, or ["HR", "DE"].')
      } else if (reason instanceof ApiError && reason.status === 409) {
        setError(reason.message)
      } else {
        setError(reason instanceof Error ? reason.message : 'Could not save the configuration.')
      }
      setShowReview(false)
    }
  }

  async function testEvaluation() {
    if (!flag || !environment) return
    setError('')
    try {
      const attributes = JSON.parse(testAttributes) as Record<string, unknown>
      setTestResult(await api<EvaluationResult>(`/api/internal/environments/${environment.id}/evaluate/${flag.key}`, {
        method: 'POST',
        body: JSON.stringify({ subjectKey: testSubject || null, attributes }),
      }))
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Evaluation failed.')
    }
  }

  async function archive() {
    if (!flag || !window.confirm(`Archive ${flag.name}? It will no longer be evaluated.`)) return
    await api(`/api/flags/${flag.id}/archive`, { method: 'POST' })
    store.closeFlag()
  }

  async function restore() {
    if (!flag) return
    await api(`/api/flags/${flag.id}/restore`, { method: 'POST' })
    await load()
  }

  if (!flag) return <div className="center-content"><div className="spinner" />Loading flag…</div>

  if (flag.isArchived) {
    return <><button className="back-button" onClick={store.closeFlag}>← Feature flags</button><div className="flag-header"><div><div className="title-line"><h1>{flag.name}</h1><span className="status-pill off"><i />Archived</span></div><button className="copy-key" onClick={() => void navigator.clipboard.writeText(flag.key)}><code>{flag.key}</code> <span>Copy</span></button><p>{flag.description || 'No description provided.'}</p></div><button className="primary-button" onClick={() => void restore()}>Restore flag</button></div><section className="card archived-card"><span className="empty-icon">⚑</span><div><h2>This flag is archived</h2><p>Archived flags are not available to evaluation clients. Restore it before changing its configuration.</p></div></section></>
  }

  return (
    <>
      <button className="back-button" onClick={store.closeFlag}>← Feature flags</button>
      <div className="flag-header">
        <div><div className="title-line"><h1>{flag.name}</h1><span className={`status-pill ${enabled ? 'on' : 'off'}`}><i />{enabled ? 'Active' : 'Inactive'}</span></div><button className="copy-key" onClick={() => void navigator.clipboard.writeText(flag.key)}><code>{flag.key}</code> <span>Copy</span></button><p>{flag.description || 'No description provided.'}</p></div>
        <div className="header-actions"><button className="danger-text-button" onClick={() => void archive()}>Archive</button><button className="primary-button" onClick={() => setShowReview(true)}>Review & save</button></div>
      </div>
      {environment?.key === 'production' && <div className="production-banner"><strong>Production environment</strong><span>Changes affect live evaluations after saving.</span></div>}
      {error && <div className="form-error page-error">{error}</div>}

      <div className="editor-layout">
        <div className="editor-main">
          <section className="card editor-section">
            <div className="section-heading"><div><h2>Serving status</h2><p>Inactive flags always evaluate to false.</p></div><label className="switch"><input type="checkbox" checked={enabled} onChange={(event) => setEnabled(event.target.checked)} /><span /></label></div>
          </section>

          <section className="card editor-section">
            <div className="section-heading"><div><h2>Targeting rules</h2><p>Rules are evaluated from top to bottom. The first match wins.</p></div><button className="secondary-button" onClick={addRule}>+ Add rule</button></div>
            {rules.length === 0 ? <div className="rule-empty">No targeting rules. Every request continues to the default behavior.</div> : (
              <div className="rules-list">{rules.map((rule, ruleIndex) => (
                <div className="rule-card" key={ruleIndex}>
                  <div className="rule-topline"><span className="rule-order">{ruleIndex + 1}</span><input className="rule-name" value={rule.name} onChange={(event) => updateRule(ruleIndex, (current) => ({ ...current, name: event.target.value }))} /><button className="icon-button" title="Remove rule" onClick={() => setRules(rules.filter((_, index) => index !== ruleIndex))}>×</button></div>
                  {rule.conditions.map((condition, conditionIndex) => (
                    <div className="condition-row" key={conditionIndex}>
                      <span className="condition-prefix">{conditionIndex === 0 ? 'IF' : 'AND'}</span>
                      <input value={condition.attribute} onChange={(event) => updateRule(ruleIndex, (current) => ({ ...current, conditions: current.conditions.map((item, index) => index === conditionIndex ? { ...item, attribute: event.target.value } : item) }))} placeholder="attribute" />
                      <select value={condition.operator} onChange={(event) => updateRule(ruleIndex, (current) => ({ ...current, conditions: current.conditions.map((item, index) => index === conditionIndex ? { ...item, operator: event.target.value as ConditionOperator } : item) }))}>{operators.map((operator) => <option key={operator.value} value={operator.value}>{operator.label}</option>)}</select>
                      <input className="mono-input" value={condition.comparisonText} onChange={(event) => updateRule(ruleIndex, (current) => ({ ...current, conditions: current.conditions.map((item, index) => index === conditionIndex ? { ...item, comparisonText: event.target.value } : item) }))} placeholder='"value"' />
                      <button className="icon-button" onClick={() => updateRule(ruleIndex, (current) => ({ ...current, conditions: current.conditions.filter((_, index) => index !== conditionIndex) }))}>×</button>
                    </div>
                  ))}
                  <div className="rule-footer"><button className="text-button" onClick={() => updateRule(ruleIndex, (current) => ({ ...current, conditions: [...current.conditions, { attribute: '', operator: 'Equals', comparisonText: '"value"' }] }))}>+ Add condition</button><label>SERVE <select value={String(rule.serveValue)} onChange={(event) => updateRule(ruleIndex, (current) => ({ ...current, serveValue: event.target.value === 'true' }))}><option value="true">true</option><option value="false">false</option></select></label></div>
                </div>
              ))}</div>
            )}
          </section>

          <section className="card editor-section">
            <div className="section-heading"><div><h2>Default behavior</h2><p>Applied when no targeting rule matches.</p></div></div>
            <div className="mode-tabs"><button className={rollout === null ? 'active' : ''} onClick={() => setRollout(null)}>Fixed value</button><button className={rollout !== null ? 'active' : ''} onClick={() => setRollout(10)}>Percentage rollout</button></div>
            {rollout === null ? (
              <div className="value-choice"><button className={!defaultValue ? 'selected' : ''} onClick={() => setDefaultValue(false)}><span className="value-dot off" />false</button><button className={defaultValue ? 'selected' : ''} onClick={() => setDefaultValue(true)}><span className="value-dot on" />true</button></div>
            ) : (
              <div className="rollout-editor"><div className="rollout-values"><label><input type="number" min="0" max="100" value={rollout} onChange={(event) => setRollout(Math.max(0, Math.min(100, Number(event.target.value))))} />% true</label><span>{100 - rollout}% false</span></div><input type="range" min="0" max="100" value={rollout} onChange={(event) => setRollout(Number(event.target.value))} /><p>Subjects are assigned deterministically using the flag and subject key.</p></div>
            )}
          </section>
        </div>

        <aside className="card evaluation-panel">
          <div className="card-header"><div><span className="eyebrow">LIVE CONFIGURATION</span><h2>Test evaluation</h2><p>See exactly how the currently saved configuration evaluates.</p></div></div>
          <label>Subject key<input value={testSubject} onChange={(event) => setTestSubject(event.target.value)} placeholder="user-123" /></label>
          <label>Attributes<textarea className="code-area" rows={8} value={testAttributes} onChange={(event) => setTestAttributes(event.target.value)} /></label>
          <button className="secondary-button full-width" onClick={() => void testEvaluation()}>Evaluate saved config</button>
          {testResult && <div className={`evaluation-result ${testResult.value ? 'true' : 'false'}`}><span>RESULT</span><strong>{String(testResult.value)}</strong><dl><div><dt>Reason</dt><dd>{testResult.reason}</dd></div>{testResult.rolloutBucket !== null && <div><dt>Bucket</dt><dd>{testResult.rolloutBucket.toFixed(2)}</dd></div>}<div><dt>Version</dt><dd>v{testResult.configurationVersion}</dd></div></dl></div>}
        </aside>
      </div>

      {showReview && <div className="dialog-backdrop" onMouseDown={() => setShowReview(false)}><div className="dialog review-dialog" onMouseDown={(event) => event.stopPropagation()}><div className="dialog-header"><div><span className="eyebrow">{environment?.name.toUpperCase()}</span><h2>Review configuration</h2><p>This will create version {flag.version + 1}.</p></div><button className="icon-button" onClick={() => setShowReview(false)}>×</button></div><div className="change-summary"><div><span>Serving status</span><strong>{enabled ? 'Active' : 'Inactive'}</strong></div><div><span>Targeting rules</span><strong>{rules.length}</strong></div><div><span>Default</span><strong>{rollout === null ? String(defaultValue) : `${rollout}% true`}</strong></div></div><label>Change note <span className="optional">optional</span><textarea rows={3} value={note} onChange={(event) => setNote(event.target.value)} placeholder="Why are you making this change?" /></label><div className="dialog-actions"><button className="secondary-button" onClick={() => setShowReview(false)}>Cancel</button><button className="primary-button" onClick={() => void save()}>Save {environment?.name} changes</button></div></div></div>}
    </>
  )
}

function toEditableRule(rule: Rule): EditableRule {
  return {
    name: rule.name,
    serveValue: rule.serveValue,
    conditions: rule.conditions.map((condition) => ({
      attribute: condition.attribute,
      operator: condition.operator,
      comparisonText: JSON.stringify(condition.comparisonValue) ?? 'null',
    })),
  }
}
