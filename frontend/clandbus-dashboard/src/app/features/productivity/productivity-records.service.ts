import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ProfessionalRecord, ProfessionalRecordInput } from './productivity.models';
@Injectable({providedIn:'root'})
export class ProductivityRecordsService {
  private readonly url='https://localhost:7004/api/Productivity/records';
  constructor(private readonly http:HttpClient){}
  list(from?:string,to?:string){let params=new HttpParams();if(from)params=params.set('from',from);if(to)params=params.set('to',to);return this.http.get<ProfessionalRecord[]>(this.url,{params,withCredentials:true});}
  create(input:ProfessionalRecordInput){return this.http.post<ProfessionalRecord>(this.url,input,{withCredentials:true});}
  update(id:number,input:ProfessionalRecordInput){return this.http.put<ProfessionalRecord>(`${this.url}/${id}`,input,{withCredentials:true});}
  remove(id:number){return this.http.delete<void>(`${this.url}/${id}`,{withCredentials:true});}
}
