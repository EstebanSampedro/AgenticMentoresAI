import React, { useState } from "react";
import "./MentorAdminView.css";
import { useErrorQueue } from "../hooks/useErrorQueue";
import { logger } from "../utils/logger";
import { getAuthenticatedToken } from "../context/ChatContext";
import { Alert, AlertTitle, AlertDescription } from "../components/ui/alert";

const API_BASE = import.meta.env.VITE_API_BASE;

const MentorAdminView = () => {
  const [loadingStudents, setLoadingStudents] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { addError } = useErrorQueue();

  const syncStudents = async () => {
    setLoadingStudents(true);
    setSuccess(false);
    setError(null);

    try {
      const token = await getAuthenticatedToken();
      if (!token) {
        setError("⚠️ No se pudo autenticar para sincronizar usuarios.");
        logger().warn("Sin token en MentorAdminView.syncStudents", {
          component: "MentorAdminView",
        });
        return;
      }

      const res = await fetch(`${API_BASE}/api/user/sync/excel`, {
        method: "POST",
        headers: {
          Accept: "application/json",
          Authorization: `Bearer ${token}`,
        },
      });

      if (!res.ok) throw new Error(`HTTP ${res.status} ${res.statusText}`);

      const data = await safeJson(res);

      // Si la API devuelve ResponseCode o algo que confirme éxito
      if (data?.ResponseCode === 0 || res.ok) {
        setSuccess(true);
        logger().info("Usuarios sincronizados correctamente", {
          component: "MentorAdminView",
        });
        // Desaparece en 5 segundos
        setTimeout(() => setSuccess(false), 5000);
      } else {
        throw new Error(data?.ResponseMessage ?? "Error desconocido en API");
      }
    } catch (err) {
      console.error("❌ Error al actualizar usuarios:", err);
      setError("❌ Error al actualizar usuarios.");
      logger().error("Error al actualizar usuarios", err, {
        component: "MentorAdminView",
      });
      addError("❌ Error al actualizar usuarios");
    } finally {
      setLoadingStudents(false);
    }
  };

  return (
    <div className="mentor-admin-container">
      <h2>Seguimiento mentor líder</h2>

      <div className="mentor-admin-actions">
        <button
          className="btn-red"
          onClick={syncStudents}
          disabled={loadingStudents}
        >
          {loadingStudents ? "Actualizando..." : "Actualizar Usuarios"}
        </button>
      </div>

      {/* 🔔 Alerta de éxito */}
      {success && (
        <Alert className="mt-4 border-green-300 bg-green-100 text-green-800">
          <AlertTitle>Actualización exitosa</AlertTitle>
          <AlertDescription>
            Los usuarios fueron sincronizados correctamente.
          </AlertDescription>
        </Alert>
      )}

      {/* ⚠️ Alerta de error */}
      {error && (
        <Alert variant="destructive" className="mt-4">
          <AlertTitle>Error</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <h3 className="mt-6">Dashboard de seguimiento (Power BI)</h3>
      <div className="dashboard-placeholder">{/* TODO: embed PowerBI */}</div>
    </div>
  );
};

// Helper para evitar excepciones al parsear JSON
async function safeJson(res: Response) {
  try {
    return await res.json();
  } catch {
    return null;
  }
}

export default MentorAdminView;
