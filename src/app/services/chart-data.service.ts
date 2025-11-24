import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

export interface ChartData {
  schema: string;
  procedure: string;
  data: any[];
}

export interface SavedChart {
  id: string;
  name: string;
  schemaName: string;
  procedureName: string;
  executedAt: string;
  chartType: 'bar' | 'line' | 'pie' | 'doughnut';
  xColumn: string;
  yColumns: string[];
  datasetRecords: any[];
  datasetColumns: string[];
}

@Injectable({
  providedIn: 'root'
})
export class ChartDataService {
  private chartDataSubject = new BehaviorSubject<ChartData | null>(null);
  public chartData$ = this.chartDataSubject.asObservable();

  private savedChartsKey = 'saved_charts';

  constructor() {}

  setChartData(chartData: ChartData): void {
    this.chartDataSubject.next(chartData);
    console.log('Chart data set:', chartData);
  }

  getChartData(): ChartData | null {
    return this.chartDataSubject.value;
  }

  clearChartData(): void {
    this.chartDataSubject.next(null);
  }

  hasChartData(): boolean {
    return this.chartDataSubject.value !== null;
  }

  // Saved charts management
  saveChart(chart: SavedChart): void {
    const charts = this.getSavedCharts();
    charts.push(chart);
    localStorage.setItem(this.savedChartsKey, JSON.stringify(charts));
    console.log('💾 Chart saved to dashboard:', chart.name);
  }

  getSavedCharts(): SavedChart[] {
    try {
      const chartsJson = localStorage.getItem(this.savedChartsKey);
      if (!chartsJson) return [];
      
      const charts = JSON.parse(chartsJson);
      return Array.isArray(charts) ? charts : [];
    } catch (error) {
      console.error('❌ Error loading saved charts:', error);
      return [];
    }
  }

  deleteChart(chartId: string): void {
    const charts = this.getSavedCharts();
    const filteredCharts = charts.filter(c => c.id !== chartId);
    localStorage.setItem(this.savedChartsKey, JSON.stringify(filteredCharts));
    console.log('🗑️ Chart deleted:', chartId);
  }

  clearAllCharts(): void {
    localStorage.removeItem(this.savedChartsKey);
    console.log('🗑️ All charts cleared');
  }
}