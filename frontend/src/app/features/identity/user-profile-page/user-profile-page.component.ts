import { Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { USER_ROLE_LABELS } from '../../../shared/models/user-role';
import { DateOnlyPipe } from '../../../shared/pipes/date-only.pipe';
import { OPEN_ISSUE_TYPE_LABELS } from '../../open-issues/models/open-issue';
import { EXPERTISE_MAX_LENGTH, UserProfile, WORKING_NOTE_MAX_LENGTH, expertiseAreas } from '../models/user-profile';
import { UserProfileService } from '../user-profile.service';

/**
 * Kişi profili (Faz 5 · F9): role, expertise, "how to work with me" note, the person's open issues and waiting steps.
 * Everyone can open anyone's profile; only the person edits their own. Contract: docs/users-api.md
 */
@Component({
  selector: 'app-user-profile-page',
  imports: [ReactiveFormsModule, RouterLink, EmptyStateComponent, StatusBadgeComponent, DateOnlyPipe],
  templateUrl: './user-profile-page.component.html',
  styleUrl: './user-profile-page.component.scss',
})
export class UserProfilePageComponent {
  private readonly _route = inject(ActivatedRoute);
  private readonly _profileService = inject(UserProfileService);
  private readonly _auth = inject(AuthService);
  private readonly _fb = inject(FormBuilder);

  protected readonly roleLabels = USER_ROLE_LABELS;
  protected readonly issueTypeLabels = OPEN_ISSUE_TYPE_LABELS;
  protected readonly expertiseMax = EXPERTISE_MAX_LENGTH;
  protected readonly noteMax = WORKING_NOTE_MAX_LENGTH;

  private readonly _userId = toSignal(
    this._route.paramMap.pipe(map(params => Number(params.get('userId')))),
    { initialValue: NaN },
  );

  protected readonly profile = rxResource({
    params: () => this._userId(),
    stream: ({ params }) => this._profileService.get(params),
  });

  protected readonly isMine = computed(() => this._auth.user()?.id === this._userId());
  protected readonly areas = computed(() =>
    this.profile.hasValue() ? expertiseAreas(this.profile.value().expertise) : []);

  protected readonly editing = signal(false);
  protected readonly saving = signal(false);
  protected readonly serverErrors = signal<Record<string, string[]>>({});

  protected readonly form = this._fb.nonNullable.group({
    expertise: ['', Validators.maxLength(EXPERTISE_MAX_LENGTH)],
    workingNote: ['', Validators.maxLength(WORKING_NOTE_MAX_LENGTH)],
  });

  protected startEditing(profile: UserProfile): void {
    this.form.setValue({ expertise: profile.expertise ?? '', workingNote: profile.workingNote ?? '' });
    this.serverErrors.set({});
    this.editing.set(true);
  }

  protected errorFor(field: 'expertise' | 'workingNote'): string | null {
    return this.serverErrors()[field]?.[0] ?? null;
  }

  protected save(): void {
    if (this.form.invalid) {
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.serverErrors.set({});
    this._profileService.updateMine({
      expertise: value.expertise.trim() || null,
      workingNote: value.workingNote.trim() || null,
    }).subscribe({
      next: profile => {
        this.saving.set(false);
        this.editing.set(false);
        this.profile.set(profile);
      },
      error: err => {
        this.saving.set(false);
        this.serverErrors.set(fieldErrorsOf(err));
      },
    });
  }

  protected waitingText(days: number): string {
    return days === 0 ? 'Bugün' : `${days} gündür bekliyor`;
  }
}
