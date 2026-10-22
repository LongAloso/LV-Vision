import { ref } from 'vue';
import { session } from '../auth/session';

export function useWebSocket(path: string, onMessage: (payload: any) => void) {
  const connected = ref(false);
  let socket: WebSocket | null = null;
  let timer: number | null = null;
  let stopped = false;
  function connect() {
    if (stopped || !session.token) return;
    const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    socket = new WebSocket(`${protocol}//${location.host}${path}`);
    socket.onopen = () => {
      connected.value = true;
      socket?.send(JSON.stringify({ type: 'auth', token: session.token }));
    };
    socket.onmessage = event => {
      try { onMessage(JSON.parse(event.data)); } catch { /* malformed server payload */ }
    };
    socket.onclose = event => {
      connected.value = false;
      if (!stopped && event.code !== 1008) timer = window.setTimeout(connect, 2000);
    };
    socket.onerror = () => socket?.close();
  }
  function stop() {
    stopped = true;
    if (timer) window.clearTimeout(timer);
    socket?.close();
  }
  connect();
  return { connected, stop };
}
