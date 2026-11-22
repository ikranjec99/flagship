export type Environment = {
  id: string
  name: string
  key: string
}

export type Project = {
  id: string
  name: string
  key: string
  createdAt: string
  environments: Environment[]
}

export type FlagListItem = {
  id: string
  name: string
  key: string
  description: string | null
  isArchived: boolean
  enabled: boolean
  defaultValue: boolean
  rolloutPercentage: number | null
  ruleCount: number
  version: number
  updatedAt: string
}

export type ConditionOperator =
  | 'Equals'
  | 'NotEquals'
  | 'OneOf'
  | 'NotOneOf'
  | 'GreaterThan'
  | 'LessThan'

export type Condition = {
  id?: string
  attribute: string
  operator: ConditionOperator
  comparisonValue: unknown
  position?: number
}

export type Rule = {
  id?: string
  name: string
  position?: number
  serveValue: boolean
  conditions: Condition[]
}

export type FlagDetail = FlagListItem & {
  projectId: string
  environmentId: string
  rules: Rule[]
}

export type EvaluationResult = {
  key: string
  value: boolean
  reason: string
  matchedRuleId: string | null
  rolloutBucket: number | null
  configurationVersion: number
}

export type ApiKey = {
  id: string
  name: string
  prefix: string
  createdAt: string
  revokedAt: string | null
}

export type CreatedApiKey = ApiKey & { secret: string }

export type AuditEvent = {
  id: number
  actor: string
  action: string
  entityType: string
  entityId: string | null
  summary: string
  note: string | null
  occurredAt: string
}
