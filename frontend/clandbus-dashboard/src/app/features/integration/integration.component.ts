import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { DashboardDataService } from '../../core/services/dashboard-data.service';
@Component({selector:'app-integration',standalone:true,imports:[CommonModule],templateUrl:'./integration.component.html',styleUrl:'./integration.component.scss'})
export class IntegrationComponent { readonly data=inject(DashboardDataService); readonly connected$=this.data.connected$; readonly status$=this.data.status$; readonly error$=this.data.error$; readonly loading$=this.data.loading$; }
