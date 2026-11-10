import { Component } from '@angular/core';
import { ProjectPickerComponent } from '../../shared/components/project-picker/project-picker.component';
import { ProjectAssessmentComponent } from './project-assessment/project-assessment.component';

/** Menu page: the assessment of the project chosen at the top. The project detail page shows the same component. */
@Component({
  imports: [ProjectPickerComponent, ProjectAssessmentComponent],
  selector: 'app-assessment-page',
  styleUrl: './assessment-page.component.scss',
  templateUrl: './assessment-page.component.html',
})
export class AssessmentPageComponent {}
