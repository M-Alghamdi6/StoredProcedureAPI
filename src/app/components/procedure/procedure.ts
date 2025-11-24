import { Component, OnInit, inject, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { Subscription } from 'rxjs';
import {
  ProcedureService,
  SchemaModel,
  StoredProcedure,
  ProcedureParameter,
  ProcedureExecutionRequest,
  ProcedureExecutionResponse
} from '../../services/procedure.service';
import { ChartDataService, SavedChart } from '../../services/chart-data.service';
import { DatasetStateService } from '../../services/dataset-state.service';
import { DatasetService, ExecuteAndCreateDatasetRequest } from '../../services/dataset.service';
import { Router } from '@angular/router';
import Chart from 'chart.js/auto';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-procedure',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, MatIconModule],
  templateUrl: './procedure.html',
  styleUrls: ['./procedure.scss']
})
export class ProcedureComponent implements OnInit, OnDestroy {
  private procService = inject(ProcedureService);
  private chartDataService = inject(ChartDataService);
  public datasetStateService = inject(DatasetStateService);
  private datasetService = inject(DatasetService);
  private router = inject(Router);
  private chartDataSubscription?: Subscription;
    
  schemas: SchemaModel[] = [];
  procedures: StoredProcedure[] = [];
  parameters: ProcedureParameter[] = [];
  executionResult?: ProcedureExecutionResponse;

  selectedSchema?: string;
  selectedProcedure?: string;
  parameterValues: { [key: string]: any } = {};
  useCache: boolean = false;

  // Preview state
  showPreview = false;
  previewData: any = null;

  // Chart/dataset state
  datasetRecords: any[] = [];
  datasetColumns: string[] = [];
  numericColumns: string[] = [];
  xColumn: string = '';
  yColumns: string[] = [];
  chartType: 'bar' | 'line' | 'pie' | 'doughnut' = 'bar';
  private chart?: Chart;
  
  // Track if chart has been rendered
  isChartRendered: boolean = false;

  // Pending dataset for saving
  pendingDataset = this.datasetStateService.getPendingDataset();

  // Loading and error state
  loading: boolean = false;
  executionError: string | null = null;

  ngOnInit() {
    this.loadSchemas();
    
    // Subscribe to chart data from execution logs
    this.chartDataSubscription = this.chartDataService.chartData$.subscribe(chartData => {
      if (chartData) {
        console.log('📊 Received chart data from execution logs:', chartData);
        
        // Set the execution result
        this.executionResult = {
          columns: Object.keys(chartData.data[0] || {}),
          rows: chartData.data.map(obj => Object.values(obj)),
          rowCount: chartData.data.length
        };

        // Set dataset columns for chart building
        this.datasetColumns = this.executionResult.columns;
        this.datasetRecords = chartData.data;
        this.numericColumns = this.detectNumericColumns(chartData.data);
        this.xColumn = this.datasetColumns[0] || '';
        this.yColumns = this.numericColumns.slice(0, 3);

        console.log('✅ Chart data loaded:', {
          columns: this.datasetColumns,
          numericColumns: this.numericColumns,
          rowCount: this.executionResult.rowCount
        });

        // Clear the chart data from service after loading
        this.chartDataService.clearChartData();
      }
    });
  }

  detectNumericColumns(data: any[]): string[] {
    if (!data || data.length === 0) return [];
    
    const firstRow = data[0];
    const numericCols: string[] = [];
    
    Object.keys(firstRow).forEach(key => {
      const value = firstRow[key];
      if (typeof value === 'number' || (!isNaN(Number(value)) && value !== null && value !== '')) {
        numericCols.push(key);
      }
    });
    
    return numericCols;
  }

  loadSchemas() {
    this.procService.getSchemas().subscribe({
      next: response => {
        console.log('✅ API Success - Schemas:', response);
        this.schemas = response.data || [];
      },
      error: err => console.error('❌ API Error - Schemas:', err)
    });
  }

  onSchemaSelect(schema: string) {
    if (!schema) return;

    this.selectedSchema = schema;
    this.procedures = [];
    this.parameters = [];
    this.executionResult = undefined;
    this.showPreview = false;

    this.procService.getProcedures(schema).subscribe({
      next: response => {
        console.log('✅ API Success - Procedures:', response);
        this.procedures = response.data || [];
      },
      error: err => console.error('❌ API Error - Procedures:', err)
    });
  }

  onProcedureSelect(proc: string) {
    if (!this.selectedSchema || !proc) return;

    this.selectedProcedure = proc;
    this.parameters = [];
    this.executionResult = undefined;
    this.parameterValues = {};
    this.showPreview = false;

    this.procService.getParameters(this.selectedSchema, proc).subscribe({
      next: response => {
        console.log('✅ API Success - Parameters:', response);
        this.parameters = response.data || [];
        this.parameters.forEach(p => this.parameterValues[p.parameterName] = '');
      },
      error: err => console.error('❌ API Error - Parameters:', err)
    });
  }

