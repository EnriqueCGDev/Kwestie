import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { WorkspaceService } from './workspace.service';

describe('WorkspaceService', () => {
  let service: WorkspaceService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(WorkspaceService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('gets an individual Workspace without sending a user ID or manual authentication', () => {
    const received = vi.fn();
    service.get('workspace-id').subscribe(received);
    const request = http.expectOne('/api/workspaces/workspace-id');
    expect(request.request.method).toBe('GET');
    expect(request.request.body).toBeNull();
    expect(request.request.params.keys()).toEqual([]);
    expect(request.request.headers.has('Authorization')).toBe(false);
    expect(request.request.withCredentials).toBe(false);
    const workspace = { workspaceId: 'workspace-id', name: 'Support', createdAt: '2026-10-08T12:00:00Z' };
    request.flush(workspace);
    expect(received).toHaveBeenCalledWith(workspace);
  });

  it('lists with the relative GET contract without manual authentication or credentials', () => {
    const received = vi.fn();
    service.list().subscribe(received);
    const request = http.expectOne('/api/workspaces');
    expect(request.request.method).toBe('GET');
    expect(request.request.body).toBeNull();
    expect(request.request.params.keys()).toEqual([]);
    expect(request.request.headers.has('Authorization')).toBe(false);
    expect(request.request.withCredentials).toBe(false);
    request.flush([]);
    expect(received).toHaveBeenCalledWith([]);
  });

  it('posts only name and returns the generated ID without adding authentication', () => {
    const received = vi.fn();
    // Extra properties from a caller must not become part of the HTTP request.
    const input = { name: 'Support', userId: 'untrusted-user' };
    service.create(input).subscribe(received);
    const request = http.expectOne('/api/workspaces');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ name: 'Support' });
    expect(request.request.params.keys()).toEqual([]);
    expect(request.request.headers.has('Authorization')).toBe(false);
    expect(request.request.withCredentials).toBe(false);
    request.flush({ workspaceId: 'new-id' });
    expect(received).toHaveBeenCalledWith({ workspaceId: 'new-id' });
  });
});
