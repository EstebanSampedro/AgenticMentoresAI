import React, { createContext, useContext, useState, useEffect, useRef, useCallback } from 'react';
import type { ReactNode } from 'react';
import * as microsoftTeams from '@microsoft/teams-js';
import * as signalR from '@microsoft/signalr';
import { useErrorQueue } from '../hooks/useErrorQueue';
import { logger } from '../utils/logger';

import { processIncomingMessage, mapApiMessageToMessage } from '../utils/messageUtils';

const API_BASE = import.meta.env.VITE_API_BASE;
const API_SignalR = import.meta.env.VITE_API_SignalR;

// Tipos
export interface Student {
    [x: string]: any;
    id: string;
    name: string;
    email: string;
    elevated: boolean;
    photo: string;
    lastMessageDate: string;
    chatId: string;
    read?: boolean;
    escalatedReason?: string;
    lastMessageContent?: string;
}
export interface Message {
    id: number;
    chatId: string;
    sender: 'IA' | 'mentor' | 'student';
    content: string;
    timestamp: string;
    type: 'text' | 'image' | 'document' | 'voice' | 'html' | 'video' | 'audio' | 'generic';
    //para recibir archivos no validado aun 
    attachments?: {
        name: string;
        contentUrl: string;
        contentType: string;
    }[];
}

export interface FilePreviewInfo {
    fileName: string;
    fileSize: number;
    mimeType: string;
    previewType: Message['type'];
    thumbnailUrl?: string;
    downloadUrl?: string;
    officeOnlineUrl?: string;
    width?: number;
    height?: number;
}

type ResumeItem = {
    id: number | string;
    chatId: string;
    summary: string;
    keyPoints: string[];
    summaryType: string;
    escalated: boolean;
    escalationReason?: string | null;
    createdAt: string;
    createdBy: string;
};

// Context
interface ChatContextType {
    students: Student[];
    selectedStudent: Student | null;
    selectStudent: (student: Student) => void;
    messages: Message[];
    resumes: ResumeItem[];
    fetchResumesFromApi: (chatId: string) => Promise<ResumeItem[]>;
    generateSummary: (chatId: string) => Promise<void>;
    isSummaryLoading: boolean;
    sendMessage: (message: Message) => void;
    updateStudent: (student: Student) => void;
    updateAIState: (chatId: string, state: boolean, reason: string) => void;
    mentorEmail: string | null;
    mensaje: string;
    fetchMoreMessages: () => Promise<void>;
    fetchMessageContext: (chatId: string, messageId: number) => Promise<void>;
    fetchNewerMessages: () => Promise<void>;
    isFetching: boolean;
    nextPageUrl: string | null;
    prevPageUrl: string | null;
    searchMessagesByMentor: (query: string) => Promise<any[]>;
    MentorType: 'Admin' | 'Senior' | 'Semi Senior' | 'Junior' | null;
    isAuthLoading: boolean;
    sendTextMessage: (chatId: string, text: string) => Promise<Message | null>;
    shouldScrollToBottom: boolean;
    setShouldScrollToBottom: React.Dispatch<React.SetStateAction<boolean>>;
    triggerScrollToBottom: () => void;
    highlightedMessageId: number | null;
    setHighlightedMessageId: React.Dispatch<React.SetStateAction<number | null>>;
    isMobile: boolean;
    setMobileScreen: React.Dispatch<React.SetStateAction<'studentList' | 'chat' | 'chatSearch' | 'summary' | 'mentorAdmin'>>;
    mobileScreen: 'studentList' | 'chat' | 'chatSearch' | 'summary' | 'mentorAdmin';
    setIsMobile: React.Dispatch<React.SetStateAction<boolean>>;
}

const ChatContext = createContext<ChatContextType | undefined>(undefined);

// Utilidades

const parseStudents = (data: any[]): Student[] => data.map(s => ({
    id: String(s.Id),
    name: s.FullName,
    email: s.Email,
    chatId: s.ChatId.toLowerCase(),
    photo: s.Photo,
    elevated: s.AIState,
    escalatedReason: s.AIChangeReason,
    lastMessageDate: s.LastMessageDate,
    lastMessageContent: s.LastMessageContent,
    read: s.IsRead,
}));

//Función para obtener token fresco en cada llamada
export const getAuthenticatedToken = async (): Promise<string | null> => {
    try {
        const token = await microsoftTeams.authentication.getAuthToken();
        if (!token || typeof token !== 'string') throw new Error("Token inválido recibido de Teams");
        return token;
    } catch (error) {
        logger().error('Error autenticarse y conseguir token', error, {
            component: 'ChatContext',
        });
        return null;
    }
};


