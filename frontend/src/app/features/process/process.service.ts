import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ProcessStage } from './models/process-stage';

/** Process API. Contract: docs/process-api.md */
@Injectable({ providedIn: 'root' })
export class ProcessService {
    private readonly http = inject(HttpClient);

    /** The 13 stages and their steps, in flow order. */
    getStages(): Observable<ProcessStage[]> {
        return this.http.get<ProcessStage[]>('/api/v1/process-stages');
    }
}
