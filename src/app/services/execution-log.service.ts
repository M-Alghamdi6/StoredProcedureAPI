import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ProcedureExecutionParameter {
  executionId: number;
  parameterName: string;
  dataType: string;
  isOutput: boolean;
  isNullable: boolean;
  parameterValue?: string;
  outputValue?: string;
}

export interface ProcedureExecutionColumn {
  executionId: number;
  columnOrdinal: number;
  columnName: string;
  dataType: string;
  isNullable: boolean;
}

export interface ProcedureExecutionLog {
  id: number;
  executedAt: string;
  schemaName: string;
  procedureName: string;
  rowCount: number;
  durationMs: number;
  parameters: ProcedureExecutionParameter[];
  columns: ProcedureExecutionColumn[];
}

export interface JSONResponse<T> {
  statusCode: number;
  message?: string;
  data?: T;
  id?: number;
}

@Injectable({
  providedIn: 'root'
})
export class ExecutionLogService {
  private apiUrl = `${environment.apiUrl}/ExecutionLogs`;

  constructor(private http: HttpClient) {}

  getRecent(top: number = 50): Observable<JSONResponse<ProcedureExecutionLog[]>> {
    const params = new HttpParams().set('top', top.toString());
    return this.http.get<JSONResponse<ProcedureExecutionLog[]>>(`${this.apiUrl}/recent`, { params });
  }

  getById(id: number): Observable<JSONResponse<ProcedureExecutionLog>> {
    return this.http.get<JSONResponse<ProcedureExecutionLog>>(`${this.apiUrl}/${id}`);
  }

  query(
    schemaName?: string,
    procedureName?: string,
    fromUtc?: Date,
    toUtc?: Date,
    top: number = 100
  ): Observable<JSONResponse<ProcedureExecutionLog[]>> {
    let params = new HttpParams().set('top', top.toString());
    
    if (schemaName) params = params.set('schemaName', schemaName);
    if (procedureName) params = params.set('procedureName', procedureName);
    if (fromUtc) params = params.set('fromUtc', fromUtc.toISOString());
    if (toUtc) params = params.set('toUtc', toUtc.toISOString());

    return this.http.get<JSONResponse<ProcedureExecutionLog[]>>(`${this.apiUrl}/query`, { params });
  }

  getLatest(): Observable<JSONResponse<ProcedureExecutionLog>> {
    return this.http.get<JSONResponse<ProcedureExecutionLog>>(`${this.apiUrl}/latest`);
  }
}