  showExecutionPreview() {
    if (!this.selectedSchema || !this.selectedProcedure) {
      alert('Please select schema and procedure');
      return;
    }

    this.previewData = {
      schema: this.selectedSchema,
      procedure: this.selectedProcedure,
      parameters: this.parameters.map(p => ({
        name: p.parameterName,
        type: p.dataType,
        value: this.parameterValues[p.parameterName] || 'NULL'
      }))
    };

    this.showPreview = true;
    console.log('📋 Preview Data:', this.previewData);
  }

  closePreview() {
    this.showPreview = false;
  }

  executeProcedure() {
    if (!this.selectedSchema || !this.selectedProcedure) {
      alert('Please select a schema and procedure');
      return;
    }

    this.loading = true;
    this.executionError = null;

    // Convert parameters with proper types
    const parameters = this.parameters.reduce((acc, p) => {
      acc[p.parameterName] = this.convertParameterValue(
        this.parameterValues[p.parameterName], 
        p.dataType
      );
      return acc;
    }, {} as { [key: string]: any });

    console.log('🚀 Executing with converted parameters:', parameters);

    const request: ProcedureExecutionRequest = {
      parameters: parameters,
      useCache: this.useCache
    };

    this.procService.executeProcedure(this.selectedSchema, this.selectedProcedure, request).subscribe({
      next: (response) => {
        console.log('✅ Execution Response:', response);
        
        const executionData = response.data;
        
        this.executionResult = executionData;
        this.showPreview = false;
        
        // Set dataset records for charting
        this.datasetRecords = this.toRecordsFromResult(executionData);
        this.datasetColumns = executionData.columns || [];
        this.numericColumns = this.detectNumericColumns(this.datasetRecords);
        this.xColumn = this.datasetColumns[0] || '';
        this.yColumns = this.numericColumns.slice(0, 3);
        
        console.log('✅ Dataset prepared:', {
          columns: this.datasetColumns,
          records: this.datasetRecords.length,
          numericColumns: this.numericColumns
        });
        
        // Check if there's a pending dataset to save - pass converted parameters
        if (this.pendingDataset) {
          this.saveExecutionToDataset(parameters);
        }
        
        this.loading = false;
      },
      error: (err) => {
        console.error('❌ Execution Error:', err);
        this.executionError = err.error?.message || err.message || 'Failed to execute procedure';
        this.loading = false;
      }
    });
  }

  saveExecutionToDataset(parameters?: { [key: string]: any }) {
    if (!this.pendingDataset || !this.executionResult) {
      console.warn('⚠️ Cannot save dataset: missing pendingDataset or executionResult');
      return;
    }

    // Prepare parameters with proper type conversion
    const params = parameters || this.parameters.reduce((acc, p) => {
      const value = this.parameterValues[p.parameterName];
      
      // Convert to appropriate type based on SQL data type
      acc[p.parameterName] = this.convertParameterValue(value, p.dataType);
      
      return acc;
    }, {} as any);

    const request: ExecuteAndCreateDatasetRequest = {
      schema: this.selectedSchema!,
      procedure: this.selectedProcedure!,
      parameters: params,
      title: this.pendingDataset.title,
      description: this.pendingDataset.description
    };

    console.log('💾 Saving dataset with request:', JSON.stringify(request, null, 2));

    this.datasetService.executeProcedureAndCreateDataset(request).subscribe({
      next: (dataset) => {
        console.log('✅ Dataset created successfully:', dataset);
        
        // Clear pending dataset
        this.datasetStateService.clearPendingDataset();
        this.pendingDataset = null;
        
        alert(`Dataset "${dataset.dataSetTitle}" created successfully!`);
        
        if (confirm('Would you like to view your dataset?')) {
          this.router.navigate(['/datasets']);
        }
      },
      error: (err) => {
        console.error('❌ Failed to create dataset:', err);
        
        let errorMessage = 'Failed to save as dataset.';
        
        if (err.error) {
          if (typeof err.error === 'string') {
            errorMessage += '\n' + err.error;
          } else if (err.error.message) {
            errorMessage += '\n' + err.error.message;
          } else if (err.error.title) {
            errorMessage += '\n' + err.error.title;
          }
        }
        
        console.error('Full error details:', {
          status: err.status,
          statusText: err.statusText,
          error: err.error,
          message: err.message,
          url: err.url
        });
        
        alert(errorMessage);
      }
    });
  }

  // Add helper method to convert parameter values based on SQL type
  private convertParameterValue(value: any, dataType: string): any {
    // Handle null/undefined/empty
    if (value === null || value === undefined || value === '') {
      return null;
    }

    const type = dataType.toLowerCase();

    // Integer types
    if (type.includes('int') || type.includes('bigint') || type.includes('smallint') || type.includes('tinyint')) {
      const num = Number(value);
      return isNaN(num) ? null : num;
    }

    // Decimal/Numeric types
    if (type.includes('decimal') || type.includes('numeric') || type.includes('money') || 
        type.includes('float') || type.includes('real')) {
      const num = parseFloat(value);
      return isNaN(num) ? null : num;
    }

    // Boolean/Bit
    if (type.includes('bit') || type.includes('bool')) {
      if (typeof value === 'boolean') return value;
      if (typeof value === 'number') return value !== 0;
      const str = String(value).toLowerCase();
      return str === 'true' || str === '1' || str === 'yes';
    }

    // Date/DateTime types
    if (type.includes('date') || type.includes('time')) {
      // Return as ISO string for proper JSON serialization
      if (value instanceof Date) {
        return value.toISOString();
      }
      // Try to parse string to date
      const date = new Date(value);
      return isNaN(date.getTime()) ? value : date.toISOString();
    }

    // Default: return as string
    return String(value);
  }

