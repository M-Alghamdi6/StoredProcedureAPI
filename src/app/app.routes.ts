import { Routes } from '@angular/router';
import { ProcedureComponent } from './components/procedure/procedure';
import { ExecutionLogsComponent } from './components/execution-logs/execution-logs.component';
import { DashboardComponent } from './components/dashboard/dashboard';
import { DatasetComponent } from './components/dataset/dataset.component';

export const routes: Routes = [
  { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
  { path: 'procedures', component: ProcedureComponent },
  { path: 'procedure', redirectTo: '/procedures', pathMatch: 'full' },
  { path: 'execution-logs', component: ExecutionLogsComponent },
  { path: 'dashboard', component: DashboardComponent },
  { path: 'datasets', component: DatasetComponent },
  { path: '**', redirectTo: '/dashboard' }
];