//Marcado como leido 
const markChatAsRead = async (chatId: string) => {
    try {
        const token = await getAuthenticatedToken();
        if (!token) {
            logger().warn('No se pudo obtener token fresco para markChatAsRead', { component: 'ChatContext' });
            return;
        }

        await fetch(`${API_BASE}/api/chat/${chatId}/read`, {
            method: 'POST',
            headers: {
                "Authorization": `Bearer ${token}`
            }
        });
    } catch (error) {
        logger().log({
            message: `Error al marcar chat como leído: ${error instanceof Error ? error.message : 'desconocido'}`,
            severity: 4,
            error,
            extra: { component: 'ChatContext' }
        });
    }
};
//Llamado a la api de lista estudiantes
const fetchStudentsFromApi = async (mentorEmail: string): Promise<Student[]> => {
    try {
        const token = await getAuthenticatedToken();

        if (!token) {
            logger().warn('No se pudo obtener token fresco para fetchStudentsFromApi', { component: 'ChatContext' });
            return [];
        }

        const response = await fetch(`${API_BASE}/api/mentor/${mentorEmail}/students`, {
            method: 'GET',
            headers: {
                "Authorization": `Bearer ${token}`
            }
        });
        logger().info("✅ Estudiantes Cargados", { component: 'ChatContext' });
        const data = await response.json();

        if (data.ResponseCode !== 0) throw new Error('Error al obtener estudiantes');
        return parseStudents(data.ResponseData);

    } catch (err) {
        logger().error('Error al cargar estudiantes:', err);

        return [];
    }
};
//Llamado a los mensajes
const fetchMessagesByChatId = async (chatId: string): Promise<{ messages: Message[]; nextUrl: string | null }> => {
    try {
        const token = await getAuthenticatedToken();
        if (!token) {
            logger().warn('No se pudo obtener token fresco para fetchMessagesByChatId', { component: 'ChatContext' });
            return { messages: [], nextUrl: null };
        }

        const response = await fetch(`${API_BASE}/api/chat/${chatId}/messages`, {
            headers: {
                Authorization: `Bearer ${token}`,
            },
        });

        const data = await response.json();
        if (!data.ResponseData?.Messages || !Array.isArray(data.ResponseData.Messages)) {
            return { messages: [], nextUrl: null };
        }

        // ✅ Usar la utilidad unificada
        const messages = data.ResponseData.Messages.map((m: any) =>
            mapApiMessageToMessage(m, chatId)
        );

        return {
            messages,
            nextUrl: data.ResponseData.UrlNextPage || null,
        };
    } catch (err) {
        logger().error('Error al cargar mensajes del chat:', err);
        return { messages: [], nextUrl: null };
    }
};


