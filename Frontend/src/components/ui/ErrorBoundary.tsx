// src/components/ui/ErrorBoundary.tsx
import React from 'react';
import { useErrorQueue } from '../../hooks/useErrorQueue';
import { logger } from '../../utils/logger';

export class ErrorBoundary extends React.Component<
  React.PropsWithChildren,
  { hasError: boolean }
> {
  state = { hasError: false };

  componentDidCatch(error: Error, info: React.ErrorInfo) {
    this.setState({ hasError: true });
    try {
      logger().log({
        message: error.message,
        severity: 4,
        error,
        extra: {
          component: 'ErrorBoundary',
          stack: error.stack,
          reactComponentStack: info.componentStack,
        },
      });
    } catch {}
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="p-4 bg-red-50 text-red-700 rounded-xl">
          Algo salió mal. Intenta recargar.
        </div>
      );
    }
    return this.props.children;
  }
}

export const ErrorBoundaryWithToast: React.FC<React.PropsWithChildren> = ({ children }) => {
  const { addError } = useErrorQueue();

  React.useEffect(() => {
    const onErr = (e: ErrorEvent) => addError(`⚠️ ${e.message}`);
    const onRej = () => addError('⚠️ Ocurrió un error inesperado');
    window.addEventListener('error', onErr);
    window.addEventListener('unhandledrejection', onRej);
    return () => {
      window.removeEventListener('error', onErr);
      window.removeEventListener('unhandledrejection', onRej);
    };
  }, [addError]);

  return <ErrorBoundary>{children}</ErrorBoundary>;
};
