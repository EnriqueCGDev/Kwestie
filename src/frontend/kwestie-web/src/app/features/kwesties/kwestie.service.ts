import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { CreateKwestieRequest, CreateKwestieResponse, KwestieSummary } from './kwestie.models';

@Service()
export class KwestieService {
  private readonly http = inject(HttpClient);

  list(workspaceId: string) {
    return this.http.get<KwestieSummary[]>(`/api/workspaces/${encodeURIComponent(workspaceId)}/kwesties`);
  }

  create(workspaceId: string, request: CreateKwestieRequest) {
    return this.http.post<CreateKwestieResponse>(
      `/api/workspaces/${encodeURIComponent(workspaceId)}/kwesties`,
      { title: request.title, description: request.description, priority: request.priority },
    );
  }
}
