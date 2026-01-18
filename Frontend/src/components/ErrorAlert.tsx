import { X, AlertCircle } from "lucide-react";
import { useErrorQueue } from "../hooks/useErrorQueue";
import "./ErrorAlert.css";

export const ErrorAlert = () => {
  const { errorQueue, removeCurrentError } = useErrorQueue();

  if (errorQueue.length === 0) return null; // se oculta solo si no hay errores

  return (
    <div className="popup-overlay">
      <div className="popup-modal">
        <div className="toast-content">
          <div className="icon"><AlertCircle size={20} /></div>
          <div className="text">
            <strong className="title">Errores</strong>
            <ul className="message-list">
              {errorQueue.map((err, i) => (
                <li key={i} className="message">{err}</li>
              ))}
            </ul>
          </div>
          <button className="close" onClick={removeCurrentError}>
            <X size={16} />
          </button>
        </div>
      </div>
    </div>
  );
};
