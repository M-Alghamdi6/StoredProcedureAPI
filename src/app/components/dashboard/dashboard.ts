import { Component, OnInit, AfterViewInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { ChartDataService, SavedChart } from '../../services/chart-data.service';
import Chart from 'chart.js/auto';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './dashboard.html',
  styleUrls: ['./dashboard.scss']
})
export class DashboardComponent implements OnInit, AfterViewInit, OnDestroy {
  savedCharts: SavedChart[] = [];
  selectedChart: SavedChart | null = null;
  private chart?: Chart;

  constructor(private chartDataService: ChartDataService) {}

  ngOnInit() {
    this.loadSavedCharts();
  }

  ngAfterViewInit() {
    // Chart will be rendered when a chart is selected
  }

  loadSavedCharts() {
    this.savedCharts = this.chartDataService.getSavedCharts();
    console.log('📊 Loaded saved charts:', this.savedCharts);
    
    // Log each chart to verify data
    this.savedCharts.forEach((chart, index) => {
      console.log(`Chart ${index}:`, {
        name: chart.name,
        schema: chart.schemaName,
        procedure: chart.procedureName,
        chartType: chart.chartType,
        records: chart.datasetRecords?.length
      });
    });
    
    // Auto-select first chart if available
    if (this.savedCharts.length > 0 && !this.selectedChart) {
      setTimeout(() => {
        this.loadSavedChart(this.savedCharts[0]);
      }, 100);
    }
  }

  loadSavedChart(chart: SavedChart) {
    if (!chart) return;
    
    this.selectedChart = chart;
    console.log('Loading chart:', {
      name: chart.name,
      schema: chart.schemaName,
      procedure: chart.procedureName,
      xColumn: chart.xColumn,
      yColumns: chart.yColumns,
      recordCount: chart.datasetRecords?.length
    });
    
    // Wait for DOM to update
    setTimeout(() => {
      this.renderChart();
    }, 100);
  }

  deleteSavedChart(chartId: string, event: Event) {
    event.stopPropagation();
    
    const confirmed = confirm('Are you sure you want to delete this chart?');
    if (!confirmed) return;

    this.chartDataService.deleteChart(chartId);
    
    // If deleted chart was selected, clear selection
    if (this.selectedChart?.id === chartId) {
      this.selectedChart = null;
      this.chart?.destroy();
      this.chart = undefined;
    }
    
    this.loadSavedCharts();
  }

  private renderChart() {
    if (!this.selectedChart || !this.selectedChart.datasetRecords || this.selectedChart.datasetRecords.length === 0) {
      console.warn('No chart data to render');
      return;
    }

    const canvas = document.getElementById('dashboardChart') as HTMLCanvasElement;
    if (!canvas) {
      console.error('Canvas element not found');
      return;
    }

    const ctx = canvas.getContext('2d');
    if (!ctx) {
      console.error('Could not get canvas context');
      return;
    }

    // Destroy previous chart
    if (this.chart) {
      this.chart.destroy();
    }

    const { xColumn, yColumns, chartType, datasetRecords } = this.selectedChart;

    if (!xColumn || !yColumns || yColumns.length === 0) {
      console.warn('Missing chart configuration');
      return;
    }

    const labels = datasetRecords.map(r => String(r?.[xColumn] ?? ''));

    const datasets = (chartType === 'pie' || chartType === 'doughnut')
      ? [
          {
            label: yColumns[0] || 'Value',
            data: datasetRecords.map(r => Number(r?.[yColumns[0]] ?? 0)),
            backgroundColor: this.makeColors(labels.length),
          }
        ]
      : yColumns.map((y, i) => ({
          label: y,
          data: datasetRecords.map(r => Number(r?.[y] ?? 0)),
          backgroundColor: this.colorPalette(i, 0.5),
          borderColor: this.colorPalette(i, 1),
          borderWidth: 2,
          fill: chartType === 'line' ? false : true,
          tension: chartType === 'line' ? 0.4 : 0,
        }));

    this.chart = new Chart(ctx, {
      type: chartType,
      data: { labels, datasets },
      options: {
        responsive: true,
        maintainAspectRatio: true,
        plugins: {
          legend: { 
            position: 'top',
            labels: {
              font: { size: 12 }
            }
          },
          title: { 
            display: true, 
            text: this.selectedChart.name,
            font: { size: 16, weight: 'bold' }
          }
        },
        scales: (chartType === 'pie' || chartType === 'doughnut')
          ? undefined
          : {
              x: { 
                ticks: { 
                  autoSkip: true, 
                  maxRotation: 45, 
                  minRotation: 0 
                } 
              },
              y: { 
                beginAtZero: true,
                ticks: {
                  callback: function(value) {
                    return Number(value).toLocaleString();
                  }
                }
              }
            }
      }
    });

    console.log('Chart rendered successfully');
  }

  private colorPalette(i: number, alpha = 1): string {
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

  private makeColors(n: number): string[] {
    return Array.from({ length: n }, (_, i) => this.colorPalette(i, 0.7));
  }

  ngOnDestroy() {
    if (this.chart) {
      this.chart.destroy();
    }
  }
}