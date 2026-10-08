import { Component, Input } from '@angular/core';

import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-toast-notification',
  standalone: true,
  imports: [
    CommonModule
  ],
  templateUrl: './toast-notification.component.html',
  styleUrl: './toast-notification.component.scss'
})
export class ToastNotificationComponent {

  @Input()
  visible = false;

  @Input()
  message = '';

  @Input()
  type:
    'success'
    | 'error'
    | 'info'
    = 'info';
}
