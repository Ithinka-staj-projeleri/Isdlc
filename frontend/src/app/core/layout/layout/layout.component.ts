import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AuthService } from '../../auth/auth.service';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { TopbarComponent } from '../topbar/topbar.component';

@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, SidebarComponent, TopbarComponent],
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.scss',
})
export class LayoutComponent implements OnInit {
  private readonly auth = inject(AuthService);

  /** Permissions are not in the token: the session is read again each time the app opens (docs/configurability.md). */
  ngOnInit(): void {
    this.auth.refreshMe();
  }
}
