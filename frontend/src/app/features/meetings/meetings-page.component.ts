import { Component } from '@angular/core';
import { ProjectPickerComponent } from '../../shared/components/project-picker/project-picker.component';
import { MeetingListComponent } from './meeting-list/meeting-list.component';

/** Menu page: the meeting minutes of the project chosen at the top. The project detail page shows the same list. */
@Component({
  imports: [ProjectPickerComponent, MeetingListComponent],
  selector: 'app-meetings-page',
  styleUrl: './meetings-page.component.scss',
  templateUrl: './meetings-page.component.html',
})
export class MeetingsPageComponent {}
