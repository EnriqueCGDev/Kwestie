import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { CreateWorkspaceRequest, CreateWorkspaceResponse, WorkspaceSummary } from './workspace.models';

@Service()
export class WorkspaceService {
  private readonly http = inject(HttpClient);

  list() {
    return this.http.get<WorkspaceSummary[]>('/api/workspaces');
  }

  create(request: CreateWorkspaceRequest) {
    return this.http.post<CreateWorkspaceResponse>('/api/workspaces', { name: request.name });
  }
}
