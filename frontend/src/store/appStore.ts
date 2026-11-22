import { create } from 'zustand'
import type { Environment, Project } from '../lib/types'

export type View = 'overview' | 'flags' | 'keys' | 'audit'

type AppState = {
  projects: Project[]
  projectId: string | null
  environmentId: string | null
  view: View
  selectedFlagId: string | null
  setProjects: (projects: Project[]) => void
  selectProject: (projectId: string) => void
  selectEnvironment: (environmentId: string) => void
  navigate: (view: View) => void
  openFlag: (flagId: string) => void
  closeFlag: () => void
}

export const useAppStore = create<AppState>((set, get) => ({
  projects: [],
  projectId: null,
  environmentId: null,
  view: 'overview',
  selectedFlagId: null,
  setProjects: (projects) => {
    const currentProject = projects.find((project) => project.id === get().projectId) ?? projects[0]
    const currentEnvironment = currentProject?.environments.find(
      (environment) => environment.id === get().environmentId,
    ) ?? currentProject?.environments.find((environment) => environment.key === 'production')
      ?? currentProject?.environments[0]
    set({
      projects,
      projectId: currentProject?.id ?? null,
      environmentId: currentEnvironment?.id ?? null,
    })
  },
  selectProject: (projectId) => {
    const project = get().projects.find((candidate) => candidate.id === projectId)
    const environment = project?.environments.find((item) => item.key === 'production')
      ?? project?.environments[0]
    set({ projectId, environmentId: environment?.id ?? null, selectedFlagId: null })
  },
  selectEnvironment: (environmentId) => set({ environmentId, selectedFlagId: null }),
  navigate: (view) => set({ view, selectedFlagId: null }),
  openFlag: (selectedFlagId) => set({ view: 'flags', selectedFlagId }),
  closeFlag: () => set({ selectedFlagId: null }),
}))

export function currentSelection(projects: Project[], projectId: string | null, environmentId: string | null): {
  project?: Project
  environment?: Environment
} {
  const project = projects.find((candidate) => candidate.id === projectId)
  return {
    project,
    environment: project?.environments.find((candidate) => candidate.id === environmentId),
  }
}
