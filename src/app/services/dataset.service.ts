import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, BehaviorSubject } from 'rxjs';
import { environment } from '../../environments/environment';

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

export interface ExecuteAndCreateDatasetRequest {
  schema: string;
  procedure: string;
  parameters?: { [key: string]: any };
  title: string;
  description?: string;
}

export interface CreateFromProcedureRequest {
  procedureExecutionId: number;
  title: string;
  description?: string;
}

export interface CreateFromBuilderRequest {
  builderId: number;
  title: string;
  description?: string;
}

export interface CreateFromInlineRequest {
  inlineId: number;
  title: string;
  description?: string;
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

export interface PendingDataset {
  schema: string;
  procedure: string;
  parameters?: { [key: string]: any };
  columns?: DatasetColumn[];
}

@Injectable({
  providedIn: 'root'
})
export class DatasetService {
  private apiUrl = `${environment.apiUrl}/Datasets`;

  constructor(private http: HttpClient) {}

  /**
   * Execute a stored procedure and create a dataset from the result
   */
  executeProcedureAndCreateDataset(request: ExecuteAndCreateDatasetRequest): Observable<Dataset> {
    console.log('📤 Sending request to API:', JSON.stringify(request, null, 2));
    return this.http.post<Dataset>(`${this.apiUrl}/procedure-execute`, request);
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
   * Create dataset from procedure execution
   */
  createFromProcedure(request: CreateFromProcedureRequest): Observable<Dataset> {
    return this.http.post<Dataset>(`${this.apiUrl}/procedure`, request);
  }

  /**
   * Create dataset from builder
   */
  createFromBuilder(request: CreateFromBuilderRequest): Observable<Dataset> {
    return this.http.post<Dataset>(`${this.apiUrl}/builder`, request);
  }

  /**
   * Create dataset from inline
   */
  createFromInline(request: CreateFromInlineRequest): Observable<Dataset> {
    return this.http.post<Dataset>(`${this.apiUrl}/inline`, request);
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
