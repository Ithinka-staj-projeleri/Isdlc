import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AgentService } from './agent.service';
import { AgentStatus } from './models/meeting-suggestions';

const STATUS_URL = '/api/v1/agents/status';

describe('AgentService', () => {
  let service: AgentService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AgentService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function answer(status: AgentStatus): void {
    http.expectOne(STATUS_URL).flush(status);
  }

  it('turns transcription on without the text assistants (AI on, no data sharing)', () => {
    let enabled: boolean | undefined;
    service.transcriptionEnabled().subscribe(value => (enabled = value));

    answer({ meetingAssistantEnabled: false, riskAssistantEnabled: false, requestAssistantEnabled: false, transcriptionEnabled: true });

    expect(enabled).toBe(true);
  });

  it('keeps transcription off when the API says so, whatever the text assistants', () => {
    let enabled: boolean | undefined;
    service.transcriptionEnabled().subscribe(value => (enabled = value));

    answer({ meetingAssistantEnabled: true, riskAssistantEnabled: true, requestAssistantEnabled: true, transcriptionEnabled: false });

    expect(enabled).toBe(false);
  });

  it('counts a failed status check as everything off', () => {
    let enabled: boolean | undefined;
    service.transcriptionEnabled().subscribe(value => (enabled = value));

    http.expectOne(STATUS_URL).flush('kapalı', { status: 503, statusText: 'Service Unavailable' });

    expect(enabled).toBe(false);
  });
});
