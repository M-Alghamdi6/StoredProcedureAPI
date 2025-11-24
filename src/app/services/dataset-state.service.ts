import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

export interface PendingDataset {
  title: string;
  description?: string;
  sourceType: number; // 1=Builder, 2=Inline, 3=Procedure
}

@Injectable({
  providedIn: 'root'
})
export class DatasetStateService {
  private pendingDatasetSubject = new BehaviorSubject<PendingDataset | null>(null);
  public pendingDataset$: Observable<PendingDataset | null> = this.pendingDatasetSubject.asObservable();

  constructor() {
    // Try to restore from session storage on service initialization
    this.restoreFromStorage();
  }

  /**
   * Set a pending dataset that will be saved after execution
   */
  setPendingDataset(dataset: PendingDataset): void {
    this.pendingDatasetSubject.next(dataset);
    // Store in session storage in case of page refresh
    sessionStorage.setItem('pendingDataset', JSON.stringify(dataset));
    console.log('✅ Pending dataset set:', dataset);
  }

  /**
   * Get the current pending dataset
   */
  getPendingDataset(): PendingDataset | null {
    return this.pendingDatasetSubject.value;
  }

  /**
   * Clear the pending dataset
   */
  clearPendingDataset(): void {
    this.pendingDatasetSubject.next(null);
    sessionStorage.removeItem('pendingDataset');
    console.log('🗑️ Pending dataset cleared');
  }

  /**
   * Check if there's a pending dataset
   */
  hasPendingDataset(): boolean {
    return this.pendingDatasetSubject.value !== null;
  }

  /**
   * Restore pending dataset from session storage
   */
  private restoreFromStorage(): void {
    const stored = sessionStorage.getItem('pendingDataset');
    if (stored) {
      try {
        const parsed = JSON.parse(stored) as PendingDataset;
        this.pendingDatasetSubject.next(parsed);
        console.log('🔄 Restored pending dataset from storage:', parsed);
      } catch (error) {
        console.error('Failed to parse pending dataset from storage:', error);
        sessionStorage.removeItem('pendingDataset');
      }
    }
  }

  /**
   * Get source type label
   */
  getSourceTypeLabel(sourceType: number): string {
    const labels: { [key: number]: string } = {
      1: 'Query Builder',
      2: 'Inline Query',
      3: 'Stored Procedure'
    };
    return labels[sourceType] || 'Unknown';
  }
}