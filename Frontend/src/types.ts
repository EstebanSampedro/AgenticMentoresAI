import type { ReactNode } from "react";

export interface Mensaje {
    [x: string]: ReactNode;
    remitente: "estudiante" | "ia" | "mentor";
    texto: string;
    hora: string;
}

export interface Estudiante {
    id: number;
    nombre: string;
    email: string;
    elevada: boolean;
    fecha: string;
    mensaje: string;
    conversacion: Mensaje[];
    resumen: string;
}
export interface Student {
    id: string;
    name: string;
    email: string;
    elevated: boolean;
    photo: string;
    lastMessageDate: string;
}

export interface Message {
    id: string;
    studentId: string;
    sender: 'IA' | 'mentor' | 'student';
    content: string;
    timestamp: string;
    type: 'text' | 'image' | 'document' | 'voice';
}
export interface Resume {
    id: number;
    resumeContent: string;
    Date: string;
}

// shared/types.ts
export type PreviewType = 'image' | 'document' | 'video' | 'audio' | 'generic';

export interface FilePreviewInfo {
  fileName: string;
  fileSize: number;
  mimeType: string;
  previewType: PreviewType;
  thumbnailUrl?: string;
  downloadUrl?: string;
  officeOnlineUrl?: string; // sólo si aplica (doc)
  width?: number;
  height?: number;
}