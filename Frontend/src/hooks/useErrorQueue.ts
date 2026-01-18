import { useState } from "react";

let listeners: (() => void)[] = [];

let queue: string[] = [];

export const useErrorQueue = () => {
  const [error, setError] = useState<string | null>(queue[0] || null);

  const update = () => setError(queue[0] || null);

  const addError = (msg: string) => {
    queue.push(msg);
    listeners.forEach((cb) => cb());
  };

  const removeCurrentError = () => {
    queue.shift();
    listeners.forEach((cb) => cb());
  };

  if (!listeners.includes(update)) {
    listeners.push(update);
  }

  return {
    currentError: error,
    errorQueue: [...queue], 
    addError,
    removeCurrentError,
  };
};

