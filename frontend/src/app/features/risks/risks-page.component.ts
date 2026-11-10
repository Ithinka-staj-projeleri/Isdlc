import { Component } from '@angular/core';
import { ProjectPickerComponent } from '../../shared/components/project-picker/project-picker.component';
import { RiskListComponent } from './risk-list/risk-list.component';

/** Menu page: the risks of the project chosen at the top. The project detail page shows the same list. */
@Component({
  imports: [ProjectPickerComponent, RiskListComponent],
  selector: 'app-risks-page',
  styleUrl: './risks-page.component.scss',
  templateUrl: './risks-page.component.html',
})
export class RisksPageComponent {}
