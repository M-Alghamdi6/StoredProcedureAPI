import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
export interface ApiResponse<T> {
  statusCode: number;
  message: string | null;
  data: T;
  id: string | null;
}

export interface SchemaModel {
  schemaName: string;
}

export interface StoredProcedure {
  name: string;
}

export interface ProcedureParameter {
  parameterName: string;
  dataType: string;
  isOutput: boolean;
  isNullable: boolean;
  maxLength?: number;
}

export interface ProcedureExecutionRequest {
  parameters: { [key: string]: any };
  useCache?: boolean;
}

export interface ProcedureExecutionResponse {
  columns: string[];
  rows: any[][];
  rowCount: number;
}

@Injectable({
  providedIn: 'root'
})
export class ProcedureService {
   private apiUrl = `${environment.apiUrl}/Procedures`;

  constructor(private http: HttpClient) { }

  getSchemas(): Observable<ApiResponse<SchemaModel[]>> {
    return this.http.get<ApiResponse<SchemaModel[]>>(`${this.apiUrl}/schemas`);
  }

  getProcedures(schema: string): Observable<ApiResponse<StoredProcedure[]>> {
    return this.http.get<ApiResponse<StoredProcedure[]>>(`${this.apiUrl}/${schema}/procedures`);
  }

  getParameters(schema: string, procedure: string): Observable<ApiResponse<ProcedureParameter[]>> {
    return this.http.get<ApiResponse<ProcedureParameter[]>>(`${this.apiUrl}/${schema}/${procedure}/parameters`);
  }

  executeProcedure(
    schema: string,
    procedure: string,
    request: ProcedureExecutionRequest
  ): Observable<ApiResponse<ProcedureExecutionResponse>> {
    return this.http.post<ApiResponse<ProcedureExecutionResponse>>(
      `${this.apiUrl}/${schema}/${procedure}/execute`,
      request
    );
  }
}