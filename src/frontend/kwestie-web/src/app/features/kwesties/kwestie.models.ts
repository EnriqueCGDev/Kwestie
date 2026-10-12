export interface CreateKwestieRequest {
  title: string;
  description: string;
  priority: number;
}

export interface CreateKwestieResponse {
  kwestieId: string;
}

export interface KwestieSummary {
  kwestieId: string;
  title: string;
  description: string;
  status: number;
  priority: number;
  createdAt: string;
}

export interface KwestieDetails extends KwestieSummary {
  updatedAt: string;
  resolvedAt: string | null;
  closedAt: string | null;
}
