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
  // View states
  currentView: 'list' | 'create' | 'detail' = 'list';
  
  // Data
  datasets: Dataset[] = [];
  selectedDataset: Dataset | null = null;
  
  // Loading & Error
  loading = false;
  error: string | null = null;

  // Create Form
  newDataset: PendingDataset = {
    title: '',
    description: '',
    sourceType: 3 // Default to Stored Procedure
  };

  constructor(
    private datasetService: DatasetService,
    private datasetStateService: DatasetStateService,
    private router: Router
  ) {}

  ngOnInit() {
    this.loadDatasets();
  }

  // Load Data
  loadDatasets() {
    this.loading = true;
    this.error = null;
    
    this.datasetService.getRecent(50).subscribe({
      next: (datasets) => {
        this.datasets = datasets || [];
        this.loading = false;
      },
      error: (err) => {
        this.error = err.message || 'Failed to load datasets';
        this.loading = false;
      }
    });
  }

  // Navigation
  showCreateForm() {
    this.currentView = 'create';
    this.newDataset = {
      title: '',
      description: '',
      sourceType: 3
    };
  }

  backToList() {
    this.currentView = 'list';
    this.selectedDataset = null;
  }

  viewDataset(dataset: Dataset) {
    this.selectedDataset = dataset;
    this.currentView = 'detail';
  }

  // Create Dataset - Just sets up the pending dataset and redirects
  createDataset() {
    if (!this.newDataset.title) {
      this.error = 'Dataset title is required';
      return;
    }

    // Store the pending dataset
    this.datasetStateService.setPendingDataset(this.newDataset);

    // Redirect to appropriate page based on source type
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
    }
  }

  // Delete
  deleteDataset(dataset: Dataset) {
    if (!confirm(`Are you sure you want to delete "${dataset.dataSetTitle}"?`)) {
      return;
    }

    this.loading = true;
    this.error = null;

    this.datasetService.deleteDataset(dataset.dataSetId).subscribe({
      next: () => {
        this.loadDatasets();
        if (this.selectedDataset?.dataSetId === dataset.dataSetId) {
          this.backToList();
        }
        this.loading = false;
      },
      error: (err) => {
        this.error = err.message || 'Failed to delete dataset';
        this.loading = false;
      }
    });
  }

  // Helpers
  getSourceFlagLabel(sourceType: number): string {
    const labels: { [key: number]: string } = {
      1: 'Query Builder',
      2: 'Inline Query',
      3: 'Stored Procedure'
    };
    return labels[sourceType] || 'Unknown';
  }

  formatDate(date: string | Date): string {
    return new Date(date).toLocaleString();
  }
}