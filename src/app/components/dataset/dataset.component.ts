import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { DatasetService, Dataset, DatasetColumn } from '../../services/dataset.service';
import { DatasetStateService, PendingDataset } from '../../services/dataset-state.service';

@Component({
  selector: 'app-dataset',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule],
  templateUrl: './dataset.component.html',
  styleUrls: ['./dataset.component.scss']
})
export class DatasetComponent implements OnInit {
  datasets: Dataset[] = [];
  selectedDataset: Dataset | null = null;
  columns: DatasetColumn[] = [];
  loading = false;
  error: string | null = null;
  currentView: 'list' | 'create' | 'detail' = 'list';
  
  newDataset = {
    title: '',
    description: '',
    sourceType: 3 // Default to Stored Procedure
  };

  constructor(
    private datasetService: DatasetService,
    private datasetStateService: DatasetStateService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadRecentDatasets();
  }

  loadRecentDatasets(): void {
    this.loading = true;
    this.error = null;
    
    this.datasetService.getRecent(50).subscribe({
      next: (response: any) => {
        console.log('✅ Datasets loaded:', response);
        // Extract the data array from the response wrapper
        const datasets = response?.data || response;
        this.datasets = Array.isArray(datasets) ? datasets : [];
        console.log('📊 Processed datasets:', this.datasets);
        this.loading = false;
      },
      error: (err) => {
        console.error('❌ Failed to load datasets:', err);
        this.error = 'Failed to load datasets';
        this.datasets = [];
        this.loading = false;
      }
    });
  }

  selectDataset(dataset: Dataset): void {
    this.selectedDataset = dataset;
    this.columns = Array.isArray(dataset.columns) ? dataset.columns : [];
    this.currentView = 'detail';
  }

  showCreateForm(): void {
    this.currentView = 'create';
    this.newDataset = {
      title: '',
      description: '',
      sourceType: 3
    };
  }

  backToList(): void {
    this.currentView = 'list';
    this.selectedDataset = null;
    this.columns = [];
  }

  createDataset(): void {
    if (!this.newDataset.title) {
      this.error = 'Title is required';
      return;
    }

    // Store the pending dataset information
    const pendingDataset: PendingDataset = {
      title: this.newDataset.title,
      description: this.newDataset.description,
      sourceType: this.newDataset.sourceType
    };

    this.datasetStateService.setPendingDataset(pendingDataset);

    // Navigate to the appropriate page based on source type
    switch (this.newDataset.sourceType) {
      case 1: // Query Builder
        this.router.navigate(['/query-builder']);
        break;
      case 2: // Inline Query
        this.router.navigate(['/inline-query']);
        break;
      case 3: // Stored Procedure
        this.router.navigate(['/procedures']);
        break;
      default:
        this.error = 'Invalid source type';
    }
  }

  getSourceFlagLabel(sourceType: number): string {
    switch (sourceType) {
      case 1: return 'Query Builder';
      case 2: return 'Inline Query';
      case 3: return 'Stored Procedure';
      default: return 'Unknown';
    }
  }

  formatDate(dateString: string): string {
    if (!dateString) return 'N/A';
    const date = new Date(dateString);
    return date.toLocaleDateString() + ' ' + date.toLocaleTimeString();
  }

  deleteDataset(datasetId: number): void {
    if (!confirm('Are you sure you want to delete this dataset?')) {
      return;
    }

    this.datasetService.deleteDataset(datasetId).subscribe({
      next: () => {
        console.log('✅ Dataset deleted');
        this.loadRecentDatasets();
        if (this.selectedDataset?.dataSetId === datasetId) {
          this.selectedDataset = null;
          this.columns = [];
        }
      },
      error: (err) => {
        console.error('❌ Failed to delete dataset:', err);
        this.error = 'Failed to delete dataset';
      }
    });
  }
}