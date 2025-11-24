import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, BehaviorSubject } from 'rxjs';
import { environment } from '../../environments/environment';
import { PendingDataset } from './dataset-state.service';
// Removed import for ./dataset.model because Dataset interface is defined below

export interface Dataset {
  dataSetId: number;
  dataSetTitle: string;
  description?: string;
  sourceType: number;  // 1=QueryBuilder, 2=INLINEQuery, 3=Stored Procedure
  builderId?: number;
  inlineId?: number;
  procedureExecutionId?: number;
  createdAt: string;
  modifiedAt: string;
  columns: DatasetColumn[];
}

export interface DatasetColumn {
  columnId: number;
  dataSetId: number;
  columnName: string;
  dataType: string;
  columnOrder: number;
}

export interface SourceFlag {
  sourceType: number;
  sourceName: string;
}

export interface CreateDatasetUnifiedRequest {
  sourceType: number;          // 1=Builder, 2=Inline, 3=Procedure
  title: string;
  description?: string;
  data: DatasetSourceData;     // holds one of the nested DTOs
}

export interface DatasetSourceData {
  builder?: BuilderSourceDto;
  inline?: InlineSourceDto;
  procedure?: ProcedureSourceDto;
}

export interface BuilderSourceDto {
  builderId: number;
  columns?: DatasetColumn[];
}

export interface InlineSourceDto {
  inlineId: number;
  columns?: DatasetColumn[];
}

export interface ProcedureSourceDto {
  procedureExecutionId: number;
}

export interface UpdateDatasetRequest {
  title: string;
  description?: string;
}

export interface ReplaceColumnsRequest {
  columns: ReplaceColumnItem[];
}

export interface ReplaceColumnItem {
  columnName: string;
  dataType: string;
}

// API Response wrapper (if your backend uses this)
export interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
}

export interface CreateFromProcedureRequest {
  procedureExecutionId: number;
  title: string;
  description?: string;
}

@Injectable({
  providedIn: 'root'
})
export class DatasetService {
  private apiUrl = `${environment.apiUrl}/Datasets`;

  constructor(private http: HttpClient) {}

  /**
   * Create a dataset using the unified create endpoint
   */
  createUnifiedDataset(request: CreateDatasetUnifiedRequest): Observable<Dataset> {
    console.log('📤 Sending request to API:', JSON.stringify(request, null, 2));
    return this.http.post<Dataset>(this.apiUrl, request);
  }

  /**
   * Get a dataset by ID
   */
  getDataset(id: number): Observable<Dataset> {
    return this.http.get<Dataset>(`${this.apiUrl}/${id}`);
  }

  /**
   * Get recent datasets
   */
  getRecent(top: number = 50): Observable<Dataset[]> {
    const params = new HttpParams().set('top', top.toString());
    return this.http.get<Dataset[]>(`${this.apiUrl}/recent`, { params });
  }

  /**
   * Get all source flags
   */
  getSourceFlags(): Observable<SourceFlag[]> {
    return this.http.get<SourceFlag[]>(`${this.apiUrl}/source-flags`);
  }

  /**
   * Update a dataset
   */
  updateDataset(id: number, request: UpdateDatasetRequest): Observable<Dataset> {
    return this.http.put<Dataset>(`${this.apiUrl}/${id}`, request);
  }

  /**
   * Replace columns for a dataset
   */
  replaceColumns(id: number, request: ReplaceColumnsRequest): Observable<Dataset> {
    return this.http.put<Dataset>(`${this.apiUrl}/${id}/columns`, request);
  }

  /**
   * Delete a dataset (if delete endpoint exists)
   */
  deleteDataset(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  /**
   * Create a dataset from a procedure
   */
 
}

@Injectable({
  providedIn: 'root'
})
export class DatasetStateService {
  private pendingDatasetSubject = new BehaviorSubject<PendingDataset | null>(null);
  public pendingDataset$ = this.pendingDatasetSubject.asObservable();

  setPendingDataset(dataset: PendingDataset) {
    this.pendingDatasetSubject.next(dataset);
    // Store in session storage in case of page refresh
    sessionStorage.setItem('pendingDataset', JSON.stringify(dataset));
  }

  getPendingDataset(): PendingDataset | null {
    const pending = this.pendingDatasetSubject.value;
    if (pending) return pending;
    
    // Try to restore from session storage
    const stored = sessionStorage.getItem('pendingDataset');
    if (stored) {
      const parsed = JSON.parse(stored);
      this.pendingDatasetSubject.next(parsed);
      return parsed;
    }
    
    return null;
  }

  clearPendingDataset() {
    this.pendingDatasetSubject.next(null);
    sessionStorage.removeItem('pendingDataset');
  }
}
