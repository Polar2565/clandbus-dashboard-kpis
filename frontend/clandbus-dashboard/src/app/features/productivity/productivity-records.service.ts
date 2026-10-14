import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ProfessionalRecord, ProfessionalRecordInput } from './productivity.models';
@Injectable({providedIn:'root'})
export class ProductivityRecordsService {
  private readonly url='https://localhost:7004/api/Productivity/records';
  constructor(private readonly http:HttpClient){}
  private params(owner:string,from?:string,to?:string){let params=new HttpParams();if(owner)params=params.set('owner',owner);if(from)params=params.set('from',from);if(to)params=params.set('to',to);return params;}
  list(owner:string,from?:string,to?:string){return this.http.get<ProfessionalRecord[]>(this.url,{params:this.params(owner,from,to),withCredentials:true});}
  create(owner:string,input:ProfessionalRecordInput){return this.http.post<ProfessionalRecord>(this.url,input,{params:this.params(owner),withCredentials:true});}
  update(owner:string,id:number,input:ProfessionalRecordInput){return this.http.put<ProfessionalRecord>(`${this.url}/${id}`,input,{params:this.params(owner),withCredentials:true});}
  remove(owner:string,id:number){return this.http.delete<void>(`${this.url}/${id}`,{params:this.params(owner),withCredentials:true});}
}
