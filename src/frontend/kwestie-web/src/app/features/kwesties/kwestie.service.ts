import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { CreateKwestieRequest, CreateKwestieResponse } from './kwestie.models';

@Service()
export class KwestieService {
  private readonly http = inject(HttpClient);

  create(workspaceId: string, request: CreateKwestieRequest) {
    return this.http.post<CreateKwestieResponse>(
      `/api/workspaces/${encodeURIComponent(workspaceId)}/kwesties`,
      { title: request.title, description: request.description, priority: request.priority },
    );
  }
}
