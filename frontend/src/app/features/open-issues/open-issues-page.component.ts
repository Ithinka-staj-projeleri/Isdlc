import { Component } from '@angular/core';
import { ProjectPickerComponent } from '../../shared/components/project-picker/project-picker.component';
import { OpenIssueListComponent } from './open-issue-list/open-issue-list.component';

/** Menu page: the open issues of the project chosen at the top. The project detail page shows the same list. */
@Component({
  imports: [ProjectPickerComponent, OpenIssueListComponent],
  selector: 'app-open-issues-page',
  styleUrl: './open-issues-page.component.scss',
  templateUrl: './open-issues-page.component.html',
})
export class OpenIssuesPageComponent {}
