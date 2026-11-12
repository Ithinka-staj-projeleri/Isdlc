import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Questionnaire } from '../question-bank.models';
import { QuestionnaireComponent } from './questionnaire.component';

const URL = '/api/v1/projects/7/questionnaires/risk-analysis';

function form(change: Partial<Questionnaire> = {}): Questionnaire {
  return {
    projectId: 7,
    set: { code: 'risk-analysis', name: 'Risk Analiz Formu', sourceDocument: 'ISDLC-Form-Analiz-014', version: '2.0', answerMode: 'SharedOptions' },
    workType: 'new-software-project',
    subcategory: 'Internal',
    participants: null,
    assessedOn: null,
    approvedBy: null,
    approvedOn: null,
    questions: [{
      question: {
        id: 11, code: 'risk-analysis/new-software-project/all/001', no: 1, section: null, group: 'Kapsam', topic: 'Kapsam Netliği',
        text: 'Kapsam yazılı mı?', requirement: 'Required', expectedInfo: null, riskDescription: 'Kapsam belirsiz kalabilir.',
        suggestedAction: null, reference: null,
        options: [
          { id: 1, code: 'risk-present', label: 'Risk Var', points: null, meaning: 'Negative', isScored: true, requiresNote: true },
          { id: 3, code: 'no-risk', label: 'Risk Yok', points: null, meaning: 'Complete', isScored: false, requiresNote: false },
        ],
      },
      answer: null,
    }],
    summary: {
      listed: 1, answered: 0, requiredUnanswered: 1, isComplete: false, missingNote: 0,
      byMeaning: { complete: 0, partial: 0, open: 0, negative: 0, notApplicable: 0 },
    },
    result: null,
    ...change,
  };
}

describe('QuestionnaireComponent', () => {
  let fixture: ComponentFixture<QuestionnaireComponent>;
  let http: HttpTestingController;
  let emitted: Questionnaire[];

  async function render(readOnly = false): Promise<void> {
    fixture = TestBed.createComponent(QuestionnaireComponent);
    fixture.componentRef.setInput('projectId', 7);
    fixture.componentRef.setInput('setCode', 'risk-analysis');
    fixture.componentRef.setInput('readOnly', readOnly);
    emitted = [];
    fixture.componentInstance.changed.subscribe(f => emitted.push(f));
    fixture.detectChanges();
    http.expectOne(URL).flush(form());
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function optionButton(label: string): HTMLButtonElement {
    return [...el().querySelectorAll<HTMLButtonElement>('button.option')].find(b => b.textContent?.trim() === label)!;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [QuestionnaireComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('shows the form, its questions and the summary, and gives the tab the loaded form', async () => {
    await render();

    expect(el().textContent).toContain('Risk Analiz Formu');
    expect(el().textContent).toContain('Kapsam yazılı mı?');
    expect(el().textContent).toContain('1 zorunlu soru bekliyor');
    expect(emitted).toHaveLength(1);
  });

  it('saves the clicked option at once and shows the scored answer\'s probability and severity', async () => {
    await render();

    optionButton('Risk Var').click();
    const req = http.expectOne(`${URL}/answers/11`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ optionId: 1, text: null, note: null, probability: null, severity: null });

    const saved = form();
    saved.questions[0].answer = { optionId: 1, text: null, note: null, probability: null, severity: null, updatedBy: { id: 1, fullName: 'Elif Yılmaz' }, updatedAt: '2026-11-12T09:00:00Z' };
    saved.summary = { ...saved.summary, answered: 1, requiredUnanswered: 0, isComplete: true, missingNote: 1 };
    req.flush(saved);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(optionButton('Risk Var').getAttribute('aria-pressed')).toBe('true');
    expect(el().querySelector('[aria-label="Olasılık"]')).not.toBeNull();
    expect(el().textContent).toContain('Bu cevap için açıklama beklenir.');
    expect(el().textContent).toContain('Tamamlandı');
    expect(emitted).toHaveLength(2);
  });

  it('saves the header when the subcategory changes', async () => {
    await render();

    const select = el().querySelector('select')!;
    select.value = 'External';
    select.dispatchEvent(new Event('change'));

    const req = http.expectOne(URL);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ subcategory: 'External', participants: null, assessedOn: null, approvedByUserId: null, approvedOn: null });
    req.flush(form({ subcategory: 'External' }));
  });

  it('cannot be changed on a project that is not active', async () => {
    await render(true);

    expect(optionButton('Risk Var').disabled).toBe(true);
    expect(el().querySelector('select')!.disabled).toBe(true);
    expect(el().textContent).toContain('yalnızca görüntülenebilir');
  });
});
