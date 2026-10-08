export interface CreateKwestieRequest {
  title: string;
  description: string;
  priority: number;
}

export interface CreateKwestieResponse {
  kwestieId: string;
}
