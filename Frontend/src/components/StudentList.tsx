import React, { useState, useMemo } from 'react';
import { useChat } from '../context/ChatContext';
import type { Student } from '../context/ChatContext';
import { GlobalSearchPanel } from './GlobalSearchPanel';
import DOMPurify from 'dompurify';
import './StudentList.css';

import iconAlert from '../assets/iconalert.png';
import iconfilter from '../assets/iconfilter.png';
import iconNewMessage from '../assets/iconnewmessage.png';
import iconList from '../assets/iconList.png';

interface StudentListProps {
    onStudentClick: (student: Student) => void;
    onAdminClick?: () => void;
}

const StudentList: React.FC<StudentListProps> = ({ onStudentClick, onAdminClick }) => {
    const { students, selectStudent, selectedStudent } = useChat();
    const [showElevated, setShowElevated] = useState(true);
    const [showNormal, setShowNormal] = useState(true);
    const { MentorType } = useChat();

    const getInitials = (name: string) => {
        return name.split(' ').map(n => n[0]).join('').slice(0, 2).toUpperCase();
    };

    const getLastMessage = (student: Student): string => {
        if (!student.lastMessageContent) return '';
        
        // Limpiar HTML y limitar a 50 caracteres
        const cleaned = student.lastMessageContent
            .replace(/<[^>]+>/g, '')  // Quitar tags HTML
            .replace(/\s+/g, ' ')     // Normalizar espacios
            .trim();
        
        return cleaned.length > 50 
            ? cleaned.slice(0, 50) + '...' 
            : cleaned;
    };

    const getDate = (date: string) => {
        if (!date) return '';
        const d = new Date(date);
        return `${d.getDate()}/${d.getMonth() + 1}`;
    };

    // ✅ Separar y ordenar estudiantes (con useMemo para optimizar)
    const { elevated, normal } = useMemo(() => {
        const sortByDate = (a: Student, b: Student) => {
            const dateA = a.lastMessageDate ?? '';
            const dateB = b.lastMessageDate ?? '';
            return dateB.localeCompare(dateA);
        };

        const elevatedStudents = students
            // IA apagada → elevado a mentor
            .filter(s => s.elevated === true)
            .sort(sortByDate);

        const normalStudents = students
            // IA prendida 
            .filter(s => s.elevated === false)
            .sort(sortByDate);

        return {
            elevated: elevatedStudents,
            normal: normalStudents,
        };
    }, [students]);

    const renderList = (list: typeof students) => (
        list.map(student => (
            <div
                key={student.id}
                className={`student-item ${selectedStudent?.id === student.id ? 'selected' : ''}`}
                onClick={() => {
                    selectStudent(student);
                    onStudentClick?.(student);
                }}
            >
                <div>
                    {/* ✅ Mostrar alerta solo si está ELEVADO */}
                    {student.elevated==false && (
                        <img src={iconAlert} className="alert-icon" alt="Alerta" />
                    )}                    
                </div>
                <div className="avatar">
                    {student.photo ? (
                        <img src={student.photo} alt={student.name} />
                    ) : (
                        <div className="initials">{getInitials(student.name)}</div>
                    )}
                </div>

                <div className="details">
                    <div className="name-row">
                        <strong>{student.name}</strong>
                        <span className="date">{getDate(student.lastMessageDate)}</span>
                    </div>
                    <div
                        className={`preview ${!student.read ? 'unread' : ''}`}
                        dangerouslySetInnerHTML={{
                            __html: DOMPurify.sanitize(getLastMessage(student)),
                        }}
                    ></div>
                </div>
            </div>
        ))
    );

    return (
        <div className="student-list">
            <div className="headerSidebar">
                <h2>Mentores Verdes I.A.</h2>
                <div className="sidebar-actions">
                    <button className="filter">
                        <img src={iconfilter} alt="Filtrar" />
                    </button>
                    <button className="newMessage">
                        <img src={iconNewMessage} alt="Nuevo mensaje" />
                    </button>
                </div>
            </div>

            <div className="SidelbarPanel">
                <div className="GlobalPanel">
                    <GlobalSearchPanel />
                </div>

                {(MentorType === 'Admin' || MentorType === 'Semi Senior' || MentorType === 'Senior') && (
                    <div className="section seguimiento-section" onClick={onAdminClick}>
                        <div className="student-item">
                            <div className="avatar">
                                <img src={iconList} alt="Seguimiento" />
                            </div>
                            <div className="details">
                                <div className="name-row">
                                    <strong>Seguimiento de mentores</strong>
                                </div>
                                <div className="preview">Resumen de respuestas</div>
                            </div>
                        </div>
                    </div>
                )}
                
                <div className="sidebarList">
                    {/* ✅ SECCIÓN 1: Normales (sin alerta) */}
                    <div className="section">
                        <h4 onClick={() => setShowNormal(!showNormal)}>
                            ▸ Estudiantes elevados a mentores ({normal.length})
                        </h4>
                        {showNormal && renderList(normal)}
                    </div>
                    {/* ✅ SECCIÓN 2: Elevados (con alerta roja) */}
                    <div className="section">
                        <h4 onClick={() => setShowElevated(!showElevated)}>
                            ▸ Estudiantes ({elevated.length})
                        </h4>
                        {showElevated && renderList(elevated)}
                    </div>

                    
                </div>
            </div>
        </div>
    );
};

export default StudentList;