  private toRecordsFromResult(result: ProcedureExecutionResponse): any[] {
    if (!result || !result.rows || !result.columns) {
      console.warn('⚠️ Invalid execution result for conversion to records');
      return [];
    }
    
    return result.rows.map(row => {
      const obj: any = {};
      result.columns.forEach((c, i) => {
        obj[c] = Array.isArray(row) ? row[i] : (row as any)[c];
      });
      return obj;
    });
  }

  private columnLooksNumeric(records: any[], col: string): boolean {
    let numeric = 0, total = 0;
    for (const r of records.slice(0, 100)) {
      const v = r?.[col];
      if (v === null || v === undefined || v === '') continue;
      total++;
      const n = typeof v === 'number' ? v : Number(v);
      if (!Number.isNaN(n)) numeric++;
    }
    // consider numeric if majority of sampled values parse to numbers
    return total > 0 && numeric / total >= 0.75;
  }

  renderChart() {
    if (!this.datasetRecords.length || !this.xColumn || !this.yColumns.length) {
      return;
    }

    const labels = this.datasetRecords.map(r => String(r?.[this.xColumn] ?? ''));

    // Build datasets
    const datasets =
      this.chartType === 'pie' || this.chartType === 'doughnut'
        ? [
            {
              label: this.yColumns[0] || 'Value',
              data: this.datasetRecords.map(r => Number(r?.[this.yColumns[0]] ?? 0)),
              backgroundColor: this.makeColors(labels.length),
            }
          ]
        : this.yColumns.map((y, i) => ({
            label: y,
            data: this.datasetRecords.map(r => Number(r?.[y] ?? 0)),
            backgroundColor: this.colorPalette(i, 0.5),
            borderColor: this.colorPalette(i, 1),
            borderWidth: 1,
            fill: false,
          }));

    const ctx = (document.getElementById('datasetChart') as HTMLCanvasElement)?.getContext('2d');
    if (!ctx) return;

    // Destroy previous chart to avoid duplicates
    this.chart?.destroy();
    this.chart = new Chart(ctx, {
      type: this.chartType,
      data: {
        labels,
        datasets
      },
      options: {
        responsive: true,
        plugins: {
          legend: { position: 'top' },
          title: { display: true, text: `${this.chartType.toUpperCase()} - ${this.xColumn}` }
        },
        scales: (this.chartType === 'pie' || this.chartType === 'doughnut')
          ? {}
          : {
              x: { ticks: { autoSkip: true, maxRotation: 45, minRotation: 0 } },
              y: { beginAtZero: true }
            }
      }
    });

    // Enable save button after chart is rendered
    this.isChartRendered = true;
  }

  private colorPalette(i: number, alpha = 1) {
    const base = [
      [54, 162, 235],
      [255, 99, 132],
      [255, 206, 86],
      [75, 192, 192],
      [153, 102, 255],
      [255, 159, 64],
      [99, 255, 132]
    ];
    const [r, g, b] = base[i % base.length];
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
  }

  private makeColors(n: number) {
    return Array.from({ length: n }, (_, i) => this.colorPalette(i, 0.7));
  }

  saveChartToDashboard() {
    if (!this.datasetRecords.length || !this.xColumn || !this.yColumns.length) {
      alert('Please configure and render a chart before saving.');
      return;
    }

    const chartName = prompt('Enter a name for this chart:', 
      `${this.selectedSchema}_${this.selectedProcedure}_Chart`);
    
    if (!chartName) return;

    const savedChart: SavedChart = {
      id: `${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
      name: chartName,
      schemaName: this.selectedSchema || '',
      procedureName: this.selectedProcedure || '',
      executedAt: new Date().toISOString(),
      chartType: this.chartType,
      xColumn: this.xColumn,
      yColumns: [...this.yColumns],
      datasetRecords: [...this.datasetRecords],
      datasetColumns: [...this.datasetColumns]
    };

    this.chartDataService.saveChart(savedChart);
    alert(`Chart "${chartName}" saved to dashboard!`);
  }
  
  onYColumnToggle(column: string, event: Event) {
    const checkbox = event.target as HTMLInputElement;
    if (checkbox.checked) {
      if (!this.yColumns.includes(column)) {
        this.yColumns = [...this.yColumns, column];
      }
    } else {
      this.yColumns = this.yColumns.filter(c => c !== column);
    }
  }

  ngOnDestroy() {
    // Cleanup chart data subscription
    if (this.chartDataSubscription) {
      this.chartDataSubscription.unsubscribe();
    }
    
    // Destroy chart instance
    if (this.chart) {
      this.chart.destroy();
    }
  }
}
