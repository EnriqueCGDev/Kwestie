import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { KwestieService } from './kwestie.service';

describe('KwestieService', () => {
  it('posts only the three allowed fields to the selected Workspace and returns the ID', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(KwestieService);
    const http = TestBed.inject(HttpTestingController);
    const received = vi.fn();
    const input = {
      title: 'Printer broken', description: 'Cannot print', priority: 2,
      createdById: 'untrusted', categoryId: 'untrusted', workspaceId: 'wrong', number: 4, key: 'KW-4', status: 1,
    };
    service.create('selected-id', input).subscribe(received);
    const request = http.expectOne('/api/workspaces/selected-id/kwesties');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ title: 'Printer broken', description: 'Cannot print', priority: 2 });
    expect(typeof request.request.body.priority).toBe('number');
    expect(request.request.params.keys()).toEqual([]);
    expect(request.request.headers.has('Authorization')).toBe(false);
    expect(request.request.withCredentials).toBe(false);
    request.flush({ kwestieId: 'created-id' }, { status: 201, statusText: 'Created' });
    expect(received).toHaveBeenCalledWith({ kwestieId: 'created-id' });
    http.verify();
  });
});
