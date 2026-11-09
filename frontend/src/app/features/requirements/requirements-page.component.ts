import { Component } from '@angular/core';
import { ProjectPickerComponent } from '../../shared/components/project-picker/project-picker.component';
import { RequirementListComponent } from './requirement-list/requirement-list.component';

/** Menu page: the requirements of the project chosen at the top. The project detail page shows the same list. */
@Component({
  imports: [ProjectPickerComponent, RequirementListComponent],
  selector: 'app-requirements-page',
  styleUrl: './requirements-page.component.scss',
  templateUrl: './requirements-page.component.html',
})
export class RequirementsPageComponent {}
