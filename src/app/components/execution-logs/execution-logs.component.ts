import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { ExecutionLogService, ProcedureExecutionLog } from '../../services/execution-log.service';
import { ChartDataService } from '../../services/chart-data.service';
import { ProcedureService, ProcedureExecutionRequest } from '../../services/procedure.service';

@Component({
  selector: 'app-execution-logs',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule],
  templateUrl: './execution-logs.component.html',
  styleUrls: ['./execution-logs.component.scss']
})
export class ExecutionLogsComponent implements OnInit {
  logs: ProcedureExecutionLog[] = [];
  selectedLog: ProcedureExecutionLog | null = null;
  loading = false;
  error = '';
  showFilters = false;

  // Filter properties
  schemaName = '';
  procedureName = '';
  fromDate = '';
  toDate = '';
  topRecords = 50;

  // Dropdown options
  schemas: string[] = [];
  procedures: string[] = [];

  constructor(
    private executionLogService: ExecutionLogService,
    private chartDataService: ChartDataService,
    private procedureService: ProcedureService,
    private router: Router
  ) {}

  ngOnInit() {
    this.loadRecentLogs();
  }

  loadRecentLogs() {
    this.loading = true;
    this.error = '';
    
    this.executionLogService.getRecent(this.topRecords).subscribe({
      next: (response) => {
        console.log('API Success - Recent Logs:', response);
        this.logs = response.data || [];
        this.extractSchemasAndProcedures();
        this.loading = false;
      },
      error: (err) => {
        console.error('API Error - Recent Logs:', err);
        this.error = err.error?.message || 'Failed to load logs';
        this.loading = false;
      }
    });
  }

  extractSchemasAndProcedures() {
    const schemaSet = new Set<string>();
    this.logs.forEach(log => {
      if (log.schemaName) schemaSet.add(log.schemaName);
    });
    this.schemas = Array.from(schemaSet).sort();
    this.updateProceduresList();
  }

  onSchemaChange() {
    this.procedureName = '';
    this.updateProceduresList();
  }

  updateProceduresList() {
    const procedureSet = new Set<string>();
    
    this.logs.forEach(log => {
      if (!this.schemaName || log.schemaName === this.schemaName) {
        if (log.procedureName) procedureSet.add(log.procedureName);
      }
    });
    
    this.procedures = Array.from(procedureSet).sort();
  }

  loadLatestLog() {
    this.loading = true;
    this.error = '';
    
    this.executionLogService.getLatest().subscribe({
      next: (response) => {
        console.log(' API Success - Latest Log:', response);
        if (response.data) {
          this.logs = [response.data];
          this.extractSchemasAndProcedures();
        }
        this.loading = false;
      },
      error: (err) => {
        console.error('API Error - Latest Log:', err);
        this.error = err.error?.message || 'Failed to load latest log';
        this.loading = false;
      }
    });
  }

  searchLogs() {
    this.loading = true;
    this.error = '';
    
    const fromUtc = this.fromDate ? new Date(this.fromDate) : undefined;
    const toUtc = this.toDate ? new Date(this.toDate) : undefined;

    this.executionLogService.query(
      this.schemaName || undefined,
      this.procedureName || undefined,
      fromUtc,
      toUtc,
      this.topRecords
    ).subscribe({
      next: (response) => {
        console.log('API Success - Search Logs:', response);
        this.logs = response.data || [];
        this.extractSchemasAndProcedures();
        this.loading = false;
      },
      error: (err) => {
        console.error(' API Error - Search Logs:', err);
        this.error = err.error?.message || 'Failed to search logs';
        this.loading = false;
      }
    });
  }

  viewLogDetails(log: ProcedureExecutionLog) {
    console.log('Selected Log:', log);
    this.selectedLog = log;
  }

  closeDetails() {
    this.selectedLog = null;
  }

  clearFilters() {
    this.schemaName = '';
    this.procedureName = '';
    this.fromDate = '';
    this.toDate = '';
    this.topRecords = 50;
    this.loadRecentLogs();
  }

  createChartFromLog(log: ProcedureExecutionLog) {
    console.log(' Creating chart from log:', log);
    
    if (!log.rowCount || log.rowCount === 0) {
      alert('No data available to create chart');
      return;
    }

    this.loading = true;
    
    // Build parameters object
    const parameters: { [key: string]: any } = {};
    
    if (log.parameters && log.parameters.length > 0) {
      log.parameters.forEach(param => {
        if (param.parameterValue !== null && param.parameterValue !== undefined) {
          // Remove @ symbol if present
          const paramName = param.parameterName.startsWith('@') 
            ? param.parameterName.substring(1) 
            : param.parameterName;
          parameters[paramName] = param.parameterValue;
        }
      });
    }
    
    console.log('📋 Parameters for execution:', parameters);
    
    // Create the request object
    const request: ProcedureExecutionRequest = {
      parameters: parameters,
      useCache: false
    };
    
    // Re-execute the procedure to get the data
    this.procedureService.executeProcedure(
      log.schemaName,
      log.procedureName,
      request
    ).subscribe({
      next: (response) => {
        console.log('✅ Procedure executed for chart:', response);
        
        if (response.data && response.data.rows && response.data.rows.length > 0) {
          // Transform rows to objects
          const data = response.data.rows.map((row: any[]) => {
            const obj: any = {};
            response.data.columns.forEach((col: string, idx: number) => {
              obj[col] = row[idx];
            });
            return obj;
          });

          console.log('📊 Transformed data for chart:', data);

          // Store in chart service
          this.chartDataService.setChartData({
            schema: log.schemaName,
            procedure: log.procedureName,
            data: data
          });

          console.log('💾 Chart data stored in service');

          // Close modal if open
          this.closeDetails();

          // Navigate to procedures page
          this.router.navigate(['/procedures']).then(() => {
            console.log('🔄 Navigated to procedures page');
          });
        } else {
          console.warn('⚠️ No data returned from procedure');
          alert('No data returned from procedure execution');
        }
        
        this.loading = false;
      },
      error: (err) => {
        console.error('❌ Failed to execute procedure:', err);
        alert(`Failed to execute procedure: ${err.error?.message || err.message || 'Unknown error'}`);
        this.loading = false;
      }
    });
  }

  formatDuration(ms: number): string {
    if (!ms) return 'N/A';
    if (ms < 1000) return `${ms}ms`;
    return `${(ms / 1000).toFixed(2)}s`;
  }

  formatDate(dateString: string): string {
    if (!dateString) return 'N/A';
    try {
      const date = new Date(dateString);
      if (isNaN(date.getTime())) return 'Invalid Date';
      return date.toLocaleString();
    } catch {
      return 'Invalid Date';
    }
  }
}