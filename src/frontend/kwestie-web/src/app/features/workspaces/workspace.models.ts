export interface WorkspaceSummary {
  workspaceId: string;
  name: string;
  createdAt: string;
}

export interface CreateWorkspaceRequest {
  name: string;
}

export interface CreateWorkspaceResponse {
  workspaceId: string;
}