// mensaje por URL (paginación)
const fetchMessagesByUrl = async (
    url: string,
    chatId: string
): Promise<{ messages: Message[]; nextUrl: string | null }> => {
    try {
        const token = await getAuthenticatedToken();
        if (!token) {
            logger().warn("No se pudo obtener token fresco para fetchMessagesByUrl", {
                component: "ChatContext",
            });
            return { messages: [], nextUrl: null };
        }

        const response = await fetch(url, {
            method: "GET",
            headers: {
                Accept: "application/json",
                Authorization: `Bearer ${token}`,
            },
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();
        const payload = data?.ResponseData;

        if (!payload?.Messages || !Array.isArray(payload.Messages)) {
            return { messages: [], nextUrl: null };
        }

        // ✅ Usar la utilidad unificada
        const messages = payload.Messages.map((m: any) =>
            mapApiMessageToMessage(m, chatId)
        );

        return {
            messages,
            nextUrl: payload.UrlNextPage || null,
        };

    } catch (err) {
        logger().error("Error al cargar más mensajes:", err, {
            component: "ChatContext",
        });
        return { messages: [], nextUrl: null };
    }
};


//Post de elevado mentor
const updateAIState = async (chatId: string, state: boolean, reason: string) => {
    try {
        const token = await getAuthenticatedToken();
        if (!token) {
            logger().warn('No se pudo obtener token fresco para updateAIState', { component: 'ChatContext' });
            return;
        }
        const response = await fetch(`${API_BASE}/api/chat/${chatId}/ai-settings`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify({
                AIState: state,
                AIChangeReason: reason
            })
        });

        const data = await response.json();

        if (data.ResponseCode !== 0) throw new Error('Error al actualizar el estado de la IA');
    } catch (err) {
        logger().error('Error al enviar estado de IA:', err);
    }
};

export const ChatProvider = ({ children }: { children: ReactNode }) => {
    const [students, setStudents] = useState<Student[]>([]);
    const [selectedStudent, setSelectedStudent] = useState<Student | null>(null);
    const [messages, setMessages] = useState<Message[]>([]);
    const [resumes, setResumes] = useState<ResumeItem[]>([]);
    const [isSummaryLoading, setIsSummaryLoading] = useState(false);
    const [mentorEmail, setMentorEmail] = useState<string | null>(null);
    const [mensaje, setMensaje] = useState('');
    const [nextPageUrl, setNextPageUrl] = useState<string | null>(null);
    const [prevPageUrl, setPrevPageUrl] = useState<string | null>(null);
    const [isFetching, setIsFetching] = useState(false);
    const [MentorType, setMentorType] = useState<'Admin' | 'Senior' | 'Semi Senior' | 'Junior' | null>(null);
    const [isAuthLoading, setIsAuthLoading] = useState(true);
    const [shouldScrollToBottom, setShouldScrollToBottom] = useState(false);
    const triggerScrollToBottom = () => setShouldScrollToBottom(true);
    const [hubConnection, setHubConnection] = useState<signalR.HubConnection | null>(null);
    const [mentorEntraId, setMentorEntraId] = useState<string | null>(null);
    //Error
    const { addError } = useErrorQueue();
    const hasShownTeamsError = useRef(false);
    // contexto ubicación de mensaje
    const [highlightedMessageId, setHighlightedMessageId] = useState<number | null>(null);
    const [isMobile, setIsMobile] = useState(window.innerWidth < 768);
    const [showSearch, setShowSearch] = useState(false);
    const [showSummary, setShowSummary] = useState(false);
    const [mobileScreen, setMobileScreen] = useState<'studentList' | 'chat' | 'chatSearch' | 'summary' | 'mentorAdmin'>('studentList');
    const inFlightResumes = useRef<Set<string>>(new Set());
    const studentsRef = useRef<Student[]>([]);
    const subscriptionsRef = useRef<Set<string>>(new Set());
    const selectedStudentRef = useRef<Student | null>(null);


    //Llamada al tipo mentor
    useEffect(() => {
        if (mentorEmail) {
            fetchMentorType(mentorEmail).finally(() => setIsAuthLoading(false));
        }
    }, [mentorEmail]);


    //Recupera el correo de teams
    useEffect(() => {
        const initTeams = async () => {
            try {
                await microsoftTeams.app.initialize();
                const context = await microsoftTeams.app.getContext();
                const email = context.user?.userPrincipalName;

                if (email) {
                    setMentorEmail(email);
                    (window as any).__mentorEmail = email;
                    logger().info("✅ Usuario autenticado en Teams", { component: 'ChatContext' });
                } else {
                    addError("⚠️ No se pudo obtener tu correo desde Teams.");
                    logger().warn("No se pudo obtener el email del mentor", { component: 'ChatContext' });
                }

            } catch (error) {
                addError("⚠️ No estás dentro de Teams o hubo un error al inicializarlo.");
                logger().error('Error fuera de teams', error, {
                    component: 'ChatContext',
                });

                if (!hasShownTeamsError.current) {
                    addError('❌ No te encuentras en TEAMS y no pudimos recuperar el correo 😥');
                    hasShownTeamsError.current = true;
                }

                setMensaje('Error al inicializar Teams: ' + (error instanceof Error ? error.message : String(error)));

                // usa un correo de fallback
                const fallbackEmail = 'dev@mentores.local';
                setMentorEmail(fallbackEmail);
                (window as any).__mentorEmail = fallbackEmail;

                // log explícito al backend
                logger().log({
                    message: `Error al inicializar Teams: ${error instanceof Error ? error.message : 'desconocido'}`,
                    severity: 4,
                    error,
                    extra: { component: 'initTeams', route: window.location.pathname },
                });
            }

        };

        initTeams();
    }, []);
    //Mobile 
    useEffect(() => {
        const handleResize = () => {
            const mobile = window.innerWidth < 768;
            setIsMobile(mobile);
            if (!mobile) {
                setMobileScreen('studentList');
            }
        };

        window.addEventListener('resize', handleResize);
        handleResize();
        return () => window.removeEventListener('resize', handleResize);
    }, []);
    //Carga estudiantes
    useEffect(() => {
        if (!mentorEmail) return;
        fetchStudentsFromApi(mentorEmail).then(setStudents);
    }, [mentorEmail]);

    //Tipo mentor
    const fetchMentorType = async (email: string) => {

        try {
            const token = await getAuthenticatedToken();

            if (!token) {
                logger().warn('No se pudo obtener token fresco para fetchMentorType', { component: 'ChatContext' });
                addError("⚠️ No se pudo obtener los datos para la autenticación.");
                return;
            }
            const res = await fetch(`${API_BASE}/api/mentor/${email}/type`, {
                method: 'GET',
                headers: {
                    "Authorization": `Bearer ${token}`
                }

            });
            const data = await res.json();
            const EntraId = data?.ResponseData?.EntraId;
            const mentorType = data?.ResponseData?.MentorType;
            if (EntraId) {
                setMentorEntraId(EntraId);
            }
            if (mentorType === 'Admin' || mentorType === 'Senior' || mentorType === 'Semi Senior' || mentorType === 'Junior') {
                setMentorType(mentorType);
            } else {
                setMentorType(null);
                logger().warn("Tipo de mentor es null o no válido.", { mentorType, data, component: 'ChatContext' });
            }


        } catch (err) {
            logger().error('Error al obtener tipo de mentor', err, {
                component: 'ChatContext',
            });
        }
    };

    //Resumen
    const fetchResumesFromApi = useCallback(async (chatId: string): Promise<ResumeItem[]> => {
        // ✅ si ya hay una request para este chat, no la duplicamos
        if (inFlightResumes.current.has(chatId)) {
            return resumes; // devolvemos lo que haya
        }
        inFlightResumes.current.add(chatId);
        try {
            const token = await getAuthenticatedToken();
            if (!token) {
                logger().warn('No se pudo obtener token fresco para fetchResumesFromApi', { component: 'ChatContext' });
                setResumes([]);
                return [];
            }

            const resp = await fetch(
                `${API_BASE}/api/chat/${encodeURIComponent(chatId)}/summary?page=1&pageSize=10`,
                { headers: { Authorization: `Bearer ${token}` } }
            );

            if (!resp.ok) {
                logger().warn("⚠️ Error al obtener resúmenes", { component: 'ChatContext' });
                setResumes([]);
                return [];
            }

            const data = await resp.json();
            if (data.ResponseCode !== 0) {
                logger().error('Error al obtener resúmenes:', data.ResponseMessage, { component: 'ChatContext' });
                setResumes([]);
                return [];
            }

            const mapped: ResumeItem[] = (data.ResponseData?.Summaries ?? [])
                .map((it: any) => ({
                    id: it.Id,
                    chatId: it.ChatId,
                    summary: it.Summary || 'Sin resumen disponible',
                    keyPoints: (it.KeyPoints ?? '').split(';').map((s: string) => s.trim()).filter(Boolean),
                    summaryType: it.SummaryType,
                    escalated: String(it.Escalated).toLowerCase() === 'true',
                    escalationReason: it.EscalationReason || '',
                    createdAt: it.CreatedAt,
                    createdBy: it.CreatedBy,
                }));

            mapped.sort((a: ResumeItem, b: ResumeItem) =>
                new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
            );

            setResumes(mapped);
            return mapped;

        } catch (err) {
            addError('No se pudo cargar los resúmenes');
            logger().error('Error al cargar resúmenes:', err);
            setResumes([]);
            return [];
        } finally {
            inFlightResumes.current.delete(chatId); // 👈 libera el candado
        }
    }, [addError, resumes]);


    // POST + polling sencillo 
    const generateSummary = async (chatId: string) => {
        try {
            setIsSummaryLoading(true);
            const token = await getAuthenticatedToken();
            if (!token) {
                logger().warn('No se pudo obtener token fresco para generateSummary', { component: 'ChatContext' });
                setIsSummaryLoading(false);
                return;
            }
            const chatsId = encodeURIComponent(chatId);
            const postResp = await fetch(`${API_BASE}/api/chat/${chatsId}/summary`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    "Authorization": `Bearer ${token}`
                },
                body: JSON.stringify({ SummaryType: 'Bajo Demanda' }),
            });

            if (!postResp.ok) {
                addError('No se pudo solicitar la generación del resumen');
                setIsSummaryLoading(false);
                return;
            }

            // baseline para detectar uno nuevo
            const prevIds = new Set(resumes.map(r => r.id));

            const startedAt = Date.now();
            const timeoutMs = 3000;
            const intervalMs = 2000;

            // primer fetch inmediato
            await fetchResumesFromApi(chatId);
            if (resumesChanged(prevIds, resumes)) {
                setIsSummaryLoading(false);
                return;
            }

            // polling
            while (Date.now() - startedAt < timeoutMs) {
                await wait(intervalMs);
                await fetchResumesFromApi(chatId);
                if (resumesChanged(prevIds, resumes)) break;
            }
        } catch (err) {
            addError('Ocurrió un problema al generar el resumen');
            logger().error('Error al generar resumen:', err);
        } finally {
            setIsSummaryLoading(false);
        }
    };

    const resumesChanged = (prevIds: Set<ResumeItem['id']>, current: ResumeItem[]) =>
        current.some(r => !prevIds.has(r.id));

    const wait = (ms: number) => new Promise(res => setTimeout(res, ms));

    //Seleccionar estudiante para contexto de carga de mensajes 
    const selectStudent = async (student: Student) => {
        setResumes([]);
        // Si el estudiante tiene chatId, cargar mensajes
        if (student.chatId && student.chatId.trim()) {
            const { messages: initialMessages, nextUrl } = await fetchMessagesByChatId(student.chatId);
            setMessages(initialMessages);
            setNextPageUrl(nextUrl);
            await markChatAsRead(student.chatId);
        } else {
            logger().info("⏳ Estudiante sin chatId - esperando primer mensaje");
            setMessages([]); 
            setNextPageUrl(null);
        }
        setStudents(prev =>
            prev.map(s => s.id === student.id ? { ...s, read: true } : s)
        );
        setSelectedStudent({ ...student, read: true });
        (window as any).__currentChatId = student.chatId || null;
    };
    //Contexto de mensaje específico
    const fetchMessageContext = async (chatId: string, messageId: number) => {
        try {
            const token = await getAuthenticatedToken();
            if (!token) {
                logger().warn('No se pudo obtener token fresco para fetchMessageContext', { component: 'ChatContext' });
                return;
            }
            const response = await fetch(
                `${API_BASE}/api/chat/${chatId}/messages/${messageId}/context?before=10&after=10`,
                {
                    headers: {
                        'Content-Type': 'application/json',
                        Authorization: `Bearer ${token}`,
                    },
                }
            );
            const data = await response.json();

            if (data.ResponseCode !== 0) {
                throw new Error('Error al obtener contexto del mensaje');
            }

            const payload = data.ResponseData;
            const contextMessages: Message[] =
                payload?.Messages && Array.isArray(payload.Messages)
                    ? payload.Messages.map((m: any) => mapApiMessageToMessage(m, chatId))
                    : [];

            const student = students.find(s => s.chatId === chatId.toLowerCase()) || null;
            if (student) {
                const isSame = selectedStudent?.chatId === student.chatId;

                setSelectedStudent(student);
                (window as any).__currentChatId = student.chatId;

                if (isMobile && isSame) {
                    setShowSearch(false);
                    setShowSummary(false);
                }
            } else {
                addError('⚠️ No se encontró el estudiante para el chat seleccionado.');
                logger().warn('No se encontró el estudiante para el chat seleccionado.', { component: 'ChatContext' });
            }

            setMessages(contextMessages);
            setNextPageUrl(payload?.UrlBeforePage ?? null);
            setPrevPageUrl(payload?.UrlAfterPage ?? null);
            //resalta el mensaje específico
            setHighlightedMessageId(messageId);
        } catch (error) {
            logger().error('Error al cargar contexto del mensaje:', error);
        }
    };
    //nuevos mensajes y antiguos mensajes
    const fetchNewerMessages = async () => {
        if (!selectedStudent || !prevPageUrl) return;

        try {
            const token = await getAuthenticatedToken();
            if (!token) {
                logger().warn('No token para fetchNewerMessages', { component: 'ChatContext' });
                return;
            }

            const response = await fetch(prevPageUrl, {
                headers: {
                    Accept: 'application/json',
                    Authorization: `Bearer ${token}`,
                },
            });

            const data = await response.json();
            const payload = data?.ResponseData;

            const newMessages: Message[] =
                payload?.Messages && Array.isArray(payload.Messages)
                    ? payload.Messages.map((m: any) =>
                        mapApiMessageToMessage(m, selectedStudent.chatId)
                    )
                    : [];

            setMessages(prev => mergeMessages(prev, newMessages));
            setPrevPageUrl(payload?.UrlAfterPage ?? null);

        } catch (err) {
            logger().error('Error al cargar mensajes nuevos:', err);
        }
    };


    //actualizar mensajes
     const selectedChatIdRef = useRef<string | null>(null);
    // useEffect(() => {
    //     selectedChatIdRef.current = selectedStudent?.chatId ?? null;
    // }, [selectedStudent]);
    //Efecto cambio de pantalla en el movil 
    useEffect(() => {
        if (selectedStudent && isMobile) {
            setMobileScreen('chat');
            setShowSearch(false);          // limpia búsqueda si estaba activa
            setShowSummary(false);         // limpia resumen si estaba activo
        }
    }, [selectedStudent]);

    //Mantener referencia actualizada de estudiantes para el signalR
    useEffect(() => {
        const newChatId = selectedStudent?.chatId || null;
        selectedChatIdRef.current = newChatId;
    }, [selectedStudent]);
    useEffect(() => {
        selectedStudentRef.current = selectedStudent;
        selectedChatIdRef.current = selectedStudent?.chatId ?? null;
    }, [selectedStudent]);

    // Conexión a SignalR Hub
    useEffect(() => {
        if (!mentorEmail || !mentorEntraId) return;

        // Inicializamos la conexión de SignalR 
        const newConnection = new signalR.HubConnectionBuilder()
            .withUrl(`${API_SignalR}/chathub`, {
                accessTokenFactory: async () => {
                    const t = await getAuthenticatedToken();
                    return t ?? "";
                },
                transport: signalR.HttpTransportType.WebSockets,
                skipNegotiation: true,
            })
            .withAutomaticReconnect()
            .build();

        const startConnection = async () => {
            try {
                // Intentamos conectar al SignalR Hub
                await newConnection.start();
                logger().info("✅ Conectado a SignalR Hub", { component: 'ChatContext' });
                // Unirse al grupo del mentor (para recibir nuevos chats)
                await newConnection.invoke("JoinMentorGroup", mentorEntraId);
                // Cargar estudiantes existentes
                const students = await fetchStudentsFromApi(mentorEmail);
                setStudents(students);

                // Unirse a los grupos de chats existentes
                for (const student of students) {
                    if (student.chatId?.trim()) {
                        try {
                            await newConnection.invoke("JoinChatGroup", student.chatId);
                        } catch (err) {
                            logger().warn(`Error al unirse al grupo ${student.chatId}`, { err });
                        }
                    }
                }
                // Guardar la conexión en el estado
                newConnection.on("ChatUpserted", async (payload: any) => {
                    const chatIdRaw =
                        payload.MsTeamsChatId ||
                        payload.msTeamsChatId ||
                        payload.ChatId ||
                        payload.chatId;
                    const chatId = typeof chatIdRaw === "string" ? chatIdRaw.toLowerCase() : "";
                    const studentId = String(payload.studentId || payload.StudentId || "");
                    const firstMessageContent = payload.message || payload.Message || "";

                    if (!chatId || !studentId) {
                        logger().error("ChatUpserted sin chatId o studentId válido", payload, { component: "ChatContext" });
                        return;
                    }

                    const timestamp = payload.Timestamp || new Date().toISOString();
                    const currentChatId = selectedChatIdRef.current;

                    // VERIFICACIÓN MEJORADA
                    const currentSelectedStudent = selectedStudentRef.current;

                    const isStudentSelected =
                        !!currentSelectedStudent &&
                        String(currentSelectedStudent.id) === String(studentId);

                    // Suscribirse
                    if (!subscriptionsRef.current.has(chatId)) {
                        try {
                            await newConnection.invoke("JoinChatGroup", chatId);
                            subscriptionsRef.current.add(chatId);
                        } catch (err) {
                            logger().warn(`Error al unirse al grupo ${chatId}`, { err });
                        }
                    }

                    // Actualizar estudiante en la lista
                    setStudents(prev => {
                        const idx = prev.findIndex(s => s.id === studentId);

                        if (idx !== -1) {
                            const updated = [...prev];
                            updated[idx] = {
                                ...updated[idx],
                                chatId,
                                lastMessageContent: firstMessageContent || updated[idx].lastMessageContent,
                                lastMessageDate: timestamp,
                                read: isStudentSelected,
                                elevated: false,
                            };

                            studentsRef.current = updated;
                            return updated;
                        }

                        // Crear nuevo
                        const newStudent: Student = {
                            id: studentId,
                            chatId,
                            name: payload.StudentData?.Name || "Estudiante Nuevo",
                            email: payload.StudentData?.Email || "",
                            photo: payload.StudentData?.Photo || "",
                            lastMessageContent: firstMessageContent,
                            lastMessageDate: timestamp,
                            read: false,
                            elevated: false,
                            escalatedReason: "",
                        };
                        const updatedList = [...prev, newStudent];
                        studentsRef.current = updatedList;
                        return updatedList;
                    });

                    // ACTUALIZAR selectedStudent SI ES EL MISMO
                    if (isStudentSelected) {
                        setSelectedStudent(prev => {
                            if (!prev) return prev;

                            return {
                                ...prev,
                                chatId,
                                lastMessageContent: firstMessageContent,
                                lastMessageDate: timestamp,
                                read: true,
                            };
                        });

                        selectedChatIdRef.current = chatId;
                        selectedStudentRef.current = {
                            ...currentSelectedStudent!,
                            chatId,
                        };

                        (window as any).__currentChatId = chatId;
                    }
                    // Agregar mensaje si está seleccionado
                    if (isStudentSelected && firstMessageContent.trim()) {

                        const firstMessage: Message = {
                            id: payload.MessageId || Date.now(),
                            chatId,
                            content: firstMessageContent,
                            timestamp,
                            sender: 'student',
                            type:  'html',
                            attachments: [],
                        };

                        setMessages(prev => {
                            const existe = prev.some(m => m.id === firstMessage.id);
                            if (existe) {
                                return prev;
                            }
                            return [...prev, firstMessage];
                        });

                        markChatAsRead(chatId);
                        triggerScrollToBottom();
                    }                     
                });


                // 6️⃣ LISTENER: ReceiveMessage - SOLO bufferiza o procesa
                newConnection.on("ReceiveMessage", (msg: any) => {
                    const chatIdRaw = msg.chatId || msg.ChatId || msg.msTeamsChatId || msg.MsTeamsChatId;
                    const chatId = typeof chatIdRaw === "string" ? chatIdRaw.toLowerCase() : "";
                    const studentId = String(msg.studentId || msg.StudentId || "");

                    if (!chatId) {
                        logger().warn("ReceiveMessage sin chatId válido", msg);
                        return;
                    }

                    // Procesar cambios de estado de IA (siempre, incluso si no está suscrito)
                    if (typeof msg.aiEnabled === 'boolean' || typeof msg.AiEnabled === 'boolean') {
                        const aiEnabled = msg.AiEnabled ?? msg.aiEnabled;
                        setStudents(prevStudents =>
                            prevStudents.map(student =>
                                student.chatId.toLowerCase() === chatId
                                    ? {
                                        ...student,
                                        elevated: aiEnabled,
                                        escalatedReason: msg.escalationReason || msg.EscalationReason || student.escalatedReason,
                                    }
                                    : student
                            )
                        );

                        if (selectedChatIdRef.current?.toLowerCase() === chatId) {
                            setSelectedStudent(prev =>
                                !prev ? prev : {
                                    ...prev,
                                    elevated: aiEnabled,
                                    escalatedReason: msg.escalationReason || msg.EscalationReason || prev.escalatedReason,
                                }
                            );
                        }
                    }

                    // Verificar si es solo update de estado
                    const content = msg.content || msg.Content || '';
                    const hasAttachments =
                        (Array.isArray(msg.attachments) && msg.attachments.length > 0) ||
                        (Array.isArray(msg.Attachments) && msg.Attachments.length > 0);

                    if (!content.trim() && !hasAttachments) {
                        return;
                    }

                    // Procesar el mensaje
                    const incomingMessage = processIncomingMessage(msg, chatId);

                    // SI ESTÁ SUSCRITO, verificar que el estudiante exista
                    const studentExists = studentsRef.current.some(
                        s => s.id === studentId || s.chatId?.toLowerCase() === chatId
                    );

                    if (!studentExists) {
                        logger().warn("Mensaje para chat suscrito pero estudiante no existe - BUFFERIZANDO", { chatId, studentId, msg });
                        return;
                    }

                    // PROCESAR MENSAJE NORMALMENTE
                    const currentChatId = selectedChatIdRef.current;
                    // Actualizar lista de estudiantes
                    setStudents(prevStudents => {
                        const updatedStudents = prevStudents.map(student => {
                            const matchesByChat = student.chatId.toLowerCase() === chatId;
                            const matchesByStudent = studentId && student.id === studentId;

                            if (matchesByChat || matchesByStudent) {
                                return {
                                    ...student,
                                    chatId: chatId,
                                    read: currentChatId?.toLowerCase() === chatId,
                                    lastMessageContent: incomingMessage.content,
                                    lastMessageDate: incomingMessage.timestamp,
                                };
                            }
                            return student;
                        });

                        const wasUpdated = updatedStudents.some((s, i) => s !== prevStudents[i]);
                        if (!wasUpdated) {
                            logger().warn("No se encontró estudiante para actualizar", { chatId, studentId, msg, component: 'ChatContext' });   
                        }

                        return updatedStudents;
                    });

                    // Agregar mensaje al panel si el chat está activo
                    if (currentChatId && chatId.toLowerCase() === currentChatId.toLowerCase()) {
                        setMessages(prev => {
                            const existe = prev.some(m => m.id === incomingMessage.id);
                            if (existe) {
                                //Hay que hacer una notificación abajo para notificar al mentor                                 
                                return prev;
                            }
                            return [...prev, incomingMessage];
                        });

                        markChatAsRead(chatId);
                        triggerScrollToBottom();
                    }
                });

                // Guardar conexión
                setHubConnection(newConnection);

            } catch (error) {
                logger().error("❌ Error al conectar con SignalR:", error, {
                    component: 'ChatContext',
                });
            }
        };

        startConnection();

        // Cleanup al desmontar
        return () => {
            newConnection.off("ChatUpserted");
            newConnection.off("ReceiveMessage");
            newConnection.stop();
        };
    }, [mentorEmail, mentorEntraId]);

    //  FUNCIONES DE AYUDA para mensajes 
    const mergeMessages = (existing: Message[], incoming: Message[]) => {
        const existingIds = new Set(existing.map(m => m.id));
        const merged = [...existing, ...incoming.filter(m => !existingIds.has(m.id))];

        return merged.sort((a, b) => {
            const diff = new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime();
            if (diff !== 0) return diff;
            return a.id - b.id; // por si hay mensajes con el mismo timestamp
        });
    };

    //  FUNCIÓN DE MENSAJES ANTIGUOS

    const fetchMoreMessages = async () => {
        if (!nextPageUrl || !selectedStudent) return;
        setIsFetching(true);

        const { messages: olderMessages, nextUrl } = await fetchMessagesByUrl(nextPageUrl, selectedStudent.chatId);

        setMessages(prev => mergeMessages(olderMessages, prev)); // agrega al inicio sin duplicar


        setNextPageUrl(nextUrl);
        setIsFetching(false);
    };


    const sendMessage = (message: Message) => {
        setMessages(prev => [...prev, message]);

        if (message.sender !== 'student') return;

        setStudents(prev =>
            prev.map(s => {
                const isTarget = s.id === message.chatId;
                const isSelected = selectedStudent?.id === s.id;
                return isTarget && !isSelected ? { ...s, read: false } : s;
            })
        );
    };
    const sendTextMessage = async (chatId: string, text: string): Promise<Message | null> => {
        try {
            const token = await getAuthenticatedToken();
            if (!token) {
                logger().warn('No se pudo obtener token fresco para sendTextMessage', { component: 'ChatContext' });
                return null;
            }
            const headers: Record<string, string> = {
                'Content-Type': 'application/json',
                "Authorization": `Bearer ${token}`
            };

            const response = await fetch(`${API_BASE}/api/chat/${chatId}/message`, {
                method: 'POST',
                headers,
                body: JSON.stringify({
                    SenderRole: 'Mentor',
                    ContentType: 'text',
                    Content: text
                })
            });
            const data = await response.json();
            if (data.ResponseCode !== 0) {
                logger().error("❌ Error al enviar mensaje:", data, { component: 'ChatContext' });
                return null;
            }
            return null;

        } catch (error) {
            logger().error("Error en sendTextMessage:", error, {
                component: 'ChatContext',
            });
            return null;
        }
    };
    //Actualizar estudiante
    const updateStudent = (updatedStudent: Student) => {
        setStudents(prev =>
            prev.map(s => s.id === updatedStudent.id ? updatedStudent : s)
        );

        setSelectedStudent(prev => {
            if (!prev || prev.id !== updatedStudent.id) return updatedStudent;
            return { ...updatedStudent };
        });
    };
    //Buscar mensajes del mentor
    const searchMessagesByMentor = async (query: string): Promise<any[]> => {
        if (!mentorEmail || !query.trim()) return [];

        try {
            const token = await getAuthenticatedToken();
            if (!token) {
                logger().warn('No se pudo obtener token fresco para searchMessagesByMentor', { component: 'ChatContext' });
                return [];
            }

            const headers: Record<string, string> = {};
            headers["Authorization"] = `Bearer ${token}`;

            const response = await fetch(`${API_BASE}/api/mentor/${mentorEmail}/messages?query=${encodeURIComponent(query)}`, {
                headers
            });
            const data = await response.json();

            if (data.ResponseCode !== 0 || !Array.isArray(data.ResponseData)) return [];

            return data.ResponseData;
        } catch (err) {
            logger().error('Error al buscar mensajes del mentor:', err);
            return [];
        }
    };


    return (
        <ChatContext.Provider
            value={{
                students,
                selectedStudent,
                messages,
                resumes,
                generateSummary,
                isSummaryLoading,
                selectStudent,
                sendMessage,
                updateStudent,
                fetchResumesFromApi,
                updateAIState,
                mentorEmail,
                mensaje,
                fetchMoreMessages,
                fetchMessageContext,
                fetchNewerMessages,
                isFetching,
                nextPageUrl,
                searchMessagesByMentor,
                MentorType,
                isAuthLoading,
                sendTextMessage,
                shouldScrollToBottom,
                setShouldScrollToBottom,
                triggerScrollToBottom,
                highlightedMessageId,
                setHighlightedMessageId,
                prevPageUrl,
                isMobile,
                setMobileScreen,
                mobileScreen,
                setIsMobile,
            }}

        >
            {children}
        </ChatContext.Provider>
    );
};

export const useChat = () => {
    const context = useContext(ChatContext);
    if (!context) throw new Error('useChat must be used within a ChatProvider');
    return context;
};